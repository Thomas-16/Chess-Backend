using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Chess_Backend.Config;
using Chess_Backend.Models;

namespace Chess_Backend.Services;

public class OpenAIWebSocketService : IHostedService, IDisposable
{
    private static readonly Uri WebSocketUri = new("wss://api.openai.com/v1/responses");
    private static readonly TimeSpan MaxConnectionAge = TimeSpan.FromMinutes(55);
    private static readonly TimeSpan ReconnectCheckInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(30);

    private readonly string _apiKey;
    private readonly ILogger<OpenAIWebSocketService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly object _lock = new();

    private ClientWebSocket? _webSocket;
    private DateTime _connectedAt;
    private Timer? _reconnectTimer;
    private bool _disposed;

    public OpenAIWebSocketService(IConfiguration configuration, ILogger<OpenAIWebSocketService> logger)
    {
        _apiKey = configuration["OPENAI_API_KEY"]
                  ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                  ?? throw new InvalidOperationException("OpenAI API key not found. Set OPENAI_API_KEY environment variable.");
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ConnectAsync(cancellationToken);
            _logger.LogInformation("OpenAI WebSocket connected on startup");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect WebSocket on startup; will connect on first request");
        }

        _reconnectTimer = new Timer(
            _ => _ = ProactiveReconnectAsync(),
            null,
            ReconnectCheckInterval,
            ReconnectCheckInterval);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _reconnectTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        MarkConnectionDead();
        _logger.LogInformation("OpenAI WebSocket service stopped");
        return Task.CompletedTask;
    }

    public async Task<string> GetChessResponseAsync(
        string userMessage,
        string gameId,
        List<ChatMessage> conversationHistory,
        ChessPositionContext? context = null)
    {
        var startTime = DateTime.UtcNow;
        await _semaphore.WaitAsync();
        try
        {
            return await SendRequestWithRetryAsync(userMessage, gameId, conversationHistory, context, startTime);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<string> SendRequestWithRetryAsync(
        string userMessage,
        string gameId,
        List<ChatMessage> conversationHistory,
        ChessPositionContext? context,
        DateTime startTime)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                await EnsureConnectedAsync(CancellationToken.None);
                string response = await SendAndReceiveAsync(userMessage, conversationHistory, context);

                var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogInformation("OpenAI WebSocket call took {ElapsedMs}ms for game {GameId}", elapsedMs, gameId);
                _logger.LogInformation("Generated AI response for game {GameId}: {Response}", gameId, response);
                return response;
            }
            catch (Exception ex) when (attempt == 0)
            {
                _logger.LogWarning(ex, "WebSocket request failed for game {GameId}, reconnecting and retrying", gameId);
                MarkConnectionDead();
            }
        }

        // Unreachable, but satisfies the compiler
        return "I'm having trouble thinking right now. Let's focus on the game!";
    }

    private async Task<string> SendAndReceiveAsync(
        string userMessage,
        List<ChatMessage> conversationHistory,
        ChessPositionContext? context)
    {
        string formattedMessage = OpenAIService.FormatMessageWithContext(userMessage, context);
        string payload = BuildRequestPayload(formattedMessage, conversationHistory);

        byte[] sendBuffer = Encoding.UTF8.GetBytes(payload);
        await _webSocket!.SendAsync(
            new ArraySegment<byte>(sendBuffer),
            WebSocketMessageType.Text,
            endOfMessage: true,
            CancellationToken.None);

        return await ReadResponseAsync();
    }

    private async Task<string> ReadResponseAsync()
    {
        var responseText = new StringBuilder();
        var buffer = new byte[4096];
        using var cts = new CancellationTokenSource(ReceiveTimeout);

        while (true)
        {
            // Read a complete WebSocket message (may arrive in multiple frames)
            using var messageStream = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await _webSocket!.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    throw new WebSocketException("Server closed the connection");
                }
                messageStream.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            string json = Encoding.UTF8.GetString(messageStream.GetBuffer(), 0, (int)messageStream.Length);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string type = root.GetProperty("type").GetString() ?? "";

            switch (type)
            {
                case "response.output_text.delta":
                    if (root.TryGetProperty("delta", out var delta))
                    {
                        responseText.Append(delta.GetString());
                    }
                    break;

                case "response.completed":
                    string text = responseText.ToString();
                    return string.IsNullOrWhiteSpace(text)
                        ? "I'm having trouble thinking right now. Let's focus on the game!"
                        : text;

                case "response.failed":
                    string error = "unknown";
                    if (root.TryGetProperty("response", out var resp)
                        && resp.TryGetProperty("error", out var errObj)
                        && errObj.TryGetProperty("message", out var errMsg))
                    {
                        error = errMsg.GetString() ?? "unknown";
                    }
                    _logger.LogError("OpenAI response failed: {Error}", error);
                    return "I'm having trouble thinking right now. Let's focus on the game!";

                case "error":
                    _logger.LogError("OpenAI WebSocket error event: {RawJson}", json);
                    return "I'm having trouble thinking right now. Let's focus on the game!";
            }
        }
    }

    private static string BuildRequestPayload(string formattedUserMessage, List<ChatMessage> conversationHistory)
    {
        var input = new List<object>
        {
            new
            {
                type = "message",
                role = "system",
                content = new[]
                {
                    new { type = "input_text", text = AIPrompts.ChessOpponentSystemPrompt }
                }
            }
        };

        foreach (var msg in conversationHistory)
        {
            input.Add(new
            {
                type = "message",
                role = msg.Speaker == "User" ? "user" : "assistant",
                content = new[]
                {
                    new { type = "input_text", text = msg.Message }
                }
            });
        }

        input.Add(new
        {
            type = "message",
            role = "user",
            content = new[]
            {
                new { type = "input_text", text = formattedUserMessage }
            }
        });

        var request = new
        {
            type = "response.create",
            model = "gpt-5-nano",
            max_output_tokens = 45,
            input
        };

        return JsonSerializer.Serialize(request);
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("Authorization", $"Bearer {_apiKey}");

        await ws.ConnectAsync(WebSocketUri, cancellationToken);

        lock (_lock)
        {
            _webSocket?.Dispose();
            _webSocket = ws;
            _connectedAt = DateTime.UtcNow;
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        bool needsConnect;
        lock (_lock)
        {
            needsConnect = _webSocket == null || _webSocket.State != WebSocketState.Open;
        }

        if (needsConnect)
        {
            _logger.LogInformation("WebSocket not connected, establishing connection...");
            await ConnectAsync(cancellationToken);
        }
    }

    private void MarkConnectionDead()
    {
        lock (_lock)
        {
            try { _webSocket?.Dispose(); } catch { /* best effort */ }
            _webSocket = null;
        }
    }

    private async Task ProactiveReconnectAsync()
    {
        try
        {
            bool shouldReconnect;
            lock (_lock)
            {
                shouldReconnect = _webSocket is { State: WebSocketState.Open }
                                  && (DateTime.UtcNow - _connectedAt) > MaxConnectionAge;
            }

            if (!shouldReconnect) return;

            await _semaphore.WaitAsync();
            try
            {
                // Re-check under semaphore
                lock (_lock)
                {
                    shouldReconnect = _webSocket is { State: WebSocketState.Open }
                                      && (DateTime.UtcNow - _connectedAt) > MaxConnectionAge;
                }

                if (!shouldReconnect) return;

                _logger.LogInformation("Proactively reconnecting WebSocket (age > 55 min)");

                // Close old connection gracefully
                if (_webSocket is { State: WebSocketState.Open })
                {
                    try
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Proactive reconnect", CancellationToken.None);
                    }
                    catch { /* best effort */ }
                }

                MarkConnectionDead();
                await ConnectAsync(CancellationToken.None);
                _logger.LogInformation("Proactive reconnect completed");
            }
            finally
            {
                _semaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Proactive reconnect failed; will retry on next request");
            MarkConnectionDead();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _reconnectTimer?.Dispose();
        _semaphore.Dispose();

        lock (_lock)
        {
            _webSocket?.Dispose();
        }
    }
}
