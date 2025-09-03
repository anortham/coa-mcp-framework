# COA MCP Framework - Logging Configuration

The COA MCP Framework provides comprehensive logging control to balance debugging needs with clean output. This guide covers all logging configuration options introduced in version 2.0.1.

## Overview

The framework includes several logging improvements:

- **Reduced Default Verbosity**: Changed from `Information` to `Warning` level by default
- **Granular Control**: Configure logging for different framework components independently
- **Category-Based Filtering**: Fine-tune logging by namespace/category
- **Performance Optimized**: Conditional debug logging to avoid string formatting overhead
- **Consumer Friendly**: Respects existing logging configuration

## Quick Start

### Basic Configuration

```csharp
var builder = new McpServerBuilder()
    .WithServerInfo("My Server", "1.0.0")
    .ConfigureFramework(options =>
    {
        // Reduce framework noise (recommended for production)
        options.FrameworkLogLevel = LogLevel.Warning;
        options.EnableDetailedToolLogging = false;
    });
```

### Development vs Production

```csharp
// Development: Enable detailed logging
builder.ConfigureFramework(options =>
{
    options.FrameworkLogLevel = LogLevel.Debug;
    options.EnableDetailedToolLogging = true;
    options.EnableDetailedMiddlewareLogging = true;
    options.EnableDetailedTransportLogging = true;
});

// Production: Minimal logging
builder.ConfigureFramework(options =>
{
    options.FrameworkLogLevel = LogLevel.Error;
    options.EnableDetailedToolLogging = false;
    options.EnableDetailedMiddlewareLogging = false;
    options.EnableDetailedTransportLogging = false;
});
```

## FrameworkOptions Reference

### Core Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnableFrameworkLogging` | `bool` | `true` | Master switch for all framework logging |
| `FrameworkLogLevel` | `LogLevel` | `Warning` | Minimum log level for framework components |
| `ConfigureLoggingIfNotConfigured` | `bool` | `true` | Whether to add logging if none exists |

### Component-Specific Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnableDetailedToolLogging` | `bool` | `false` | Detailed tool execution logs |
| `EnableDetailedMiddlewareLogging` | `bool` | `false` | Detailed middleware operation logs |
| `EnableDetailedTransportLogging` | `bool` | `false` | Detailed transport layer logs |
| `SuppressStartupLogs` | `bool` | `false` | Hide startup and initialization messages |

### Complete Example

```csharp
var builder = new McpServerBuilder()
    .WithServerInfo("My Server", "1.0.0")
    .ConfigureFramework(options =>
    {
        // Master controls
        options.EnableFrameworkLogging = true;
        options.FrameworkLogLevel = LogLevel.Warning;
        options.ConfigureLoggingIfNotConfigured = true;
        
        // Component verbosity
        options.EnableDetailedToolLogging = false;
        options.EnableDetailedMiddlewareLogging = false;
        options.EnableDetailedTransportLogging = false;
        
        // Startup behavior
        options.SuppressStartupLogs = false;
    });
```

## Category-Based Filtering

The framework uses specific logging categories for fine-grained control:

### Framework Categories

- **`COA.Mcp.Framework.Pipeline.Middleware`** - Middleware operations (logging, token counting)
- **`COA.Mcp.Framework.Transport`** - Transport layer (HTTP, WebSocket, stdio)
- **`COA.Mcp.Framework.Base`** - Tool execution and lifecycle
- **`COA.Mcp.Framework.Server`** - Server startup and management
- **`COA.Mcp.Framework.Pipeline`** - Request/response processing

### Configuration Examples

```csharp
builder.ConfigureLogging(logging =>
{
    // Quiet all framework categories
    logging.AddFilter("COA.Mcp.Framework", LogLevel.Warning);
    
    // Specific category control
    logging.AddFilter("COA.Mcp.Framework.Pipeline.Middleware", LogLevel.Error);
    logging.AddFilter("COA.Mcp.Framework.Transport", LogLevel.Information);
    
    // Your application categories
    logging.AddFilter("MyApp.Tools", LogLevel.Debug);
    logging.AddFilter("MyApp.Services", LogLevel.Information);
});
```

## Common Scenarios

### Scenario 1: Testing Environment

Minimal output during test runs:

```csharp
builder.ConfigureFramework(options =>
{
    options.FrameworkLogLevel = LogLevel.Error;
    options.EnableDetailedToolLogging = false;
    options.EnableDetailedMiddlewareLogging = false;
    options.EnableDetailedTransportLogging = false;
    options.SuppressStartupLogs = true;
});
```

### Scenario 2: Debugging Middleware Issues

Enable detailed middleware logging:

