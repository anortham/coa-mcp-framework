# COA.Mcp.Framework

The core framework for building Model Context Protocol (MCP) servers and tools in .NET. This package provides the foundational components for creating type-safe, validated, and AI-optimized MCP tools.

## Features

- **Type-safe tool development** with generic base classes
- **Automatic parameter validation** with attributes
- **Built-in error handling** with recovery steps
- **Prompt system** for interactive AI conversations
- **Multiple transport options** (stdio, HTTP, WebSocket)
- **Service lifecycle management**
- **Resource providers** for exposing data
- **Schema generation** from C# types

## Installation

```xml
<PackageReference Include="COA.Mcp.Framework" Version="1.7.0" />
```

## Quick Start

### 1. Create a Simple Tool

```csharp
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;

// Define parameters
public class CalculatorParams
{
    [Required]
    public double A { get; set; }
    
    [Required] 
    public double B { get; set; }
    
    [Required]
    [AllowedValues("add", "subtract", "multiply", "divide")]
    public string Operation { get; set; }
}

// Define result
public class CalculatorResult : ToolResultBase
{
    public override string Operation => "calculate";
    public double Result { get; set; }
    public string Formula { get; set; }
}

// Implement tool
public class CalculatorTool : McpToolBase<CalculatorParams, CalculatorResult>
{
    public override string Name => "calculator";
    public override string Description => "Performs basic arithmetic operations";
    
    protected override async Task<CalculatorResult> ExecuteInternalAsync(
        CalculatorParams parameters,
        CancellationToken cancellationToken)
    {
        double result = parameters.Operation switch
        {
            "add" => parameters.A + parameters.B,
            "subtract" => parameters.A - parameters.B,
            "multiply" => parameters.A * parameters.B,
            "divide" => parameters.A / parameters.B,
            _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
        };
        
        return new CalculatorResult
        {
            Result = result,
            Formula = $"{parameters.A} {parameters.Operation} {parameters.B} = {result}"
        };
    }
}
```

### 2. Create an MCP Server

```csharp
using COA.Mcp.Framework.Server;
using COA.Mcp.Framework.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Option 1: Using McpServerBuilder (Recommended)
var builder = new McpServerBuilder()
    .WithServerInfo("my-mcp-server", "1.0.0")
    .UseStdioTransport()  // Default transport
    .RegisterToolType<CalculatorTool>()
    .RegisterToolType<WeatherTool>();

// Run the server
await builder.RunAsync();

// Option 2: Using Host.CreateApplicationBuilder with DI
var hostBuilder = Host.CreateApplicationBuilder(args);

// Configure MCP framework
hostBuilder.Services.AddMcpFramework(options =>
{
    options.DiscoverToolsFromAssembly(typeof(Program).Assembly);
    options.EnableValidation = true;
});

// Register specific tools
hostBuilder.Services.AddMcpTool<CalculatorTool>();
hostBuilder.Services.AddMcpTool<WeatherTool>();

// Add transport
hostBuilder.Services.AddSingleton<IMcpTransport, StdioTransport>();

var host = hostBuilder.Build();
await host.RunAsync();
```

## Core Components

### Base Classes

#### McpToolBase<TParams, TResult>
Base class for all MCP tools with type-safe parameters and results.

```csharp
public abstract class MyTool : McpToolBase<MyParams, MyResult>
{
    public override string Name => "my_tool";
    public override string Description => "Tool description";
    
    protected override async Task<MyResult> ExecuteInternalAsync(
        MyParams parameters,
        CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

#### ToolResultBase
Base class for tool results with built-in success/error handling.

```csharp
public class MyResult : ToolResultBase
{
    public override string Operation => "my_operation";
    public string Data { get; set; }
    
    // Note: Warnings is not an override, just a property
    public List<string> Warnings { get; set; }
}
```

### Parameter Validation Attributes

Validate parameters automatically using attributes:

```csharp
public class MyParams
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; }
    
    [Range(1, 100)]
    public int Count { get; set; }
    
    [Email]
    public string EmailAddress { get; set; }
    
    [Url]
    public string Website { get; set; }
    
    [RegularExpression(@"^\d{3}-\d{3}-\d{4}$")]
    public string PhoneNumber { get; set; }
    
    [AllowedValues("small", "medium", "large")]
    public string Size { get; set; }
    
    [FileExists]
    public string FilePath { get; set; }
    
    [DirectoryExists]
    public string DirectoryPath { get; set; }
}
```

### Resource Management

For tools that manage resources like database connections, use `DisposableToolBase`:

```csharp
using COA.Mcp.Framework.Base;

public class DatabaseTool : DisposableToolBase<DbParams, DbResult>
{
    private SqlConnection _connection;
    
    public override string Name => "database_query";
    public override string Description => "Executes database queries";
    
