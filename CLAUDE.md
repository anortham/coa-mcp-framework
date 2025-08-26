# COA MCP Framework - AI Assistant Guide

## 🚨 CRITICAL: Framework vs Server Execution

**Framework changes require:**
1. Build framework: `dotnet build`
2. Run tests: `dotnet test` (must be 100% passing)
3. Pack NuGet: `dotnet pack -c Release`
4. Update consumer package references
5. Restart MCP servers

## ⚠️ AI-CRITICAL GOTCHAS (READ FIRST)

1. **NEVER assume types exist** - Always verify with CodeNav tools first
2. **USE NUNIT ONLY** - `[Test]`, `[TestCase]`, `Assert.That()` - xUnit prohibited
3. **Inherit from McpToolBase<TParams,TResult>** - Automatic validation included
4. **Build-test-pack cycle** - Framework changes need full rebuild cycle
5. **Validation helpers available** - `ValidateRequired()`, `ValidateRange()`, `ValidatePositive()`
6. **Cache grows unbounded** - Configure `MaxCacheSize`, `EvictionStrategy` in TypeVerificationOptions
7. **Sequential async kills performance** - Use `ConcurrentAsyncUtilities.ExecuteConcurrentlyAsync()`

## 🚀 Quick Patterns (Copy-Paste Ready)

### Standard Tool Template
```csharp
public class MyTool : McpToolBase<MyParams, MyResult>
{
    protected override async Task<MyResult> ExecuteAsync(MyParams parameters)
    {
        ValidateRequired(parameters.RequiredField, nameof(parameters.RequiredField));
        ValidateRange(parameters.Count, 1, 100, nameof(parameters.Count));
        
        // Implementation here
        return new MyResult { Success = true };
    }
}
```

### Disposable Tool (DB/Files)
```csharp
public class DatabaseTool : DisposableToolBase<DbParams, DbResult>
{
    protected override async Task<DbResult> ExecuteAsync(DbParams parameters)
    {
        ValidateRequired(parameters.ConnectionString, nameof(parameters.ConnectionString));
        
        using var connection = new SqlConnection(parameters.ConnectionString);
        // Implementation with automatic cleanup
    }
}
```

### Error with Recovery
```csharp
protected override Dictionary<string, string> ErrorMessages => new()
{
    ["validation_failed"] = "Required field missing. Add: parameters.RequiredField = 'value'",
    ["range_error"] = "Count must be 1-100. Current: {0}. Fix: parameters.Count = 50"
};
```

### Middleware Configuration
```csharp
// Essential setup - copy-paste and modify
services.Configure<TypeVerificationOptions>(options =>
{
    options.Enabled = true;
    options.Mode = TypeVerificationMode.Warning; // Start with warnings
    options.MaxCacheSize = 10000;
    options.EvictionStrategy = CacheEvictionStrategy.LRU;
    options.MaxMemoryBytes = 50 * 1024 * 1024; // 50MB
});

var builder = McpServerBuilder.Create("my-server", services)
    .WithGlobalMiddleware(new List<ISimpleMiddleware>
    {
        new TypeVerificationMiddleware(typeService, stateManager, logger, typeOptions),
        new LoggingSimpleMiddleware(logger, LogLevel.Information)
    });
```

## 📍 Essential Files (AI Priority Order)

| When You Need | File |
|---------------|------|
| **Tool base class** | `src/COA.Mcp.Framework/Base/McpToolBase.Generic.cs` |
| **Server setup** | `src/COA.Mcp.Framework/Server/McpServerBuilder.cs` |
| **Type verification** | `src/COA.Mcp.Framework/Pipeline/Middleware/TypeVerificationMiddleware.cs` |
| **Cache management** | `src/COA.Mcp.Framework/Services/VerificationStateManager.cs` |
| **Async utilities** | `src/COA.Mcp.Framework/Utilities/ConcurrentAsyncUtilities.cs` |
| **Configuration** | `src/COA.Mcp.Framework/Configuration/TypeVerificationOptions.cs` |
| **Examples** | `examples/SimpleMcpServer/` |

## 🛑 Troubleshooting (When Things Go Wrong)

| Problem | Solution |
|---------|----------|
| **Changes not working** | `dotnet build && dotnet pack -c Release` → Update consumer |
| **Type not found** | Use CodeNav to verify type exists, check namespaces |
| **Tool not registering** | Verify inheritance from McpToolBase<TParams,TResult> |
| **Tests failing** | Check NUnit syntax: `[Test]`, `Assert.That()` |
| **Memory growing** | Configure MaxCacheSize, use LRU eviction |
| **Slow async** | Replace `foreach+await` with `ConcurrentAsyncUtilities` |
| **Middleware not running** | Check Order property, verify registration |

## 📊 Current Status
- **Version:** 1.7.22
- **Tests:** 647 passing (100%) - NUnit framework
- **Build:** 0 warnings
- **Example:** `examples/SimpleMcpServer/` (5 tools + 2 prompts)

## 🛠️ Tool Development Essentials

**Base Classes:**
- `McpToolBase<TParams, TResult>` - Standard tools with automatic validation
- `DisposableToolBase<TParams, TResult>` - Tools with resources (DB, files, HTTP)

**Validation Helpers (built-in):**
- `ValidateRequired(value, paramName)` - Null/empty checks
- `ValidateRange(value, min, max, paramName)` - Numeric ranges
- `ValidatePositive(value, paramName)` - Positive numbers only
- `ValidateNotEmpty(collection, paramName)` - Non-empty collections

**Override Points:**
- `ExecuteAsync()` - Main implementation (required)
- `ErrorMessages` - Custom error messages with recovery steps
- `TokenBudget` - Per-tool token limits
- `Middleware` - Lifecycle hooks (logging, validation)

## ⚡ Performance Essentials

**Cache Configuration:**
```csharp
options.MaxCacheSize = 10000; // Entries limit
options.EvictionStrategy = CacheEvictionStrategy.LRU; // LRU recommended
options.MaxMemoryBytes = 50 * 1024 * 1024; // Memory limit
options.EnableFileWatching = true; // Auto-invalidate on changes
```

**Concurrent Processing:**
```csharp
// Instead of slow sequential:
foreach (var item in items) await ProcessAsync(item);

// Use concurrent:
await ConcurrentAsyncUtilities.ExecuteConcurrentlyAsync(items, ProcessAsync, maxConcurrency: 10);
```

## 🎨 Advanced Features

**Visualization:** Implement `IVisualizationProvider` for rich UI data
**Prompts:** Use `PromptBase` with `{{variable}}` substitution
**Middleware:** Order matters - TypeVerification(5), TDD(10), Logging(100)

## 📁 Project Structure
```
COA.Mcp.Framework/
├── Base/                    # Tool base classes with validation
├── Server/                  # Server infrastructure
├── Pipeline/Middleware/     # TypeVerification, TDD enforcement
├── Services/               # Cache management, utilities
├── Configuration/          # Options and settings
└── examples/               # Working examples
```

---
**Remember**: Framework changes require rebuild + repack + consumer update!