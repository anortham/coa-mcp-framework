# Validation and Error Handling in COA MCP Framework

> **IMPORTANT**: There is no `ErrorHelpers` class in the framework. All validation helpers are **protected methods** in the `McpToolBase<TParams, TResult>` class.

## Overview

The COA MCP Framework provides comprehensive validation and error handling capabilities at multiple levels:

1. **Tool-Level Validation**: Through the `McpToolBase` class validation helpers
2. **Middleware-Level Validation**: Advanced type verification and TDD enforcement
3. **Framework-Level Validation**: Automatic parameter validation using data annotations

All tools that inherit from `McpToolBase` automatically get access to validation helpers, error result builders, and customizable error messages. Additionally, the new middleware system provides proactive validation before tool execution begins.

## Middleware-Level Validation

The framework now includes advanced middleware that performs validation before your tool code executes, preventing common issues and enforcing best practices.

### Type Verification Middleware

Prevents AI-hallucinated types by verifying that referenced types actually exist in the codebase:

#### Configuration
```csharp
services.Configure<TypeVerificationOptions>(options =>
{
    options.Enabled = true;
    options.Mode = TypeVerificationMode.Strict; // Blocks execution on unverified types
    options.CacheExpirationHours = 24;
    options.RequireMemberVerification = true;
    options.WhitelistedTypes = new HashSet<string> { "string", "int", "object" };
});
```

#### Error Messages and Recovery
When type verification fails, users receive detailed guidance:

```
🚫 TYPE VERIFICATION FAILED: Unverified types detected

Issues detected:
  • Type 'UserService' not found or not verified
  • Member 'user.FullName' not verified (User type may not have FullName property)

🔍 Type Verification Process:
1. VERIFY: Use CodeNav tools to check if types exist
2. RESOLVE: Import required namespaces or fix type names  
3. VALIDATE: Hover over types to verify member access

📋 Required actions:
1. Verify type definitions:
   • Check if 'UserService' exists in the codebase
   • Verify 'User' type has 'FullName' property
   
2. Fix type issues:
   • Add missing using statements: using MyProject.Services;
   • Correct property names: user.FirstName + user.LastName
   • Import types: import { UserService } from './services';

3. Re-run after fixes to verify resolution

💡 TIP: Use 'Go to Definition' to verify types exist before coding!
```

#### Validation Modes
- **Strict**: Blocks operations with unverified types
- **Warning**: Logs warnings but allows execution
- **Disabled**: No type verification

### TDD Enforcement Middleware

Enforces Test-Driven Development practices by requiring failing tests before implementation:

#### Configuration
```csharp
services.Configure<TddEnforcementOptions>(options =>
{
    options.Enabled = true;
    options.Mode = TddEnforcementMode.Warning; // Warns but allows execution
    options.RequireFailingTest = true;
    options.AllowRefactoring = true;
    options.TestRunners = new Dictionary<string, TestRunnerConfig>
    {
        ["csharp"] = new TestRunnerConfig
        {
            Command = "dotnet test",
            TimeoutMs = 30000,
            FailingTestPatterns = new List<string> { @"Failed: \d+", @"Test Run Failed" }
        }
    };
});
```

#### Error Messages and Recovery
When TDD enforcement fails, users receive workflow guidance:

```
🚫 TDD VIOLATION: Implementation without proper test coverage

Issues detected:
  • No failing tests found - write a failing test first (RED phase)
  • Tests haven't been run recently - run tests first to verify current state

🔍 TDD Workflow (Red-Green-Refactor):
1. RED: Write a failing test that describes the desired behavior
2. GREEN: Write the minimal code to make the test pass  
3. REFACTOR: Improve the code while keeping tests green

📋 Required actions:
1. Write a failing test first:
   • Create or update test files
   • Write tests that describe the expected behavior  
   • Verify tests fail before implementing

2. Run tests to verify current state:
   dotnet test

3. After tests fail, implement the minimal code to pass

💡 TIP: This ensures your code is tested and behaves as expected!
```

#### TDD Phases
- **Red**: Writing failing tests (required before implementation)
- **Green**: Writing minimal code to pass tests (allowed)
- **Refactor**: Improving code while tests pass (configurable)

### Middleware Integration with Tools

Middleware validation happens automatically before your tool's `ExecuteInternalAsync` method:

```csharp
public class MyCodeTool : McpToolBase<CodeParams, CodeResult>
{
    protected override async Task<CodeResult> ExecuteInternalAsync(
        CodeParams parameters, 
        CancellationToken cancellationToken)
    {
        // By the time this method runs:
        // 1. TypeVerificationMiddleware has verified all types exist
        // 2. TddEnforcementMiddleware has checked for failing tests
        // 3. Your tool-level validation (below) can focus on business logic
        
        var filePath = ValidateRequired(parameters.FilePath, nameof(parameters.FilePath));
        var content = ValidateRequired(parameters.Content, nameof(parameters.Content));
        
        // Your implementation logic here
    }
}
```