    protected override async Task<DbResult> ExecuteInternalAsync(
        DbParams parameters,
        CancellationToken cancellationToken)
    {
        _connection = new SqlConnection(parameters.ConnectionString);
        await _connection.OpenAsync(cancellationToken);
        
        // Execute query...
        return new DbResult { /* ... */ };
    }
    
    protected override async ValueTask DisposeManagedResourcesAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
    }
}
```

### Error Handling

Framework provides structured error responses with recovery steps:

```csharp
protected override async Task<MyResult> ExecuteInternalAsync(
    MyParams parameters,
    CancellationToken cancellationToken)
{
    try
    {
        // Tool logic
    }
    catch (FileNotFoundException ex)
    {
        throw new McpException(
            ErrorCode.FileNotFound,
            $"File not found: {ex.FileName}",
            recoverySteps: new[]
            {
                "Verify the file path is correct",
                "Ensure the file exists",
                "Check file permissions"
            });
    }
}
```

## Prompts System

Create interactive prompts for AI conversations:

```csharp
using COA.Mcp.Framework.Prompts;

public class CodeReviewPrompt : PromptBase
{
    public override string Name => "code_review";
    public override string Description => "Reviews code for quality and suggests improvements";
    
    public override PromptArgument[] Arguments => new[]
    {
        new PromptArgument
        {
            Name = "language",
            Description = "Programming language",
            Required = true
        },
        new PromptArgument
        {
            Name = "focus",
            Description = "Review focus (security, performance, style)",
            Required = false
        }
    };
    
    public override Task<PromptMessage[]> GetMessagesAsync(
        Dictionary<string, string> arguments,
        CancellationToken cancellationToken)
    {
        var language = arguments["language"];
        var focus = arguments.GetValueOrDefault("focus", "general");
        
        return Task.FromResult(new[]
        {
            CreateSystemMessage($"You are a {language} code reviewer focusing on {focus}."),
            CreateUserMessage("Review the following code:\n{{code}}")
        });
    }
}
```

## Transport Options

The framework supports multiple transport mechanisms for different use cases:

### Stdio Transport (Default)
For command-line integration and process communication:

```csharp
// Using McpServerBuilder (Recommended)
var builder = new McpServerBuilder()
    .UseStdioTransport(options =>
    {
        options.Input = Console.OpenStandardInput();
        options.Output = Console.OpenStandardOutput();
    });

// Or with DI
services.AddSingleton<IMcpTransport, StdioTransport>();
```

### HTTP Transport
For REST API access with optional WebSocket upgrade:

```csharp
// Using McpServerBuilder
var builder = new McpServerBuilder()
    .UseHttpTransport(options =>
    {
        options.Port = 5000;
        options.Host = "localhost";
        options.EnableWebSocket = true;  // Enable WebSocket upgrade
        options.EnableCors = true;
        options.UseHttps = false;
    });

// Or with DI
services.Configure<HttpTransportOptions>(options =>
{
    options.Port = 5000;
    options.BasePath = "/mcp";
});
services.AddSingleton<IMcpTransport, HttpTransport>();
```

### WebSocket Transport
For dedicated real-time bidirectional communication:

```csharp
// Using McpServerBuilder
var builder = new McpServerBuilder()
    .UseWebSocketTransport(options =>
    {
        options.Port = 5001;
        options.Host = "localhost";
        options.UseHttps = false;
    });

// Or manually with DI
var options = new HttpTransportOptions
{
    Port = 5001,
    Host = "localhost",
    EnableWebSocket = true
};
services.AddSingleton<IMcpTransport>(provider =>
{
    var logger = provider.GetService<ILogger<WebSocketTransport>>();
    return new WebSocketTransport(options, logger);
});
```

## Service Management

Manage long-running services with auto-start capability:

```csharp
public class DataSyncService : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Start background sync
        return Task.CompletedTask;
    }
    
    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Clean shutdown
        return Task.CompletedTask;
    }
}

// Use auto-service management via McpServerBuilder
var builder = new McpServerBuilder()
    .WithServerInfo("My Server", "1.0.0")
    .UseStdioTransport()
    .UseAutoService(config =>
    {
        config.ServiceId = "data-sync-service";
        config.ExecutablePath = "path/to/service.exe";
        config.Port = 5100;
        config.HealthEndpoint = "http://localhost:5100/health";
        config.AutoRestart = true;
        config.StartupTimeoutSeconds = 30;
    });
```

## Resource Providers

Expose data resources to AI agents:

```csharp
public class DocumentResourceProvider : IResourceProvider
{
    public string Name => "documents";
    
    public async Task<Resource[]> ListResourcesAsync(CancellationToken cancellationToken)
    {
        return new[]
        {
            new Resource
            {
                Uri = "document://readme",
                Name = "README",
                Description = "Project documentation",
                MimeType = "text/markdown"
            }
        };
    }
    
