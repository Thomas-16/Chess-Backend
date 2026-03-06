using Chess_Backend.Data;
using Chess_Backend.Models;
using Chess_Backend.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Chess_Backend.Tests.Data;

public class ChessDbContextTests : IDisposable
{
    private readonly ChessDbContext _context;

    public ChessDbContextTests()
    {
        _context = TestHelper.CreateInMemoryContext();
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task CanAddAndRetrieveGame()
    {
        var game = new Game();
        _context.Games.Add(game);
        await _context.SaveChangesAsync();

        var retrieved = await _context.Games.FindAsync(game.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(game.Id, retrieved.Id);
    }

    [Fact]
    public async Task CanAddAndRetrieveChatMessage()
    {
        var game = new Game();
        _context.Games.Add(game);
        await _context.SaveChangesAsync();

        var msg = new ChatMessage
        {
            GameId = game.Id,
            Speaker = "User",
            Message = "Hello!"
        };
        _context.ChatMessages.Add(msg);
        await _context.SaveChangesAsync();

        var retrieved = await _context.ChatMessages.FirstOrDefaultAsync(m => m.GameId == game.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("User", retrieved.Speaker);
        Assert.Equal("Hello!", retrieved.Message);
    }

    [Fact]
    public async Task GameChatMessages_NavigationProperty_Works()
    {
        var game = new Game();
        _context.Games.Add(game);
        await _context.SaveChangesAsync();

        _context.ChatMessages.Add(new ChatMessage { GameId = game.Id, Speaker = "User", Message = "First" });
        _context.ChatMessages.Add(new ChatMessage { GameId = game.Id, Speaker = "AI", Message = "Second" });
        await _context.SaveChangesAsync();

        var gameWithMessages = await _context.Games
            .Include(g => g.ChatMessages)
            .FirstAsync(g => g.Id == game.Id);

        Assert.Equal(2, gameWithMessages.ChatMessages.Count);
    }

    [Fact]
    public async Task CascadeDelete_RemovesChatMessagesWhenGameDeleted()
    {
        var game = new Game();
        _context.Games.Add(game);
        await _context.SaveChangesAsync();

        _context.ChatMessages.Add(new ChatMessage { GameId = game.Id, Speaker = "User", Message = "Test" });
        _context.ChatMessages.Add(new ChatMessage { GameId = game.Id, Speaker = "AI", Message = "Response" });
        await _context.SaveChangesAsync();

        Assert.Equal(2, await _context.ChatMessages.CountAsync(m => m.GameId == game.Id));

        _context.Games.Remove(game);
        await _context.SaveChangesAsync();

        Assert.Equal(0, await _context.ChatMessages.CountAsync(m => m.GameId == game.Id));
    }

    [Fact]
    public async Task MultipleGames_HaveSeparateChatMessages()
    {
        var game1 = new Game();
        var game2 = new Game();
        _context.Games.AddRange(game1, game2);
        await _context.SaveChangesAsync();

        _context.ChatMessages.Add(new ChatMessage { GameId = game1.Id, Speaker = "User", Message = "Game1 msg" });
        _context.ChatMessages.Add(new ChatMessage { GameId = game2.Id, Speaker = "User", Message = "Game2 msg1" });
        _context.ChatMessages.Add(new ChatMessage { GameId = game2.Id, Speaker = "AI", Message = "Game2 msg2" });
        await _context.SaveChangesAsync();

        var game1Messages = await _context.ChatMessages.Where(m => m.GameId == game1.Id).ToListAsync();
        var game2Messages = await _context.ChatMessages.Where(m => m.GameId == game2.Id).ToListAsync();

        Assert.Single(game1Messages);
        Assert.Equal(2, game2Messages.Count);
    }

    [Fact]
    public async Task ChatMessage_ForeignKey_LinksToGame()
    {
        var game = new Game();
        _context.Games.Add(game);
        await _context.SaveChangesAsync();

        var msg = new ChatMessage { GameId = game.Id, Speaker = "User", Message = "Test" };
        _context.ChatMessages.Add(msg);
        await _context.SaveChangesAsync();

        var retrieved = await _context.ChatMessages
            .Include(m => m.Game)
            .FirstAsync(m => m.Id == msg.Id);

        Assert.NotNull(retrieved.Game);
        Assert.Equal(game.Id, retrieved.Game.Id);
    }
}
