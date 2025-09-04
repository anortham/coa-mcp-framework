# Lifecycle Hooks and Middleware

The COA MCP Framework provides a powerful lifecycle hooks system that allows you to intercept and extend tool execution at various points. This enables cross-cutting concerns like logging, token counting, performance monitoring, and custom business logic.

## Overview

The lifecycle hooks system is implemented using a simple middleware pattern that provides three key extension points:

- **Before Execution**: Called before the tool's main logic executes
- **After Execution**: Called after successful tool execution 
- **On Error**: Called when tool execution fails

## Core Interfaces

### ISimpleMiddleware

The base interface for all middleware:

```csharp
public interface ISimpleMiddleware
{
    int Order { get; }           // Execution order (lower runs first)
    bool IsEnabled { get; }      // Whether middleware is active
    
    Task OnBeforeExecutionAsync(string toolName, object? parameters);
    Task OnAfterExecutionAsync(string toolName, object? parameters, object? result, long elapsedMs);
    Task OnErrorAsync(string toolName, object? parameters, Exception exception, long elapsedMs);
}
```

### SimpleMiddlewareBase

A convenient base class with default implementations:

```csharp
public abstract class SimpleMiddlewareBase : ISimpleMiddleware
{
    public virtual int Order { get; set; } = 0;
    public virtual bool IsEnabled { get; set; } = true;
    
    // Default implementations that do nothing
    public virtual Task OnBeforeExecutionAsync(string toolName, object? parameters) => Task.CompletedTask;
    public virtual Task OnAfterExecutionAsync(string toolName, object? parameters, object? result, long elapsedMs) => Task.CompletedTask;
    public virtual Task OnErrorAsync(string toolName, object? parameters, Exception exception, long elapsedMs) => Task.CompletedTask;
}
```

## Using Middleware in Tools

### Global Middleware (Recommended for v1.7.18+)

Global middleware applies to ALL tools automatically and is configured at the server level:

```csharp
// Configure global middleware in server builder
var builder = new McpServerBuilder()
    .WithServerInfo("My Server", "1.0.0")
    .AddTypeVerificationMiddleware()  // Applies to all tools
    .AddTddEnforcementMiddleware()    // Applies to all tools
    .AddLoggingMiddleware()           // Applies to all tools
    .AddTokenCountingMiddleware()     // Applies to all tools
    .RegisterTool<MyTool>()
    .Build();
```

### Tool-Specific Middleware

To add middleware to specific tools, override the `ToolSpecificMiddleware` property:

```csharp
public class MyTool : McpToolBase<MyParameters, MyResult>
{
    private readonly ILogger<MyTool> _logger;
    
    public MyTool(IServiceProvider? serviceProvider, ILogger<MyTool>? logger = null) 
        : base(serviceProvider, logger)
    {
        _logger = logger;
    }
    
    protected override IReadOnlyList<ISimpleMiddleware>? ToolSpecificMiddleware => 
        new List<ISimpleMiddleware>
        {
            new CustomMiddleware(),            // Tool-specific only
            new DatabaseTransactionMiddleware() // Tool-specific only
        };
    
    // Tool implementation...
}
```

### Combined Middleware

Tools automatically combine global middleware (from DI) with tool-specific middleware, sorted by `Order` property:

```csharp
// Execution order example:
// 1. Global TypeVerification (Order: 5)
// 2. Global TDD Enforcement (Order: 10) 
// 3. Tool-specific Custom (Order: 20)
// 4. Global Logging (Order: 30)
// 5. Global TokenCounting (Order: 100)
```

## Built-in Middleware

The framework provides four built-in middleware implementations:

### LoggingSimpleMiddleware

Provides comprehensive execution logging:

```csharp
var loggingMiddleware = new LoggingSimpleMiddleware(logger, LogLevel.Information);
// Order: 10 (runs early)
// Logs: Tool start, completion time, errors, and optionally parameters (debug level)
```

### TokenCountingSimpleMiddleware  

Estimates and logs token usage:

