# COA MCP Client Library

A strongly-typed, feature-rich C# client library for interacting with MCP (Model Context Protocol) servers over HTTP and WebSocket.

## Features

- 🔒 **Type Safety**: Strongly-typed client with generic support for parameters and results
- 🔄 **Resilience**: Built-in retry logic, circuit breaker, and connection management
- 🌐 **Multiple Transports**: Support for both HTTP and WebSocket connections
- 🔑 **Authentication**: API Key, JWT, Basic, and custom authentication support
- 🎯 **Fluent API**: Intuitive builder pattern for configuration and tool invocation
- 📊 **Observability**: Request logging, metrics, and event notifications
- ⚡ **Performance**: Connection pooling, request batching, and optimized serialization
- 🛡️ **Error Handling**: Comprehensive error handling with detailed exceptions

## Installation

```bash
dotnet add package COA.Mcp.Client
```

## Quick Start

### Basic Usage

```csharp
using COA.Mcp.Client;

// Create and connect to an MCP server
var client = await McpClientBuilder
    .Create("http://localhost:5000")
    .BuildAndInitializeAsync();

// List available tools
var tools = await client.ListToolsAsync();
foreach (var tool in tools.Tools)
{
    Console.WriteLine($"{tool.Name}: {tool.Description}");
}

// Call a tool
var result = await client.CallToolAsync("weather", new { location = "Seattle" });
Console.WriteLine($"Result: {result.Content.First()}");
```

### Strongly-Typed Client

```csharp
// Define your parameter and result types
public class WeatherParams
{
    public string Location { get; set; }
    public string Units { get; set; } = "celsius";
}

public class WeatherResult : ToolResultBase
{
    public double Temperature { get; set; }
    public string Description { get; set; }
    public int Humidity { get; set; }
}

// Create a typed client
var typedClient = await McpClientBuilder
    .Create("http://localhost:5000")
    .BuildTypedAndInitializeAsync<WeatherParams, WeatherResult>();

// Call tool with type safety
var result = await typedClient.CallToolAsync("weather", new WeatherParams 
{ 
    Location = "Seattle",
    Units = "fahrenheit"
});

if (result.Success)
{
    Console.WriteLine($"Temperature: {result.Temperature}°F");
    Console.WriteLine($"Conditions: {result.Description}");
}
```

## Configuration

### Fluent Configuration

```csharp
var client = McpClientBuilder
    .Create("http://localhost:5000")
    .WithTimeout(TimeSpan.FromSeconds(60))
    .WithRetry(maxAttempts: 3, delayMs: 1000)
    .WithCircuitBreaker(failureThreshold: 5, durationSeconds: 30)
    .WithApiKey("your-api-key")
    .WithHeader("X-Custom-Header", "value")
    .WithRequestLogging(true)
    .WithMetrics(true)
    .Build();
```

### Manual Configuration

```csharp
var options = new McpClientOptions
{
    BaseUrl = "http://localhost:5000",
    TimeoutSeconds = 30,
    EnableRetries = true,
    MaxRetryAttempts = 3,
    RetryDelayMs = 1000,
    EnableCircuitBreaker = true,
    CircuitBreakerFailureThreshold = 5,
    Authentication = new AuthenticationOptions
    {
        Type = AuthenticationType.ApiKey,
        ApiKey = "your-api-key"
    }
};

var client = new McpHttpClient(options);
```

## Authentication

### API Key

```csharp
var client = McpClientBuilder
    .Create("http://localhost:5000")
    .WithApiKey("your-api-key", "X-API-Key")
    .Build();
```

### JWT Token

```csharp
var client = McpClientBuilder
    .Create("http://localhost:5000")
    .WithJwtToken(token, refreshFunc: async () => await GetNewToken())
    .Build();
```

### Basic Authentication

```csharp
var client = McpClientBuilder
    .Create("http://localhost:5000")
    .WithBasicAuth("username", "password")
    .Build();
```

### Custom Authentication

```csharp
var client = McpClientBuilder
    .Create("http://localhost:5000")
    .WithCustomAuth(async request =>
    {
        request.Headers.Add("X-Custom-Auth", await GetAuthToken());
    })
    .Build();
```

## Advanced Features

### Batch Operations

