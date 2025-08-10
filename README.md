# COA MCP Framework

A comprehensive .NET framework for building and consuming Model Context Protocol (MCP) servers with built-in token optimization, AI-friendly responses, strong typing, and developer-first design.

[![NuGet Version](https://img.shields.io/nuget/v/COA.Mcp.Framework)](https://www.nuget.org/packages/COA.Mcp.Framework)
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)](https://github.com/anortham/COA-Mcp-Framework)
[![Tests](https://img.shields.io/badge/tests-492%20passing-success)](https://github.com/anortham/COA-Mcp-Framework)
[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-blue)](https://dotnet.microsoft.com/download)

## 🚀 Quick Start

### Install the Framework

```xml
<!-- Add to your .csproj file -->
<PackageReference Include="COA.Mcp.Framework" Version="1.4.7" />
```

### Create Your First MCP Server

```csharp
using COA.Mcp.Framework.Server;
using COA.Mcp.Framework.Base;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// 1. Define your tool parameters
public class WeatherParameters
{
    [Required]
    [Description("City name or coordinates")]
    public string Location { get; set; }
    
    [Range(1, 10)]
    [Description("Number of forecast days (1-10)")]
    public int ForecastDays { get; set; } = 3;
}

// 2. Define your result type
public class WeatherResult : ToolResultBase
{
    public override string Operation => "get_weather";
    public string Location { get; set; }
    public double Temperature { get; set; }
    public string Condition { get; set; }
    public List<ForecastDay> Forecast { get; set; }
}

// 3. Implement your tool with full type safety
public class WeatherTool : McpToolBase<WeatherParameters, WeatherResult>
{
    private readonly IWeatherService _weatherService;
    
    public override string Name => "get_weather";
    public override string Description => "Get weather for a location";
    public override ToolCategory Category => ToolCategory.Query;
    
    public WeatherTool(IWeatherService weatherService)
    {
        _weatherService = weatherService;
    }
    
    protected override async Task<WeatherResult> ExecuteInternalAsync(
        WeatherParameters parameters,
        CancellationToken cancellationToken)
    {
        // Parameters are already validated!
        var weather = await _weatherService.GetWeatherAsync(
            parameters.Location, 
            parameters.ForecastDays);
        
        return new WeatherResult
        {
            Success = true,
            Location = parameters.Location,
            Temperature = weather.Current.Temperature,
            Condition = weather.Current.Condition,
            Forecast = weather.GetForecast(parameters.ForecastDays)
        };
    }
}

// 4. Create and run your server
// Program.cs
var builder = new McpServerBuilder()
    .WithServerInfo("Weather Server", "1.0.0")
    .ConfigureLogging(logging =>
    {
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
    });

// Register services
builder.Services.AddSingleton<IWeatherService, WeatherService>();

// Register tools
builder.RegisterToolType<WeatherTool>();

// Build and run
await builder.RunAsync();
```

Your MCP server is ready! 🎉

## 📦 NuGet Packages

| Package | Version | Description |
|---------|---------|-------------|
| **COA.Mcp.Framework** | 1.4.7 | Core framework with MCP protocol included |
| **COA.Mcp.Protocol** | 1.4.7 | Low-level protocol types and JSON-RPC |
| **COA.Mcp.Client** | 1.4.7 | Strongly-typed C# client for MCP servers |
| **COA.Mcp.Framework.TokenOptimization** | 1.4.7 | Advanced token management and AI response optimization |
| **COA.Mcp.Framework.Testing** | 1.4.7 | Testing helpers, assertions, and benchmarks |
| **COA.Mcp.Framework.Templates** | 1.4.7 | Project templates for quick starts |
| **COA.Mcp.Framework.Migration** | 1.4.7 | Migration tools for updating from older versions |

## ✨ Key Features

### 🔒 **Type-Safe Tool Development**
- Generic base class `McpToolBase<TParams, TResult>` ensures compile-time type safety
- Automatic parameter validation using data annotations
- No manual JSON parsing required

### 🏗️ **Clean Architecture**
- Single unified tool registry
- Fluent server builder API
- Dependency injection support
- Clear separation of concerns

### 🛡️ **Comprehensive Error Handling**
- Standardized error models with `ErrorInfo` and `RecoveryInfo`
- AI-friendly error messages with recovery steps
- Built-in validation helpers

### 🧠 **Token Optimization** (Optional)
- Pre-estimation to prevent context overflow
- Progressive reduction for large datasets
- Smart truncation with resource URIs

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

```csharp
// Implement custom resource providers for large data
public class SearchResultResourceProvider : IResourceProvider
{
    public string UriScheme => "search-results";
    
    public bool CanHandle(string uri) => 
        uri.StartsWith($"{UriScheme}://");
    
    public async Task<Resource> GetResourceAsync(string uri, CancellationToken ct)
    {
        var sessionId = ExtractSessionId(uri);
        var results = await LoadSearchResultsAsync(sessionId);
        
        return new Resource
        {
            Uri = uri,
            Name = $"Search Results {sessionId}",
            MimeType = "application/json",
            Contents = JsonSerializer.Serialize(results)
        };
    }
}

// Register in your server
builder.Services.AddSingleton<IResourceProvider, SearchResultResourceProvider>();
builder.Services.AddSingleton<IResourceRegistry, ResourceRegistry>();
```

### Token Optimization (with optional package)

```csharp
// Add the TokenOptimization package
// <PackageReference Include="COA.Mcp.Framework.TokenOptimization" Version="1.4.0" />

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
| Test Suite | 100% pass | 492/492 ✓ |
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