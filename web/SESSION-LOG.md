# Session Log — NAB XLIFF Web & Desktop Development

## Session: 2026-03-24

### Context
Exploratory development session building a web UI and desktop app for NAB AL Tools' XLIFF translation management. Started from an existing Blazor Server experiment on `feature/webui` branch.

### What was built

#### 1. Documentation System
- Created comprehensive 7-tab documentation page (`Documentation.razor`)
- Tabs: Oversigt, Workflow, Arkitektur, AI-integration, MCP Server, Cloud, Det smarte
- All in Danish with technical depth
- Visual diagrams built in CSS/HTML (architecture layers, flow diagrams, sequence diagrams, comparison tables)
- Standalone HTML guide at `docs/nab-al-tools-guide.html` (later superseded by Blazor version)

#### 2. MCP Server Deep Dive
- Added detailed MCP tab with:
  - Expandable tool cards (9 tools with parameters, output, practical notes)
  - Process diagram showing Blazor ↔ Node.js child process communication
  - 7-step sequence diagram of the Blazor startup lifecycle
  - Full communication path visualization (User → Blazor → stdin → Node.js → disk)
  - Pagination, propagation, and status-system explanations
  - Tool annotations documentation
  - Error handling examples

#### 3. Cloud Architecture Analysis
- Documented 3 cloud approaches (Sidecar agent, HTTP Tunnel, Git Sync)
- Added comparison table with 6 criteria
- Documented 3 local app approaches (PWA, MAUI Hybrid, Self-contained)
- Full comparison table across all 6 approaches
- Recommended progression: PWA → MAUI Hybrid

#### 4. New Features in Blazor Web App
- **Create new language**: `createLanguageXlf` MCP tool exposed in Home.razor wizard
  - Language dropdown (21 languages) + custom code input
  - Match Base App option
  - Auto-refresh file list after creation
- **Fixed AI chat file path**: System prompt now includes XLF file path so AI doesn't ask user for it
- **Fixed createLanguageXlf response parsing**: MCP returns plain text, not JSON — added regex parser

#### 5. MAUI Desktop App (NabXliffApp)
- Created complete .NET MAUI Blazor Hybrid project
- 32+ files: csproj, MauiProgram.cs, App.xaml, MainPage.xaml, all components adapted
- Key adaptations:
  - Removed all `@rendermode InteractiveServer`
  - Removed ReconnectModal (no server connection)
  - Added explicit `using Microsoft.Extensions.Configuration/Logging`
  - Inlined scoped CSS into app.css (MAUI doesn't generate CSS bundles)
  - Removed missing Styles/Fonts XAML references
  - Added `McpBridge.OnStateChanged` event for NavMenu reactivity
- Editable Settings page saving to `~/.nab-xliff-web/appsettings.json`
- Configuration: bundled defaults + user overrides via ConfigurationBuilder

#### 6. Glossary Management Page
- 3-tab Glossary page:
  - **Built-in**: View 187 BC terms in 24 languages with filter/pagination
  - **Local**: Full CRUD editor — load, add, edit, delete, save to TSV file
  - **Extract from XLF**: Frequency analysis of translated texts, auto-exclude BC terms, select and add to local glossary

#### 7. Landing Page
- Danish (`web/landing/index.html`) and English (`web/landing/en.html`) versions
- Dark theme matching app aesthetic
- Sections: Hero, Features (6 cards), Screenshots (3 placeholders), Installation (4 steps), System Requirements
- Language switcher in nav
- Self-contained HTML with inline CSS, Google Fonts only

#### 8. CI/CD Pipeline
- `.github/workflows/desktop-app.yml`:
  - Builds MAUI app (self-contained win-x64)
  - Deploys landing page to GitHub Pages
  - Creates GitHub Release with zip artifact (manual trigger)

### Bugs Fixed
| Bug | Cause | Fix |
|-----|-------|-----|
| AI asks for file path | System prompt didn't include XLF path | Inject path in `BuildChatSystemPrompt` |
| createLanguageXlf JSON parse error | MCP returns plain text "Successfully..." | Regex parser `ParseCreateLanguageResult` |
| NavMenu stays "Not connected" | No re-render after MCP init | `OnStateChanged` event + NavMenu subscription |
| MAUI app won't start | Missing Styles.xaml/Colors.xaml references | Removed from App.xaml |
| MAUI app no styling | Scoped CSS bundle not generated | Inlined all styles in app.css |
| MAUI build fails (NU1605) | Preview package version conflicts | Updated to stable 10.0.x versions |
| MAUI build fails (NETSDK1112) | Restore without RuntimeIdentifier | Removed separate restore/build, use `dotnet publish` directly |
| Settings overwritten on rebuild | appsettings.json in build output | Save to user profile instead |

### Key Decisions
1. **Copy-don't-reference** for MAUI: Web project uses SDK.Web which can't be referenced by MAUI. Copied services/models/components instead of shared library (simpler).
2. **Scoped CSS → app.css** in MAUI: MAUI Blazor Hybrid doesn't generate `.styles.css` bundles. All layout CSS moved to global stylesheet.
3. **User profile for settings**: `~/.nab-xliff-web/appsettings.json` survives rebuilds and isn't in git.
4. **Event-based NavMenu**: MAUI doesn't auto-re-render across components like Blazor Server's SignalR circuit does.

### Files Created/Modified
~50 new files across NabXliffApp/, landing/, docs/, .github/workflows/
~15 modified files in NabXliffWeb/ (features, fixes, documentation page)
