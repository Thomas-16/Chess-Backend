using Chess_Backend.Models;
using Chess_Backend.Services;

namespace Chess_Backend.Tests.Services;

public class FormatMessageWithContextTests
{
    [Fact]
    public void NullContext_ReturnsPlainMessage()
    {
        string result = OpenAIService.FormatMessageWithContext("Hello", null);
        Assert.Equal("Hello", result);
    }

    [Fact]
    public void IncludesPlayerColor()
    {
        var ctx = CreateContext(playerColor: "Black");
        string result = OpenAIService.FormatMessageWithContext("test", ctx);
        Assert.Contains("You: Black", result);
    }

    [Fact]
    public void IncludesMoveNumberAndTurn()
    {
        var ctx = CreateContext(moveNumber: 15, turnToMove: "Black");
        string result = OpenAIService.FormatMessageWithContext("test", ctx);
        Assert.Contains("Move 15 (Black to move)", result);
    }

    [Fact]
    public void PositiveMaterialDifference_HasPlusSign()
    {
        var ctx = CreateContext(playerMaterial: 3900, opponentMaterial: 3600);
        string result = OpenAIService.FormatMessageWithContext("test", ctx);
        Assert.Contains("Material: +300cp", result);
    }

    [Fact]
    public void NegativeMaterialDifference_HasMinusSign()
    {
        var ctx = CreateContext(playerMaterial: 3000, opponentMaterial: 3500);
        string result = OpenAIService.FormatMessageWithContext("test", ctx);
        Assert.Contains("Material: -500cp", result);
    }

    [Fact]
    public void EqualMaterial_ShowsZero()
    {
        var ctx = CreateContext(playerMaterial: 3900, opponentMaterial: 3900);
        string result = OpenAIService.FormatMessageWithContext("test", ctx);
        Assert.Contains("Material: 0cp", result);
    }

    [Fact]
    public void IncludesPieceCounts()
    {
        var ctx = CreateContext(playerPieces: new[] { "Q", "R", "B" }, opponentPieces: new[] { "R", "N" });
        string result = OpenAIService.FormatMessageWithContext("test", ctx);
        Assert.Contains("You: Q+R+B", result);
        Assert.Contains("Opp: R+N", result);
    }

    [Fact]
    public void IncludesRecentMoves_WhenPresent()
    {
        var ctx = CreateContext(recentMoves: new[] { "e4", "e5", "Nf3" });
        string result = OpenAIService.FormatMessageWithContext("test", ctx);
        Assert.Contains("Last moves: e4, e5, Nf3", result);
    }

    [Fact]
    public void OmitsRecentMoves_WhenEmpty()
    {
        var ctx = CreateContext(recentMoves: Array.Empty<string>());
        string result = OpenAIService.FormatMessageWithContext("test", ctx);
        Assert.DoesNotContain("Last moves", result);
    }

    [Fact]
    public void IncludesGamePhase()
    {
        var ctx = CreateContext(gamePhase: "Middlegame");
        string result = OpenAIService.FormatMessageWithContext("test", ctx);
        Assert.Contains("Phase: Middlegame", result);
    }

    [Fact]
    public void IncludesCheckStatus_WhenInCheck()
    {
        var ctx = CreateContext(isInCheck: true);
        string result = OpenAIService.FormatMessageWithContext("test", ctx);
        Assert.Contains("In check!", result);
    }

    [Fact]
    public void OmitsCheckStatus_WhenNotInCheck()
    {
        var ctx = CreateContext(isInCheck: false);
        string result = OpenAIService.FormatMessageWithContext("test", ctx);
        Assert.DoesNotContain("In check!", result);
    }

    [Fact]
    public void IncludesLegalMoveCount_WhenProvided()
    {
        var ctx = CreateContext(legalMoveCount: 3);
        string result = OpenAIService.FormatMessageWithContext("test", ctx);
        Assert.Contains("Only 3 legal moves", result);
    }

    [Fact]
    public void OmitsLegalMoveCount_WhenNull()
    {
        var ctx = CreateContext(legalMoveCount: null);
        string result = OpenAIService.FormatMessageWithContext("test", ctx);
        Assert.DoesNotContain("legal moves", result);
    }

    [Fact]
    public void IncludesUserMessage_AtEnd()
    {
        var ctx = CreateContext();
        string result = OpenAIService.FormatMessageWithContext("Nice move!", ctx);
        Assert.Contains("User message: Nice move!", result);
    }

    [Fact]
    public void IncludesPositionPerspectiveLabel()
    {
        var ctx = CreateContext();
        string result = OpenAIService.FormatMessageWithContext("test", ctx);
        Assert.Contains("[Position from the perspective of YOU, the AI]", result);
    }

    private static ChessPositionContext CreateContext(
        string playerColor = "White",
        int moveNumber = 1,
        string turnToMove = "White",
        int playerMaterial = 3900,
        int opponentMaterial = 3900,
        string[]? playerPieces = null,
        string[]? opponentPieces = null,
        string[]? recentMoves = null,
        string gamePhase = "Opening",
        bool isInCheck = false,
        int? legalMoveCount = null)
    {
        return new ChessPositionContext
        {
            PlayerColor = playerColor,
            MoveNumber = moveNumber,
            TurnToMove = turnToMove,
            PlayerMaterialCentipawns = playerMaterial,
            OpponentMaterialCentipawns = opponentMaterial,
            PlayerPieces = playerPieces ?? new[] { "K", "Q", "R", "R", "B", "B", "N", "N" },
            OpponentPieces = opponentPieces ?? new[] { "K", "Q", "R", "R", "B", "B", "N", "N" },
            RecentMoves = recentMoves ?? new[] { "e4" },
            GamePhase = gamePhase,
            IsInCheck = isInCheck,
            LegalMoveCount = legalMoveCount
        };
    }
}
