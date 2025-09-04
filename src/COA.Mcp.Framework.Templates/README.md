# COA.Mcp.Framework.Templates

.NET project templates for quickly creating MCP servers with best practices, proper structure, and all necessary configurations. Get started with a production-ready MCP server in seconds.

## Features

- **Ready-to-run MCP server template** with example tools
- **Structured project layout** following best practices
- **Pre-configured dependencies** and settings
- **Docker support** included
- **Unit test project** with examples
- **GitHub Actions** workflow ready
- **Comprehensive examples** of tools, prompts, and resources

## Installation

Install the template package globally:

```bash
# Install templates
dotnet new install COA.Mcp.Framework.Templates

# View installed templates
dotnet new list mcp

# Update templates
dotnet new update COA.Mcp.Framework.Templates

# Uninstall templates
dotnet new uninstall COA.Mcp.Framework.Templates
```

## Quick Start

### Create a New MCP Server

```bash
# Create a new MCP server project
dotnet new mcp-server -n MyMcpServer

# Navigate to the project
cd MyMcpServer

# Run the server
dotnet run

# Or run with stdio transport (for Claude Desktop)
dotnet run -- --transport stdio
```

### Template Options

```bash
# Full syntax
dotnet new mcp-server -n MyServer [options]

# Available options:
--framework net9.0|net8.0     # Target framework (default: net9.0)
--enable-docker true|false    # Include Docker support (default: true)
--enable-tests true|false     # Include test project (default: true)
--enable-github true|false    # Include GitHub Actions (default: true)
--transport stdio|http|ws     # Default transport (default: stdio)
--include-examples true|false # Include example tools (default: true)
```

### Examples

```bash
# Minimal server without examples
dotnet new mcp-server -n MinimalServer --include-examples false

# HTTP API server
dotnet new mcp-server -n ApiServer --transport http

# Server with all features
dotnet new mcp-server -n FullServer \
  --enable-docker true \
  --enable-tests true \
  --enable-github true \
  --include-examples true
```

## Generated Project Structure

```
MyMcpServer/
├── MyMcpServer.csproj          # Main project file
├── Program.cs                  # Entry point with DI setup
├── appsettings.json           # Configuration
├── Dockerfile                 # Docker container definition
├── .dockerignore             # Docker ignore rules
├── README.md                 # Project documentation
│
├── Tools/                    # MCP tools
│   ├── HelloWorldTool.cs    # Example simple tool
│   └── SystemInfoTool.cs    # Example complex tool
│
├── Prompts/                  # MCP prompts (if enabled)
│   └── CodeReviewPrompt.cs  # Example prompt
│
├── Resources/                # MCP resources (if enabled)
│   └── DocumentResource.cs  # Example resource provider
│
├── Services/                 # Background services
│   └── HealthCheckService.cs # Example hosted service
│
├── Models/                   # Data models
│   ├── Parameters/          # Tool parameter models
│   └── Results/             # Tool result models
│
├── Configuration/            # Configuration classes
│   └── ServerOptions.cs     # Server configuration
│
├── .github/                  # GitHub Actions (if enabled)
│   └── workflows/
│       ├── build.yml        # Build and test workflow
│       └── publish.yml      # NuGet publish workflow
│
└── MyMcpServer.Tests/        # Test project (if enabled)
    ├── MyMcpServer.Tests.csproj
    ├── Tools/
    │   ├── HelloWorldToolTests.cs
    │   └── SystemInfoToolTests.cs
    └── TestBase.cs           # Base test class
```

## Generated Code Examples

### Program.cs
```csharp
using COA.Mcp.Framework.Server;
using COA.Mcp.Framework.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyMcpServer.Tools;

var builder = Host.CreateApplicationBuilder(args);

// Configure MCP framework
builder.Services.AddMcpFramework(options =>
{
    options.ServerInfo = new ServerInfo
    {
        Name = "my-mcp-server",
        Version = "1.0.0",
        Description = "My MCP Server"
    };
});

// Register tools
builder.Services.AddSingleton<IMcpTool, HelloWorldTool>();
builder.Services.AddSingleton<IMcpTool, SystemInfoTool>();

// Configure transport based on command line or config
var transport = args.FirstOrDefault(a => a.StartsWith("--transport"))
    ?.Split('=').LastOrDefault() ?? "stdio";

builder.Services.AddSingleton<IMcpTransport>(provider =>
{
    return transport.ToLower() switch
    {
        "http" => new HttpTransport(provider.GetRequiredService<ILogger<HttpTransport>>()),
        "ws" => new WebSocketTransport(provider.GetRequiredService<ILogger<WebSocketTransport>>()),
        _ => new StdioTransport(provider.GetRequiredService<ILogger<StdioTransport>>())
    };
});

var host = builder.Build();

// Start MCP server
var server = host.Services.GetRequiredService<McpServer>();
await server.StartAsync();

await host.RunAsync();
```

### Example Tool
```csharp
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;

namespace MyMcpServer.Tools;

public class HelloWorldParams
{
    [Required]
    [Description("Name to greet")]
    public string Name { get; set; }
    
    [Description("Language for greeting")]
    [AllowedValues("en", "es", "fr", "de", "ja")]
    public string Language { get; set; } = "en";
}

public class HelloWorldResult : ToolResultBase
{
    public override string Operation => "greet";
    public string Greeting { get; set; }
    public string Language { get; set; }
}

public class HelloWorldTool : McpToolBase<HelloWorldParams, HelloWorldResult>
{
    public override string Name => "hello_world";
    public override string Description => "Greets a user in different languages";
    
    private static readonly Dictionary<string, string> Greetings = new()
    {
        ["en"] = "Hello",
        ["es"] = "Hola",
        ["fr"] = "Bonjour",
        ["de"] = "Hallo",
        ["ja"] = "こんにちは"
    };
    
    protected override async Task<HelloWorldResult> ExecuteInternalAsync(
        HelloWorldParams parameters,
        CancellationToken cancellationToken)
    {
        var greeting = Greetings[parameters.Language];
        
        return new HelloWorldResult
        {
            Greeting = $"{greeting}, {parameters.Name}!",
            Language = parameters.Language
        };
    }
}
```

