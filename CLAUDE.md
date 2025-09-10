# COA MCP Framework - AI Assistant Guide

## 🚨 CRITICAL: Framework vs Server Execution

**Framework changes require:**
1. Build framework: `dotnet build`
2. Run tests: `dotnet test` (must be 100% passing)
3. Pack NuGet: `dotnet pack -c Release`
4. Update consumer package references
5. Restart MCP servers

## ⚠️ AI-CRITICAL GOTCHAS (READ FIRST)

1. **NEVER assume types exist** - Always verify with CodeNav tools first
2. **USE NUNIT ONLY** - `[Test]`, `[TestCase]`, `Assert.That()` - xUnit prohibited
3. **Inherit from McpToolBase<TParams,TResult>** - Automatic validation included
4. **Build-test-pack cycle** - Framework changes need full rebuild cycle
5. **Validation helpers available** - `ValidateRequired()`, `ValidateRange()`, `ValidatePositive()`
6. **Sequential async kills performance** - Use `ConcurrentAsyncUtilities.ExecuteConcurrentlyAsync()`
7. **Use CreateMinimal() or CreateProduction()** - Factory methods provide better defaults than manual setup

## 🚀 Quick Patterns (Copy-Paste Ready)

### Standard Tool Template
```csharp
public class MyTool : McpToolBase<MyParams, MyResult>
{
    protected override async Task<MyResult> ExecuteAsync(MyParams parameters)
    {
        ValidateRequired(parameters.RequiredField, nameof(parameters.RequiredField));
        ValidateRange(parameters.Count, 1, 100, nameof(parameters.Count));
        
        // Implementation here
        return new MyResult { Success = true };
    }
}
```

### 🆕 Enhanced Behavioral Adoption Tool Template
```csharp
public class MySmartTool : McpToolBase<MyParams, MyResult>, 
    IPrioritizedTool,  // 🆕 Enhanced priority interface (replaces IToolPriority)
    ISymbolicRead,     // Marks as symbol-aware
    ITypeAware         // Marks as type-safe
{
    // 🆕 Enhanced tool description with imperative language
    public override string Description => 
        DefaultToolDescriptionProvider.TransformToImperative(
            "Searches code with Tree-sitter parsing for accurate type information", 
            Priority);
    
    // IPrioritizedTool implementation - enhanced interface
    public int Priority => 85; // Higher = more recommended (1-100 scale)
    public string[] PreferredScenarios => new[] { "type_verification", "code_exploration" };
    
    protected override async Task<MyResult> ExecuteAsync(MyParams parameters)
    {
        ValidateRequired(parameters.RequiredField, nameof(parameters.RequiredField));
        
        try
        {
            // Implementation here
            return new MyResult { Success = true };
        }
        catch (FileNotFoundException ex)
        {
            // Enhanced error recovery with context
            throw new McpException("FILE_NOT_FOUND", new Dictionary<string, object>
            {
                ["file_path"] = ex.FileName,
                ["directory"] = Path.GetDirectoryName(ex.FileName),
                ["operation"] = "read"
            });
        }
    }
}
```

### 🆕 Tool Priority & Behavioral Adoption APIs

**IPrioritizedTool Interface:**
```csharp
public interface IPrioritizedTool : IToolMarker
{
    int Priority { get; }  // 1-100, where 100 is highest priority
    string[] PreferredScenarios { get; }  // "type_verification", "code_exploration", etc.
}
```

**DefaultToolDescriptionProvider.TransformToImperative():**
- **90-100**: "CRITICAL - "
- **80-89**: "USE FIRST - " 
- **70-79**: "RECOMMENDED - "
- **60-69**: "PREFER - "
- **Below 60**: No prefix

**Tool Marker Interfaces:**
```csharp
ISymbolicRead    // Reads symbols without loading entire files
ITypeAware       // Understands type systems
ICanEdit         // Can modify files
ISymbolicEdit    // Can edit specific symbols
// 9 total marker interfaces available
```

