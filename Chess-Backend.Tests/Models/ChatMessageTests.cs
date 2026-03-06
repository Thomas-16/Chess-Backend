using Chess_Backend.Models;

namespace Chess_Backend.Tests.Models;

public class ChatMessageTests
{
    [Fact]
    public void DefaultId_IsZero()
    {
        var msg = new ChatMessage();
        Assert.Equal(0, msg.Id);
    }

    [Fact]
    public void DefaultGameId_IsEmptyString()
    {
        var msg = new ChatMessage();
        Assert.Equal(string.Empty, msg.GameId);
    }

    [Fact]
    public void DefaultSpeaker_IsEmptyString()
    {
        var msg = new ChatMessage();
        Assert.Equal(string.Empty, msg.Speaker);
    }

    [Fact]
    public void DefaultMessage_IsEmptyString()
    {
        var msg = new ChatMessage();
        Assert.Equal(string.Empty, msg.Message);
    }

    [Fact]
    public void DefaultTimestamp_IsCloseToUtcNow()
    {
        var before = DateTime.UtcNow;
        var msg = new ChatMessage();
        var after = DateTime.UtcNow;

        Assert.InRange(msg.Timestamp, before, after);
    }

    [Fact]
    public void CanSetProperties()
    {
        var msg = new ChatMessage
        {
            GameId = "game-123",
            Speaker = "User",
            Message = "Hello!",
        };

        Assert.Equal("game-123", msg.GameId);
        Assert.Equal("User", msg.Speaker);
        Assert.Equal("Hello!", msg.Message);
    }
}
