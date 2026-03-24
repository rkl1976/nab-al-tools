# CLAUDE.md — Web & Desktop Projects

This file provides guidance for the Blazor web app and MAUI desktop app in the `web/` directory.
For the VS Code extension, see the root `CLAUDE.md`.

## Project Structure

```
web/
├── NabXliffWeb/     # Blazor Server web app (development/debugging)
├── NabXliffApp/     # .NET MAUI Blazor Hybrid desktop app (distribution)
├── landing/         # Static landing page (GitHub Pages)
│   ├── index.html   # Danish version
│   └── en.html      # English version
└── PLAN.md          # Original implementation plan
```

## Architecture

```
User (Browser or Desktop)
  ↓
Blazor UI (Razor components)
  ↓
Services (C# DI)
  ├── McpBridgeService    → stdio → Node.js MCP Server → .xlf files
  ├── TranslationAgentService → Azure OpenAI (chat + tools)
  └── SessionStateService → ~/.nab-xliff-web/config.json
```

### Two hosting modes, shared components

| | NabXliffWeb (Blazor Server) | NabXliffApp (MAUI Hybrid) |
|---|---|---|
| **SDK** | Microsoft.NET.Sdk.Web | Microsoft.NET.Sdk.Razor + MAUI |
| **Render mode** | InteractiveServer (SignalR) | Native WebView2 (no server) |
| **Host** | Kestrel HTTP server | WinUI3 window |
| **Target** | Development, debugging | End-user distribution |
| **Config** | appsettings.json (project dir) | ~/.nab-xliff-web/appsettings.json |

### Key difference: MAUI components must NOT use
- `@rendermode InteractiveServer` (always interactive in MAUI)
- `ReconnectModal` (no server connection to lose)
- `HttpContext` (does not exist in MAUI)
- `@using static Microsoft.AspNetCore.Components.Web.RenderMode`

## Build & Run

```bash
# Blazor Server (development)
cd web/NabXliffWeb
dotnet run                    # http://localhost:5299

# MAUI Desktop (run directly)
cd web/NabXliffApp
dotnet build
bin\Debug\net10.0-windows10.0.19041.0\win-x64\NabXliffApp.exe

# Publish MAUI as self-contained
dotnet publish -c Release -r win-x64 --self-contained -o ./publish
```

## Services

### McpBridgeService (Singleton)
- Starts Node.js MCP server as child process via `StdioClientTransport`
- Command configured in appsettings.json: `npx -y @nabsolutions/nab-al-tools-mcp`
- Fires `OnStateChanged` event when initialized (MAUI NavMenu listens)
- All tool calls go through `CallToolAsync()` → stdin JSON → stdout JSON

### TranslationAgentService (Scoped)
- Creates Azure OpenAI chat agents with MCP tools
- System prompt includes XLF file path and target language
- Supports streaming responses for chat interface

### SessionStateService (Scoped)
- Persists state to `~/.nab-xliff-web/config.json`
- Tracks: AppFolderPath, XlfFilePath, TargetLanguage, pagination

## Pages

| Route | Page | Description |
|-------|------|-------------|
| `/` | Home | Setup wizard: connect → select XLF → ready. Also creates new languages |
| `/chat` | Chat | AI chat with streaming, suggestion chips, markdown rendering |
| `/translate` | Translate | Manual translation table with batch AI translate |
| `/review` | Review | Review by state (needs-review, translated, final, signed-off) |
| `/search` | Search | Keyword/regex search in source or target text |
| `/glossary` | Glossary | View built-in BC glossary, manage local glossary, extract terms from XLF |
| `/settings` | Settings | Azure OpenAI + MCP config (editable in MAUI, read-only in web) |
| `/docs` | Documentation | 7-tab documentation with architecture diagrams |

## Configuration

User settings in MAUI are saved to `~/.nab-xliff-web/appsettings.json` (survives rebuilds).
Loaded via `ConfigurationBuilder` with bundled defaults + user overrides.

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com",
    "DeploymentName": "gpt-4.1",
    "ApiKey": "your-key"
  },
  "McpServer": {
    "Command": "npx",
    "Arguments": ["-y", "@nabsolutions/nab-al-tools-mcp"]
  }
}
```

## NuGet Dependencies

- `Azure.AI.OpenAI` 2.9.0-beta.1
- `Azure.Identity` 1.19.0
- `Microsoft.Agents.AI.OpenAI` 1.0.0-rc4
- `ModelContextProtocol` 1.1.0
- `Microsoft.AspNetCore.Components.WebView.Maui` (MAUI only)

## Coding Conventions

- Follow root CLAUDE.md conventions (TypeScript strict for extension, C# for web)
- Razor pages: no `@rendermode` in MAUI versions
- Services: always add `using Microsoft.Extensions.Configuration` and `using Microsoft.Extensions.Logging` explicitly in MAUI (not implicit like in SDK.Web)
- Scoped CSS does NOT work in MAUI Blazor Hybrid — put all styles in `wwwroot/app.css`
- NavMenu uses `McpBridge.OnStateChanged` event to re-render after initialization

## CI/CD

- `.github/workflows/desktop-app.yml` — Builds MAUI app + deploys landing page to GitHub Pages
- Landing page served from `web/landing/` directory
- MAUI app published as `NabXliffApp-win-x64.zip` in GitHub Releases