    public async Task<ResourceContent> GetResourceAsync(
        string uri,
        CancellationToken cancellationToken)
    {
        if (uri == "document://readme")
        {
            return new ResourceContent
            {
                Uri = uri,
                MimeType = "text/markdown",
                Text = await File.ReadAllTextAsync("README.md", cancellationToken)
            };
        }
        
        throw new ResourceNotFoundException(uri);
    }
}

// Register provider
services.AddSingleton<IResourceProvider, DocumentResourceProvider>();
```

## Schema Generation

Automatic JSON schema generation from C# types:

```csharp
// Schema is automatically generated from parameter types
public class ComplexParams
{
    [Required]
    [Description("User identifier")]
    public string UserId { get; set; }
    
    [Description("Optional filters")]
    public FilterOptions Filters { get; set; }
    
    public class FilterOptions
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string[] Tags { get; set; }
    }
}

// Schema will include nested types, descriptions, and validation rules
```

## Advanced Features

### Tool Categories
Organize tools by category:

```csharp
[ToolCategory(ToolCategory.DataAccess)]
public class DatabaseQueryTool : McpToolBase<QueryParams, QueryResult>
{
    // Tool implementation
}
```

### Execution Context
Access request context in tools:

```csharp
protected override async Task<MyResult> ExecuteInternalAsync(
    MyParams parameters,
    CancellationToken cancellationToken)
{
    // Access context
    var context = ExecutionContext;
    var requestId = context.RequestId;
    var timestamp = context.Timestamp;
    var metadata = context.Metadata;
    
    // Tool logic
}
```

### Custom Validators
Create custom parameter validators:

```csharp
public class PrimeNumberAttribute : ParameterValidationAttribute
{
    public override ValidationResult Validate(object value, ValidationContext context)
    {
        if (value is int number && IsPrime(number))
            return ValidationResult.Success;
            
        return new ValidationResult("Value must be a prime number");
    }
}
```

## Configuration

### Server Configuration
```json
{
  "Mcp": {
    "Server": {
      "Name": "my-server",
      "Version": "1.0.0",
      "MaxConcurrentRequests": 10,
      "RequestTimeout": "00:00:30",
      "EnableDetailedErrors": true
    },
    "Transport": {
      "Type": "Stdio",
      "BufferSize": 65536
    },
    "Logging": {
      "LogLevel": "Information",
      "EnableRequestLogging": true
    }
  }
}
```

### Dependency Injection
```csharp
// Configure services
services.AddMcpServer(options =>
{
    options.ServerInfo = new ServerInfo { Name = "server" };
    options.MaxConcurrentRequests = 10;
});

// Register all tools in assembly
services.AddMcpToolsFromAssembly(typeof(Program).Assembly);

// Register all prompts
services.AddMcpPromptsFromAssembly(typeof(Program).Assembly);

// Register all resources
services.AddMcpResourcesFromAssembly(typeof(Program).Assembly);
```

## Testing

Use the Testing package for unit and integration tests:

```csharp
using COA.Mcp.Framework.Testing;

[TestFixture]
public class CalculatorToolTests : ToolTestBase<CalculatorTool>
{
    [Test]
    public async Task Add_TwoNumbers_ReturnsSum()
    {
        // Arrange
        var parameters = new CalculatorParams
        {
            A = 5,
            B = 3,
            Operation = "add"
        };
        
        // Act
        var result = await ExecuteToolAsync(parameters);
        
        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Result, Is.EqualTo(8));
    }
}
```

## Best Practices

1. **Always validate parameters** - Use validation attributes
2. **Return structured errors** - Include recovery steps
3. **Keep tools focused** - One tool, one responsibility
4. **Use async/await** - All operations should be async
5. **Handle cancellation** - Respect CancellationToken
6. **Log appropriately** - Use ILogger for diagnostics
7. **Document tools** - Clear names and descriptions
8. **Test thoroughly** - Unit and integration tests

## Migration from Earlier Versions

### From 1.3.x to 1.4.0
- Update package reference to 1.4.0
- Service management API changed - see migration guide
- New prompt system - update prompt implementations

### From 1.2.x to 1.3.x
- Schema generation improved - regenerate schemas
- Transport configuration moved to options pattern

## Troubleshooting

### Tool not discovered
- Ensure tool implements IMcpTool
- Register tool in DI container
- Check tool has public parameterless constructor

### Validation not working
- Verify attributes are from COA.Mcp.Framework
- Check property has public getter/setter
- Ensure validation is not disabled in config

### Transport connection issues
- Verify transport configuration
- Check firewall/port settings for HTTP/WebSocket
- Enable detailed logging for diagnostics

## Examples

See the `/examples/SimpleMcpServer` directory for a complete working example.

## Support

- GitHub Issues: [Report bugs or request features](https://github.com/yourusername/coa-mcp-framework)
- Documentation: [Full documentation](https://docs.example.com)
- NuGet: [COA.Mcp.Framework](https://www.nuget.org/packages/COA.Mcp.Framework)

## License

See LICENSE file in the repository root.