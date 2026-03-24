# Roadmap — NAB XLIFF Web & Desktop

## Completed

### Phase 1: Blazor Server App (NabXliffWeb)
- [x] Project scaffold with .NET 10 Blazor Server
- [x] MCP Bridge service (StdioClientTransport → Node.js MCP server)
- [x] Session state service with persistence
- [x] Setup wizard (connect → select XLF → ready)
- [x] Chat page with AI streaming and markdown rendering
- [x] Translate page with batch AI translation
- [x] Review page with state filtering
- [x] Search page with regex and keyword support
- [x] Settings page (read-only config display)
- [x] Navigation with MCP connection status

### Phase 2: Documentation System
- [x] 7-tab documentation page (Oversigt, Workflow, Arkitektur, AI, MCP Server, Cloud, Det smarte)
- [x] Visual architecture diagrams (CSS/HTML)
- [x] MCP tool-by-tool documentation with expandable details
- [x] Process diagrams (Blazor ↔ Node.js communication)
- [x] Cloud architecture analysis (Sidecar, Tunnel, Git Sync)
- [x] PWA/MAUI/Self-contained comparison
- [x] Standalone HTML documentation page (docs/nab-al-tools-guide.html)

### Phase 3: Feature Additions
- [x] Create new language file (createLanguageXlf via MCP)
- [x] Fix AI chat file path injection (system prompt includes XLF path)
- [x] Fix MCP createLanguageXlf response parsing (plain text, not JSON)
- [x] Documentation nav menu item

### Phase 4: MAUI Desktop App (NabXliffApp)
- [x] MAUI Blazor Hybrid project (net10.0-windows)
- [x] All components adapted (removed @rendermode, ReconnectModal, HttpContext)
- [x] Services copied with correct namespaces and usings
- [x] Fix scoped CSS (inlined in app.css, not bundle)
- [x] Fix missing styles/fonts XAML references
- [x] NavMenu state change event (OnStateChanged)
- [x] Editable Settings page with user profile persistence (~/.nab-xliff-web/)
- [x] Glossary page with 3 tabs:
  - Built-in BC glossary viewer (187 terms, 24 languages)
  - Local glossary editor (CRUD, save to TSV)
  - Term extraction from XLF files (frequency analysis, BC dedup)
- [x] App icon (purple T)

### Phase 5: Distribution
- [x] Landing page (Danish + English) with download, features, installation guide
- [x] GitHub Actions workflow (build MAUI → deploy Pages → create Release)
- [x] Language switcher on landing pages

---

## In Progress

### Polish & Quality
- [ ] Toast notifications for save/load operations
- [ ] Keyboard shortcuts (Ctrl+S for save, etc.)
- [ ] Error boundary improvements in MAUI
- [ ] Loading skeletons for better perceived performance

---

## Planned

### Short Term
- [ ] Dark mode toggle in the app
- [ ] Translation statistics dashboard (progress bars, completion %)
- [ ] Export/import translations (CSV support in UI)
- [ ] Undo/redo for translation edits
- [ ] Bulk actions (select multiple → batch translate/approve)

### Medium Term
- [ ] Glossary: edit built-in terms override (local takes precedence)
- [ ] Glossary: auto-validate translations against glossary terms
- [ ] Translation memory (learn from previous translations)
- [ ] Diff view for changed source texts (show what changed)
- [ ] Multi-file support (work with multiple XLF files simultaneously)
- [ ] Auto-update check for desktop app

### Long Term
- [ ] macOS support via MAUI (net10.0-maccatalyst)
- [ ] Cloud-hosted version with sidecar agent pattern
- [ ] Team collaboration (shared glossaries, review assignments)
- [ ] Translation quality scoring (AI-powered review)
- [ ] Plugin system for custom translation providers (beyond Azure OpenAI)
- [ ] Integration with Azure DevOps / GitHub for PR-based translation workflows