### Configuration (appsettings.json)
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "COA.Mcp": "Debug"
    }
  },
  "Mcp": {
    "Server": {
      "Name": "my-mcp-server",
      "Version": "1.0.0",
      "MaxConcurrentRequests": 10
    },
    "Transport": {
      "Type": "Stdio",
      "Http": {
        "Port": 3000,
        "BasePath": "/mcp"
      },
      "WebSocket": {
        "Port": 3001,
        "Path": "/ws"
      }
    }
  }
}
```

### Dockerfile
```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["MyMcpServer.csproj", "."]
RUN dotnet restore "MyMcpServer.csproj"
COPY . .
RUN dotnet build "MyMcpServer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MyMcpServer.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Expose ports for HTTP/WebSocket transports
EXPOSE 3000 3001

ENTRYPOINT ["dotnet", "MyMcpServer.dll"]
```

## Customization

### Adding New Tools

1. Create a new tool class in the `Tools/` folder:

```csharp
public class MyCustomTool : McpToolBase<MyParams, MyResult>
{
    public override string Name => "my_custom_tool";
    public override string Description => "Does something custom";
    
    protected override async Task<MyResult> ExecuteInternalAsync(
        MyParams parameters,
        CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

2. Register in `Program.cs`:

```csharp
builder.Services.AddSingleton<IMcpTool, MyCustomTool>();
```

### Adding Prompts

```csharp
public class MyPrompt : PromptBase
{
    public override string Name => "my_prompt";
    public override string Description => "Custom prompt";
    
    public override Task<PromptMessage[]> GetMessagesAsync(
        Dictionary<string, string> arguments,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new[]
        {
            CreateSystemMessage("You are a helpful assistant."),
            CreateUserMessage("Help with: {{task}}")
        });
    }
}
```

### Adding Resources

```csharp
public class MyResourceProvider : IResourceProvider
{
    public string Name => "my_resources";
    
    public async Task<Resource[]> ListResourcesAsync(
        CancellationToken cancellationToken)
    {
        // Return available resources
    }
    
    public async Task<ResourceContent> GetResourceAsync(
        string uri,
        CancellationToken cancellationToken)
    {
        // Return resource content
    }
}
```

## Integration with Claude Desktop

### Configuration for Claude Desktop

Add to Claude Desktop config (`claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "my-server": {
      "command": "dotnet",
      "args": ["run", "--project", "C:/path/to/MyMcpServer"],
      "env": {
        "DOTNET_ENVIRONMENT": "Production"
      }
    }
  }
}
```

### Or using the built executable:

```json
{
  "mcpServers": {
    "my-server": {
      "command": "C:/path/to/MyMcpServer.exe",
      "args": ["--transport", "stdio"]
    }
  }
}
```

## Running and Testing

### Development

```bash
# Run with hot reload
dotnet watch run

# Run with specific transport
dotnet run -- --transport http

# Run with verbose logging
dotnet run -- --log-level Debug
```

### Testing

```bash
# Run tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test
dotnet test --filter "FullyQualifiedName~HelloWorldToolTests"
```

### Building

```bash
# Build for current platform
dotnet build -c Release

# Publish self-contained executable
dotnet publish -c Release -r win-x64 --self-contained

# Build Docker image
docker build -t my-mcp-server .

# Run Docker container
docker run -p 3000:3000 my-mcp-server --transport http
```

## Advanced Templates

### Custom Template Creation

Create your own template based on this one:

1. Copy the template project
2. Modify as needed
3. Add `.template.config/template.json`
4. Package as NuGet

Example `template.json`:
```json
{
  "$schema": "http://json.schemastore.org/template",
  "author": "Your Name",
  "classifications": ["MCP", "Web", "Service"],
  "identity": "YourCompany.MCP.Templates",
  "name": "Custom MCP Server",
  "shortName": "custom-mcp",
  "tags": {
    "language": "C#",
    "type": "project"
  },
  "symbols": {
    "enableFeatureX": {
      "type": "parameter",
      "datatype": "bool",
      "defaultValue": "true",
      "description": "Enable Feature X"
    }
  }
}
```

## Best Practices

1. **Keep tools focused** - One responsibility per tool
2. **Use parameter validation** - Leverage built-in attributes
3. **Return meaningful errors** - Include recovery steps
4. **Log appropriately** - Use structured logging
5. **Test thoroughly** - Unit and integration tests
6. **Document tools** - Clear descriptions and parameter docs
7. **Version properly** - Follow semantic versioning

## Troubleshooting

### Template Not Found
```bash
# Reinstall templates
dotnet new uninstall COA.Mcp.Framework.Templates
dotnet new install COA.Mcp.Framework.Templates
```

### Build Errors
```bash
# Clear NuGet cache
dotnet nuget locals all --clear

# Restore packages
dotnet restore
```

### Runtime Issues
- Check logs in console output
- Verify transport configuration
- Ensure all dependencies are installed

## Updates and Versioning

The template follows the framework version:
- Template 1.4.0 → Framework 1.4.0
- Always use matching versions

Update to latest:
```bash
dotnet new update COA.Mcp.Framework.Templates
```

## Contributing

To contribute templates:
1. Fork the repository
2. Create new template in `/templates/`
3. Add tests
4. Submit pull request

## License

Part of the COA MCP Framework. See LICENSE for details.