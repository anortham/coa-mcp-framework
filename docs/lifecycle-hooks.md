# Lifecycle Hooks and Middleware

The COA MCP Framework provides a powerful lifecycle hooks system that allows you to intercept and extend tool execution at various points. This enables cross-cutting concerns like logging, token counting, performance monitoring, and custom business logic.

## Overview

The lifecycle hooks system is implemented using a simple middleware pattern that provides three key extension points:

- **Before Execution**: Called before the tool's main logic executes
- **After Execution**: Called after successful tool execution 
- **On Error**: Called when tool execution fails

## Core Interfaces

### ISimpleMiddleware

The base interface for all middleware:

```csharp
public interface ISimpleMiddleware
{
    int Order { get; }           // Execution order (lower runs first)
    bool IsEnabled { get; }      // Whether middleware is active
    
    Task OnBeforeExecutionAsync(string toolName, object? parameters);
    Task OnAfterExecutionAsync(string toolName, object? parameters, object? result, long elapsedMs);
    Task OnErrorAsync(string toolName, object? parameters, Exception exception, long elapsedMs);
}
```

### SimpleMiddlewareBase

A convenient base class with default implementations:

```csharp
public abstract class SimpleMiddlewareBase : ISimpleMiddleware
{
    public virtual int Order { get; set; } = 0;
    public virtual bool IsEnabled { get; set; } = true;
    
    // Default implementations that do nothing
    public virtual Task OnBeforeExecutionAsync(string toolName, object? parameters) => Task.CompletedTask;
    public virtual Task OnAfterExecutionAsync(string toolName, object? parameters, object? result, long elapsedMs) => Task.CompletedTask;
    public virtual Task OnErrorAsync(string toolName, object? parameters, Exception exception, long elapsedMs) => Task.CompletedTask;
}
```

## Using Middleware in Tools

To add lifecycle hooks to a tool, override the `Middleware` property in your tool class:

```csharp
public class MyTool : McpToolBase<MyParameters, MyResult>
{
    private readonly ILogger<MyTool> _logger;
    
    public MyTool(ILogger<MyTool> logger) : base(logger)
    {
        _logger = logger;
    }
    
    protected override IReadOnlyList<ISimpleMiddleware>? Middleware => new List<ISimpleMiddleware>
    {
        new LoggingSimpleMiddleware(_logger),
        new TokenCountingSimpleMiddleware(_logger),
        new CustomMiddleware()
    };
    
    // Tool implementation...
}
```

## Built-in Middleware

The framework provides two built-in middleware implementations:

### LoggingSimpleMiddleware

Provides comprehensive execution logging:

```csharp
var loggingMiddleware = new LoggingSimpleMiddleware(logger, LogLevel.Information);
// Order: 10 (runs early)
// Logs: Tool start, completion time, errors, and optionally parameters (debug level)
```

### TokenCountingSimpleMiddleware  

Estimates and logs token usage:

```csharp
var tokenMiddleware = new TokenCountingSimpleMiddleware(logger);
// Order: 100 (runs later for accurate timing)
// Logs: Estimated input/output tokens based on JSON serialization
```

## Execution Flow

When middleware is configured, the execution flow becomes:

1. **Sort middleware by Order** (ascending)
2. **Before hooks** (in order): middleware1 → middleware2 → middleware3
3. **Parameter validation**
4. **Tool execution**
5. **After hooks** (reverse order): middleware3 → middleware2 → middleware1
6. **Error hooks** (reverse order, if an error occurs)

The reverse order for "after" and "error" hooks allows for proper cleanup and unwinding of resources.

## Custom Middleware Examples

### Performance Monitoring

```csharp
public class PerformanceMiddleware : SimpleMiddlewareBase
{
    private readonly IMetrics _metrics;
    
    public PerformanceMiddleware(IMetrics metrics)
    {
        _metrics = metrics;
        Order = 20; // Run after logging
    }
    
    public override Task OnAfterExecutionAsync(string toolName, object? parameters, object? result, long elapsedMs)
    {
        _metrics.RecordToolExecution(toolName, elapsedMs);
        
        if (elapsedMs > 1000)
        {
            _metrics.IncrementSlowToolCount(toolName);
        }
        
        return Task.CompletedTask;
    }
}
```

### Audit Trail