```csharp
builder.ConfigureFramework(options =>
{
    options.FrameworkLogLevel = LogLevel.Debug;
    options.EnableDetailedMiddlewareLogging = true; // Focus on middleware
    options.EnableDetailedToolLogging = false;      // Keep other noise down
});

// Or use category filtering
builder.ConfigureLogging(logging =>
{
    logging.AddFilter("COA.Mcp.Framework.Pipeline.Middleware", LogLevel.Debug);
    logging.AddFilter("COA.Mcp.Framework", LogLevel.Warning); // Everything else quiet
});
```

### Scenario 3: Transport Layer Debugging

Enable detailed transport logging:

```csharp
builder.ConfigureFramework(options =>
{
    options.EnableDetailedTransportLogging = true;
});

// Additional category filtering
builder.ConfigureLogging(logging =>
{
    logging.AddFilter("COA.Mcp.Framework.Transport", LogLevel.Trace);
});
```

### Scenario 4: Consumer Override

Respect existing consumer logging configuration:

```csharp
// Consumer configures their logging first
services.AddLogging(logging =>
{
    logging.AddSerilog(); // Consumer's choice
    logging.SetMinimumLevel(LogLevel.Debug);
});

// Framework respects existing configuration
var builder = new McpServerBuilder()
    .ConfigureFramework(options =>
    {
        options.ConfigureLoggingIfNotConfigured = true; // Won't override
        options.FrameworkLogLevel = LogLevel.Warning;
    });
```

### Scenario 5: Completely Silent Framework

Disable all framework logging:

```csharp
builder.ConfigureFramework(options =>
{
    options.EnableFrameworkLogging = false;
});

// Or use category filtering
builder.ConfigureLogging(logging =>
{
    logging.AddFilter("COA.Mcp.Framework", LogLevel.None);
});
```

## Performance Considerations

The framework now uses conditional debug logging to avoid performance overhead:

```csharp
// Old (always formats string)
_logger.LogDebug("Processing {Count} items", items.Count);

// New (only formats when debug enabled)
if (_logger.IsEnabled(LogLevel.Debug))
{
    _logger.LogDebug("Processing {Count} items", items.Count);
}
```

This means:
- **No performance cost** when debug logging is disabled
- **String formatting only occurs** when messages will actually be logged
- **Significant performance improvement** in production scenarios

## Migration Guide

### From Version 1.x

If you were experiencing excessive logging output in version 1.x:

1. **Automatic Improvement**: Version 2.0.1 defaults to `Warning` level, significantly reducing output
2. **Optional Fine-tuning**: Use `ConfigureFramework()` for additional control
3. **No Breaking Changes**: Existing logging configuration continues to work

### Example Migration

```csharp
// Before (Version 1.x) - lots of output
var builder = new McpServerBuilder()
    .WithServerInfo("My Server", "1.0.0");

// After (Version 2.0.1) - much quieter by default
var builder = new McpServerBuilder()
    .WithServerInfo("My Server", "1.0.0");
    // Framework now defaults to Warning level

// Optional: Further customization
var builder = new McpServerBuilder()
    .WithServerInfo("My Server", "1.0.0")
    .ConfigureFramework(options =>
    {
        options.FrameworkLogLevel = LogLevel.Error; // Even quieter
    });
```

## Best Practices

1. **Start Quiet**: Use `Warning` or `Error` level in production
2. **Debug Selectively**: Enable detailed logging only for specific components when debugging
3. **Use Categories**: Prefer category filtering over global log levels for fine control
4. **Respect Consumers**: Set `ConfigureLoggingIfNotConfigured = true` in reusable libraries
5. **Test Impact**: Verify logging configuration doesn't impact test performance

## Troubleshooting

### Problem: Still seeing too much output

**Solution**: Check if your application is setting a lower log level:

```csharp
// This overrides framework settings
logging.SetMinimumLevel(LogLevel.Debug); // Remove or set to Warning

// Use category filtering instead
logging.AddFilter("MyApp", LogLevel.Debug);
logging.AddFilter("COA.Mcp.Framework", LogLevel.Warning);
```

### Problem: Missing important debug information

**Solution**: Enable detailed logging for specific components:

```csharp
builder.ConfigureFramework(options =>
{
    options.EnableDetailedToolLogging = true; // If debugging tool issues
    options.EnableDetailedMiddlewareLogging = true; // If debugging middleware
});
```

### Problem: Framework not respecting my logging configuration

**Solution**: Ensure framework doesn't override your config:

```csharp
builder.ConfigureFramework(options =>
{
    options.ConfigureLoggingIfNotConfigured = true; // Default behavior
    options.EnableFrameworkLogging = false; // Or disable framework logging entirely
});
```