### Disposable Tool (DB/Files)
```csharp
public class DatabaseTool : DisposableToolBase<DbParams, DbResult>
{
    protected override async Task<DbResult> ExecuteAsync(DbParams parameters)
    {
        ValidateRequired(parameters.ConnectionString, nameof(parameters.ConnectionString));
        
        using var connection = new SqlConnection(parameters.ConnectionString);
        // Implementation with automatic cleanup
    }
}
```

### Error with Recovery
```csharp
protected override Dictionary<string, string> ErrorMessages => new()
{
    ["validation_failed"] = "Required field missing. Add: parameters.RequiredField = 'value'",
    ["range_error"] = "Count must be 1-100. Current: {0}. Fix: parameters.Count = 50"
};
```

### 🆕 Complete Behavioral Adoption Server
```csharp
var builder = new McpServerBuilder()
    .WithServerInfo("My Smart Server", "1.0.0")
    
    // 🆕 Template-based instructions with variables
    .WithInstructionsFromTemplate("Templates/server-guidance.scriban", new TemplateVariables
    {
        AvailableTools = new[] { "search", "navigate", "edit" },
        BuiltInTools = new[] { "Read", "Grep", "Bash", "Search", "WebSearch" },
        EnforcementLevel = WorkflowEnforcement.Recommend,
        CustomVariables = new() { ["ProjectType"] = "C# Library" }
    })
    
    // OR: Built-in template contexts
    .WithTemplateInstructions(options =>
    {
        options.ContextName = "codesearch"; // Built-in: general, codesearch, database
        options.EnableConditionalLogic = true;
        options.CustomVariables["ProjectType"] = "C# Library";
    })
    
    // 🆕 Professional tool comparisons (evidence-based, no manipulation!)
    .WithToolComparison(
        task: "Navigate to definition",
        serverTool: "goto_definition",
        builtInTool: "Read + manual search",
        advantage: "Direct jump with exact type signatures", 
        performanceMetric: "Instant vs 30+ seconds manual search"
    )
    .WithToolComparison(
        task: "Find code patterns",
        serverTool: "text_search",
        builtInTool: "grep",
        advantage: "Lucene-indexed with Tree-sitter parsing",
        performanceMetric: "100x faster, searches millions of lines in <500ms"
    )
    
    // 🆕 Set workflow enforcement level
    .WithWorkflowEnforcement(WorkflowEnforcement.Recommend) // Suggest/Recommend/StronglyUrge
    
    // Tool management with priority and workflows
    .ConfigureToolManagement(config =>
    {
        config.EnableWorkflowSuggestions = true;
        config.EnableToolPriority = true;
        config.UseDefaultDescriptionProvider = true; // 🆕 Enables TransformToImperative
    })
    
    // Smart error recovery with context
    .WithAdvancedErrorRecovery(options =>
    {
        options.EnableRecoveryGuidance = true;
        options.Tone = ErrorRecoveryTone.Professional;
        options.IncludeOriginalError = true;
        options.IncludePreventionTips = true;
    });
```

### 🆕 Template Variables & Scriban Integration

**Available in .scriban template files:**
```scriban
# Server & Tool Information
{{server_info.name}} - {{server_info.version}}
{{available_tools}} - Array of your server's tools
{{builtin_tools}} - ["Read", "Grep", "Bash", "Search", "WebSearch"]

# 🆕 Professional Tool Comparisons
{{tool_comparisons}} - Dictionary of ToolComparison objects
{{#for comparison in tool_comparisons}}
### {{comparison.task}}
- USE: {{comparison.server_tool}} - {{comparison.advantage}}
- AVOID: {{comparison.builtin_tool}}
- Performance: {{comparison.performance_metric}}
{{/for}}

# 🆕 Workflow Enforcement & Conditional Logic
{{enforcement_level}} - "suggest", "recommend", or "strongly_urge"
{{#has_tool available_tools "text_search"}}Use text_search for patterns{{/has_tool}}
{{#has_marker available_markers "ISymbolicRead"}}Symbol-aware tools available{{/has_marker}}

# Custom Variables
{{ProjectType}} {{TeamName}} - Any custom variables you define
```