```csharp
var tokenMiddleware = new TokenCountingSimpleMiddleware(logger);
// Order: 100 (runs later for accurate timing)
// Logs: Estimated input/output tokens based on JSON serialization
```

### TypeVerificationMiddleware

Prevents AI-hallucinated types in code generation by verifying type existence before allowing code operations:

```csharp
var typeVerificationMiddleware = new TypeVerificationMiddleware(
    typeResolutionService, 
    verificationStateManager, 
    logger, 
    Options.Create(new TypeVerificationOptions
    {
        Enabled = true,
        Mode = TypeVerificationMode.Strict,
        CacheExpirationHours = 24,
        AutoVerifyOnHover = true,
        RequireMemberVerification = true
    }));
// Order: 5 (runs very early, before TDD enforcement)
// Blocks: Edit, Write, MultiEdit, NotebookEdit operations with unverified types
// Features: Intelligent caching, file modification tracking, member access validation
```

**Key Features:**
- **Smart Type Resolution**: Detects types, classes, interfaces, enums, and member access patterns
- **Session-Scoped Caching**: Remembers verified types with file modification tracking
- **Multiple Verification Modes**: Strict (blocks), Warning (logs), or Disabled
- **Language Support**: C#, TypeScript, JavaScript with extensible patterns
- **Error Recovery**: Provides specific guidance on how to resolve type issues

### TddEnforcementMiddleware

Enforces Test-Driven Development practices by requiring failing tests before implementation:

```csharp
var tddMiddleware = new TddEnforcementMiddleware(
    testStatusService,
    logger,
    Options.Create(new TddEnforcementOptions
    {
        Enabled = true,
        Mode = TddEnforcementMode.Warning,
        RequireFailingTest = true,
        AllowRefactoring = true
    }));
// Order: 10 (runs after type verification)
// Enforces: Red-Green-Refactor TDD workflow
// Features: Multi-language test runner support, refactoring detection
```

**Key Features:**
- **TDD Workflow Enforcement**: Implements Red (failing test) → Green (minimal code) → Refactor cycle
- **Multi-Language Support**: Detects and runs dotnet test, npm test, pytest, and other test runners
- **Smart Detection**: Distinguishes between new functionality and refactoring operations
- **Comprehensive Guidance**: Provides detailed violation messages with recovery steps
- **Flexible Configuration**: Supports strict blocking or warning-only modes

## Execution Flow

When middleware is configured, the execution flow becomes:

1. **Sort middleware by Order** (ascending)
2. **Before hooks** (in order): middleware1 → middleware2 → middleware3
3. **Parameter validation**
4. **Tool execution**
5. **After hooks** (reverse order): middleware3 → middleware2 → middleware1
6. **Error hooks** (reverse order, if an error occurs)

The reverse order for "after" and "error" hooks allows for proper cleanup and unwinding of resources.

## Custom Middleware Examples

### Performance Monitoring

```csharp
public class PerformanceMiddleware : SimpleMiddlewareBase
{
    private readonly IMetrics _metrics;
    
    public PerformanceMiddleware(IMetrics metrics)
    {
        _metrics = metrics;
        Order = 20; // Run after logging
    }
    
    public override Task OnAfterExecutionAsync(string toolName, object? parameters, object? result, long elapsedMs)
    {
        _metrics.RecordToolExecution(toolName, elapsedMs);
        
        if (elapsedMs > 1000)
        {
            _metrics.IncrementSlowToolCount(toolName);
        }
        
        return Task.CompletedTask;
    }
}
```

### Audit Trail

```csharp
public class AuditMiddleware : SimpleMiddlewareBase
{
    private readonly IAuditService _auditService;
    
    public AuditMiddleware(IAuditService auditService)
    {
        _auditService = auditService;
        Order = 30;
    }
    
    public override async Task OnAfterExecutionAsync(string toolName, object? parameters, object? result, long elapsedMs)
    {
        await _auditService.LogToolExecution(new AuditEntry
        {
            ToolName = toolName,
            Parameters = parameters,
            Result = result,
            ElapsedMs = elapsedMs,
            Timestamp = DateTimeOffset.UtcNow
        });
    }
}
```

