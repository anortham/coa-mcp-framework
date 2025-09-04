# Migration Guide: Global Middleware Support (v1.7.18)

This document outlines the breaking changes and new features introduced in v1.7.18 to support global middleware functionality.

## 🚨 Breaking Changes

### Tool Constructor Changes

**All tools that inherit from `McpToolBase` or `DisposableToolBase` must update their constructors.**

#### Before (v1.7.17 and earlier):
```csharp
public class MyTool : McpToolBase<MyParameters, MyResult>
{
    public MyTool(ILogger<MyTool> logger) : base(logger)
    {
        // Tool implementation
    }
}

public class MyDisposableTool : DisposableToolBase<MyParameters, MyResult>
{
    public MyDisposableTool(ILogger<MyDisposableTool> logger) : base(logger)
    {
        // Tool implementation
    }
}
```

#### After (v1.7.18+):
```csharp
public class MyTool : McpToolBase<MyParameters, MyResult>
{
    public MyTool(IServiceProvider? serviceProvider, ILogger<MyTool>? logger = null) 
        : base(serviceProvider, logger)
    {
        // Tool implementation
    }
}

public class MyDisposableTool : DisposableToolBase<MyParameters, MyResult>
{
    public MyDisposableTool(IServiceProvider? serviceProvider, ILogger<MyDisposableTool>? logger = null) 
        : base(serviceProvider, logger)
    {
        // Tool implementation
    }
}
```

### Key Changes:
1. **First parameter**: `IServiceProvider? serviceProvider` (new)
2. **Second parameter**: `ILogger? logger` (now optional with default `null`)
3. Both parameters are nullable for maximum flexibility

## 🆕 New Features

### Global Middleware Registration

The `McpServerBuilder` now supports global middleware that applies to ALL tools automatically:

#### Instance-Based Registration
```csharp
var builder = new McpServerBuilder();

// Single middleware instance
var loggingMiddleware = new LoggingSimpleMiddleware(logger, LogLevel.Information);
builder.WithGlobalMiddleware(loggingMiddleware);

// Multiple middleware instances
var middleware1 = new TypeVerificationMiddleware(typeService, stateManager, logger, typeOptions);
var middleware2 = new TddEnforcementMiddleware(testService, logger, tddOptions);
builder.WithGlobalMiddleware(middleware1, middleware2);

// From a collection
var middlewareList = new List<ISimpleMiddleware> { middleware1, middleware2 };
builder.WithGlobalMiddleware(middlewareList);
```

#### Type-Based Registration (with DI)
```csharp
var builder = new McpServerBuilder();

// Register middleware type for DI resolution
builder.AddGlobalMiddleware<LoggingSimpleMiddleware>();

// With custom factory
builder.AddGlobalMiddleware<CustomMiddleware>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<CustomMiddleware>>();
    var config = provider.GetRequiredService<IConfiguration>();
    return new CustomMiddleware(logger, config);
});

// With custom lifetime
builder.AddGlobalMiddleware<TokenCountingSimpleMiddleware>(ServiceLifetime.Transient);
```

#### Built-in Convenience Methods
```csharp
var builder = new McpServerBuilder();

// Add built-in middleware with sensible defaults
builder.AddTypeVerificationMiddleware(options => {
    options.Mode = TypeVerificationMode.Strict;
    options.EnableDetailedLogging = true;
});

builder.AddTddEnforcementMiddleware(options => {
    options.Mode = TddEnforcementMode.Warning;
    options.RequireFailingTest = true;
});

builder.AddLoggingMiddleware(LogLevel.Information);
builder.AddTokenCountingMiddleware();
```

### Global + Tool-Specific Middleware Combination

Tools can now have both global middleware (from DI) and tool-specific middleware:

```csharp
public class MyTool : McpToolBase<MyParameters, MyResult>
{
    public MyTool(IServiceProvider? serviceProvider, ILogger<MyTool>? logger = null) 
        : base(serviceProvider, logger)
    {
    }

    // Tool-specific middleware (combines with global middleware automatically)
    protected override IReadOnlyList<ISimpleMiddleware>? ToolSpecificMiddleware => 
        new List<ISimpleMiddleware>
        {
            new CustomTimingMiddleware(Logger), // Tool-specific
            new SpecialValidationMiddleware()   // Tool-specific
        };
}
```

**Execution Order**: All middleware is sorted by `Order` property:
1. Global middleware with lower `Order` values
2. Tool-specific middleware with lower `Order` values  
3. Continues in ascending `Order`

## 🔧 Migration Steps

### Step 1: Update Tool Constructors

Use find/replace or your IDE to update all tool constructors:

**Find**: `(ILogger<.*> logger) : base(logger)`  
**Replace**: `(IServiceProvider? serviceProvider, ILogger<$1>? logger = null) : base(serviceProvider, logger)`

### Step 2: Update Tool Registration

If you're manually registering tools, ensure DI can resolve the new constructor parameters:

```csharp
// Your tools now need access to IServiceProvider
services.AddScoped<MyTool>(); // DI will automatically provide IServiceProvider
```

### Step 3: Update Tests

Test tools need to be updated to match the new constructor signature:

#### Before:
```csharp
[Test]
public void MyTool_ShouldWork()
{
    var logger = Mock.Of<ILogger<MyTool>>();
    var tool = new MyTool(logger);
    // Test logic
}
```

#### After:
```csharp
[Test]
public void MyTool_ShouldWork()
{
    var logger = Mock.Of<ILogger<MyTool>>();
    var tool = new MyTool(null, logger); // null serviceProvider for tests
    // Test logic
}
```

