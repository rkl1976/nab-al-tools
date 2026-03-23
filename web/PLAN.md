# NAB XLIFF Web - Implementation Plan & Status

## Overview

A Blazor Server web application for XLIFF translation management, connecting to the existing NAB AL Tools MCP server for all XLIFF operations. Azure OpenAI is used for AI-powered batch translation via Microsoft Agent Framework's `AIAgent`.

## Architecture

```
[Blazor Server UI] <--> [TranslationAgentService (AIAgent + Azure OpenAI)]
                              |
                         [McpBridgeService (MCP Client via stdio)]
                              |
                         [npx @nabsolutions/nab-al-tools-mcp]
                              |
                         [XLIFF files on disk]
```

**Key decision**: Reuses existing MCP server as the tool source -- does NOT reimplement XLIFF parsing in C#. Saves ~2000 lines of code and avoids dual maintenance.

## Technology Stack

| Component | Technology | Version |
|-----------|-----------|---------|
| Runtime | .NET 10 | 10.0 |
| UI Framework | Blazor Server (Interactive) | .NET 10 |
| MCP Client | ModelContextProtocol C# SDK | 1.1.0 |
| AI Agent | Microsoft.Agents.AI.OpenAI | 1.0.0-rc4 |
| Azure OpenAI | Azure.AI.OpenAI | 2.9.0-beta.1 |
| Auth | Azure.Identity | 1.19.0 |
| MCP Server | @nabsolutions/nab-al-tools-mcp | via npx |

## Project Structure

```
web/
  NabXliffWeb/
    NabXliffWeb.csproj
    Program.cs                          # DI setup, service registration
    appsettings.json                    # Azure OpenAI + MCP config
    Models/
      TranslationModels.cs              # C# records matching TS interfaces
    Services/
      McpBridgeService.cs               # Singleton - MCP server lifecycle + typed tool calls
      TranslationAgentService.cs        # Scoped - AIAgent with Azure OpenAI + MCP tools
      SessionStateService.cs            # Scoped - current file, language, filter state
    Components/
      App.razor                         # Root component
      Routes.razor                      # Routing
      _Imports.razor                    # Global usings
      Layout/
        MainLayout.razor                # App shell with sidebar + content area
        MainLayout.razor.css
        NavMenu.razor                   # Sidebar navigation
        NavMenu.razor.css
      Pages/
        Home.razor                      # Project setup, MCP initialize, XLF file picker
        Chat.razor                      # Free-form chat with AI agent (Copilot-style)
        Translate.razor                 # Untranslated texts, inline edit, AI batch translate
        Review.razor                    # Filter by state, approve/reject, inline edit
        Search.razor                    # Keyword/regex search in source/target
        Settings.razor                  # Azure OpenAI config display
        Error.razor                     # Error page (template)
        NotFound.razor                  # 404 page (template)
    wwwroot/
      app.css                           # Global styles (custom design system)
```

## Key Services

### McpBridgeService (Singleton)
- Starts MCP server via `McpClient.CreateAsync(StdioClientTransport)` with `npx @nabsolutions/nab-al-tools-mcp`
- Calls `initialize` tool with appFolderPath
- Exposes typed methods: `GetTextsToTranslateAsync()`, `SaveTranslatedTextsAsync()`, `GetTranslatedTextsByStateAsync()`, `GetTextsByKeywordAsync()`, `GetGlossaryTermsAsync()`, `RefreshXlfAsync()`
- Exposes `IList<AITool>` for agent service
- Implements `IAsyncDisposable` for clean shutdown

### TranslationAgentService (Scoped)
- Creates `AIAgent` via: `AzureOpenAIClient.GetChatClient().AsIChatClient().AsAIAgent(instructions, tools)`
- System prompt includes: target language, glossary terms, translation rules (preserve %1/%2, respect maxLength)
- Batch translate: sends N texts to agent, parses JSON array response to `TranslationToSave[]`
- Streaming support via `RunStreamingAsync()`
- Supports both API key and DefaultAzureCredential authentication

### SessionStateService (Scoped)
- Holds current: filePath, targetLanguage, pageOffset/pageSize, stateFilter, available XLF files

## Data Models

C# records in `Models/TranslationModels.cs` matching TypeScript interfaces from:
- `extension/src/ChatTools/shared/XliffToolsCore.ts`
- `extension/src/ChatTools/shared/GlossaryCore.ts`

```csharp
record UntranslatedText(string Id, string SourceText, string SourceLanguage, string? Comment, int? MaxLength, string Context);
record UntranslatedTextsResult(int TotalUntranslatedCount, int ReturnedCount, List<UntranslatedText> Texts);
record TranslatedTextWithState(string Id, string SourceText, string SourceLanguage, string TargetText, string[]? AlternativeTranslations, string? Comment, string? TranslationState, string? ReviewReason, int? MaxLength, string Context);
record TranslationToSave(string Id, string TargetText, string? TargetState);
record GlossaryEntry(string Source, string Target, string Description);
record TranslatedText(string SourceText, string[] TargetTexts, string SourceLanguage);
```