### Rate Limiting

```csharp
public class RateLimitMiddleware : SimpleMiddlewareBase
{
    private readonly IRateLimiter _rateLimiter;
    
    public RateLimitMiddleware(IRateLimiter rateLimiter)
    {
        _rateLimiter = rateLimiter;
        Order = 5; // Run very early
    }
    
    public override async Task OnBeforeExecutionAsync(string toolName, object? parameters)
    {
        if (!await _rateLimiter.IsAllowedAsync(toolName))
        {
            throw new RateLimitExceededException($"Rate limit exceeded for tool '{toolName}'");
        }
    }
}
```

### Type Verification with Custom Configuration

```csharp
public class CustomTypeVerificationMiddleware : SimpleMiddlewareBase
{
    private readonly TypeVerificationMiddleware _baseMiddleware;
    
    public CustomTypeVerificationMiddleware(
        ITypeResolutionService typeService,
        IVerificationStateManager stateManager,
        ILogger<TypeVerificationMiddleware> logger)
    {
        var options = Options.Create(new TypeVerificationOptions
        {
            Enabled = true,
            Mode = TypeVerificationMode.Strict,
            CacheExpirationHours = 24,
            AutoVerifyOnHover = true,
            RequireMemberVerification = true,
            WhitelistedTypes = new HashSet<string> 
            { 
                "CustomApiClient", "InternalType", "LegacyService" 
            },
            ExcludedPaths = new List<string>
            {
                "*/Generated/*", "*.g.cs", "*/bin/*", "*/obj/*"
            },
            LanguageConfigs = new Dictionary<string, LanguageVerificationConfig>
            {
                [".cs"] = new LanguageVerificationConfig
                {
                    RequireExactMatch = true,
                    CaseSensitive = true,
                    EnableGenericTypeInference = true,
                    LanguageSpecificWhitelist = new HashSet<string> 
                    { 
                        "var", "dynamic", "object" 
                    }
                },
                [".ts"] = new LanguageVerificationConfig
                {
                    RequireExactMatch = false,
                    CaseSensitive = true,
                    EnableGenericTypeInference = true,
                    IgnorePatterns = new List<string> 
                    { 
                        @"any\[\]", @"Record<.*>" 
                    }
                }
            }
        });
        
        _baseMiddleware = new TypeVerificationMiddleware(typeService, stateManager, logger, options);
        Order = 5; // Very early, before TDD enforcement
    }
    
    public override Task OnBeforeExecutionAsync(string toolName, object? parameters)
    {
        return _baseMiddleware.OnBeforeExecutionAsync(toolName, parameters);
    }
}
```

### TDD Enforcement with Multi-Language Support

