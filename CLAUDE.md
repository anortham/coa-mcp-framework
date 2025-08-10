# COA MCP Framework - AI Assistant Guide

## 🚨 CRITICAL: Framework vs Server Execution

**Framework changes require:**
1. Build framework: `dotnet build -c Debug`
2. Pack NuGet: `dotnet pack -c Release`  
3. Update consumer package references
4. Restart MCP servers

## 📁 Project Structure

```
COA.Mcp.Framework/
├── Base/McpToolBase.Generic.cs     # Generic tool base class
├── Server/                          # Server infrastructure with prompt support
├── Transport/                       # Multiple transport types (stdio, HTTP, WebSocket)
├── Prompts/                        # Interactive prompt system
├── Schema/                         # Type-safe schema system
├── Registration/                   # Tool and prompt registries
└── Models/                         # Error models with recovery info
```

## ⚠️ Key Development Rules

1. **ALWAYS verify types/methods exist** - Never assume, always check
2. **Build and test before committing** - All tests must pass
3. **Use existing patterns** - Follow established code conventions
4. **Include recovery steps in errors** - Help AI agents recover from failures
5. **Prefer manual registration** - Explicit control over auto-discovery

## 🔧 Common Tasks

### When modifying framework code:
```bash
dotnet build                        # Build framework
dotnet test                         # Run all tests (must be 100% passing)
dotnet pack -c Release -o ./nupkg  # Create NuGet package
```

### When helping with tool development:
- Tools inherit from `McpToolBase<TParams, TResult>`
- Parameters are validated automatically
- Use `ValidateRequired()`, `ValidateRange()` helpers
- Return `ToolResultBase`-derived types

### When helping with prompt development:
- Prompts inherit from `PromptBase`
- Use helper methods: `CreateSystemMessage()`, `CreateUserMessage()`
- Implement argument validation
- Support variable substitution with `{{variable}}` syntax

## 📊 Current Status
- Version: 1.1.0
- Tests: 492 passing (100%)
- Build warnings: 0
- Examples: SimpleMcpServer (4 tools + 2 prompts)
- Test Framework: NUnit (not xUnit)

## 🛑 Common Issues & Solutions

| Issue | Solution |
|-------|----------|
| Changes not reflected | Rebuild, repack, update package reference |
| Tool not found | Verify inheritance from McpToolBase |
| Validation errors | Use built-in validation helpers |
| Token limits | Add TokenOptimization package |

## 📍 Important Files

- Tool base: `src/COA.Mcp.Framework/Base/McpToolBase.Generic.cs`
- Prompt base: `src/COA.Mcp.Framework/Prompts/PromptBase.cs`
- Server builder: `src/COA.Mcp.Framework/Server/McpServerBuilder.cs`
- Working example: `examples/SimpleMcpServer/`

---
**Remember**: This is a framework - changes require rebuild + repack + consumer update!