### Server Configuration (Updated v1.8+)
```csharp
// EASY START: Use factory methods for better defaults
var builder = McpServerBuilder.CreateMinimal("my-server", "1.0.0")
    .DiscoverTools(typeof(Program).Assembly);

// OR: Production-ready setup with observability
var builder = McpServerBuilder.CreateProduction("my-server", "1.0.0")
    .DiscoverTools(typeof(Program).Assembly);

// OR: Manual configuration with improved defaults
var builder = new McpServerBuilder()
    .WithServerInfo("my-server", "1.0.0")
    .ConfigureFramework(options =>
    {
        options.EnableFrameworkLogging = false; // Clean stdout for MCP protocol (DEFAULT)
    })
    .AddLoggingMiddleware(LogLevel.Information); // Simple logging middleware
```

### ❌ REMOVED FEATURES (v1.8+)
These features were removed as they never worked reliably:
- `TypeVerificationMiddleware` - Caused startup failures
- `TddEnforcementMiddleware` - Never functional
- `AddTypeVerification()` service method
- `AddTddEnforcement()` service method

## 📍 Essential Files (AI Priority Order)

| When You Need | File |
|---------------|------|
| **Tool base class** | `src/COA.Mcp.Framework/Base/McpToolBase.Generic.cs` |
| **Server setup** | `src/COA.Mcp.Framework/Server/McpServerBuilder.cs` |
| **🆕 Enhanced priority** | `src/COA.Mcp.Framework/Interfaces/IPrioritizedTool.cs` |
| **🆕 Tool markers** | `src/COA.Mcp.Framework/Interfaces/IToolMarker.cs` |
| **🆕 Tool comparisons** | `src/COA.Mcp.Framework/Configuration/ToolComparison.cs` |
| **🆕 WorkflowEnforcement** | `src/COA.Mcp.Framework/Configuration/WorkflowEnforcement.cs` |
| **🆕 Description provider** | `src/COA.Mcp.Framework/Services/DefaultToolDescriptionProvider.cs` |
| **🆕 Template variables** | `src/COA.Mcp.Framework/Services/TemplateVariables.cs` |
| **🆕 Template processor** | `src/COA.Mcp.Framework/Services/InstructionTemplateProcessor.cs` |
| **🆕 Template manager** | `src/COA.Mcp.Framework/Services/InstructionTemplateManager.cs` |
| **🆕 Error recovery** | `src/COA.Mcp.Framework/Services/ErrorRecoveryTemplateProcessor.cs` |
| **🆕 Workflow suggestions** | `src/COA.Mcp.Framework/Services/WorkflowSuggestionManager.cs` |
| **🆕 Tool priority (legacy)** | `src/COA.Mcp.Framework/Interfaces/IToolPriority.cs` |
| **Async utilities** | `src/COA.Mcp.Framework/Utilities/ConcurrentAsyncUtilities.cs` |
| **Configuration options** | `src/COA.Mcp.Framework/Configuration/FrameworkOptions.cs` |
| **Examples** | `examples/SimpleMcpServer/` |

## 🆕 Template Variables & Enhancement Features

### Available Template Variables in .scriban files:
```scriban
# Server Information
{{server_info.name}} - Server name
{{server_info.version}} - Server version

# Tool Information  
{{available_tools}} - Array of your server's tools
{{builtin_tools}} - Array of Claude's built-in tools ["Read", "Grep", "Bash", "Search", "WebSearch"]
{{tool_priorities}} - Dictionary of tool priorities

# 🆕 Professional Tool Comparisons
{{tool_comparisons}} - Dictionary of ToolComparison objects with:
  - task: "Find code patterns"  
  - server_tool: "text_search"
  - builtin_tool: "grep"
  - advantage: "Lucene-indexed with Tree-sitter parsing"
  - performance_metric: "100x faster, <500ms"

# 🆕 Workflow Enforcement
{{enforcement_level}} - "suggest", "recommend", or "strongly_urge"

# Conditional Logic Helpers
{{#has_tool available_tools "text_search"}}Use text_search for patterns{{/has_tool}}
{{#has_builtin builtin_tools "grep"}}Avoid grep, use text_search instead{{/has_builtin}}
{{#has_marker available_markers "ISymbolicRead"}}Symbol-aware tools available{{/has_marker}}
```

