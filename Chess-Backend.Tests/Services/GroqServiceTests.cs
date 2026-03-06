using System.Net;
using Chess_Backend.Models;
using Chess_Backend.Services;
using Chess_Backend.Tests.Helpers;

namespace Chess_Backend.Tests.Services;

public class GroqServiceTests
{
    private readonly string _successResponse = """
        {
            "choices": [
                {
                    "message": {
                        "content": "Nice try, but your queen is mine!"
                    }
                }
            ]
        }
        """;

    private GroqService CreateService(HttpClient httpClient)
    {
        var config = TestHelper.CreateFakeConfiguration();
        var logger = TestHelper.CreateMockLogger<GroqService>();
        return new GroqService(httpClient, config, logger);
    }

    [Fact]
    public async Task SuccessfulResponse_ReturnsAIMessage()
    {
        var httpClient = TestHelper.CreateMockHttpClient(HttpStatusCode.OK, _successResponse);
        var service = CreateService(httpClient);

        var result = await service.GetChessResponseAsync("e4", "game-1", new List<ChatMessage>());

        Assert.Equal("Nice try, but your queen is mine!", result);
    }

    [Fact]
    public async Task EmptyChoices_ReturnsFallbackMessage()
    {
        var response = """{"choices": []}""";
        var httpClient = TestHelper.CreateMockHttpClient(HttpStatusCode.OK, response);
        var service = CreateService(httpClient);

        var result = await service.GetChessResponseAsync("e4", "game-1", new List<ChatMessage>());

        Assert.Contains("trouble thinking", result);
    }

    [Fact]
    public async Task NullContent_ReturnsFallbackMessage()
    {
        var response = """{"choices": [{"message": {"content": null}}]}""";
        var httpClient = TestHelper.CreateMockHttpClient(HttpStatusCode.OK, response);
        var service = CreateService(httpClient);

        var result = await service.GetChessResponseAsync("e4", "game-1", new List<ChatMessage>());

        Assert.Contains("trouble thinking", result);
    }

    [Fact]
    public async Task ApiError_ReturnsFallbackMessage()
    {
        var httpClient = TestHelper.CreateMockHttpClient(HttpStatusCode.InternalServerError, "Server error");
        var service = CreateService(httpClient);

        var result = await service.GetChessResponseAsync("e4", "game-1", new List<ChatMessage>());

        Assert.Contains("trouble thinking", result);
    }

    [Fact]
    public async Task HttpException_ReturnsFallbackMessage()
    {
        var httpClient = TestHelper.CreateThrowingHttpClient(new HttpRequestException("Connection refused"));
        var service = CreateService(httpClient);

        var result = await service.GetChessResponseAsync("e4", "game-1", new List<ChatMessage>());

        Assert.Contains("trouble thinking", result);
    }

    [Fact]
    public async Task RequestUrl_IsGroqEndpoint()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, _successResponse);
        var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient);

        await service.GetChessResponseAsync("e4", "game-1", new List<ChatMessage>());

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("https://api.groq.com/openai/v1/chat/completions", handler.LastRequest.RequestUri?.ToString());
    }

    [Fact]
    public async Task Request_ContainsAuthorizationHeader()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, _successResponse);
        var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient);

        await service.GetChessResponseAsync("e4", "game-1", new List<ChatMessage>());

        Assert.NotNull(handler.LastRequest);
        var authHeader = handler.LastRequest.Headers.Authorization;
        Assert.NotNull(authHeader);
        Assert.Equal("Bearer", authHeader.Scheme);
        Assert.Equal("fake-groq-key", authHeader.Parameter);
    }

    [Fact]
    public async Task RequestBody_ContainsModelAndMessages()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, _successResponse);
        var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient);

        await service.GetChessResponseAsync("e4", "game-1", new List<ChatMessage>());

        Assert.NotNull(handler.LastRequestContent);
        Assert.Contains("meta-llama/llama-4-scout-17b-16e-instruct", handler.LastRequestContent);
        Assert.Contains("\"messages\"", handler.LastRequestContent);
        Assert.Contains("\"temperature\"", handler.LastRequestContent);
    }

    [Fact]
    public async Task ConversationHistory_IsIncludedInRequest()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, _successResponse);
        var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient);

        var history = new List<ChatMessage>
        {
            new() { Speaker = "User", Message = "Hello" },
            new() { Speaker = "AI", Message = "Hi there!" }
        };

        await service.GetChessResponseAsync("What now?", "game-1", history);

        Assert.NotNull(handler.LastRequestContent);
        Assert.Contains("Hello", handler.LastRequestContent);
        Assert.Contains("Hi there!", handler.LastRequestContent);
    }

    [Fact]
    public async Task PositionContext_IsFormattedInRequest()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, _successResponse);
        var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient);

        var context = new ChessPositionContext
        {
            PlayerColor = "Black",
            MoveNumber = 10,
            GamePhase = "Middlegame"
        };

        await service.GetChessResponseAsync("Nice move!", "game-1", new List<ChatMessage>(), context);

        Assert.NotNull(handler.LastRequestContent);
        Assert.Contains("Black", handler.LastRequestContent);
        Assert.Contains("Middlegame", handler.LastRequestContent);
    }
}
