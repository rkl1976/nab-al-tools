# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

NAB AL Tools is a VS Code extension for AL language development and Microsoft Dynamics 365 Business Central. It provides XLIFF translation management, documentation generation, permission set management, CSV tools, and AI integration via Language Model Tools and an MCP server.

## Build & Development Commands

All commands run from the `extension/` directory:

```bash
cd extension
npm install              # Install dependencies
npm run webpack          # Development build (bundled)
npm run webpack-prod     # Production build with minification
npm run watch            # Watch mode for development
npm run test-compile     # TypeScript compilation check (outputs to out/)
npm run lint             # Prettier check + ESLint
npm run lint:fix         # Auto-fix Prettier + ESLint issues
npm run test             # Run all unit tests with c8 coverage (requires VS Code electron host)
npm run test:coverage:full  # Full coverage report
```

### Running Tests

Tests require VS Code's electron host. On headless Linux environments:
```bash
xvfb-run --auto-servernum --server-args="-screen 0 1280x1024x24" npm run test
```

On Windows/macOS with a display, `npm run test` works directly.

To debug interactively: open `nab-al-tools.code-workspace` in VS Code, use Debug menu (`Ctrl+Shift+D`), select `Automatic Tests`, press `F5`.

### Pre-commit Checklist

```bash
npm run test-compile  # Zero errors/warnings required
npm run lint          # Zero errors/warnings required
npm run webpack       # Must succeed
npm run test          # All tests must pass
```

## Architecture

### Entry Points (Webpack bundles)

| Source | Output | Purpose |
|--------|--------|---------|
| `src/extension.ts` | `dist/nab-al-tools.js` | Main VS Code extension (100+ commands) |
| `src/cli/CreateDocumentation.ts` | `dist/cli/CreateDocumentation.js` | CLI documentation tool |
| `src/cli/RefreshXLF.ts` | `dist/cli/RefreshXLF.js` | CLI XLIFF refresh tool |
| `src/mcp/server.ts` | `dist/mcp/server.js` | MCP server for AI integration |

### Critical Dependency Rule

**CLI tools and MCP server MUST NOT import `vscode`**. They run standalone outside VS Code. Use Node.js native APIs instead. Verify with:
```powershell
.\.github\workflows\scripts\vscode-dependency-test.ps1
```

### Key Source Modules (`extension/src/`)

- **`extension.ts`** — Activation, command registration
- **`NABfunctions.ts`** — Primary command implementations
- **`XliffFunctions.ts`** — XLIFF file manipulation
- **`ALParser.ts`** — AL language parsing
- **`ALObject/`** — AL object type system (16 specialized files for tables, pages, codeunits, etc.)
- **`Xliff/XLIFFDocument.ts`** — XLIFF document model
- **`ChatTools/`** — VS Code Language Model API integration
- **`mcp/`** — Model Context Protocol server (must be vscode-independent)
- **`cli/`** — CLI tools (must be vscode-independent)
- **`Settings/`** — Extension configuration management
- **`Documentation.ts`** — External documentation generation

### Frontend Webviews (`extension/frontend/`)

Webview UIs for XLIFF editing, permission set naming, and template editing.

### Test App (`test-app/`)

AL test application with XLIFF test fixtures used by the test suite.

### Template App (`template-app/`)

AL project template used by the extension's project scaffolding feature.

## Coding Conventions

- **TypeScript strict mode** — all strict checks enabled, no unused locals/parameters
- **Prettier** for formatting (double quotes, 80 char width, no semicolons enforced by ESLint)
- **Naming**: PascalCase for types/classes/enums/files/directories, camelCase for functions/variables
- **Interfaces**: PascalCase with `I` prefix (e.g., `IOpenXliffIdParam`)
- **`undefined` over `null`** — never use `null`
- **Explicit return types** on all functions
- **Explicit visibility** (`public`/`private`/`protected`) on class members
- **JSDoc comments** for public APIs
- **Test pattern**: `suite()` + `test()` with Arrange/Act/Assert, files named `*.test.ts` in `src/test/`

## Documentation Updates

When changing features, update relevant docs:
1. `extension/CHANGELOG.md` — always (entries under newest version)
2. `extension/README.md` — if user-facing functionality changes
3. `extension/MCP_SERVER.md` — if MCP server tools change
4. `extension/mcp-resources/README.md` — if MCP usage examples need updating

## TDD Approach

Bug fixes: write a failing test reproducing the bug first, then fix. New features: Red-Green-Refactor cycle.