### Custom Middleware Error Handling

You can customize middleware error messages by overriding the error message providers:

```csharp
public class CustomTypeVerificationMiddleware : TypeVerificationMiddleware
{
    public CustomTypeVerificationMiddleware(
        ITypeResolutionService typeService,
        IVerificationStateManager stateManager,
        ILogger<TypeVerificationMiddleware> logger,
        IOptions<TypeVerificationOptions> options) 
        : base(typeService, stateManager, logger, options)
    {
    }

    protected override string GetTypeVerificationError(List<string> unverifiedTypes, string filePath)
    {
        return $"Custom error: {unverifiedTypes.Count} unverified types in {filePath}. " +
               $"Please verify these types exist: {string.Join(", ", unverifiedTypes)}";
    }
}
```

## Tool-Level Validation Helper Methods

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

### Framework-Wide Validation Strategy

1. **Layer your validation approach**:
   - **Middleware Level**: Type verification, TDD enforcement, security checks
   - **Framework Level**: Data annotation validation for basic requirements
   - **Tool Level**: Business logic validation using helper methods

2. **Configure middleware appropriately**:
   - **Development**: Strict type verification + Warning TDD enforcement
   - **Production**: Warning type verification + Disabled TDD enforcement
   - **Learning environments**: Strict enforcement for both

### Tool-Level Validation

3. **Use validation helpers consistently** - Always validate parameters using the provided helper methods
4. **Provide meaningful error messages** - Override `ErrorMessages` for domain-specific errors
5. **Include recovery steps** - Help AI agents understand how to fix errors
6. **Use appropriate error codes** - Standard codes like "VALIDATION_ERROR", "FILE_NOT_FOUND", etc.
7. **Validate early** - Check parameters at the start of your `ExecuteInternalAsync` method
8. **Be specific** - Include parameter names and expected formats in error messages

### Middleware Integration

9. **Don't duplicate middleware validation** - If middleware handles type checking, don't repeat it in tools
10. **Focus on business logic** - Let middleware handle infrastructure concerns, tools handle domain logic
11. **Provide complementary error messages** - Middleware gives technical guidance, tools give domain context

### Error Recovery Design

12. **Progressive error recovery** - Start with automated fixes, then guided manual steps
13. **Context-aware messages** - Include file paths, line numbers, and specific values in errors
14. **Actionable guidance** - Every error should tell users exactly what to do next

## Complete Example: Multi-Layer Validation

This example shows how middleware, framework, and tool validation work together:

### 1. Server Configuration with Middleware

```csharp
var builder = McpServerBuilder.Create("validation-demo")
    .WithGlobalMiddleware(new List<ISimpleMiddleware>
    {
        // Middleware validation (runs first)
        new TypeVerificationMiddleware(typeService, stateManager, logger, typeOptions),
        new TddEnforcementMiddleware(testService, logger, tddOptions),
        new LoggingSimpleMiddleware(logger, LogLevel.Information)
    });
```

### 2. Parameter Class with Framework Validation

```csharp
public class FileSearchParams
{
    [Required(ErrorMessage = "Search path is required")]
    [StringLength(500, ErrorMessage = "Path cannot exceed 500 characters")]
    public string SearchPath { get; set; } = "";
    
    [Required(ErrorMessage = "Search pattern is required")]
    [RegularExpression(@"^[^<>:""|?*]+$", ErrorMessage = "Pattern contains invalid characters")]
    public string Pattern { get; set; } = "";
    
    [Range(1, 1000, ErrorMessage = "Max results must be between 1 and 1000")]
    public int MaxResults { get; set; } = 50;
}
```

### 3. Tool with Business Logic Validation

