# Simple MCP Server Example

This example demonstrates how to create a simple MCP (Model Context Protocol) server using the COA MCP Framework.

## Features

This example server includes five tools:

1. **Calculator Tool** - Performs basic arithmetic operations (add, subtract, multiply, divide)
2. **String Manipulation Tool** - Various string operations (reverse, capitalize, extract emails, etc.)
3. **Data Store Tool** - In-memory key-value storage with CRUD operations
4. **System Info Tool** - Retrieves system and runtime information
5. **Lifecycle Example Tool** - Demonstrates middleware and lifecycle hooks (NEW)

## Running the Example

### Prerequisites

- .NET 9.0 SDK
- COA MCP Framework (referenced as project)

### Build and Run

```bash
# Navigate to the example directory
cd examples/SimpleMcpServer

# Build the project
dotnet build

# Run the server
dotnet run
```

The server will start and listen for MCP protocol connections on stdin/stdout.

## Testing with Claude Desktop

To use this server with Claude Desktop, add it to your Claude configuration:

```json
{
  "mcpServers": {
    "simple-example": {
      "command": "dotnet",
      "args": ["run", "--project", "C:\\source\\COA MCP Framework\\examples\\SimpleMcpServer"],
      "env": {}
    }
  }
}
```

## Tool Examples

### Calculator Tool

```json
{
  "tool": "calculator",
  "parameters": {
    "operation": "add",
    "a": 10,
    "b": 5
  }
}
```

Response:
```json
{
  "success": true,
  "result": 15,
  "expression": "10 + 5",
  "calculation": "10 + 5 = 15"
}
```

### String Manipulation Tool

```json
{
  "tool": "string_manipulation",
  "parameters": {
    "text": "Hello World",
    "operation": "reverse"
  }
}
```

Response:
```json
{
  "success": true,
  "original": "Hello World",
  "result": "dlroW olleH",
  "operation": "reverse"
}
```

### Data Store Tool

Store a value:
```json
{
  "tool": "data_store",
  "parameters": {
    "operation": "set",
    "key": "username",
    "value": "john_doe"
  }
}
```

Retrieve a value:
```json
{
  "tool": "data_store",
  "parameters": {
    "operation": "get",
    "key": "username"
  }
}
```

### System Info Tool

```json
{
  "tool": "system_info",
  "parameters": {
    "includeEnvironmentVariables": false,
    "includeDrives": true
  }
}
```

### Lifecycle Example Tool (NEW)

Test middleware functionality:
```json
{
  "tool": "lifecycle_example",
  "parameters": {
    "text": "Hello Framework",
    "operation": "uppercase",
    "processingDelayMs": 500
  }
}
```

Response:
```json
{
  "success": true,
  "originalText": "Hello Framework", 
  "processedText": "HELLO FRAMEWORK",
  "operation": "uppercase",
  "processingTimeMs": 500,
  "timestamp": "2025-01-12T10:30:00Z"
}
```

This tool demonstrates:
- **Token counting middleware** - Estimates and logs token usage
- **Custom timing middleware** - Categorizes performance and logs execution details  
- **Error handling** - Middleware error hooks in action
- **Order-based execution** - Multiple middleware working together

## Architecture

The example demonstrates key framework concepts:

1. **Tool Implementation** - Each tool extends `McpToolBase<TParams, TResult>` for type safety
2. **Dependency Injection** - Tools can receive services through constructor injection
3. **Input Validation** - Using the framework's validation helpers
4. **Schema Generation** - Automatic JSON schema generation from parameter types
5. **Async Operations** - All tools support cancellation tokens
6. **Logging** - Integrated logging through Microsoft.Extensions.Logging

## Extending the Example

To add a new tool:

1. Create a new class extending `McpToolBase<TParams, TResult>`
2. Define parameter and result classes
3. Implement the `ExecuteInternalAsync` method
4. Register the tool in Program.cs using `builder.RegisterToolType<YourTool>()`

## Framework Features Demonstrated

- **Type-safe tools** with compile-time checking
- **Automatic tool discovery** and registration
- **Built-in validation** helpers
- **Logging integration**
- **Dependency injection** support
- **Fluent configuration** API
- **Async/await** pattern throughout
- **Cancellation token** support
- **Lifecycle hooks & middleware** - Cross-cutting concerns like logging and token counting (NEW)

For detailed information on middleware and lifecycle hooks, see the [Lifecycle Hooks Guide](../../docs/lifecycle-hooks.md).

## License

This example is part of the COA MCP Framework and follows the same license terms.