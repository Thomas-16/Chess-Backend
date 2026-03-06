using Chess_Backend.Models;

namespace Chess_Backend.Tests.Models;

public class GameTests
{
    [Fact]
    public void DefaultId_IsNonEmptyGuidString()
    {
        var game = new Game();

        Assert.False(string.IsNullOrEmpty(game.Id));
        Assert.True(Guid.TryParse(game.Id, out _));
    }

    [Fact]
    public void DefaultPGN_IsEmptyString()
    {
        var game = new Game();
        Assert.Equal(string.Empty, game.PGN);
    }

    [Fact]
    public void DefaultResult_IsEmptyString()
    {
        var game = new Game();
        Assert.Equal(string.Empty, game.Result);
    }

    [Fact]
    public void DefaultStartTime_IsCloseToUtcNow()
    {
        var before = DateTime.UtcNow;
        var game = new Game();
        var after = DateTime.UtcNow;

        Assert.InRange(game.StartTime, before, after);
    }

    [Fact]
    public void DefaultEndTime_IsNull()
    {
        var game = new Game();
        Assert.Null(game.EndTime);
    }

    [Fact]
    public void DefaultChatMessages_IsEmptyCollection()
    {
        var game = new Game();
        Assert.NotNull(game.ChatMessages);
        Assert.Empty(game.ChatMessages);
    }

    [Fact]
    public void TwoGames_HaveDifferentIds()
    {
        var game1 = new Game();
        var game2 = new Game();
        Assert.NotEqual(game1.Id, game2.Id);
    }
}
