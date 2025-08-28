# Copy-Paste Tool Templates

Ready-to-use templates for common MCP tool patterns. Just copy, rename, and customize.

## How to Use

1. Copy the template file
2. Replace `[TOOL_NAME]` with your tool name (PascalCase)
3. Replace `[tool_name]` with your tool name (lowercase)  
4. Replace `[DESCRIPTION]` with what your tool does
5. Fill in the TODO sections with your logic
6. Register your tool in Program.cs

## Available Templates

### 📁 [FileSystemTool.cs](FileSystemTool.cs)
Read and write files, list directories.
- File operations
- Path validation  
- Error handling for I/O

### 🗄️ [DatabaseTool.cs](DatabaseTool.cs)
Query databases with connection management.
- SQL queries
- Connection handling
- Parameter validation

### 🌐 [ApiClientTool.cs](ApiClientTool.cs)
Call external REST APIs.
- HTTP client
- JSON serialization
- Error handling

### 🔄 [DataTransformTool.cs](DataTransformTool.cs)
Transform data between formats.
- JSON/XML/CSV processing
- Data validation
- Format conversion

### 📊 [ReportTool.cs](ReportTool.cs)
Generate reports and summaries.
- Data aggregation
- Formatted output
- Export capabilities

## Quick Reference

### Basic Tool Structure
```csharp
public class [TOOL_NAME] : McpToolBase<[TOOL_NAME]Params, [TOOL_NAME]Result>
{
    public override string Name => "[tool_name]";
    public override string Description => "[DESCRIPTION]";
    
    protected override async Task<[TOOL_NAME]Result> ExecuteInternalAsync(
        [TOOL_NAME]Params parameters, CancellationToken cancellationToken)
    {
        // TODO: Your logic here
        return new [TOOL_NAME]Result { Success = true };
    }
}
```

### Parameter Class
```csharp
public class [TOOL_NAME]Params
{
    [Required]
    public string? RequiredField { get; set; }
    
    public string? OptionalField { get; set; }
}
```

### Result Class  
```csharp
public class [TOOL_NAME]Result : ToolResultBase
{
    public override string Operation => "[tool_name]";
    
    public string? Data { get; set; }
    public int Count { get; set; }
}
```

## Need Help?

- **Validation failing?** See [../COMMON_PITFALLS.md](../COMMON_PITFALLS.md#parameter-validation-is-failing)
- **JSON issues?** See [../COMMON_PITFALLS.md](../COMMON_PITFALLS.md#json-serialization-issues)  
- **Async problems?** See [../COMMON_PITFALLS.md](../COMMON_PITFALLS.md#asyncawait-confusion)

Happy coding! 🚀