#### For tests that need global middleware:
```csharp
[Test]
public void MyTool_WithGlobalMiddleware_ShouldWork()
{
    var services = new ServiceCollection();
    services.AddSingleton<ISimpleMiddleware>(new TestMiddleware());
    var serviceProvider = services.BuildServiceProvider();
    
    var logger = Mock.Of<ILogger<MyTool>>();
    var tool = new MyTool(serviceProvider, logger);
    // Test logic - tool will now have global middleware
}
```

### Step 4: Configure Global Middleware (Optional)

Add global middleware configuration to your server setup:

```csharp
// In your Program.cs or server configuration
var builder = new McpServerBuilder()
    .WithServerInfo("My Server", "1.0.0")
    .AddTypeVerificationMiddleware() // Prevent AI type hallucination
    .AddTddEnforcementMiddleware()   // Enforce TDD practices
    .AddLoggingMiddleware()          // Add execution logging
    .AddTokenCountingMiddleware()    // Monitor token usage
    .RegisterTool<MyTool>()
    .Build();
```

## 📋 Testing Your Migration

### Verify Constructor Updates
```bash
# Search for old constructor patterns (should return no results)
grep -r "base(logger)" --include="*.cs" src/

# Search for new constructor patterns (should find your tools)
grep -r "base(serviceProvider, logger)" --include="*.cs" src/
```

### Test Global Middleware
```csharp
[Test]
public void GlobalMiddleware_IsAppliedToAllTools()
{
    var executionLog = new List<string>();
    var globalMiddleware = new TestMiddleware("global", executionLog);
    
    var builder = new McpServerBuilder();
    builder.WithGlobalMiddleware(globalMiddleware);
    
    var serviceProvider = builder.Services.BuildServiceProvider();
    var tool = new MyTool(serviceProvider);
    
    await tool.ExecuteAsync(new MyParameters());
    
    Assert.That(executionLog, Contains.Item("Before: global"));
    Assert.That(executionLog, Contains.Item("After: global"));
}
```

## 🔍 Troubleshooting

### Common Issues

#### "Cannot resolve service for type 'IServiceProvider'"
```csharp
// Problem: DI container doesn't have IServiceProvider registered
services.AddSingleton<MyTool>(); // This won't work

// Solution: Let DI provide IServiceProvider automatically  
services.AddScoped<MyTool>(); // DI automatically provides IServiceProvider
```

#### "Method signature error" in tests
```csharp
// Problem: Test still using old constructor
var tool = new MyTool(logger); // Won't compile

// Solution: Update test to use new signature
var tool = new MyTool(null, logger); // Works for tests
```

#### Global middleware not executing
```csharp
// Problem: Tool created without serviceProvider
var tool = new MyTool(null, logger); // No global middleware

// Solution: Provide serviceProvider with middleware
var builder = new McpServerBuilder();
builder.WithGlobalMiddleware(myMiddleware);
var serviceProvider = builder.Services.BuildServiceProvider();
var tool = new MyTool(serviceProvider, logger); // Has global middleware
```

### Validation Steps

1. ✅ All tools compile with new constructor signature
2. ✅ Tests pass with updated constructor calls  
3. ✅ Global middleware executes when tools are created with serviceProvider
4. ✅ Tool-specific middleware still works as before
5. ✅ Middleware ordering is correct (global + tool-specific sorted by Order)

## 🎯 Benefits of This Change

### Before Global Middleware
- Middleware had to be manually added to every tool
- Inconsistent middleware application across tools
- Difficult to add cross-cutting concerns like logging or security

### After Global Middleware  
- ✅ **Consistent cross-cutting concerns**: Logging, token counting, type verification applied everywhere
- ✅ **Easier maintenance**: Add middleware once, applies to all tools
- ✅ **Better separation of concerns**: Tools focus on business logic, middleware handles cross-cutting concerns
- ✅ **Flexible configuration**: Mix global and tool-specific middleware as needed
- ✅ **DI-friendly**: Middleware can leverage dependency injection

## 📚 Related Documentation

- [Lifecycle Hooks and Middleware](lifecycle-hooks.md) - Complete middleware documentation
- [VALIDATION_AND_ERROR_HANDLING.md](VALIDATION_AND_ERROR_HANDLING.md) - Built-in validation helpers
- [examples/SimpleMcpServer/](../examples/SimpleMcpServer/) - Working examples with global middleware

## 💡 Best Practices

### 1. Use Global Middleware for Cross-Cutting Concerns
```csharp
// Good: Apply logging and token counting globally
builder.AddLoggingMiddleware()
       .AddTokenCountingMiddleware();
```

### 2. Keep Tool-Specific Middleware Focused
```csharp
// Good: Tool-specific validation or business logic
protected override IReadOnlyList<ISimpleMiddleware>? ToolSpecificMiddleware => 
    new List<ISimpleMiddleware>
    {
        new DatabaseTransactionMiddleware(), // Only for database tools
        new CacheInvalidationMiddleware()    // Only for cache-affecting tools
    };
```

### 3. Order Middleware Thoughtfully
```csharp
// Recommended order values:
// 1-10:   Security, authentication, type verification
// 11-50:  Logging, TDD enforcement, business validation  
// 51-100: Token counting, performance monitoring, cleanup
```

### 4. Test Both Global and Tool-Specific Middleware
```csharp
[Test]
public void Tool_CombinesGlobalAndToolSpecificMiddleware()
{
    // Verify middleware from both sources execute correctly
}
```

This migration enables powerful new capabilities while maintaining backward compatibility where possible. The breaking change to constructors is necessary to enable dependency injection of global middleware, providing much better separation of concerns and maintainability.