```csharp
public class ComprehensiveTddMiddleware : SimpleMiddlewareBase
{
    private readonly TddEnforcementMiddleware _baseMiddleware;
    
    public ComprehensiveTddMiddleware(
        ITestStatusService testService,
        ILogger<TddEnforcementMiddleware> logger)
    {
        var options = Options.Create(new TddEnforcementOptions
        {
            Enabled = true,
            Mode = TddEnforcementMode.Strict,
            RequireFailingTest = true,
            AllowRefactoring = true,
            TestRunners = new Dictionary<string, TestRunnerConfig>
            {
                ["csharp"] = new TestRunnerConfig
                {
                    Command = "dotnet test",
                    TimeoutMs = 60000,
                    Arguments = new List<string> { "--no-build", "--verbosity", "minimal" },
                    FailingTestPatterns = new List<string> 
                    { 
                        @"Failed: \d+", @"Total tests: \d+\. Passed: \d+\. Failed: [^0]\d*" 
                    }
                },
                ["typescript"] = new TestRunnerConfig
                {
                    Command = "npm",
                    Arguments = new List<string> { "test", "--", "--reporter=json" },
                    TimeoutMs = 45000,
                    EnvironmentVariables = new Dictionary<string, string> 
                    { 
                        ["NODE_ENV"] = "test" 
                    }
                },
                ["python"] = new TestRunnerConfig
                {
                    Command = "pytest",
                    Arguments = new List<string> { "-v", "--tb=short", "--json-report" },
                    TimeoutMs = 60000
                }
            },
            TestFilePatterns = new List<string>
            {
                "*test*", "*Test*", "*tests*", "*Tests*",
                "*spec*", "*Spec*", "*.spec.*", "*.test.*",
                "__tests__", "__test__"
            },
            GeneratedCodePatterns = new List<string>
            {
                "*.generated.*", "*.g.*", "*.designer.*", 
                "*AssemblyInfo.cs", "*.min.js", "dist/*"
            },
            PreTestCommands = new List<string>
            {
                "dotnet build --no-restore", // For C# projects
                "npm run compile"            // For TypeScript projects
            },
            NewFunctionalityPatterns = new List<string>
            {
                @"public\s+class\s+[A-Z]\w+",     // C# public class
                @"export\s+class\s+[A-Z]\w+",     // TypeScript export class
                @"public\s+interface\s+I[A-Z]\w+", // C# interface
                @"export\s+interface\s+[A-Z]\w+",  // TypeScript interface
                @"def\s+[a-z_]\w*\s*\(",          // Python function
                @"class\s+[A-Z]\w*\s*\(",         // Python class
            },
            RefactoringPatterns = new List<string>
            {
                @"// Extracted from \w+",
                @"// Moved from \w+",
                @"// Renamed from \w+",
                @"private\s+\w+\s+Extract[A-Z]\w*"
            },
            MinimumComplexityThreshold = 5, // Lower threshold for strict TDD
            EnableDetailedLogging = true
        });
        
        _baseMiddleware = new TddEnforcementMiddleware(testService, logger, options);
        Order = 10; // After type verification
    }
    
    public override Task OnBeforeExecutionAsync(string toolName, object? parameters)
    {
        return _baseMiddleware.OnBeforeExecutionAsync(toolName, parameters);
    }
}
```

## Configuration and Usage Scenarios

### Development Environment Setup

```csharp
// Development: Strict type verification, warning-level TDD
var serverBuilder = new McpServerBuilder()
    .WithServerInfo("Development Server", "1.0.0")
    .AddTypeVerificationMiddleware(options =>
    {
        options.Enabled = true;
        options.Mode = TypeVerificationMode.Strict;
        options.EnableDetailedLogging = true;
    })
    .AddTddEnforcementMiddleware(options =>
    {
        options.Enabled = true;
        options.Mode = TddEnforcementMode.Warning;
        options.EnableDetailedLogging = true;
    })
    .AddLoggingMiddleware(LogLevel.Debug)
    .AddTokenCountingMiddleware()
    .RegisterTool<MyTool>()
    .Build();
```

### Production Environment Setup

```csharp
// Production: Warning-level type verification, disabled TDD
var serverBuilder = new McpServerBuilder()
    .WithServerInfo("Production Server", "1.0.0")
    .AddTypeVerificationMiddleware(options =>
    {
        options.Enabled = true;
        options.Mode = TypeVerificationMode.Warning;
        options.EnableDetailedLogging = false;
        options.CacheExpirationHours = 48; // Longer cache in production
    })
    // TDD enforcement usually disabled in production
    .AddLoggingMiddleware(LogLevel.Information)
    .AddTokenCountingMiddleware()
    .RegisterTool<MyTool>()
    .Build();
```

### Team Onboarding Configuration

```csharp
// New team members: Detailed guidance and strict enforcement
var options = new TddEnforcementOptions
{
    Enabled = true,
    Mode = TddEnforcementMode.Strict,
    RequireFailingTest = true,
    AllowRefactoring = false, // Force TDD for learning
    MinimumComplexityThreshold = 3, // Lower threshold
    EnableDetailedLogging = true,
    PreTestCommands = new List<string>
    {
        "echo 'Running TDD workflow check...'",
        "dotnet build --no-restore"
    }
};
```

