# McpServerTemplate

A Model Context Protocol (MCP) server built with the COA MCP Framework.

## Features

- Built on COA MCP Framework for rapid development
- Automatic token optimization and management
- AI-optimized response formatting
- Built-in insights and action suggestions
- Docker support included

## Getting Started

### Prerequisites

- .NET 8.0 or later
- Docker (optional, for containerized deployment)

### Running Locally

```bash
dotnet run
```

### Running with Docker

```bash
docker build -t mcpservertemplate .
docker run -p 5000:5000 mcpservertemplate
```

## Tool Registration Patterns

The COA MCP Framework supports three patterns for registering tools:

### Pattern 1: McpToolBase + Manual Registration (RECOMMENDED)
```csharp
public class MyTool : McpToolBase<MyParams, MyResult>
{
    public override string Name => "my_tool";
    public override string Description => "Does something useful";
    
    protected override async Task<MyResult> ExecuteInternalAsync(
        MyParams parameters, CancellationToken cancellationToken)
    {
        // Implementation
    }
}

// Register in Program.cs:
builder.RegisterToolType<MyTool>();
```

### Pattern 2: Interface Implementation + Auto-Discovery
```csharp
public class MyTool : IMcpTool<MyParams, MyResult>
{
    // Interface implementation
}

// Auto-discover in Program.cs:
builder.DiscoverTools(typeof(Program).Assembly);
```

### Pattern 3: Attribute-Based (Legacy)
```csharp
[McpServerToolType]
public class MyToolService
{
    [McpServerTool("my_tool")]
    public async Task<object> ExecuteAsync(MyParams parameters)
    {
        // Implementation
    }
}

// Auto-discover in Program.cs:
builder.DiscoverTools(typeof(Program).Assembly);
```

## Example Tools Included

### hello_world
Simple greeting tool that demonstrates basic MCP tool structure.
- **Parameters:**
  - `name` (string, optional): Name of the person to greet
  - `includeTime` (bool, optional): Include current UTC time in greeting

### get_system_info
Gets system information including OS, runtime, and environment details.
- **Parameters:**
  - `includeEnvironment` (bool, optional): Include environment variables
  - `maxTokens` (int, optional): Maximum tokens for response

## Configuration

Edit `appsettings.json` to configure:

- Logging levels
- Token optimization settings
- Response building options
- Caching behavior

## Adding New Tools

1. Create a new class inheriting from `McpToolBase`
2. Add the `[McpServerToolType]` attribute to the class
3. Add the `[McpServerTool]` attribute to your execute method
4. The tool will be automatically discovered at startup

Example:

```csharp
[McpServerToolType]
public class MyTool : McpToolBase
{
    public override string ToolName => "my_tool";
    public override ToolCategory Category => ToolCategory.Query;

    [McpServerTool(Name = "my_tool")]
    [Description("My tool description")]
    public async Task<object> ExecuteAsync(MyToolParams parameters)
    {
        // Tool implementation
    }
}
```

## Testing

```bash
dotnet test
```

## Contributing

Please read our contributing guidelines before submitting pull requests.

## License

This project is licensed under the MIT License.