# COA MCP Framework - AI Assistant Guide

## 🚨 CRITICAL: Framework vs Server Execution

```
⚠️ FRAMEWORK CHANGES REQUIRE:
1. Build framework: dotnet build -c Debug
2. Pack NuGet: dotnet pack -c Release  
3. Update consumer package references
4. Restart MCP servers
```

## 🏗️ Current Architecture (v1.0.0)

### Core Components
```
COA.Mcp.Framework/
├── Base/McpToolBase.Generic.cs     # Generic base: McpToolBase<TParams, TResult>
├── Server/
│   ├── McpServer.cs                # Server with CreateBuilder() static method
│   └── McpServerBuilder.cs         # Fluent builder API
├── Registration/McpToolRegistry.cs # Unified registry (manual + discovery)
├── Models/
│   ├── ErrorModels.cs              # ErrorInfo, RecoveryInfo, SuggestedAction
│   └── ToolResultBase.cs           # Base result with Success, Error, Meta
└── Interfaces/IMcpTool.cs          # Tool interface (generic & non-generic)
```

### Package Dependencies
- **COA.Mcp.Protocol** (1.3.x) - Included as dependency
- **COA.Mcp.Framework** (1.0.0) - Core framework
- **Optional**: TokenOptimization, Testing, CLI packages

## 📝 Quick Reference

### Creating a Tool
```csharp
public class MyTool : McpToolBase<MyParams, MyResult>
{
    public override string Name => "my_tool";
    public override string Description => "Tool description";
    public override ToolCategory Category => ToolCategory.Query;
    
    protected override async Task<MyResult> ExecuteInternalAsync(
        MyParams parameters,  // Already validated!
        CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

### Server Setup
```csharp
var builder = new McpServerBuilder()
    .WithServerInfo("Server Name", "1.0.0")
    .ConfigureLogging(/* ... */);

// Register tools
builder.RegisterToolType<MyTool>();  // Manual
builder.DiscoverTools(assembly);     // Auto-discovery

await builder.RunAsync();
```

### Error Handling
```csharp
return new MyResult
{
    Success = false,
    Operation = "operation_name",
    Error = new ErrorInfo
    {
        Code = "ERROR_CODE",
        Message = "Error message",
        Recovery = new RecoveryInfo
        {
            Steps = new[] { "Step 1", "Step 2" },
            SuggestedActions = new[] { /* ... */ }
        }
    }
};
```

## ✅ Current State

### What Works
- ✅ Generic McpToolBase<TParams, TResult> with ExecuteInternalAsync
- ✅ McpServerBuilder with fluent API
- ✅ Manual tool registration (RegisterToolType<T>)
- ✅ Tool discovery (DiscoverTools)
- ✅ Error models with recovery steps
- ✅ SimpleMcpServer example (4 working tools)
- ✅ 230 tests passing, 0 warnings, 0 errors

### Not Yet Implemented
- ❌ AddMcpFramework service extension
- ❌ Automatic attribute-based discovery by default
- ❌ Project templates (dotnet new mcp-server)

## 🛠️ Development Commands

```bash
# Build framework
dotnet build

# Run tests
dotnet test

# Pack NuGet locally
dotnet pack -c Release -o ./nupkg

# Run example
cd examples/SimpleMcpServer
dotnet run
```

## 🎯 Best Practices

1. **ALWAYS** verify types/methods exist before using
2. **ALWAYS** build and test before committing
3. **PREFER** manual tool registration for explicit control
4. **USE** ValidateRequired/ValidateRange helpers in tools
5. **RETURN** ToolResultBase-derived types for consistency
6. **INCLUDE** recovery steps in errors for AI agents

## 📊 Quality Standards

- Build: 0 warnings, 0 errors
- Tests: 100% passing (currently 230/230)
- Coverage: Target ≥85%
- Performance: <5% framework overhead

## 🔍 Common Issues

| Issue | Solution |
|-------|----------|
| Changes not reflected | Rebuild, repack, update package reference |
| Tool not found | Check registration, verify tool inherits from McpToolBase |
| Validation errors | Use ValidateRequired/ValidateRange helpers |
| Token limits | Add TokenOptimization package, use ExecuteWithTokenManagement |

## 📁 Key Files

- `examples/SimpleMcpServer/` - Working example with 4 tools
- `src/COA.Mcp.Framework/Base/McpToolBase.Generic.cs` - Tool base class
- `src/COA.Mcp.Framework/Server/McpServerBuilder.cs` - Server builder
- `tests/COA.Mcp.Framework.Tests/` - Unit tests

---

**Remember**: Framework code requires rebuild + repack. MCP servers need package update + restart.