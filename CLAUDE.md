# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
dotnet build                          # Build the project
dotnet run                            # Run on http://localhost:5032
dotnet run --launch-profile https     # Run on https://localhost:7049
dotnet ef migrations add <Name>       # Create EF Core migration
dotnet ef database update             # Apply pending migrations
```

No test project is configured. Health endpoints: `GET /health`, `GET /health/ready`.

## Architecture

ASP.NET Core (.NET 8.0) backend for an interactive chess game with AI chat and voice. Uses SignalR for real-time communication with the Chess-React frontend.

### SignalR Hub (`Hubs/ChessHub.cs`)
Single hub at `/chesshub`. All game and chat communication goes through here:
- `StartNewGame()` → creates DB game record, returns game ID
- `SendChatMessage(gameId, message, context?)` → gets AI response + TTS audio, saves to DB
- `SaveGameResult(gameId, pgn, result)` → persists final game state
- `GetChatHistory(gameId)` → returns conversation for a game

### LLM Provider System
Two interchangeable AI services selected by `LLM_PROVIDER` env var:
- `Services/OpenAIService.cs` — GPT-5-nano with reasoning (`ReasoningEffort.Low`)
- `Services/GroqService.cs` — Llama 4 Scout via HTTP (not SDK)

Both implement the same `GetChessResponseAsync(message, conversationHistory, positionContext?)` signature. ChessHub resolves which to call at construction time.

### AI Personality (`Config/AIPrompts.cs`)
Single static class holding the system prompt. The AI plays as a witty, trash-talking chess opponent. Responses are kept under 2 sentences. The prompt includes rules about when to use chess notation and how to react to game events.

### Voice (`Services/VoiceService.cs`)
ElevenLabs TTS converts AI text responses to base64-encoded audio, returned alongside the text in SignalR responses.

### Database
PostgreSQL via Entity Framework Core. Two tables:
- `Games` — PGN, result, timestamps
- `ChatMessages` — speaker, message text, linked to game via `GameId`

`ChessDbContext` auto-parses `DATABASE_URL` (Render format) or falls back to local PostgreSQL. `EnsureCreated()` runs on startup.

### Conversation Context
Last 6 messages retrieved from DB for each AI call. `ChessPositionContext` (sent from frontend) provides current board state, material balance, game phase, and recent moves to the AI.

## Namespace

The C# namespace is `Chess_Backend` (underscore, not hyphen).

## Environment Variables

| Variable | Required | Purpose |
|----------|----------|---------|
| `LLM_PROVIDER` | Yes | `OPENAI` or `GROQ` |
| `OPENAI_API_KEY` | If OPENAI | OpenAI API key |
| `GROQ_API_KEY` | If GROQ | Groq API key |
| `ELEVENLABS_API_KEY` | Yes | ElevenLabs TTS key |
| `DATABASE_URL` | No | PostgreSQL connection (Render format); defaults to local |

## Deployment

Docker multi-stage build available. Production runs on port 5000 (`ASPNETCORE_URLS=http://+:5000`). Currently deployed on Render.