### Language-Specific Configurations

```csharp
// C# Enterprise Project
var csharpConfig = new TypeVerificationOptions
{
    LanguageConfigs = new Dictionary<string, LanguageVerificationConfig>
    {
        [".cs"] = new LanguageVerificationConfig
        {
            RequireExactMatch = true,
            CaseSensitive = true,
            LanguageSpecificWhitelist = new HashSet<string>
            {
                "var", "dynamic", "object", "string", "int", "bool", "DateTime"
            },
            IgnorePatterns = new List<string>
            {
                @"System\.\w+", // Allow System namespace
                @"Microsoft\.\w+", // Allow Microsoft namespace
                @"IEnumerable<\w+>", // Generic collections
            }
        }
    },
    ExcludedPaths = new List<string>
    {
        "*/bin/*", "*/obj/*", "*.generated.cs", "*.designer.cs",
        "AssemblyInfo.cs", "GlobalUsings.cs"
    }
};

// TypeScript React Project
var typescriptConfig = new TypeVerificationOptions
{
    LanguageConfigs = new Dictionary<string, LanguageVerificationConfig>
    {
        [".ts"] = new LanguageVerificationConfig
        {
            RequireExactMatch = false, // More flexible for TS
            CaseSensitive = true,
            IgnorePatterns = new List<string>
            {
                @"React\.\w+", // React types
                @"\w+Props", // Component props
                @"\w+State", // Component state
                @"any\[\]", // Loose arrays
            }
        },
        [".tsx"] = new LanguageVerificationConfig
        {
            RequireExactMatch = false,
            CaseSensitive = true,
            LanguageSpecificWhitelist = new HashSet<string>
            {
                "JSX.Element", "React.FC", "React.Component"
            }
        }
    },
    WatchedFilePatterns = new List<string>
    {
        "*.ts", "*.tsx", "*.d.ts", "package.json", "tsconfig.json"
    }
};
```

### Error Recovery Examples

When type verification fails, middleware provides specific recovery guidance:

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

When TDD enforcement fails, middleware provides workflow guidance:

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

## Best Practices

### 1. Order Your Middleware Thoughtfully

- **Very early (1-5)**: Type verification, security, rate limiting  
- **Early (5-15)**: TDD enforcement, authentication, logging
- **Medium (15-50)**: Business validation, monitoring
- **Late (50-100)**: Token counting, cleanup, reporting

### 2. Handle Errors Gracefully

```csharp
public override async Task OnErrorAsync(string toolName, object? parameters, Exception exception, long elapsedMs)
{
    try
    {
        await _service.HandleErrorAsync(toolName, exception);
    }
    catch (Exception ex)
    {
        // Log but don't re-throw - middleware errors shouldn't break the chain
        _logger.LogError(ex, "Error in middleware error handler");
    }
}
```

### 3. Use Conditional Logic

```csharp
public override Task OnBeforeExecutionAsync(string toolName, object? parameters)
{
    // Only apply to specific tool categories
    if (toolName.StartsWith("sensitive_"))
    {
        return ValidateSecurityAsync(parameters);
    }
    
    return Task.CompletedTask;
}
```

### 4. Leverage Tool Categories

```csharp
public class DatabaseMiddleware : SimpleMiddlewareBase
{
    public override async Task OnBeforeExecutionAsync(string toolName, object? parameters)
    {
        // Only apply to database tools
        if (IsToolCategory(toolName, ToolCategory.DataAccess))
        {
            await _dbContext.BeginTransactionAsync();
        }
    }
}
```

## Testing Middleware

The framework includes comprehensive test coverage for middleware. Here's an example of testing custom middleware:

```csharp
[Test]
public async Task CustomMiddleware_CallsLifecycleHooks()
{
    // Arrange
    var middleware = new Mock<ISimpleMiddleware>();
    middleware.Setup(m => m.IsEnabled).Returns(true);
    middleware.Setup(m => m.Order).Returns(1);

    var tool = new TestTool(logger, new List<ISimpleMiddleware> { middleware.Object });
    var parameters = new TestParameters { Value = "test" };

    // Act
    var result = await tool.ExecuteAsync(parameters);

    // Assert
    middleware.Verify(m => m.OnBeforeExecutionAsync("test_tool", parameters), Times.Once);
    middleware.Verify(m => m.OnAfterExecutionAsync("test_tool", parameters, result, It.IsAny<long>()), Times.Once);
}
```

## Complete Example

See `examples/SimpleMcpServer/Tools/LifecycleExampleTool.cs` for a complete working example that demonstrates:

- Built-in logging and token counting middleware
- Custom timing middleware with performance categorization
- Parameter validation and error handling
- Proper middleware ordering and lifecycle management

## Troubleshooting and Testing

### Common Issues and Solutions

#### Type Verification Issues

**Issue**: Types are being flagged as unverified even though they exist
```csharp
// Problem: Case sensitivity or namespace issues
var user = new UserService(); // Flagged as unverified

// Solution 1: Check exact casing and namespace
var user = new MyProject.Services.UserService();

// Solution 2: Add to whitelist
options.WhitelistedTypes.Add("UserService");

// Solution 3: Configure namespace fallback
options.EnableNamespaceFallback = true;
```

**Issue**: Performance degradation with many type checks
```csharp
// Solution: Optimize cache settings
var options = new TypeVerificationOptions
{
    MaxCacheSize = 50000, // Increase cache size
    CacheExpirationHours = 48, // Longer expiration
    PreloadTypes = true, // Preload commonly used types
    EnableFileWatching = true // Efficient invalidation
};
```

#### TDD Enforcement Issues

**Issue**: Tests are not being detected properly
```csharp
// Problem: Test runner configuration
var options = new TddEnforcementOptions
{
    TestRunners = new Dictionary<string, TestRunnerConfig>
    {
        ["csharp"] = new TestRunnerConfig
        {
            Command = "dotnet test",
            Arguments = new List<string> { "--logger", "console;verbosity=detailed" },
            FailingTestPatterns = new List<string>
            {
                @"Failed!\s+- Failed:\s+(\d+)", // Custom pattern for your test output
                @"Test Run Failed\."
            }
        }
    }
};
```

**Issue**: Refactoring operations being blocked
```csharp
// Solution: Enhance refactoring detection
options.RefactoringPatterns.AddRange(new[]
{
    @"// Refactored by AI assistant",
    @"// Extracted method: \w+",
    @"private\s+static\s+\w+\s+\w+Helper", // Helper methods
});

options.AllowRefactoring = true;
options.MinimumComplexityThreshold = 15; // Higher threshold for refactoring
```

### Testing Middleware Components

#### Unit Testing Type Verification

```csharp
[Test]
public async Task TypeVerification_WithValidType_ShouldPass()
{
    // Arrange
    var mockTypeService = new Mock<ITypeResolutionService>();
    var mockStateManager = new Mock<IVerificationStateManager>();
    var logger = Mock.Of<ILogger<TypeVerificationMiddleware>>();
    
    mockTypeService.Setup(x => x.ResolveTypeAsync("UserService", It.IsAny<string>()))
               .ReturnsAsync(new TypeResolutionResult { IsResolved = true });
    
    mockStateManager.Setup(x => x.IsTypeVerifiedAsync("UserService", It.IsAny<string>()))
                   .ReturnsAsync(false);
    
    var options = Options.Create(new TypeVerificationOptions { Enabled = true });
    var middleware = new TypeVerificationMiddleware(
        mockTypeService.Object, mockStateManager.Object, logger, options);
    
    var parameters = new { file_path = "test.cs", new_string = "var service = new UserService();" };
    
    // Act & Assert
    await Assert.DoesNotThrowAsync(() => 
        middleware.OnBeforeExecutionAsync("Edit", parameters));
    
    mockTypeService.Verify(x => x.ResolveTypeAsync("UserService", It.IsAny<string>()), Times.Once);
}

[Test]
public async Task TypeVerification_WithInvalidType_ShouldThrow()
{
    // Arrange
    var mockTypeService = new Mock<ITypeResolutionService>();
    mockTypeService.Setup(x => x.ResolveTypeAsync("NonExistentType", It.IsAny<string>()))
               .ReturnsAsync(new TypeResolutionResult { IsResolved = false });
    
    // Test implementation...
}
```

