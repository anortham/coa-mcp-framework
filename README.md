# COA MCP Framework

A comprehensive .NET framework for building and consuming Model Context Protocol (MCP) servers with built-in token optimization, AI-friendly responses, strong typing, and developer-first design.

[![NuGet Version](https://img.shields.io/nuget/v/COA.Mcp.Framework)](https://www.nuget.org/packages/COA.Mcp.Framework)
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)](https://github.com/anortham/COA-Mcp-Framework)
[![Tests](https://img.shields.io/badge/tests-647%20passing-success)](https://github.com/anortham/COA-Mcp-Framework)
[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-blue)](https://dotnet.microsoft.com/download)

## 🚀 Quick Start

**Never used MCP before?** Follow our **[5-Minute Quickstart Guide](QUICKSTART.md)** 👈

### Super Simple Example

1. **Install:** `dotnet add package COA.Mcp.Framework`

2. **Copy this code** into Program.cs:

```csharp
using COA.Mcp.Framework.Server;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;

public class EchoTool : McpToolBase<EchoParams, EchoResult>
{
    public override string Name => "echo";
    public override string Description => "Echoes back your message";
    
    protected override async Task<EchoResult> ExecuteInternalAsync(
        EchoParams parameters, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return new EchoResult 
        { 
            Success = true, 
            Response = $"You said: {parameters.Text}" 
        };
    }
}

public class EchoParams { public string Text { get; set; } = ""; }
public class EchoResult : ToolResultBase 
{ 
    public override string Operation => "echo";
    public string Response { get; set; } = "";
}

class Program
{
    static async Task Main(string[] args)
    {
        var builder = new McpServerBuilder()
            .WithServerInfo("My First MCP Server", "1.0.0");
        
        builder.RegisterToolType<EchoTool>();
        await builder.RunAsync();
    }
}
```

3. **Run it:** `dotnet run`

**🎉 That's it!** You have a working MCP server.

### Next Steps

**Want more examples?**
- [Hello World Example](examples/1-HelloWorld/) - Even simpler
- [Multiple Tools Example](examples/2-BasicTools/) - Calculator, text processing, etc.
- [Full-Featured Example](examples/SimpleMcpServer/) - All the bells and whistles

**Need help?**
- [5-Minute Quickstart](QUICKSTART.md) - Step-by-step tutorial
- [Common Issues](docs/COMMON_PITFALLS.md) - Solutions to typical problems
- [Transport Guide](docs/WHICH_TRANSPORT.md) - STDIO vs HTTP vs WebSocket

**Ready for production?** See [Advanced Features](#-advanced-features) below.

### 🆕 Enable AI-Powered Middleware

Add intelligent type verification and TDD enforcement to your MCP server:

```csharp
// Using McpServerBuilder (applies globally to all tools)
var builder = new McpServerBuilder()
    .WithServerInfo("My MCP Server", "1.0.0")
    .AddTypeVerificationMiddleware(options =>
    {
        options.Mode = TypeVerificationMode.Strict; // or Warning
        options.WhitelistedTypes.Add("MyCustomType");
    })
    .AddTddEnforcementMiddleware(options =>
    {
        options.Mode = TddEnforcementMode.Warning; // or Strict
        options.TestFilePatterns.Add("**/*Spec.cs"); // Custom test patterns
    });

// Or with DI when using Host
services.AddAdvancedEnforcement(
    configureTypeVerification: o =>
    {
        o.Mode = TypeVerificationMode.Strict;
        o.WhitelistedTypes.Add("MyCustomType");
    },
    configureTddEnforcement: o =>
    {
        o.Mode = TddEnforcementMode.Warning;
        o.TestFilePatterns.Add("**/*Spec.cs");
    });
```

**Benefits:**
- 🛡️ **Type Safety**: Catches undefined types before code generation fails
- 🧪 **Quality Assurance**: Enforces proper testing practices
- 🚀 **AI-Friendly**: Provides clear error messages with recovery steps
- ⚡ **Performance**: Intelligent caching with file modification detection

## 📦 NuGet Packages

- COA.Mcp.Framework — Core framework with MCP protocol included
- COA.Mcp.Protocol — Low-level protocol types and JSON-RPC
- COA.Mcp.Client — Strongly-typed C# client for MCP servers
- COA.Mcp.Framework.TokenOptimization — Advanced token management and AI response optimization
- COA.Mcp.Framework.Testing — Testing helpers, assertions, and benchmarks
- COA.Mcp.Framework.Templates — Project templates for quick starts
- COA.Mcp.Framework.Migration — Migration tools for updating from older versions

## ✨ Key Features

### 🔒 **Type-Safe Tool Development**
- Generic base class `McpToolBase<TParams, TResult>` ensures compile-time type safety
- Automatic parameter validation using data annotations
- No manual JSON parsing required

### 🏗️ **Clean Architecture**
- Single unified tool registry with automatic disposal
- Fluent server builder API
- Dependency injection support
- Clear separation of concerns
- IAsyncDisposable support for resource management

### 🛡️ **Comprehensive Error Handling**
- Standardized error models with `ErrorInfo` and `RecoveryInfo`
- AI-friendly error messages with recovery steps
- **Built-in validation helpers** - `ValidateRequired()`, `ValidatePositive()`, `ValidateRange()`, `ValidateNotEmpty()`
- **Error result helpers** - `CreateErrorResult()`, `CreateValidationErrorResult()` with recovery steps
- **Customizable error messages** - Override `ErrorMessages` property for tool-specific guidance

### 🎯 **Generic Type Safety**
- **Generic Parameter Validation**: `IParameterValidator<TParams>` eliminates casting with strongly-typed validation
- **Generic Resource Caching**: `IResourceCache<TResource>` supports any resource type with compile-time safety
- **Generic Response Building**: `BaseResponseBuilder<TInput, TResult>` and `AIOptimizedResponse<T>` prevent object casting
- **Backward Compatible**: All generic interfaces include non-generic versions for seamless migration

### 🧠 **Token Management**
- Pre-estimation to prevent context overflow
- Progressive reduction for large datasets
- Smart truncation with resource URIs
- **Per-tool token budgets** - Configure limits via `ConfigureTokenBudgets()` in server builder
- **Hierarchical configuration** - Tool-specific, category, and default budget settings

### 🔗 **Lifecycle Hooks & Middleware**
- **Extensible execution pipeline** - Add cross-cutting concerns with simple middleware
- **Built-in middleware** - Logging, token counting, performance monitoring
- **🆕 Type Verification Middleware** - Prevents AI hallucinated types in code generation with intelligent caching
- **🆕 TDD Enforcement Middleware** - Enforces Test-Driven Development workflow (Red-Green-Refactor)
- **Smart caching system** - Session-scoped type verification with file modification invalidation
- **Multi-platform test integration** - Supports dotnet test, npm test, pytest, and more
- **Custom middleware support** - Implement `ISimpleMiddleware` for custom logic
- **Per-tool configuration** - Override `ToolSpecificMiddleware` for tool-specific hooks
- See **[Lifecycle Hooks Guide](docs/lifecycle-hooks.md)** for detailed documentation

### 💬 **Interactive Prompts**
- Guide users through complex operations with prompt templates
- Customizable arguments with validation
- Built-in message builders for system/user/assistant roles
- Variable substitution in templates

### 🚀 **Auto-Service Management** (New)
- Automatic startup of background services
- Health monitoring and auto-restart capabilities
- Port conflict detection
- Support for multiple managed services

### 🚄 **Rapid Development**
- Minimal boilerplate code
- Built-in validation helpers
- Comprehensive IntelliSense support
- Rich example projects

## 🎯 Client Library

### Consuming MCP Servers with COA.Mcp.Client

The framework includes a strongly-typed C# client library for interacting with MCP servers:

```csharp
// Create a typed client with fluent configuration
var client = await McpClientBuilder
    .Create("http://localhost:5000")
    .WithTimeout(TimeSpan.FromSeconds(30))
    .WithRetry(maxAttempts: 3, delayMs: 1000)
    .WithApiKey("your-api-key")
    .BuildAndInitializeAsync();

// List available tools
var tools = await client.ListToolsAsync();

// Call a tool with type safety
var result = await client.CallToolAsync("weather", new { location = "Seattle" });
```

### Strongly-Typed Client Operations

```csharp
// Define your types
public class WeatherParams
{
    public string Location { get; set; }
    public string Units { get; set; } = "celsius";
}

public class WeatherResult : ToolResultBase
{
    public override string Operation => "get_weather";
    public double Temperature { get; set; }
    public string Description { get; set; }
}

// Create a typed client
var typedClient = McpClientBuilder
    .Create("http://localhost:5000")
    .BuildTyped<WeatherParams, WeatherResult>();

// Call with full type safety
var weather = await typedClient.CallToolAsync("weather", 
    new WeatherParams { Location = "Seattle" });

if (weather.Success)
{
    Console.WriteLine($"Temperature: {weather.Temperature}°");
}
```

## 📝 Interactive Prompts

### Creating Custom Prompts

Prompts provide interactive templates to guide users through complex operations:

```csharp
// Define a prompt to help users generate code
public class CodeGeneratorPrompt : PromptBase
{
    public override string Name => "code-generator";
    public override string Description => 
        "Generate code snippets based on requirements";

    public override List<PromptArgument> Arguments => new()
    {
        new PromptArgument
        {
            Name = "language",
            Description = "Programming language (csharp, python, js)",
            Required = true
        },
        new PromptArgument
        {
            Name = "type",
            Description = "Type of code (class, function, interface)",
            Required = true
        },
        new PromptArgument
        {
            Name = "name",
            Description = "Name of the component",
            Required = true
        }
    };

    public override async Task<GetPromptResult> RenderAsync(
        Dictionary<string, object>? arguments = null,
        CancellationToken cancellationToken = default)
    {
        var language = GetRequiredArgument<string>(arguments, "language");
        var type = GetRequiredArgument<string>(arguments, "type");
        var name = GetRequiredArgument<string>(arguments, "name");
        
        return new GetPromptResult
        {
            Description = $"Generate {language} {type}: {name}",
            Messages = new List<PromptMessage>
            {
                CreateSystemMessage($"You are an expert {language} developer."),
                CreateUserMessage($"Generate a {type} named '{name}' in {language}."),
                CreateAssistantMessage("I'll help you create that component...")
            }
        };
    }
}

// Register prompts in your server
builder.RegisterPromptType<CodeGeneratorPrompt>();
```

### Variable Substitution

Use the built-in variable substitution for dynamic templates:

```csharp
var template = "Hello {{name}}, your project {{project}} is ready!";
var result = SubstituteVariables(template, new Dictionary<string, object>
{
    ["name"] = "Developer",
    ["project"] = "MCP Server"
});
// Result: "Hello Developer, your project MCP Server is ready!"
```

## 🔗 Lifecycle Hooks & Middleware

The framework provides a powerful middleware system for adding cross-cutting concerns to your tools:

### Adding Middleware to Tools

```csharp
public class MyTool : McpToolBase<MyParams, MyResult>
{
    private readonly ILogger<MyTool>? _logger;

    public MyTool(IServiceProvider? serviceProvider, ILogger<MyTool>? logger = null)
        : base(serviceProvider, logger)
    {
        _logger = logger;
    }

    // Configure middleware for this specific tool
    protected override IReadOnlyList<ISimpleMiddleware>? ToolSpecificMiddleware => new List<ISimpleMiddleware>
    {
        // Built-in token counting middleware
        new TokenCountingSimpleMiddleware(),

        // Custom timing middleware
        new TimingMiddleware(_logger!)
    };

    // Your tool implementation...
}
```

### Built-in Middleware

- **TokenCountingSimpleMiddleware**: Estimates and logs token usage
- **LoggingSimpleMiddleware**: Comprehensive execution logging

### Custom Middleware

```csharp
public class TimingMiddleware : SimpleMiddlewareBase
{
    private readonly ILogger _logger;
    
    public TimingMiddleware(ILogger logger)
    {
        _logger = logger;
        Order = 50; // Controls execution order
    }
    
    public override Task OnBeforeExecutionAsync(string toolName, object? parameters)
    {
        _logger.LogInformation("🚀 Starting {ToolName}", toolName);
        return Task.CompletedTask;
    }
    
    public override Task OnAfterExecutionAsync(string toolName, object? parameters, object? result, long elapsedMs)
    {
        var performance = elapsedMs < 100 ? "⚡ Fast" : elapsedMs < 1000 ? "🚶 Normal" : "🐌 Slow";
        _logger.LogInformation("✅ {ToolName} completed: {Performance} ({ElapsedMs}ms)", 
            toolName, performance, elapsedMs);
        return Task.CompletedTask;
    }
    
    public override Task OnErrorAsync(string toolName, object? parameters, Exception exception, long elapsedMs)
    {
        _logger.LogWarning("💥 {ToolName} failed after {ElapsedMs}ms: {Error}", 
            toolName, elapsedMs, exception.Message);
        return Task.CompletedTask;
    }
}
```

### Middleware Execution Flow

1. **Sort by Order** (ascending): Lower numbers run first
2. **Before hooks** (in order): middleware1 → middleware2 → middleware3  
3. **Tool execution** with validation and token management
4. **After hooks** (reverse order): middleware3 → middleware2 → middleware1
5. **Error hooks** (reverse order, if error occurs)

**📖 For complete documentation and advanced examples, see [Lifecycle Hooks Guide](docs/lifecycle-hooks.md)**

## 🏁 Getting Started

### Working Example: SimpleMcpServer

Check out our complete working example in `examples/SimpleMcpServer/`:

```csharp
// From examples/SimpleMcpServer/Tools/CalculatorTool.cs
public class CalculatorTool : McpToolBase<CalculatorParameters, CalculatorResult>
{
    public override string Name => "calculator";
    public override string Description => "Performs basic arithmetic operations";
    public override ToolCategory Category => ToolCategory.Utility;

    protected override async Task<CalculatorResult> ExecuteInternalAsync(
        CalculatorParameters parameters,
        CancellationToken cancellationToken)
    {
        // Validate inputs using base class helpers
        ValidateRequired(parameters.Operation, nameof(parameters.Operation));
        ValidateRequired(parameters.A, nameof(parameters.A));
        ValidateRequired(parameters.B, nameof(parameters.B));

        var a = parameters.A!.Value;
        var b = parameters.B!.Value;
        
        double result = parameters.Operation.ToLower() switch
        {
            "add" or "+" => a + b,
            "subtract" or "-" => a - b,
            "multiply" or "*" => a * b,
            "divide" or "/" => b != 0 ? a / b : 
                throw new DivideByZeroException("Cannot divide by zero"),
            _ => throw new NotSupportedException($"Operation '{parameters.Operation}' is not supported")
        };

        return new CalculatorResult
        {
            Success = true,
            Operation = parameters.Operation,
            Expression = $"{a} {parameters.Operation} {b}",
            Result = result,
            Meta = new ToolMetadata
            {
                ExecutionTime = $"{stopwatch.ElapsedMilliseconds}ms"
            }
        };
    }
}
```

### Setting Up Your Server

```csharp
// Program.cs for your MCP server
var builder = new McpServerBuilder()
    .WithServerInfo("My MCP Server", "1.0.0")
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
    });

// Register your services
builder.Services.AddSingleton<IMyService, MyService>();

// Register your tools (two options)

// Option 1: Manual registration (recommended for explicit control)
builder.RegisterToolType<MyFirstTool>();
builder.RegisterToolType<MySecondTool>();
builder.RegisterToolType<LifecycleExampleTool>(); // Example with middleware

// Option 2: Automatic discovery (scans assembly for tools)
builder.DiscoverTools(typeof(Program).Assembly);

// Build and run
await builder.RunAsync();
```

### Transport Configuration

The framework supports multiple transport types for flexibility:

```csharp
// Default: Standard I/O (for Claude Desktop and CLI tools)
var builder = new McpServerBuilder()
    .WithServerInfo("My Server", "1.0.0");
    // Uses stdio transport by default

// HTTP Transport (for web-based clients)
var builder = new McpServerBuilder()
    .WithServerInfo("My Server", "1.0.0")
    .UseHttpTransport(options =>
    {
        options.Port = 5000;
        options.EnableWebSocket = true;
        options.EnableCors = true;
        options.Authentication = AuthenticationType.ApiKey;
        options.ApiKey = "your-api-key";
    });

// WebSocket Transport (for real-time communication)
var builder = new McpServerBuilder()
    .WithServerInfo("My Server", "1.0.0")
    .UseWebSocketTransport(options =>
    {
        options.Port = 8080;
        options.Host = "localhost";
        options.UseHttps = false;
    });
```

### Logging Configuration

The framework provides granular control over logging to reduce noise and improve debugging experience:

```csharp
var builder = new McpServerBuilder()
    .WithServerInfo("My Server", "1.0.0")
    .ConfigureLogging(logging =>
    {
        // Standard logging configuration
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
        
        // Optional: Configure specific categories
        logging.AddFilter("COA.Mcp.Framework", LogLevel.Warning); // Quiet framework
        logging.AddFilter("MyApp", LogLevel.Debug); // Verbose for your code
    })
    .ConfigureFramework(options =>
    {
        // Framework-specific logging options
        options.FrameworkLogLevel = LogLevel.Warning;        // Default framework log level
        options.EnableDetailedToolLogging = false;          // Reduce tool execution noise
        options.EnableDetailedMiddlewareLogging = false;    // Reduce middleware noise
        options.EnableDetailedTransportLogging = false;     // Reduce transport noise
        
        // Advanced options
        options.EnableFrameworkLogging = true;              // Enable/disable framework logging entirely
        options.ConfigureLoggingIfNotConfigured = true;     // Don't override existing logging config
        options.SuppressStartupLogs = false;                // Show/hide startup messages
    });
```

#### Logging Categories

The framework uses these logging categories for fine-grained control:

- `COA.Mcp.Framework.Pipeline.Middleware` - Middleware operations (type verification, TDD enforcement, etc.)
- `COA.Mcp.Framework.Transport` - Transport layer operations (HTTP, WebSocket, stdio)
- `COA.Mcp.Framework.Base` - Tool execution and lifecycle events
- `COA.Mcp.Framework.Server` - Server startup and management
- `COA.Mcp.Framework.Pipeline` - Request/response pipeline processing

#### Quick Configuration Examples

```csharp
// Minimal logging (production)
builder.ConfigureFramework(options =>
{
    options.FrameworkLogLevel = LogLevel.Error;
    options.EnableDetailedToolLogging = false;
    options.EnableDetailedMiddlewareLogging = false;
    options.EnableDetailedTransportLogging = false;
});

// Debug mode (development)
builder.ConfigureFramework(options =>
{
    options.FrameworkLogLevel = LogLevel.Debug;
    options.EnableDetailedToolLogging = true;
    options.EnableDetailedMiddlewareLogging = true;
    options.EnableDetailedTransportLogging = true;
});

// Completely disable framework logging
builder.ConfigureFramework(options =>
{
    options.EnableFrameworkLogging = false;
});
```

### 🚀 Auto-Service Management

The framework now supports automatic service startup, enabling dual-mode architectures where an MCP server can act as both a STDIO client and an HTTP service provider:

```csharp
// Configure auto-started services alongside your MCP server
var builder = new McpServerBuilder()
    .WithServerInfo("My MCP Server", "1.0.0")
    .UseStdioTransport()  // Primary transport for Claude
    .UseAutoService(config =>
    {
        config.ServiceId = "my-http-api";
        config.ExecutablePath = Assembly.GetExecutingAssembly().Location;
        config.Arguments = new[] { "--mode", "http", "--port", "5100" };
        config.Port = 5100;
        config.HealthEndpoint = "http://localhost:5100/health";
        config.AutoRestart = true;
        config.MaxRestartAttempts = 3;
    });

await builder.RunAsync();
```

#### Key Features:
- **Automatic Startup**: Services start automatically when the MCP server launches
- **Health Monitoring**: Periodic health checks with configurable intervals
- **Auto-Restart**: Automatic recovery from service failures with retry limits
- **Port Detection**: Checks if ports are already in use before starting
- **Graceful Shutdown**: Clean service termination when MCP server stops
- **Multiple Services**: Support for multiple auto-started services

#### Configuration Options:

```csharp
public class ServiceConfiguration
{
    public string ServiceId { get; set; }              // Unique service identifier
    public string ExecutablePath { get; set; }         // Path to executable
    public string[] Arguments { get; set; }            // Command-line arguments
    public int Port { get; set; }                      // Service port
    public string HealthEndpoint { get; set; }         // Health check URL
    public int StartupTimeoutSeconds { get; set; }     // Startup timeout (default: 30)
    public int HealthCheckIntervalSeconds { get; set; } // Health check interval (default: 60)
    public bool AutoRestart { get; set; }              // Enable auto-restart (default: true)
    public int MaxRestartAttempts { get; set; }        // Max restart attempts (default: 3)
    public Dictionary<string, string> EnvironmentVariables { get; set; } // Environment vars
}
```

#### Multiple Services Example:

```csharp
var builder = new McpServerBuilder()
    .WithServerInfo("Multi-Service MCP", "1.0.0")
    .UseStdioTransport()
    .UseAutoServices(
        config =>
        {
            config.ServiceId = "api-service";
            config.ExecutablePath = "api.exe";
            config.Port = 5100;
            config.HealthEndpoint = "http://localhost:5100/health";
        },
        config =>
        {
            config.ServiceId = "worker-service";
            config.ExecutablePath = "worker.exe";
            config.Port = 5200;
            config.HealthEndpoint = "http://localhost:5200/health";
        }
    );
```

#### Custom Health Checks:

```csharp
// Register a custom health check for advanced scenarios
var serviceProvider = builder.Services.BuildServiceProvider();
var serviceManager = serviceProvider.GetRequiredService<IServiceManager>();

serviceManager.RegisterHealthCheck("my-service", async () =>
{
    // Custom health check logic
    var client = new HttpClient();
    var response = await client.GetAsync("http://localhost:5100/custom-health");
    return response.IsSuccessStatusCode;
});
```

This feature is ideal for:
- **Dual-mode architectures**: MCP servers that need to expose HTTP APIs
- **Microservice coordination**: Managing related services together
- **Development environments**: Simplified local development setup
- **Federation scenarios**: MCP servers that communicate with each other

## 🔥 Advanced Features

### Generic Type Safety - Eliminate Object Casting

The framework provides generic versions of key interfaces to eliminate object casting and improve type safety:

#### Generic Parameter Validation

```csharp
// Before: Non-generic parameter validation (still supported)
public class LegacyTool : McpToolBase<MyParams, MyResult>
{
    protected override Task<MyResult> ExecuteInternalAsync(
        MyParams parameters, CancellationToken cancellationToken)
    {
        // Parameters already validated and strongly typed!
        return ProcessParameters(parameters); // No casting needed
    }
}

// New: Explicit generic parameter validator for advanced scenarios
public class CustomValidationTool : McpToolBase<ComplexParams, ComplexResult>
{
    private readonly IParameterValidator<ComplexParams> _validator;
    
    public CustomValidationTool(IParameterValidator<ComplexParams> validator)
    {
        _validator = validator; // Strongly typed, no object casting
    }
    
    protected override async Task<ComplexResult> ExecuteInternalAsync(
        ComplexParams parameters, CancellationToken cancellationToken)
    {
        // Custom validation with no object casting
        var validationResult = _validator.Validate(parameters);
        
        if (!validationResult.IsValid)
        {
            return CreateErrorResult("VALIDATION_FAILED", 
                string.Join(", ", validationResult.Errors.Select(e => e.Message)));
        }
        
        // Process strongly-typed parameters
        return ProcessComplexParameters(parameters);
    }
}
```

#### Generic Resource Caching

```csharp
// Cache any resource type with compile-time safety
public class SearchResultResourceProvider : IResourceProvider
{
    private readonly IResourceCache<SearchResultData> _cache; // Strongly typed cache!
    private readonly ISearchService _searchService;
    
    public SearchResultResourceProvider(
        IResourceCache<SearchResultData> cache, // No more object casting
        ISearchService searchService)
    {
        _cache = cache;
        _searchService = searchService;
    }
    
    public async Task<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken ct)
    {
        // Check cache first - strongly typed, no casting
        var cached = await _cache.GetAsync(uri);
        if (cached != null)
        {
            return CreateReadResourceResult(cached); // Type-safe operations
        }
        
        // Generate new data
        var searchData = await _searchService.SearchAsync(ExtractQuery(uri));
        
        // Store in cache - type-safe storage
        await _cache.SetAsync(uri, searchData, TimeSpan.FromMinutes(10));
        
        return CreateReadResourceResult(searchData);
    }
    
    private ReadResourceResult CreateReadResourceResult(SearchResultData data)
    {
        return new ReadResourceResult
        {
            Contents = new List<ResourceContent>
            {
                new ResourceContent
                {
                    Uri = data.OriginalQuery,
                    Text = JsonSerializer.Serialize(data), // Type-safe serialization
                    MimeType = "application/json"
                }
            }
        };
    }
}

// Register the strongly-typed cache
builder.Services.AddSingleton<IResourceCache<SearchResultData>, InMemoryResourceCache<SearchResultData>>();
```

#### Generic Response Building

```csharp
// Before: Object-based response building (still supported for backward compatibility)
public class LegacyResponseBuilder : BaseResponseBuilder
{
    public override async Task<object> BuildResponseAsync(object data, ResponseContext context)
    {
        return new AIOptimizedResponse // Returns object, requires casting
        {
            Data = new AIResponseData
            {
                Results = data, // object type, requires casting later
                Meta = new AIResponseMeta { /* ... */ }
            }
        };
    }
}

// New: Strongly-typed response building
public class TypedResponseBuilder : BaseResponseBuilder<SearchData, SearchResult>
{
    public override async Task<SearchResult> BuildResponseAsync(SearchData data, ResponseContext context)
    {
        return new SearchResult // Strongly typed return, no casting needed
        {
            Success = true,
            Operation = "search_data",
            Query = data.Query,
            Results = data.Items, // Type-safe property access
            TotalFound = data.Items.Count,
            ExecutionTime = context.ElapsedTime,
            // No object casting anywhere!
        };
    }
}

// Using the generic AIOptimizedResponse<T>
public class OptimizedSearchTool : McpToolBase<SearchParams, AIOptimizedResponse<SearchResultSummary>>
{
    protected override async Task<AIOptimizedResponse<SearchResultSummary>> ExecuteInternalAsync(
        SearchParams parameters, CancellationToken cancellationToken)
    {
        var searchData = await SearchAsync(parameters.Query);
        
        return new AIOptimizedResponse<SearchResultSummary> // Generic type, no casting!
        {
            Success = true,
            Operation = "search_optimized",
            Data = new AIResponseData<SearchResultSummary>
            {
                Results = new SearchResultSummary
                {
                    Query = parameters.Query,
                    TotalMatches = searchData.Count,
                    TopResults = searchData.Take(5).ToList()
                },
                Meta = new AIResponseMeta
                {
                    TokenUsage = EstimateTokens(searchData),
                    OptimizationApplied = searchData.Count > 100,
                    ResourceUri = searchData.Count > 100 ? StoreAsResource(searchData) : null
                }
            }
        };
    }
}
```

#### Migration Examples

```csharp
// Easy migration from non-generic to generic interfaces
public class MigrationExample
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Option 1: Use generic interface directly
        services.AddSingleton<IParameterValidator<MyParams>, DefaultParameterValidator<MyParams>>();
        services.AddSingleton<IResourceCache<MyResource>, InMemoryResourceCache<MyResource>>();
        
        // Option 2: Keep existing non-generic registrations (fully backward compatible)
        services.AddSingleton<IParameterValidator, DefaultParameterValidator>();
        services.AddSingleton<IResourceCache, InMemoryResourceCache>();
        
        // Option 3: Convert existing non-generic to generic using extension methods
        var nonGenericValidator = serviceProvider.GetService<IParameterValidator>();
        var typedValidator = nonGenericValidator.ForType<MyParams>(); // Extension method conversion
    }
}
```

#### Key Benefits

- **🎯 Compile-time Safety**: Catch type errors at build time instead of runtime
- **🚀 Better Performance**: No boxing/unboxing or reflection-based casting
- **🧠 Enhanced IntelliSense**: Full type information available in IDE
- **🔄 Seamless Migration**: Non-generic interfaces still work, upgrade at your own pace
- **🛠️ Cleaner Code**: Eliminate try-catch blocks around casting operations

### Customizable Error Messages

Override the `ErrorMessages` property in your tools to provide context-specific error messages and recovery guidance:

```csharp
public class DatabaseTool : McpToolBase<DbParams, DbResult>
{
    // Custom error message provider
    protected override ErrorMessageProvider ErrorMessages => new DatabaseErrorMessageProvider();
    
    // ... tool implementation
}

public class DatabaseErrorMessageProvider : ErrorMessageProvider
{
    public override string ToolExecutionFailed(string toolName, string details)
    {
        return $"Database operation '{toolName}' failed: {details}. Check connection status.";
    }
    
    public override RecoveryInfo GetRecoveryInfo(string errorCode, string? context = null, Exception? exception = null)
    {
        return errorCode switch
        {
            "CONNECTION_FAILED" => new RecoveryInfo
            {
                Steps = new[]
                {
                    "Verify database connection string",
                    "Check network connectivity",
                    "Ensure database server is running"
                },
                SuggestedActions = new[]
                {
                    new SuggestedAction
                    {
                        Tool = "test_connection",
                        Description = "Test database connectivity",
                        Parameters = new { timeout = 30 }
                    }
                }
            },
            _ => base.GetRecoveryInfo(errorCode, context, exception)
        };
    }
}
```

### Token Budget Configuration

Configure token limits per tool, category, or globally using the server builder:

```csharp
var builder = new McpServerBuilder()
    .WithServerInfo("My Server", "1.0.0")
    .ConfigureTokenBudgets(budgets =>
    {
        // Tool-specific limits (highest priority)
        budgets.ForTool<LargeDataTool>()
            .MaxTokens(20000)
            .WarningThreshold(16000)
            .WithStrategy(TokenLimitStrategy.Truncate)
            .Apply();
            
        budgets.ForTool<SearchTool>()
            .MaxTokens(5000)
            .WithStrategy(TokenLimitStrategy.Throw)
            .Apply();
        
        // Category-based limits (medium priority)
        budgets.ForCategory(ToolCategory.Analysis)
            .MaxTokens(15000)
            .WarningThreshold(12000)
            .Apply();
            
        budgets.ForCategory(ToolCategory.Query)
            .MaxTokens(8000)
            .Apply();
        
        // Default limits (lowest priority)
        budgets.Default()
            .MaxTokens(10000)
            .WarningThreshold(8000)
            .WithStrategy(TokenLimitStrategy.Warn)
            .EstimationMultiplier(1.2) // Conservative estimates
            .Apply();
    });
```

#### Token Budget Strategies

- **Warn**: Log warning and continue (default)
- **Throw**: Throw exception to prevent execution
- **Truncate**: Truncate output to stay within limits
- **Ignore**: No token limit enforcement

#### Per-Tool Token Budget Override

```csharp
public class HighVolumeAnalysisTool : McpToolBase<AnalysisParams, AnalysisResult>
{
    // Override the default token budget for this specific tool
    protected override TokenBudgetConfiguration TokenBudget => new()
    {
        MaxTokens = 50000,
        WarningThreshold = 40000,
        Strategy = TokenLimitStrategy.Truncate,
        EstimationMultiplier = 1.5
    };
    
    // ... tool implementation
}
```

### Built-in Validation Helpers

The framework provides several validation helpers in the `McpToolBase` class to simplify parameter validation:

```csharp
public class DataProcessingTool : McpToolBase<DataParams, DataResult>
{
    protected override async Task<DataResult> ExecuteInternalAsync(
        DataParams parameters,
        CancellationToken cancellationToken)
    {
        // Validate required parameters (throws ValidationException if null/empty)
        var filePath = ValidateRequired(parameters.FilePath, nameof(parameters.FilePath));
        var query = ValidateRequired(parameters.Query, nameof(parameters.Query));
        
        // Validate positive numbers
        var maxResults = ValidatePositive(parameters.MaxResults, nameof(parameters.MaxResults));
        
        // Validate ranges
        var priority = ValidateRange(parameters.Priority, 1, 10, nameof(parameters.Priority));
        
        // Validate collections aren't empty
        var tags = ValidateNotEmpty(parameters.Tags, nameof(parameters.Tags));
        
        // All validation passed - process the data
        return await ProcessDataAsync(filePath, query, maxResults, priority, tags);
    }
}
```

#### Available Validation Helpers

| Helper | Purpose | Throws |
|--------|---------|--------|
| `ValidateRequired<T>(value, paramName)` | Ensures value is not null or empty string | `ValidationException` |
| `ValidatePositive(value, paramName)` | Ensures numeric value > 0 | `ValidationException` |
| `ValidateRange(value, min, max, paramName)` | Ensures value is within range | `ValidationException` |
| `ValidateNotEmpty<T>(collection, paramName)` | Ensures collection has items | `ValidationException` |

### Built-in Error Result Helpers

The framework provides helpers to create standardized error results with recovery information:

```csharp
public class DatabaseTool : McpToolBase<DbParams, DbResult>
{
    protected override async Task<DbResult> ExecuteInternalAsync(
        DbParams parameters,
        CancellationToken cancellationToken)
    {
        try
        {
            var connectionString = ValidateRequired(parameters.ConnectionString, nameof(parameters.ConnectionString));
            
            // Attempt database operation
            var result = await ExecuteDatabaseQuery(connectionString, parameters.Query);
            
            return new DbResult
            {
                Success = true,
                Operation = "database_query",
                Data = result
            };
        }
        catch (SqlException ex) when (ex.Number == 2) // Connection timeout
        {
            // Create standardized error with recovery steps
            return new DbResult
            {
                Success = false,
                Operation = "database_query",
                Error = CreateErrorResult(
                    "database_query", 
                    $"Database connection timeout: {ex.Message}",
                    "Verify database server is running and accessible"
                )
            };
        }
        catch (ArgumentException ex)
        {
            // Create validation error with specific guidance
            return new DbResult
            {
                Success = false,
                Operation = "database_query",
                Error = CreateValidationErrorResult(
                    "database_query",
                    "connectionString",
                    "Must be a valid SQL Server connection string"
                )
            };
        }
    }
}
```

#### Available Error Result Helpers

| Helper | Purpose | Returns |
|--------|---------|---------|
| `CreateErrorResult(operation, error, recoveryStep?)` | Creates `ErrorInfo` with recovery guidance | `ErrorInfo` |
| `CreateValidationErrorResult(operation, paramName, requirement)` | Creates validation-specific error | `ErrorInfo` |
| `CreateSuccessResult<T>(data, message?)` | Creates successful `ToolResult<T>` | `ToolResult<T>` |
| `CreateErrorResult<T>(errorMessage, errorCode?)` | Creates failed `ToolResult<T>` | `ToolResult<T>` |

### Error Handling with Recovery Steps

```csharp
public class FileAnalysisTool : McpToolBase<FileAnalysisParams, FileAnalysisResult>
{
    protected override async Task<FileAnalysisResult> ExecuteInternalAsync(
        FileAnalysisParams parameters,
        CancellationToken cancellationToken)
    {
        try
        {
            var filePath = ValidateRequired(parameters.FilePath, nameof(parameters.FilePath));
            
            if (!File.Exists(filePath))
            {
                // Return AI-friendly error with recovery steps
                return new FileAnalysisResult
                {
                    Success = false,
                    Operation = "analyze_file",
                    Error = new ErrorInfo
                    {
                        Code = "FILE_NOT_FOUND",
                        Message = $"File not found: {filePath}",
                        Recovery = new RecoveryInfo
                        {
                            Steps = new[]
                            {
                                "Verify the file path is correct",
                                "Check if the file exists",
                                "Ensure you have read permissions"
                            },
                            SuggestedActions = new[]
                            {
                                new SuggestedAction
                                {
                                    Tool = "list_files",
                                    Description = "List files in directory",
                                    Parameters = new { path = Path.GetDirectoryName(filePath) }
                                }
                            }
                        }
                    }
                };
            }
            
            // Perform analysis...
            var analysis = await AnalyzeFileAsync(filePath);
            
            return new FileAnalysisResult
            {
                Success = true,
                Operation = "analyze_file",
                FilePath = filePath,
                Analysis = analysis,
                Insights = GenerateInsights(analysis),
                Actions = GenerateNextActions(analysis)
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            return CreateErrorResult(
                "PERMISSION_DENIED",
                $"Access denied: {ex.Message}",
                new[] { "Check file permissions", "Run with appropriate privileges" }
            );
        }
    }
}
```

### Resource Providers

#### Automatic Resource Caching (v1.4.8+)
The framework now provides automatic singleton-level caching for resources, solving the lifetime mismatch between scoped providers and singleton registry:

```csharp
// Resource caching is automatically configured by McpServerBuilder
// No additional setup required - just implement your provider!

public class SearchResultResourceProvider : IResourceProvider
{
    private readonly ISearchService _searchService; // Can be scoped!
    
    public SearchResultResourceProvider(ISearchService searchService)
    {
        _searchService = searchService; // Scoped dependency is OK
    }
    
    public string Scheme => "search-results";
    public string Name => "Search Results Provider";
    public string Description => "Provides search result resources";
    
    public bool CanHandle(string uri) => 
        uri.StartsWith($"{Scheme}://");
    
    public async Task<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken ct)
    {
        // No need to implement caching - framework handles it!
        var sessionId = ExtractSessionId(uri);
        var results = await _searchService.LoadResultsAsync(sessionId);
        
        return new ReadResourceResult
        {
            Contents = new List<ResourceContent>
            {
                new ResourceContent
                {
                    Uri = uri,
                    Text = JsonSerializer.Serialize(results),
                    MimeType = "application/json"
                }
            }
        };
    }
    
    public async Task<List<Resource>> ListResourcesAsync(CancellationToken ct)
    {
        // List available resources
        var sessions = await _searchService.GetActiveSessionsAsync();
        return sessions.Select(s => new Resource
        {
            Uri = $"{Scheme}://{s.Id}",
            Name = $"Search Results {s.Id}",
            Description = $"Results for query: {s.Query}",
            MimeType = "application/json"
        }).ToList();
    }
}

// Register your provider - caching is automatic!
builder.Services.AddScoped<IResourceProvider, SearchResultResourceProvider>();

// Optional: Configure cache settings
builder.Services.Configure<ResourceCacheOptions>(options =>
{
    options.DefaultExpiration = TimeSpan.FromMinutes(10);
    options.SlidingExpiration = TimeSpan.FromMinutes(5);
    options.MaxSizeBytes = 200 * 1024 * 1024; // 200 MB
});
```

#### Why Resource Caching?
- **Solves lifetime mismatch**: Scoped providers can work with singleton registry
- **Improves performance**: Automatic caching of expensive operations
- **Memory efficient**: Built-in size limits and expiration
- **Transparent**: No code changes needed in existing providers
- **Resilient**: Failures in cache don't affect core functionality

### Token Optimization (with optional package)

```csharp
// Add the TokenOptimization package
// <PackageReference Include="COA.Mcp.Framework.TokenOptimization" Version="1.7.17" />

public class SearchTool : McpToolBase<SearchParams, SearchResult>
{
    protected override async Task<SearchResult> ExecuteInternalAsync(
        SearchParams parameters,
        CancellationToken cancellationToken)
    {
        var results = await SearchAsync(parameters.Query);
        
        // Use token management from base class
        return await ExecuteWithTokenManagement(async () =>
        {
            // Automatically handles token limits
            return new SearchResult
            {
                Success = true,
                Results = results, // Auto-truncated if needed
                TotalCount = results.Count,
                ResourceUri = results.Count > 100 ? 
                    await StoreAsResourceAsync(results) : null
            };
        });
    }
}
```

### Testing Your Tools

```csharp
using COA.Mcp.Framework.Testing;
using FluentAssertions;

[TestFixture]
public class WeatherToolTests
{
    private WeatherTool _tool;
    private Mock<IWeatherService> _weatherService;
    
    [SetUp]
    public void Setup()
    {
        _weatherService = new Mock<IWeatherService>();
        _tool = new WeatherTool(_weatherService.Object);
    }
    
    [Test]
    public async Task GetWeather_WithValidLocation_ReturnsWeatherData()
    {
        // Arrange
        var parameters = new WeatherParameters
        {
            Location = "Seattle",
            ForecastDays = 3
        };
        
        _weatherService
            .Setup(x => x.GetWeatherAsync("Seattle", 3))
            .ReturnsAsync(new WeatherData { /* ... */ });
        
        // Act
        var result = await _tool.ExecuteAsync(parameters);
        
        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Location.Should().Be("Seattle");
        result.Forecast.Should().HaveCount(3);
    }
    
    [Test]
    public async Task GetWeather_WithMissingLocation_ReturnsError()
    {
        // Arrange
        var parameters = new WeatherParameters { Location = null };
        
        // Act
        var result = await _tool.ExecuteAsync(parameters);
        
        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error.Code.Should().Be("VALIDATION_ERROR");
    }
}
```

## 📚 Documentation

### Framework Structure

```
COA.Mcp.Framework/
├── Base/
│   └── McpToolBase.Generic.cs    # Generic base class for tools
├── Server/
│   ├── McpServer.cs              # Main server implementation
│   ├── McpServerBuilder.cs       # Fluent builder API
│   └── Services/                 # Auto-service management
│       ├── ServiceManager.cs     # Service lifecycle management
│       ├── ServiceConfiguration.cs # Service config model
│       └── ServiceLifecycleHost.cs # IHostedService integration
├── Registration/
│   └── McpToolRegistry.cs        # Unified tool registry
├── Interfaces/
│   ├── IMcpTool.cs              # Tool interfaces
│   └── IResourceProvider.cs     # Resource provider pattern
├── Models/
│   ├── ErrorModels.cs           # Error handling models
│   └── ToolResultBase.cs        # Base result class
└── Enums/
    └── ToolCategory.cs          # Tool categorization
```

### Key Components

- **McpToolBase<TParams, TResult>**: Generic base class for type-safe tool implementation
- **McpServerBuilder**: Fluent API for server configuration
- **McpToolRegistry**: Manages tool registration and discovery
- **ToolResultBase**: Standard result format with error handling
- **IResourceProvider**: Interface for custom resource providers

## 🏆 Real-World Examples

The framework powers production MCP servers:
- **CodeSearch MCP**: File and text searching with Lucene indexing
- **CodeNav MCP**: C# code navigation using Roslyn
- **SimpleMcpServer**: Example project with calculator, data store, and system info tools

## 📈 Performance

| Metric | Target | Actual |
|--------|--------|--------|
| Build Time | <3s | 2.46s |
| Test Suite | 100% pass | 562/562 ✓ |
| Warnings | 0 | 0 ✓ |
| Framework Overhead | <5% | ~3% |

## 🤝 Contributing

We welcome contributions! Key areas:
- Additional tool examples
- Performance optimizations
- Documentation improvements
- Test coverage expansion

## 📄 License

MIT License - see [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

Built on experience from:
- COA CodeSearch MCP - Token optimization patterns
- COA CodeNav MCP - Roslyn integration patterns
- The MCP community - Feedback and ideas

---

## 📖 Documentation

For comprehensive documentation, guides, and examples, see the **[Documentation Hub](docs/README.md)**.

**Ready to build your MCP server?** Clone the repo and check out the examples:

```bash
git clone https://github.com/anortham/COA-Mcp-Framework.git
cd COA-Mcp-Framework/examples/SimpleMcpServer
dotnet run
```

For detailed guidance, see [CLAUDE.md](CLAUDE.md) for AI-assisted development tips.