```csharp
var batchCalls = new Dictionary<string, WeatherParams>
{
    ["seattle"] = new WeatherParams { Location = "Seattle" },
    ["portland"] = new WeatherParams { Location = "Portland" },
    ["vancouver"] = new WeatherParams { Location = "Vancouver" }
};

var results = await typedClient.CallToolsBatchAsync(batchCalls);
foreach (var (key, result) in results)
{
    Console.WriteLine($"{key}: {result.Temperature}°");
}
```

### Event Handling

```csharp
client.Connected += (sender, e) =>
{
    Console.WriteLine($"Connected to {e.ServerUrl} at {e.ConnectedAt}");
};

client.Disconnected += (sender, e) =>
{
    Console.WriteLine($"Disconnected: {e.Reason}");
};

client.NotificationReceived += (sender, e) =>
{
    Console.WriteLine($"Notification: {e.Method}");
};
```

### Fluent Tool Invocation

```csharp
// Standard client
var result = await client
    .CallTool("weather")
    .WithParameters(new { location = "Seattle" })
    .WithCancellation(cancellationToken)
    .ExecuteAsync();

// Typed client with retry
var typedResult = await typedClient
    .CallTool("weather")
    .WithParameters(new WeatherParams { Location = "Seattle" })
    .WithRetry()
    .ExecuteAsync();
```

### WebSocket Support

```csharp
var client = McpClientBuilder
    .Create("ws://localhost:5000")
    .UseWebSocket("/mcp/ws")
    .Build();

// WebSocket connection provides real-time bidirectional communication
await client.ConnectAsync();
```

## Error Handling

```csharp
try
{
    var result = await client.CallToolAsync("tool_name", parameters);
}
catch (McpClientException ex)
{
    // General client errors
    Console.WriteLine($"Client error: {ex.Message}");
}
catch (JsonRpcException ex)
{
    // JSON-RPC protocol errors
    Console.WriteLine($"RPC error {ex.Code}: {ex.Message}");
    Console.WriteLine($"Error data: {ex.Data}");
}
catch (BrokenCircuitException ex)
{
    // Circuit breaker is open
    Console.WriteLine("Service temporarily unavailable");
}
```

## Logging

```csharp
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

var client = McpClientBuilder
    .Create("http://localhost:5000")
    .UseLoggerFactory(loggerFactory)
    .WithRequestLogging(true)
    .Build();
```

## Performance Considerations

1. **Connection Pooling**: Reuse `HttpClient` instances
2. **Circuit Breaker**: Prevents cascading failures
3. **Retry Logic**: Handles transient failures automatically
4. **Batch Operations**: Execute multiple tool calls in parallel
5. **Timeout Configuration**: Set appropriate timeouts for your use case

## Examples

See the [McpClientExample](../../examples/McpClientExample) project for comprehensive usage examples.

## API Reference

### IMcpClient

Core interface for MCP client operations:
- `ConnectAsync()` - Connect to the server
- `InitializeAsync()` - Initialize the MCP session
- `ListToolsAsync()` - List available tools
- `CallToolAsync()` - Call a tool by name
- `ListResourcesAsync()` - List available resources
- `ReadResourceAsync()` - Read a resource by URI
- `ListPromptsAsync()` - List available prompts
- `GetPromptAsync()` - Get a prompt by name

### ITypedMcpClient<TParams, TResult>

Strongly-typed client interface:
- `CallToolAsync(name, params)` - Type-safe tool invocation
- `CallToolWithRetryAsync()` - Automatic retry on failure
- `CallToolsBatchAsync()` - Parallel batch operations

### McpClientBuilder

Fluent builder for client configuration:
- `WithBaseUrl()` - Set server URL
- `UseWebSocket()` - Enable WebSocket transport
- `WithTimeout()` - Set request timeout
- `WithRetry()` - Configure retry policy
- `WithCircuitBreaker()` - Configure circuit breaker
- `WithApiKey()` - Set API key authentication
- `WithJwtToken()` - Set JWT authentication
- `Build()` - Create client instance
- `BuildTyped<TParams, TResult>()` - Create typed client

## Contributing

Contributions are welcome! Please see the main repository's contributing guidelines.

## License

This library is part of the COA MCP Framework and follows the same license terms.