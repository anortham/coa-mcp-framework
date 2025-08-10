# COA.Mcp.Protocol

Low-level protocol types and JSON-RPC implementation for the Model Context Protocol (MCP). This package provides the foundational protocol definitions, message types, and communication primitives used by the MCP framework.

## Overview

This package implements the MCP specification, providing:
- JSON-RPC 2.0 message handling
- MCP protocol types and structures
- Request/response/notification definitions
- Error codes and exceptions
- Capability markers for feature negotiation

## Installation

```xml
<PackageReference Include="COA.Mcp.Protocol" Version="1.4.0" />
```

Most users should use `COA.Mcp.Framework` instead, which includes this package and provides higher-level abstractions.

## Core Components

### JSON-RPC Messages

The protocol uses JSON-RPC 2.0 for communication. All messages inherit from these base types:

```csharp
using COA.Mcp.Protocol;

// Request message
var request = new JsonRpcRequest
{
    JsonRpc = "2.0",
    Id = "123",
    Method = "tools/call",
    Params = new ToolCallParams { /* ... */ }
};

// Response message
var response = new JsonRpcResponse
{
    JsonRpc = "2.0",
    Id = "123",
    Result = new ToolCallResult { /* ... */ }
};

// Error response
var errorResponse = new JsonRpcResponse
{
    JsonRpc = "2.0",
    Id = "123",
    Error = new JsonRpcError
    {
        Code = ErrorCode.MethodNotFound,
        Message = "Unknown method: invalid/method"
    }
};

// Notification (no ID, no response expected)
var notification = new JsonRpcNotification
{
    JsonRpc = "2.0",
    Method = "notifications/progress",
    Params = new ProgressParams { /* ... */ }
};
```

### MCP Protocol Types

#### Initialize Request/Response

Establishes connection and negotiates capabilities:

```csharp
// Client sends initialization
var initRequest = new InitializeRequest
{
    ProtocolVersion = "1.0",
    ClientInfo = new ClientInfo
    {
        Name = "my-client",
        Version = "1.0.0"
    },
    Capabilities = new ClientCapabilities
    {
        Tools = true,
        Prompts = true,
        Resources = true,
        Logging = true
    }
};

// Server responds with its capabilities
var initResponse = new InitializeResponse
{
    ProtocolVersion = "1.0",
    ServerInfo = new ServerInfo
    {
        Name = "my-server",
        Version = "1.0.0"
    },
    Capabilities = new ServerCapabilities
    {
        Tools = new ToolsCapability { SupportsProgress = true },
        Prompts = new PromptsCapability { SupportsStreaming = false },
        Resources = new ResourcesCapability { SupportsSubscriptions = true }
    }
};
```

#### Tool Definitions

Define available tools and their schemas:

```csharp
// Tool descriptor
var tool = new Tool
{
    Name = "calculate",
    Description = "Performs calculations",
    InputSchema = new JsonSchema
    {
        Type = "object",
        Properties = new Dictionary<string, JsonSchema>
        {
            ["operation"] = new JsonSchema
            {
                Type = "string",
                Enum = new[] { "add", "subtract", "multiply", "divide" }
            },
            ["a"] = new JsonSchema { Type = "number" },
            ["b"] = new JsonSchema { Type = "number" }
        },
        Required = new[] { "operation", "a", "b" }
    }
};

// List tools response
var toolsList = new ListToolsResult
{
    Tools = new[] { tool }
};
```

#### Tool Execution

Execute tools with parameters:

```csharp
// Tool call request
var callRequest = new CallToolRequest
{
    Name = "calculate",
    Arguments = new
    {
        operation = "multiply",
        a = 5,
        b = 3
    }
};

// Tool call result
var callResult = new CallToolResult
{
    Content = new[]
    {
        new TextContent
        {
            Type = "text",
            Text = "5 × 3 = 15"
        }
    },
    IsError = false
};
```

#### Prompts

Interactive prompt definitions:

```csharp
// Prompt descriptor
var prompt = new Prompt
{
    Name = "code_review",
    Description = "Reviews code for quality",
    Arguments = new[]
    {
        new PromptArgument
        {
            Name = "language",
            Description = "Programming language",
            Required = true
        }
    }
};

// Get prompt result
var promptResult = new GetPromptResult
{
    Messages = new[]
    {
        new PromptMessage
        {
            Role = "system",
            Content = new TextContent
            {
                Type = "text",
                Text = "You are a code reviewer"
            }
        }
    }
};
```

#### Resources

Data resources exposed by the server:

```csharp
// Resource descriptor
var resource = new Resource
{
    Uri = "file:///docs/readme.md",
    Name = "README",
    Description = "Project documentation",
    MimeType = "text/markdown"
};

// Resource content
var content = new ResourceContent
{
    Uri = "file:///docs/readme.md",
    MimeType = "text/markdown",
    Text = "# Project Documentation\n..."
};

// List resources
var resourcesList = new ListResourcesResult
{
    Resources = new[] { resource }
};
```

### Error Handling

Standard error codes and exceptions:

```csharp
// Standard error codes (from JsonRpcErrorCodes class)
public static class JsonRpcErrorCodes
{
    // JSON-RPC 2.0 Standard Error Codes
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;
    
    // Server Error Range (-32000 to -32099)
    public const int ServerError = -32000;
    public const int ResourceNotFound = -32001;
    public const int ResourceAccessDenied = -32002;
    public const int OperationTimeout = -32003;
    public const int ServiceUnavailable = -32004;
    public const int OperationCancelled = -32005;
    
    // MCP-Specific Error Codes (-32100 to -32199)
    public const int ToolNotFound = -32100;
    public const int ToolExecutionError = -32101;
    public const int WorkspaceNotFound = -32102;
    public const int IndexNotAvailable = -32103;
    public const int MemoryOperationFailed = -32104;
}

// MCP-specific exceptions
public class McpException : Exception
{
    public int Code { get; set; }
    public object Data { get; set; }
    
    public McpException(int code, string message, object data = null)
        : base(message)
    {
        Code = code;
        Data = data;
    }
}

// Usage
throw new McpException(
    JsonRpcErrorCodes.InvalidParams,
    "Missing required parameter: location",
    new { parameter = "location", type = "string" }
);
```

### Capability Markers

Negotiate features between client and server:

```csharp
// Check capabilities
public class CapabilityChecker
{
    private readonly ServerCapabilities _capabilities;
    
    public bool SupportsTools => _capabilities.Tools != null;
    public bool SupportsPrompts => _capabilities.Prompts != null;
    public bool SupportsResources => _capabilities.Resources != null;
    
    public bool SupportsToolProgress =>
        _capabilities.Tools?.SupportsProgress ?? false;
        
    public bool SupportsResourceSubscriptions =>
        _capabilities.Resources?.SupportsSubscriptions ?? false;
}
```

## Message Flow

### Typical Session

```csharp
// 1. Client connects and initializes
Client -> Server: InitializeRequest
Server -> Client: InitializeResponse

// 2. Client discovers available tools
Client -> Server: ListToolsRequest
Server -> Client: ListToolsResult

// 3. Client calls a tool
Client -> Server: CallToolRequest
Server -> Client: CallToolResult

// 4. Server sends progress notifications (optional)
Server -> Client: ProgressNotification

// 5. Client disconnects
Client -> Server: Disconnect notification
```

### Request/Response Correlation

Match responses to requests using IDs:

```csharp
public class MessageCorrelator
{
    private readonly Dictionary<string, TaskCompletionSource<JsonRpcResponse>> _pending;
    
    public async Task<T> SendRequestAsync<T>(JsonRpcRequest request)
    {
        var tcs = new TaskCompletionSource<JsonRpcResponse>();
        _pending[request.Id] = tcs;
        
        // Send request
        await SendMessageAsync(request);
        
        // Wait for response
        var response = await tcs.Task;
        
        if (response.Error != null)
            throw new McpException(response.Error.Code, response.Error.Message);
            
        return (T)response.Result;
    }
    
    public void HandleResponse(JsonRpcResponse response)
    {
        if (_pending.TryGetValue(response.Id, out var tcs))
        {
            tcs.SetResult(response);
            _pending.Remove(response.Id);
        }
    }
}
```

