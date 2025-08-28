# Basic Tools MCP Server

Shows how to create **multiple tools with validation** in a single MCP server.

## What This Adds

Building on the Hello World example, this shows:

- **Multiple tools** in one server
- **Parameter validation** with helpful error messages
- **Optional parameters** with sensible defaults
- **Error handling** for edge cases
- **Range validation** for numeric inputs

## The Tools

### 🧮 Calculator Tool
Performs basic arithmetic operations.

**Usage:**
```json
{
  "operation": "add",
  "a": 5,
  "b": 3
}
```

**Operations:** add, subtract, multiply, divide

### 📝 Text Tool  
Text processing operations.

**Usage:**
```json
{
  "text": "Hello World",
  "operation": "uppercase"
}
```

**Operations:** uppercase, lowercase, reverse, length, words

### 🎲 Random Tool
Generates random numbers.

**Usage:**
```json
{
  "min": 1,
  "max": 100,
  "count": 5
}
```

All parameters are optional with defaults.

## Quick Start

1. **Run the server:**
   ```bash
   dotnet run
   ```

2. **Test the tools** (if using HTTP transport):
   ```bash
   # Calculator
   curl -X POST http://localhost:5000/tools/calculator \
     -H "Content-Type: application/json" \
     -d '{"operation": "add", "a": 5, "b": 3}'

   # Text processing
   curl -X POST http://localhost:5000/tools/text \
     -H "Content-Type: application/json" \
     -d '{"text": "Hello World", "operation": "uppercase"}'

   # Random numbers
   curl -X POST http://localhost:5000/tools/random \
     -H "Content-Type: application/json" \
     -d '{"min": 1, "max": 10, "count": 3}'
   ```

## Key Concepts Demonstrated

### Validation Helpers
```csharp
// Check required parameters
ValidateRequired(parameters.Operation, nameof(parameters.Operation));

// Check numeric ranges  
ValidateRange(count, 1, 1000, nameof(parameters.Count));
```

### Optional Parameters
```csharp
// Use nullable types with defaults
public int? Count { get; set; } // Optional

// Handle in code
var count = parameters.Count ?? 1; // Default to 1
```

### Error Handling
```csharp
// Validation errors are handled automatically
// Business logic errors use exceptions
if (b == 0) 
    throw new InvalidOperationException("Division by zero");
```

### Multiple Result Types
Each tool can have its own result structure while inheriting from `ToolResultBase`.

## Next Steps

- **Add more tools** - Copy the pattern for new functionality
- **Add HTTP transport** - See `WHICH_TRANSPORT.md` guide
- **Add services** - Check `../3-WithServices/` for dependency injection
- **Go production** - See `../SimpleMcpServer/` for full features

## Troubleshooting

**"Parameter validation failing"?**
- Make sure to use nullable types (`int?` not `int`) for optional parameters
- Check that required parameters are actually being sent

**"Tool not found"?**  
- Verify tool names match exactly (case sensitive)
- Make sure you registered all tools with `builder.RegisterToolType<>()`

**Need help?** See `../docs/COMMON_PITFALLS.md` for solutions to common issues.