```csharp
public class FileSearchTool : McpToolBase<FileSearchParams, ToolResult<FileSearchResults>>
{
    private FileSearchErrorMessages? _errorMessages;
    
    protected override ErrorMessageProvider ErrorMessages =>
        _errorMessages ??= new FileSearchErrorMessages();
    
    public override string Name => "search_files";
    public override string Description => "Search for files by pattern with comprehensive validation";
    
    protected override async Task<ToolResult<FileSearchResults>> ExecuteInternalAsync(
        FileSearchParams parameters, 
        CancellationToken cancellationToken)
    {
        try
        {
            // At this point:
            // 1. TypeVerificationMiddleware has verified all types in any code being generated
            // 2. TddEnforcementMiddleware has checked test coverage requirements  
            // 3. Framework has validated [Required], [Range], etc. annotations
            
            // Tool-level validation focuses on business logic
            var searchPath = ValidateRequired(parameters.SearchPath, nameof(parameters.SearchPath));
            var pattern = ValidateRequired(parameters.Pattern, nameof(parameters.Pattern));
            var maxResults = ValidateRange(parameters.MaxResults, 1, 1000, nameof(parameters.MaxResults));
            
            // Business-specific validation
            if (!Directory.Exists(searchPath))
            {
                return CreateErrorResult<FileSearchResults>(
                    $"Directory '{searchPath}' does not exist",
                    "DIRECTORY_NOT_FOUND"
                );
            }
            
            if (IsRestrictedPath(searchPath))
            {
                return CreateErrorResult<FileSearchResults>(
                    $"Access to '{searchPath}' is restricted for security reasons",
                    "RESTRICTED_PATH"
                );
            }
            
            // Perform search with business rules
            var results = await SearchFilesWithBusinessRulesAsync(
                searchPath, pattern, maxResults, cancellationToken);
            
            return CreateSuccessResult(results, 
                $"Found {results.Files.Count} files matching '{pattern}' in '{searchPath}'");
        }
        catch (DirectoryNotFoundException ex)
        {
            return CreateErrorResult<FileSearchResults>(
                $"Directory not found: {ex.Message}", 
                "DIRECTORY_NOT_FOUND"
            );
        }
        catch (UnauthorizedAccessException ex)
        {
            return CreateErrorResult<FileSearchResults>(
                $"Access denied: {ex.Message}", 
                "ACCESS_DENIED"
            );
        }
        catch (SecurityException ex)
        {
            return CreateErrorResult<FileSearchResults>(
                $"Security violation: {ex.Message}", 
                "SECURITY_VIOLATION"
            );
        }
    }
    
    private bool IsRestrictedPath(string path)
    {
        var restrictedPaths = new[] { "C:\\Windows\\System32", "/etc", "/root" };
        return restrictedPaths.Any(restricted => 
            path.StartsWith(restricted, StringComparison.OrdinalIgnoreCase));
    }
    
    private async Task<FileSearchResults> SearchFilesWithBusinessRulesAsync(
        string searchPath, string pattern, int maxResults, CancellationToken cancellationToken)
    {
        // Implementation with business rules
        // - Filter out sensitive files
        // - Apply access controls  
        // - Log search activity
        // - Etc.
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
                        "Verify the search path exists and is spelled correctly",
                        "Check that the directory hasn't been moved or deleted",
                        "Ensure you have permission to access the parent directory",
                        "Try using an absolute path instead of a relative path"
                    }
                },
                "ACCESS_DENIED" => new RecoveryInfo
                {
                    Steps = new[]
                    {
                        "Check file system permissions for the directory",
                        "Ensure your user account has read access",
                        "Try running with elevated privileges if appropriate",
                        "Contact your system administrator if this is a shared resource"
                    }
                },
                "RESTRICTED_PATH" => new RecoveryInfo
                {
                    Steps = new[]
                    {
                        "Choose a different search location",
                        "Avoid system directories and protected paths",
                        "Use user directories or application-specific folders",
                        "Contact administrator if business requirements need this path"
                    }
                },
                "SECURITY_VIOLATION" => new RecoveryInfo
                {
                    Steps = new[]
                    {
                        "Review the search request for potential security issues",
                        "Ensure search patterns don't attempt path traversal",
                        "Use only trusted file paths and patterns",
                        "Contact security team if this error persists"
                    }
                },
                _ => base.GetRecoveryInfo(errorCode, context, exception)
            };
        }
    }
}
```

### 4. Complete Validation Flow

```
1. Request arrives → TypeVerificationMiddleware checks types (if generating code)
2. → TddEnforcementMiddleware checks test coverage (if implementing new features)
3. → Framework validates [Required], [Range], etc. annotations  
4. → Tool validates business rules and performs operation
5. ← Tool returns domain-specific error with recovery steps
6. ← Framework packages error with full context
7. ← Middleware logs and potentially transforms error
8. ← User receives comprehensive error with specific guidance
```
```

## Summary

The COA MCP Framework provides comprehensive, multi-layered validation:

### Three Validation Layers:
1. **Middleware**: Type verification and TDD enforcement before tool execution
2. **Framework**: Automatic data annotation validation 
3. **Tool**: Business logic validation using protected helper methods

### Key Points:
- **Middleware validation** runs first and handles infrastructure concerns
- **Framework validation** handles basic data requirements automatically  
- **Tool validation** focuses on business rules and domain logic
- **All layers provide recovery guidance** to help users fix issues

### Getting Started:
1. Configure middleware in your server builder for infrastructure validation
2. Use data annotations on parameter classes for basic validation
3. Inherit from `McpToolBase<TParams, TResult>` to access validation helpers
4. Override `ErrorMessages` property for custom error handling

**Remember**: All tool validation helpers are **protected methods in `McpToolBase`**, not separate helper classes. Import `COA.Mcp.Framework.Base` and inherit from `McpToolBase<TParams, TResult>` to access these capabilities.