```csharp
public class AuditMiddleware : SimpleMiddlewareBase
{
    private readonly IAuditService _auditService;
    
    public AuditMiddleware(IAuditService auditService)
    {
        _auditService = auditService;
        Order = 30;
    }
    
    public override async Task OnAfterExecutionAsync(string toolName, object? parameters, object? result, long elapsedMs)
    {
        await _auditService.LogToolExecution(new AuditEntry
        {
            ToolName = toolName,
            Parameters = parameters,
            Result = result,
            ElapsedMs = elapsedMs,
            Timestamp = DateTimeOffset.UtcNow
        });
    }
}
```

### Rate Limiting

```csharp
public class RateLimitMiddleware : SimpleMiddlewareBase
{
    private readonly IRateLimiter _rateLimiter;
    
    public RateLimitMiddleware(IRateLimiter rateLimiter)
    {
        _rateLimiter = rateLimiter;
        Order = 5; // Run very early
    }
    
    public override async Task OnBeforeExecutionAsync(string toolName, object? parameters)
    {
        if (!await _rateLimiter.IsAllowedAsync(toolName))
        {
            throw new RateLimitExceededException($"Rate limit exceeded for tool '{toolName}'");
        }
    }
}
```

## Best Practices

### 1. Order Your Middleware Thoughtfully

- **Low numbers (1-10)**: Infrastructure concerns (rate limiting, auth)
- **Medium numbers (10-50)**: Logging, monitoring
- **High numbers (50-100)**: Business logic, token counting

### 2. Handle Errors Gracefully

```csharp
public override async Task OnErrorAsync(string toolName, object? parameters, Exception exception, long elapsedMs)
{
    try
    {
        await _service.HandleErrorAsync(toolName, exception);
    }
    catch (Exception ex)
    {
        // Log but don't re-throw - middleware errors shouldn't break the chain
        _logger.LogError(ex, "Error in middleware error handler");
    }
}
```

### 3. Use Conditional Logic

```csharp
public override Task OnBeforeExecutionAsync(string toolName, object? parameters)
{
    // Only apply to specific tool categories
    if (toolName.StartsWith("sensitive_"))
    {
        return ValidateSecurityAsync(parameters);
    }
    
    return Task.CompletedTask;
}
```

### 4. Leverage Tool Categories

```csharp
public class DatabaseMiddleware : SimpleMiddlewareBase
{
    public override async Task OnBeforeExecutionAsync(string toolName, object? parameters)
    {
        // Only apply to database tools
        if (IsToolCategory(toolName, ToolCategory.DataAccess))
        {
            await _dbContext.BeginTransactionAsync();
        }
    }
}
```

## Testing Middleware

The framework includes comprehensive test coverage for middleware. Here's an example of testing custom middleware:

```csharp
[Test]
public async Task CustomMiddleware_CallsLifecycleHooks()
{
    // Arrange
    var middleware = new Mock<ISimpleMiddleware>();
    middleware.Setup(m => m.IsEnabled).Returns(true);
    middleware.Setup(m => m.Order).Returns(1);

    var tool = new TestTool(logger, new List<ISimpleMiddleware> { middleware.Object });
    var parameters = new TestParameters { Value = "test" };

    // Act
    var result = await tool.ExecuteAsync(parameters);

    // Assert
    middleware.Verify(m => m.OnBeforeExecutionAsync("test_tool", parameters), Times.Once);
    middleware.Verify(m => m.OnAfterExecutionAsync("test_tool", parameters, result, It.IsAny<long>()), Times.Once);
}
```

## Complete Example

See `examples/SimpleMcpServer/Tools/LifecycleExampleTool.cs` for a complete working example that demonstrates:

- Built-in logging and token counting middleware
- Custom timing middleware with performance categorization
- Parameter validation and error handling
- Proper middleware ordering and lifecycle management

## Migration Notes

### From Previous Complex Pipeline

The previous complex pipeline system with `IToolExecutionPipeline` has been replaced with this simpler, more flexible middleware approach:

- **Old**: Complex pipeline with fixed execution states
- **New**: Simple middleware with three clear extension points
- **Benefits**: Better performance, easier to understand, more flexible

### Updating Existing Code

If you have tools that previously used pipeline features:

1. Replace `IToolExecutionPipeline` with `IReadOnlyList<ISimpleMiddleware>`
2. Convert pipeline hooks to middleware methods
3. Use built-in middleware for common scenarios (logging, token counting)

## Performance Considerations

- Middleware executes in the same thread as the tool
- Keep middleware lightweight to avoid impacting tool performance  
- Use async operations sparingly - prefer fire-and-forget patterns for non-critical operations
- Disabled middleware (`IsEnabled = false`) are completely skipped

The lifecycle hooks system provides a clean, testable way to extend tool behavior without modifying core tool logic. It's designed to be both powerful and simple to use.