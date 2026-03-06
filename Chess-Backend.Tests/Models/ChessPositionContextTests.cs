using Chess_Backend.Models;

namespace Chess_Backend.Tests.Models;

public class ChessPositionContextTests
{
    [Fact]
    public void DefaultPlayerColor_IsWhite()
    {
        var ctx = new ChessPositionContext();
        Assert.Equal("White", ctx.PlayerColor);
    }

    [Fact]
    public void DefaultMoveNumber_IsZero()
    {
        var ctx = new ChessPositionContext();
        Assert.Equal(0, ctx.MoveNumber);
    }

    [Fact]
    public void DefaultTurnToMove_IsWhite()
    {
        var ctx = new ChessPositionContext();
        Assert.Equal("White", ctx.TurnToMove);
    }

    [Fact]
    public void DefaultMaterialValues_AreZero()
    {
        var ctx = new ChessPositionContext();
        Assert.Equal(0, ctx.PlayerMaterialCentipawns);
        Assert.Equal(0, ctx.OpponentMaterialCentipawns);
    }

    [Fact]
    public void DefaultPieceArrays_AreEmpty()
    {
        var ctx = new ChessPositionContext();
        Assert.Empty(ctx.PlayerPieces);
        Assert.Empty(ctx.OpponentPieces);
    }

    [Fact]
    public void DefaultRecentMoves_IsEmpty()
    {
        var ctx = new ChessPositionContext();
        Assert.Empty(ctx.RecentMoves);
    }

    [Fact]
    public void DefaultGamePhase_IsOpening()
    {
        var ctx = new ChessPositionContext();
        Assert.Equal("Opening", ctx.GamePhase);
    }

    [Fact]
    public void DefaultIsInCheck_IsFalse()
    {
        var ctx = new ChessPositionContext();
        Assert.False(ctx.IsInCheck);
    }

    [Fact]
    public void DefaultLegalMoveCount_IsNull()
    {
        var ctx = new ChessPositionContext();
        Assert.Null(ctx.LegalMoveCount);
    }
}