## Configuration (appsettings.json)

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com",
    "DeploymentName": "gpt-4o",
    "ApiKey": ""
  },
  "McpServer": {
    "Command": "npx",
    "Arguments": ["-y", "@nabsolutions/nab-al-tools-mcp"]
  },
  "Translation": {
    "DefaultBatchSize": 20,
    "DefaultPageSize": 50
  }
}
```

## Dataflow

1. **Setup**: User selects app folder -> MCP `initialize` -> loads g.xlf + settings -> discovers XLF files
2. **Browse**: `getTextsToTranslate` -> render table with inline edit per row
3. **AI Batch**: User selects N texts -> agent prompt with texts + glossary -> LLM generates -> parse -> show proposals -> user reviews -> `saveTranslatedTexts`
4. **Manual edit**: Inline edit in row -> blur/enter -> `saveTranslatedTexts` with state "translated"
5. **Review**: Filter by state -> approve (state -> "signed-off") or reject (state -> "needs-review-translation")
6. **Search**: Keyword/regex search in source or target texts -> inline edit results

## API Notes (discovered during implementation)

These are non-obvious API details that were discovered and are important for future work:

- **ModelContextProtocol 1.1.0**: `McpClient.CreateAsync()` (NOT `McpClientFactory.CreateAsync()`)
- **ContentBlock**: Abstract base class. Use `result.Content.OfType<TextContentBlock>().Select(c => c.Text)` to extract text
- **CallToolResult.IsError**: Type is `bool?` (nullable), check with `== true`
- **ChatClient -> AIAgent**: Must chain `.AsIChatClient().AsAIAgent()` (ChatClient is OpenAI SDK type, AsAIAgent requires IChatClient from Microsoft.Extensions.AI)

---

## Implementation Status

### Phase 1: Scaffold + MCP Bridge ✅ COMPLETE
- [x] `dotnet new blazor` project in `web/`
- [x] NuGet packages added (ModelContextProtocol, Microsoft.Agents.AI.OpenAI, Azure.AI.OpenAI, Azure.Identity)
- [x] `TranslationModels.cs` - all C# records matching TS interfaces
- [x] `McpBridgeService` - MCP server lifecycle, initialize, all 7 tool methods
- [x] `SessionStateService` - session state management
- [x] Build succeeds with zero errors/warnings

### Phase 2: Core UI ✅ COMPLETE
- [x] `MainLayout.razor` + `NavMenu.razor` with dark sidebar navigation
- [x] `Home.razor` - project setup, MCP initialize, XLF file picker with auto-select
- [x] `Translate.razor` - untranslated texts table, inline edit, select for batch, pagination
- [x] `Review.razor` - state filter bar, approve/reject, inline edit, double-click to edit, alternative translations
- [x] `Search.razor` - keyword/regex search with options (case sensitive, search in target, regex)
- [x] `Settings.razor` - Azure OpenAI config display with configuration guide
- [x] Custom CSS design system in `app.css` (gradient sidebar, state badges, cards, responsive)

### Phase 3: AI Agent ✅ COMPLETE
- [x] `TranslationAgentService` with Azure OpenAI + MCP tools
- [x] Batch translate with `IProgress<string>` for UI feedback
- [x] Streaming support via `RunStreamingAsync()`
- [x] Glossary injection in agent system prompt
- [x] Translation rules in system prompt (preserve placeholders, respect maxLength)
- [x] JSON parsing of agent responses
- [x] **Chat page** (`Chat.razor`) - Copilot-style free-form chat with AIAgent
  - Streaming responses with typing indicator
  - Conversation history via `AgentSession` (multi-turn)
  - Suggestion chips for common tasks
  - Markdown rendering (tables, code blocks, bold, lists, headers)
  - Agent has full MCP tool access (translate, search, review, glossary, etc.)
  - `CreateChatAgentAsync()` on `TranslationAgentService`

### Phase 4: Polish ⬚ PARTIALLY COMPLETE
- [x] Settings page for Azure OpenAI configuration display
- [x] Error handling with alert messages
- [x] Loading spinners and empty state indicators
- [ ] **GlossaryPanel** - Dedicated sidebar component showing glossary terms
- [ ] **TranslationBatchPanel** - Dedicated component with streaming progress UI
- [ ] **Keyboard shortcuts** (Ctrl+Enter to save, Escape to cancel edit)
- [ ] **Bulk approve/reject** in Review page (select multiple -> batch action)
- [ ] **Statistics dashboard** (total/untranslated/translated/needs-review counts)
- [ ] **Toast notifications** instead of inline alerts
- [ ] **Dark mode** support
- [ ] **Export/import** functionality
- [ ] **Undo** support for saves

## Known Limitations

1. **Session state is scoped** - each browser tab gets its own session, but `McpBridgeService` is singleton (shared MCP server process). If two users connect to different app folders simultaneously, they'll conflict since MCP server has global state.
2. **No authentication** - the app has no user authentication. Add ASP.NET Core Identity or Azure AD for production use.
3. **appsettings.json credentials** - API keys in appsettings.json should be moved to Azure Key Vault or user secrets for production.
4. **No WebSocket reconnect handling** - if the MCP server process dies, the user must restart the app.
5. **Translation.DefaultBatchSize/DefaultPageSize** config values are defined but not yet read by the services (hardcoded to 50).

## How to Run

```bash
cd web/NabXliffWeb
dotnet run
```

Open browser at `http://localhost:5173` (or port shown in console).

## How to Test

1. Start the app with `dotnet run`
2. On the Setup page, enter an AL app folder path (e.g., the repo's `test-app/Xliff-test`)
3. Click Initialize - should show XLF files found in Translations folder
4. Select an XLF file
5. Navigate to Translate page - should show untranslated texts
6. Test manual inline translation (type + blur or click save icon)
7. If Azure OpenAI is configured, select texts and click "AI Translate" for batch translation
8. Navigate to Review page - filter by state, approve/reject translations
9. Navigate to Search page - search for keywords in source/target texts

## Prerequisites

- .NET 10 SDK
- Node.js (for npx to run MCP server)
- Azure OpenAI deployment (endpoint + key or managed identity) - optional, only for AI translation
