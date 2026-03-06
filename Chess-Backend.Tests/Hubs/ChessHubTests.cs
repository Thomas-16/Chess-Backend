using System.Net;
using Chess_Backend.Data;
using Chess_Backend.Hubs;
using Chess_Backend.Models;
using Chess_Backend.Services;
using Chess_Backend.Tests.Helpers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Chess_Backend.Tests.Hubs;

public class ChessHubTests : IDisposable
{
    private readonly ChessDbContext _context;
    private readonly ChessHub _hub;
    private readonly Mock<IHubCallerClients> _mockClients;
    private readonly Mock<ISingleClientProxy> _mockCallerProxy;

    public ChessHubTests()
    {
        _context = TestHelper.CreateInMemoryContext();

        // Set LLM_PROVIDER for the hub constructor
        Environment.SetEnvironmentVariable("LLM_PROVIDER", "GROQ");

        // Create GroqService with mock HTTP that returns a valid response
        var groqHandler = new MockHttpMessageHandler(HttpStatusCode.OK, """
            {"choices": [{"message": {"content": "Interesting move!"}}]}
            """);
        var groqHttpClient = new HttpClient(groqHandler);
        var groqConfig = TestHelper.CreateFakeConfiguration();
        var groqService = new GroqService(groqHttpClient, groqConfig, TestHelper.CreateMockLogger<GroqService>());

        // Create OpenAIService - need to set env var since it uses OpenAI SDK
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "fake-openai-key");
        var openAiConfig = TestHelper.CreateFakeConfiguration();
        var openAiService = new OpenAIService(openAiConfig, TestHelper.CreateMockLogger<OpenAIService>());

        // Create VoiceService with mock HTTP
        var voiceHandler = new MockHttpMessageHandler(HttpStatusCode.OK, new byte[] { 0x00, 0x01 });
        var voiceHttpClient = new HttpClient(voiceHandler);
        var voiceConfig = TestHelper.CreateFakeConfiguration();
        var voiceService = new VoiceService(voiceHttpClient, voiceConfig, TestHelper.CreateMockLogger<VoiceService>());

        var logger = TestHelper.CreateMockLogger<ChessHub>();

        _hub = new ChessHub(_context, groqService, openAiService, voiceService, logger);

        // Mock SignalR clients
        _mockClients = new Mock<IHubCallerClients>();
        _mockCallerProxy = new Mock<ISingleClientProxy>();
        _mockClients.Setup(c => c.Caller).Returns(_mockCallerProxy.Object);
        _hub.Clients = _mockClients.Object;

        // Mock hub context
        var mockContext = new Mock<HubCallerContext>();
        mockContext.Setup(c => c.ConnectionId).Returns("test-connection-id");
        _hub.Context = mockContext.Object;
    }

    public void Dispose()
    {
        _context.Dispose();
        _hub.Dispose();
    }

    [Fact]
    public async Task StartNewGame_CreatesGameInDatabase()
    {
        var gameId = await _hub.StartNewGame();

        Assert.False(string.IsNullOrEmpty(gameId));
        var game = await _context.Games.FindAsync(gameId);
        Assert.NotNull(game);
    }

    [Fact]
    public async Task StartNewGame_ReturnsValidGuidId()
    {
        var gameId = await _hub.StartNewGame();
        Assert.True(Guid.TryParse(gameId, out _));
    }

    [Fact]
    public async Task StartNewGame_MultipleCallsCreateDifferentGames()
    {
        var id1 = await _hub.StartNewGame();
        var id2 = await _hub.StartNewGame();

        Assert.NotEqual(id1, id2);
        Assert.Equal(2, await _context.Games.CountAsync());
    }

    [Fact]
    public async Task SendChatMessage_SavesUserAndAIMessages()
    {
        var gameId = await _hub.StartNewGame();

        await _hub.SendChatMessage(gameId, "e4");

        var messages = await _context.ChatMessages
            .Where(m => m.GameId == gameId)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        Assert.Equal(2, messages.Count);
        Assert.Equal("User", messages[0].Speaker);
        Assert.Equal("e4", messages[0].Message);
        Assert.Equal("AI", messages[1].Speaker);
        Assert.False(string.IsNullOrEmpty(messages[1].Message));
    }

    [Fact]
    public async Task SendChatMessage_SendsResponseToCaller()
    {
        var gameId = await _hub.StartNewGame();

        await _hub.SendChatMessage(gameId, "e4");

        _mockCallerProxy.Verify(
            c => c.SendCoreAsync("ReceiveChatResponse", It.IsAny<object?[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task SendChatMessage_InvalidGameId_SendsErrorToCaller()
    {
        await _hub.SendChatMessage("nonexistent-game", "e4");

        _mockCallerProxy.Verify(
            c => c.SendCoreAsync("ChatError", It.IsAny<object?[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task SendChatMessage_WithPositionContext_Succeeds()
    {
        var gameId = await _hub.StartNewGame();
        var context = new ChessPositionContext
        {
            PlayerColor = "White",
            MoveNumber = 5,
            GamePhase = "Opening"
        };

        await _hub.SendChatMessage(gameId, "Nf3", context);

        var messages = await _context.ChatMessages.Where(m => m.GameId == gameId).ToListAsync();
        Assert.Equal(2, messages.Count);
    }

    [Fact]
    public async Task SaveGameResult_UpdatesGameRecord()
    {
        var gameId = await _hub.StartNewGame();
        string pgn = "1. e4 e5 2. Nf3 Nc6";
        string result = "1-0";

        await _hub.SaveGameResult(gameId, pgn, result);

        var game = await _context.Games.FindAsync(gameId);
        Assert.NotNull(game);
        Assert.Equal(pgn, game.PGN);
        Assert.Equal(result, game.Result);
        Assert.NotNull(game.EndTime);
    }

    [Fact]
    public async Task SaveGameResult_SendsGameSavedConfirmation()
    {
        var gameId = await _hub.StartNewGame();

        await _hub.SaveGameResult(gameId, "1. e4", "1-0");

        _mockCallerProxy.Verify(
            c => c.SendCoreAsync("GameSaved", It.IsAny<object?[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task SaveGameResult_InvalidGameId_SendsError()
    {
        await _hub.SaveGameResult("nonexistent", "1. e4", "1-0");

        _mockCallerProxy.Verify(
            c => c.SendCoreAsync("SaveError", It.IsAny<object?[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task GetChatHistory_ReturnsMessagesOrderedByTimestamp()
    {
        var gameId = await _hub.StartNewGame();

        // Add messages with explicit timestamps
        _context.ChatMessages.Add(new ChatMessage
        {
            GameId = gameId, Speaker = "User", Message = "First",
            Timestamp = DateTime.UtcNow.AddMinutes(-2)
        });
        _context.ChatMessages.Add(new ChatMessage
        {
            GameId = gameId, Speaker = "AI", Message = "Second",
            Timestamp = DateTime.UtcNow.AddMinutes(-1)
        });
        _context.ChatMessages.Add(new ChatMessage
        {
            GameId = gameId, Speaker = "User", Message = "Third",
            Timestamp = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        await _hub.GetChatHistory(gameId);

        _mockCallerProxy.Verify(
            c => c.SendCoreAsync("ChatHistory", It.IsAny<object?[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task GetChatHistory_EmptyGame_SendsEmptyList()
    {
        var gameId = await _hub.StartNewGame();

        await _hub.GetChatHistory(gameId);

        _mockCallerProxy.Verify(
            c => c.SendCoreAsync("ChatHistory", It.IsAny<object?[]>(), default),
            Times.Once);
    }
}