### 🆕 Enhanced Tool Development Patterns:
```csharp
// Priority-based descriptions with TransformToImperative
public override string Description => 
    DefaultToolDescriptionProvider.TransformToImperative(
        "Locates symbol definitions with Tree-sitter parsing", 
        Priority); // Results in "USE FIRST - Locates symbol definitions..."

// WorkflowEnforcement levels for appropriate guidance strength:
// - Suggest: "Consider using..."
// - Recommend: "RECOMMENDED: Use..." 
// - StronglyUrge: "ALWAYS use... for these scenarios"
```

## 🛑 Troubleshooting (When Things Go Wrong)

| Problem | Solution |
|---------|----------|
| **Changes not working** | `dotnet build && dotnet pack -c Release` → Update consumer |
| **Type not found** | Use CodeNav to verify type exists, check namespaces |
| **Tool not registering** | Verify inheritance from McpToolBase<TParams,TResult> |
| **Tests failing** | Check NUnit syntax: `[Test]`, `Assert.That()` |
| **Memory growing** | Configure MaxCacheSize, use LRU eviction |
| **Slow async** | Replace `foreach+await` with `ConcurrentAsyncUtilities` |
| **Middleware not running** | Check Order property, verify registration |

## 📊 Current Status
- **Version:** 1.7.22
- **Tests:** 647 passing (100%) - NUnit framework
- **Build:** 0 warnings
- **Example:** `examples/SimpleMcpServer/` (5 tools + 2 prompts)

## 🛠️ Tool Development Essentials

**Base Classes:**
- `McpToolBase<TParams, TResult>` - Standard tools with automatic validation
- `DisposableToolBase<TParams, TResult>` - Tools with resources (DB, files, HTTP)

**Validation Helpers (built-in):**
- `ValidateRequired(value, paramName)` - Null/empty checks
- `ValidateRange(value, min, max, paramName)` - Numeric ranges
- `ValidatePositive(value, paramName)` - Positive numbers only
- `ValidateNotEmpty(collection, paramName)` - Non-empty collections

**Override Points:**
- `ExecuteAsync()` - Main implementation (required)
- `ErrorMessages` - Custom error messages with recovery steps
- `TokenBudget` - Per-tool token limits
- `ToolSpecificMiddleware` - Lifecycle hooks (logging, validation)

## ⚡ Performance Essentials

**Cache Configuration:**
```csharp
options.MaxCacheSize = 10000; // Entries limit
options.EvictionStrategy = CacheEvictionStrategy.LRU; // LRU recommended
options.MaxMemoryBytes = 50 * 1024 * 1024; // Memory limit
options.EnableFileWatching = true; // Auto-invalidate on changes
```

**Concurrent Processing:**
```csharp
// Instead of slow sequential:
foreach (var item in items) await ProcessAsync(item);

// Use concurrent:
await ConcurrentAsyncUtilities.ExecuteConcurrentlyAsync(items, ProcessAsync, maxConcurrency: 10);
```

## 🎨 Advanced Features

**Visualization:** Implement `IVisualizationProvider` for rich UI data
**Prompts:** Use `PromptBase` with `{{variable}}` substitution
**Middleware:** Order matters - TypeVerification(5), TDD(10), Logging(100)

## 📁 Project Structure
```
COA.Mcp.Framework/
├── Base/                    # Tool base classes with validation
├── Server/                  # Server infrastructure
├── Pipeline/Middleware/     # TypeVerification, TDD enforcement
├── Services/               # Cache management, utilities
├── Configuration/          # Options and settings
└── examples/               # Working examples
```

---
**Remember**: Framework changes require rebuild + repack + consumer update!
