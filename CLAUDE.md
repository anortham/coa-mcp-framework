# COA MCP Framework - AI Assistant Guide

## 🚨 CRITICAL: Framework vs Server Execution

**Framework changes require:**
1. Build framework: `dotnet build`
2. Run tests: `dotnet test` (must be 100% passing)
3. Pack NuGet: `dotnet pack -c Release`
4. Update consumer package references
5. Restart MCP servers

## 📁 Project Structure

```
COA.Mcp.Framework/
├── Base/McpToolBase.Generic.cs     # Generic tool base class
├── Server/                          # Server infrastructure with prompt support
├── Transport/                       # Multiple transport types (stdio, HTTP, WebSocket)
├── Prompts/                        # Interactive prompt system
├── Schema/                         # Type-safe schema system
├── Registration/                   # Tool and prompt registries
├── Pipeline/                       # Lifecycle hooks and middleware system
├── Models/                         # Error models with recovery info
└── COA.Mcp.Visualization/         # Visualization protocol contracts (NEW)
```

## ⚠️ Key Development Rules

1. **ALWAYS verify types/methods exist** - Never assume, always check
2. **Build and test before committing** - All tests must pass
3. **Use existing patterns** - Follow established code conventions
4. **Include recovery steps in errors** - Help AI agents recover from failures
5. **Prefer manual registration** - Explicit control over auto-discovery
6. **USE NUNIT FOR TESTS ONLY** - This project uses NUnit exclusively! Use `[Test]`, `[TestCase]`, `Assert.That()`. xUnit is prohibited - all imports/usage will be flagged.

## 🔧 Development Commands

```bash
# Essential commands for framework development
dotnet build                        # Build framework
dotnet test                         # Run all tests (must be 100% passing)
dotnet pack -c Release              # Create NuGet packages
```

## 🛠️ Tool Development

**Base Classes:**
- `McpToolBase<TParams, TResult>` - Standard tools
- `DisposableToolBase<TParams, TResult>` - Tools with resources (DB, files)

**Key Patterns:**
- Parameters validated automatically
- Use validation helpers from McpToolBase: `ValidateRequired()`, `ValidateRange()`, `ValidatePositive()`, `ValidateNotEmpty()`
- Return `ToolResultBase`-derived types
- Override `ErrorMessages` for custom error messages with recovery steps
- Override `TokenBudget` for per-tool token limits
- Override `Middleware` for lifecycle hooks (logging, token counting)

## 💬 Prompt Development

**Base Class:** `PromptBase`
- Use `CreateSystemMessage()`, `CreateUserMessage()` helpers
- Implement argument validation
- Support variable substitution with `{{variable}}` syntax

## 📊 Current Status
- **Version:** 1.7.17
- **Tests:** 538 passing (100%) - NUnit framework
- **Build:** 0 warnings
- **Example:** `examples/SimpleMcpServer/` (5 tools + 2 prompts)

## 🎨 Visualization Protocol

**NEW**: Tools can provide structured visualization data for rich UI clients:
- Implement `IVisualizationProvider` for tools that need visualization
- Return `VisualizationDescriptor` with data and display hints
- VS Code Bridge handles all rendering - no markdown generation needed
- Protocol is language-agnostic (works with TypeScript/Python/Rust MCP servers)
- See `docs/VISUALIZATION_PROTOCOL.md` for full specification

## 🛑 Common Issues & Solutions

| Issue | Solution |
|-------|----------|
| Changes not reflected | Rebuild, repack, update package reference |
| Tool not found | Verify inheritance from McpToolBase |
| Validation errors | Use validation helpers from McpToolBase (ValidateRequired, ValidateRange, etc.) |
| Token limits | Configure TokenBudgets in server builder |
| Custom error messages | Override ErrorMessages property in tool |
| Lifecycle hooks not working | Override Middleware property with ISimpleMiddleware list |
| Visualization not showing | Check IVisualizationProvider implementation |

## 📍 Key Files

| Component | File Path |
|-----------|-----------|
| Tool base (includes validation helpers) | `src/COA.Mcp.Framework/Base/McpToolBase.Generic.cs` |
| Prompt base | `src/COA.Mcp.Framework/Prompts/PromptBase.cs` |
| Server builder | `src/COA.Mcp.Framework/Server/McpServerBuilder.cs` |
| Middleware | `src/COA.Mcp.Framework/Pipeline/SimpleMiddleware.cs` |
| Example server | `examples/SimpleMcpServer/` |

---
**Remember**: This is a framework - changes require rebuild + repack + consumer update!