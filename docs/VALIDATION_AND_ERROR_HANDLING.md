# Validation and Error Handling in COA MCP Framework

> **IMPORTANT**: There is no `ErrorHelpers` class in the framework. All validation helpers are **protected methods** in the `McpToolBase<TParams, TResult>` class.

## Overview

The COA MCP Framework provides comprehensive validation and error handling capabilities through the `McpToolBase` class. All tools that inherit from `McpToolBase` automatically get access to validation helpers, error result builders, and customizable error messages.

## Validation Helper Methods

All validation helpers are **protected methods** available in any class that inherits from `McpToolBase<TParams, TResult>`.

### Available Validation Helpers

#### `ValidateRequired<T>(T? value, string parameterName)`
Validates that a required parameter is not null or empty (for strings).

```csharp
public class MyTool : McpToolBase<MyParams, MyResult>
{
    protected override async Task<MyResult> ExecuteInternalAsync(MyParams parameters, CancellationToken cancellationToken)
    {
        // Throws ValidationException if null or empty
        var filePath = ValidateRequired(parameters.FilePath, nameof(parameters.FilePath));
        
        // Your tool logic here
    }
}
```

#### `ValidatePositive(int value, string parameterName)`
Validates that a numeric value is positive (> 0).

```csharp
protected override async Task<MyResult> ExecuteInternalAsync(MyParams parameters, CancellationToken cancellationToken)
{
    // Throws ValidationException if <= 0
    var maxResults = ValidatePositive(parameters.MaxResults, nameof(parameters.MaxResults));
    
    // Your tool logic here
}
```

#### `ValidateRange(int value, int min, int max, string parameterName)`
Validates that a value is within a specified range (inclusive).

```csharp
protected override async Task<MyResult> ExecuteInternalAsync(MyParams parameters, CancellationToken cancellationToken)
{
    // Throws ValidationException if not between 1 and 100
    var limit = ValidateRange(parameters.Limit, 1, 100, nameof(parameters.Limit));
    
    // Your tool logic here
}
```

#### `ValidateNotEmpty<T>(ICollection<T>? collection, string parameterName)`
Validates that a collection is not null or empty.

```csharp
protected override async Task<MyResult> ExecuteInternalAsync(MyParams parameters, CancellationToken cancellationToken)
{
    // Throws ValidationException if null or empty
    var items = ValidateNotEmpty(parameters.Items, nameof(parameters.Items));
    
    // Your tool logic here
}
```

## Error Result Helper Methods

These protected methods help create standardized error responses:

### `CreateErrorResult(string operation, string error, string? recoveryStep = null)`
Creates a standardized `ErrorInfo` object with recovery information.

```csharp
protected override async Task<MyResult> ExecuteInternalAsync(MyParams parameters, CancellationToken cancellationToken)
{
    try
    {
        // Your tool logic here
    }
    catch (Exception ex)
    {
        var errorInfo = CreateErrorResult(
            "file_operation", 
            ex.Message, 
            "Ensure the file exists and you have read permissions"
        );
        // Handle error or return error result
    }
}
```

### `CreateValidationErrorResult(string operation, string paramName, string requirement)`
Creates a validation-specific error result.

```csharp
var validationError = CreateValidationErrorResult(
    "search_files", 
    "pattern", 
    "Pattern cannot be empty or contain invalid characters"
);
```

## Response Result Helper Methods

For tools returning `ToolResult<TData>` types:

### `CreateSuccessResult<TData>(TData data, string? message = null)`
Creates a successful result with typed data.

```csharp
protected override async Task<ToolResult<SearchResults>> ExecuteInternalAsync(MyParams parameters, CancellationToken cancellationToken)
{
    var searchResults = PerformSearch(parameters);
    return CreateSuccessResult(searchResults, "Search completed successfully");
}
```

### `CreateErrorResult<TData>(string errorMessage, string errorCode = "TOOL_ERROR")`
Creates a failed result with error information.

```csharp
protected override async Task<ToolResult<SearchResults>> ExecuteInternalAsync(MyParams parameters, CancellationToken cancellationToken)
{
    if (someErrorCondition)
    {
        return CreateErrorResult<SearchResults>("Search failed: invalid pattern", "VALIDATION_ERROR");
    }
    
    // Success path...
}
```

## Custom Error Messages

Override the `ErrorMessages` property to provide custom error messages:

```csharp
public class MyTool : McpToolBase<MyParams, MyResult>
{
    private CustomErrorMessageProvider? _customErrorMessages;
    
    protected override ErrorMessageProvider ErrorMessages =>
        _customErrorMessages ??= new CustomErrorMessageProvider();
}

public class CustomErrorMessageProvider : ErrorMessageProvider
{
    public override string ParameterRequired(string paramName)
    {
        return paramName switch
        {
            "filePath" => "Please specify a valid file path",
            "query" => "Search query is required",
            _ => base.ParameterRequired(paramName)
        };
    }
    
    public override RecoveryInfo GetRecoveryInfo(string errorCode, string? context = null, Exception? exception = null)
    {
        return errorCode switch
        {
            "FILE_NOT_FOUND" => new RecoveryInfo
            {
                Steps = new[]
                {
                    "Check if the file path is correct",
                    "Ensure the file exists in the specified location",
                    "Verify you have read permissions for the file"
                }
            },
            _ => base.GetRecoveryInfo(errorCode, context, exception)
        };
    }
}
```

## Built-in Error Message Methods

The `ErrorMessageProvider` class provides these methods (available through `ErrorMessages` property):

- `ValidationFailed(string paramName, string requirement)` - Generic validation error
- `ToolExecutionFailed(string toolName, string details)` - Tool execution error
- `ParameterRequired(string paramName)` - Required parameter missing
- `RangeValidationFailed(string paramName, object min, object max)` - Range validation error
- `MustBePositive(string paramName)` - Positive value validation error
- `CannotBeEmpty(string paramName)` - Empty collection/string error
- `GetRecoveryInfo(string errorCode, string? context, Exception? exception)` - Recovery steps

## Automatic Parameter Validation

The framework also supports automatic validation using data annotations on your parameter classes:

```csharp
public class MyToolParams
{
    [Required(ErrorMessage = "File path is required")]
    [StringLength(500, ErrorMessage = "Path cannot exceed 500 characters")]
    public string FilePath { get; set; } = "";
    
    [Range(1, 1000, ErrorMessage = "Max results must be between 1 and 1000")]
    public int MaxResults { get; set; } = 50;
    
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string? NotificationEmail { get; set; }
}
```

The `ValidateParameters(TParams parameters)` method automatically validates these annotations before your tool executes.

## Best Practices

1. **Use validation helpers consistently** - Always validate parameters using the provided helper methods
2. **Provide meaningful error messages** - Override `ErrorMessages` for domain-specific errors
3. **Include recovery steps** - Help AI agents understand how to fix errors
4. **Use appropriate error codes** - Standard codes like "VALIDATION_ERROR", "FILE_NOT_FOUND", etc.
5. **Validate early** - Check parameters at the start of your `ExecuteInternalAsync` method
6. **Be specific** - Include parameter names and expected formats in error messages

## Example Complete Tool

```csharp
public class FileSearchTool : McpToolBase<FileSearchParams, ToolResult<FileSearchResults>>
{
    private FileSearchErrorMessages? _errorMessages;
    
    protected override ErrorMessageProvider ErrorMessages =>
        _errorMessages ??= new FileSearchErrorMessages();
    
    public override string Name => "search_files";
    public override string Description => "Search for files by pattern";
    
    protected override async Task<ToolResult<FileSearchResults>> ExecuteInternalAsync(
        FileSearchParams parameters, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Validate parameters using helpers
            var searchPath = ValidateRequired(parameters.SearchPath, nameof(parameters.SearchPath));
            var pattern = ValidateRequired(parameters.Pattern, nameof(parameters.Pattern));
            var maxResults = ValidateRange(parameters.MaxResults, 1, 1000, nameof(parameters.MaxResults));
            
            // Perform search
            var results = await SearchFilesAsync(searchPath, pattern, maxResults, cancellationToken);
            
            return CreateSuccessResult(results, $"Found {results.Files.Count} files");
        }
        catch (DirectoryNotFoundException)
        {
            return CreateErrorResult<FileSearchResults>(
                "Search directory not found", 
                "DIRECTORY_NOT_FOUND"
            );
        }
        catch (UnauthorizedAccessException)
        {
            return CreateErrorResult<FileSearchResults>(
                "Access denied to search directory", 
                "ACCESS_DENIED"
            );
        }
    }
    
    private class FileSearchErrorMessages : ErrorMessageProvider
    {
        public override RecoveryInfo GetRecoveryInfo(string errorCode, string? context = null, Exception? exception = null)
        {
            return errorCode switch
            {
                "DIRECTORY_NOT_FOUND" => new RecoveryInfo
                {
                    Steps = new[]
                    {
                        "Verify the search path exists",
                        "Check for typos in the directory path",
                        "Ensure the directory is accessible"
                    }
                },
                "ACCESS_DENIED" => new RecoveryInfo
                {
                    Steps = new[]
                    {
                        "Check file system permissions",
                        "Run with appropriate user privileges",
                        "Verify the directory allows read access"
                    }
                },
                _ => base.GetRecoveryInfo(errorCode, context, exception)
            };
        }
    }
}
```

## Summary

**Remember**: All validation and error helpers are **protected methods in `McpToolBase`**, not separate helper classes. Import `COA.Mcp.Framework.Base` and inherit from `McpToolBase<TParams, TResult>` to access these capabilities.