#### Integration Testing TDD Enforcement

```csharp
[Test]
public async Task TddEnforcement_WithFailingTests_ShouldAllowImplementation()
{
    // Arrange
    var mockTestService = new Mock<ITestStatusService>();
    var logger = Mock.Of<ILogger<TddEnforcementMiddleware>>();
    
    mockTestService.Setup(x => x.GetTestStatusAsync(It.IsAny<string>()))
               .ReturnsAsync(new TestStatus 
               { 
                   HasFailingTests = true,
                   LastTestRun = DateTime.UtcNow.AddMinutes(-5)
               });
    
    var options = Options.Create(new TddEnforcementOptions 
    { 
        Enabled = true,
        Mode = TddEnforcementMode.Strict 
    });
    
    var middleware = new TddEnforcementMiddleware(mockTestService.Object, logger, options);
    var parameters = new { file_path = "service.cs", new_string = "public class NewService {}" };
    
    // Act & Assert
    await Assert.DoesNotThrowAsync(() => 
        middleware.OnBeforeExecutionAsync("Write", parameters));
}
```

### Performance Testing

```csharp
[Test]
public async Task MiddlewarePipeline_PerformanceTest()
{
    // Test with multiple middleware components
    var middleware = new List<ISimpleMiddleware>
    {
        new TypeVerificationMiddleware(typeService, stateManager, logger, typeOptions),
        new TddEnforcementMiddleware(testService, logger, tddOptions),
        new TokenCountingSimpleMiddleware(logger)
    };
    
    var stopwatch = Stopwatch.StartNew();
    
    // Execute middleware pipeline 1000 times
    for (int i = 0; i < 1000; i++)
    {
        foreach (var m in middleware.OrderBy(x => x.Order))
        {
            await m.OnBeforeExecutionAsync("Edit", testParameters);
        }
    }
    
    stopwatch.Stop();
    
    Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(5000), 
        "Middleware pipeline should complete 1000 iterations in under 5 seconds");
}
```

### Debugging Middleware

Enable detailed logging for troubleshooting:

```csharp
services.Configure<TypeVerificationOptions>(options =>
{
    options.EnableDetailedLogging = true;
});

services.Configure<TddEnforcementOptions>(options =>
{
    options.EnableDetailedLogging = true;
});

// Use structured logging to analyze middleware behavior
_logger.LogDebug("TypeVerification: Checking {TypeCount} types in {FilePath}", 
    extractedTypes.Count, filePath);
```

## Migration Notes

### From Previous Complex Pipeline

The previous complex pipeline system with `IToolExecutionPipeline` has been replaced with this simpler, more flexible middleware approach:

- **Old**: Complex pipeline with fixed execution states
- **New**: Simple middleware with three clear extension points
- **Benefits**: Better performance, easier to understand, more flexible

### Updating Existing Code

If you have tools that previously used pipeline features:

1. Replace `IToolExecutionPipeline` with `IReadOnlyList<ISimpleMiddleware>`
2. Convert pipeline hooks to middleware methods
3. Use built-in middleware for common scenarios (logging, token counting)

## Performance Considerations

- Middleware executes in the same thread as the tool
- Keep middleware lightweight to avoid impacting tool performance  
- Use async operations sparingly - prefer fire-and-forget patterns for non-critical operations
- Disabled middleware (`IsEnabled = false`) are completely skipped

The lifecycle hooks system provides a clean, testable way to extend tool behavior without modifying core tool logic. It's designed to be both powerful and simple to use.