## Content Types

MCP supports multiple content types in responses:

```csharp
// Text content
var textContent = new TextContent
{
    Type = "text",
    Text = "Plain text response"
};

// Image content
var imageContent = new ImageContent
{
    Type = "image",
    Data = Convert.ToBase64String(imageBytes),
    MimeType = "image/png"
};

// Resource reference
var resourceContent = new ResourceContent
{
    Type = "resource",
    Uri = "resource://data/report.pdf"
};

// Mixed content response
var result = new CallToolResult
{
    Content = new IContent[]
    {
        textContent,
        imageContent,
        resourceContent
    }
};
```

## Serialization

The protocol uses System.Text.Json with specific settings:

```csharp
public static class ProtocolSerializer
{
    public static JsonSerializerOptions Options => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(),
            new ContentConverter(),  // Handles polymorphic content
            new JsonSchemaConverter() // Handles schema types
        }
    };
    
    public static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, Options);
    }
    
    public static T Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, Options);
    }
}
```

## Protocol Extensions

Custom protocol extensions for additional functionality:

```csharp
// Define custom capability
public class CustomCapability
{
    public bool SupportsFeatureX { get; set; }
    public int MaxBatchSize { get; set; }
}

// Extend server capabilities
public class ExtendedServerCapabilities : ServerCapabilities
{
    public CustomCapability Custom { get; set; }
}

// Use in initialization
var initResponse = new InitializeResponse
{
    Capabilities = new ExtendedServerCapabilities
    {
        Tools = new ToolsCapability(),
        Custom = new CustomCapability
        {
            SupportsFeatureX = true,
            MaxBatchSize = 100
        }
    }
};
```

## Best Practices

1. **Always validate protocol version** during initialization
2. **Check capabilities** before using features
3. **Include request IDs** for proper correlation
4. **Handle all error codes** appropriately
5. **Respect protocol limits** (message size, etc.)
6. **Use notifications** for one-way messages
7. **Implement proper cleanup** on disconnect

## Common Patterns

### Batch Operations

```csharp
// Send multiple requests
var batchRequest = new[]
{
    new JsonRpcRequest { Id = "1", Method = "tools/list" },
    new JsonRpcRequest { Id = "2", Method = "prompts/list" },
    new JsonRpcRequest { Id = "3", Method = "resources/list" }
};

// Process responses
var responses = await Task.WhenAll(
    batchRequest.Select(req => SendRequestAsync(req))
);
```

### Progress Reporting

```csharp
// Send progress notifications during long operations
public async Task LongRunningOperation(IProgress<int> progress)
{
    for (int i = 0; i <= 100; i += 10)
    {
        await Task.Delay(100);
        
        var notification = new ProgressNotification
        {
            Token = "operation-123",
            Progress = i,
            Total = 100,
            Message = $"Processing... {i}%"
        };
        
        await SendNotificationAsync(notification);
        progress?.Report(i);
    }
}
```

## Debugging

Enable protocol logging for debugging:

```csharp
public class ProtocolLogger
{
    private readonly ILogger _logger;
    
    public void LogMessage(object message, bool isOutgoing)
    {
        var direction = isOutgoing ? "→" : "←";
        var json = ProtocolSerializer.Serialize(message);
        
        _logger.LogDebug($"{direction} {json}");
    }
}
```

## Compliance

This implementation follows:
- [MCP Specification v1.0](https://spec.modelcontextprotocol.io)
- [JSON-RPC 2.0 Specification](https://www.jsonrpc.org/specification)

## See Also

- `COA.Mcp.Framework` - High-level framework for building MCP servers
- `COA.Mcp.Client` - Client library for consuming MCP servers
- `COA.Mcp.Framework.Transport` - Transport implementations

## License

Part of the COA MCP Framework. See LICENSE for details.