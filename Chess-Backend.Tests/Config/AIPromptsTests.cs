using Chess_Backend.Config;

namespace Chess_Backend.Tests.Config;

public class AIPromptsTests
{
    [Fact]
    public void ChessOpponentSystemPrompt_IsNotNullOrEmpty()
    {
        Assert.False(string.IsNullOrEmpty(AIPrompts.ChessOpponentSystemPrompt));
    }

    [Fact]
    public void ChessOpponentSystemPrompt_ContainsWittyPersonality()
    {
        Assert.Contains("witty", AIPrompts.ChessOpponentSystemPrompt);
        Assert.Contains("trash-talking", AIPrompts.ChessOpponentSystemPrompt);
    }

    [Fact]
    public void ChessOpponentSystemPrompt_EnforcesResponseLength()
    {
        Assert.Contains("2 sentences", AIPrompts.ChessOpponentSystemPrompt);
    }

    [Fact]
    public void ChessOpponentSystemPrompt_ForbidsEmojis()
    {
        Assert.Contains("No emojis", AIPrompts.ChessOpponentSystemPrompt);
    }

    [Fact]
    public void ChessOpponentSystemPrompt_SpeaksAsPlayer()
    {
        Assert.Contains("my pieces", AIPrompts.ChessOpponentSystemPrompt);
        Assert.Contains("your king", AIPrompts.ChessOpponentSystemPrompt);
    }
}
