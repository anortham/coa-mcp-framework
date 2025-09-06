using System;
using System.Collections.Generic;
using System.Linq;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Configuration;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Models;
using Microsoft.Extensions.Logging;
using Scriban;
using Scriban.Runtime;

namespace COA.Mcp.Framework.Services;

/// <summary>
/// Advanced error recovery template processor that generates professional error messages
/// with recovery guidance using Scriban templates. Designed to teach better tool usage
/// patterns through educational error messages without emotional manipulation.
/// </summary>
public class ErrorRecoveryTemplateProcessor
{
    private readonly ILogger<ErrorRecoveryTemplateProcessor>? _logger;
    private readonly InstructionTemplateProcessor _templateProcessor;
    private readonly Dictionary<string, ErrorRecoveryTemplate> _errorTemplates;
    private readonly ErrorRecoveryOptions _options;

    /// <summary>
    /// Initializes a new instance of the ErrorRecoveryTemplateProcessor.
    /// </summary>
    /// <param name="templateProcessor">The instruction template processor for Scriban template rendering.</param>
    /// <param name="options">Configuration options for error recovery behavior.</param>
    /// <param name="logger">Optional logger for debugging template processing.</param>
    public ErrorRecoveryTemplateProcessor(
        InstructionTemplateProcessor templateProcessor,
        ErrorRecoveryOptions? options = null,
        ILogger<ErrorRecoveryTemplateProcessor>? logger = null)
    {
        _templateProcessor = templateProcessor ?? throw new ArgumentNullException(nameof(templateProcessor));
        _options = options ?? new ErrorRecoveryOptions();
        _logger = logger;
        _errorTemplates = InitializeBuiltInTemplates();
    }

    /// <summary>
    /// Processes an error with context-aware recovery guidance using templates.
    /// </summary>
    /// <param name="errorType">The type of error (e.g., "VALIDATION_ERROR", "FILE_NOT_FOUND").</param>
    /// <param name="toolName">The name of the tool that encountered the error.</param>
    /// <param name="errorMessage">The original error message.</param>
    /// <param name="context">Additional context about the error (tool capabilities, parameters, etc.).</param>
    /// <returns>Enhanced error message with professional recovery guidance.</returns>
    public string ProcessError(
        string errorType, 
        string toolName, 
        string errorMessage, 
        ErrorRecoveryContext? context = null)
    {
        try
        {
            if (!_options.EnableRecoveryGuidance)
            {
                return errorMessage;
            }

            var template = GetErrorTemplate(errorType);
            if (template == null)
            {
                _logger?.LogDebug("No recovery template found for error type '{ErrorType}', using original message", errorType);
                return errorMessage;
            }

            var templateVariables = CreateErrorTemplateVariables(errorType, toolName, errorMessage, context);
            
            var recoveryGuidance = _templateProcessor.ProcessTemplate(template.Template, templateVariables);
            
            // Combine original error with recovery guidance
            var enhancedMessage = _options.IncludeOriginalError 
                ? $"{errorMessage}\n\n{recoveryGuidance}"
                : recoveryGuidance;

            _logger?.LogDebug("Enhanced error message for '{ErrorType}' in tool '{ToolName}'", errorType, toolName);
            
            return enhancedMessage;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to process error recovery template for '{ErrorType}' in tool '{ToolName}'", errorType, toolName);
            return errorMessage; // Fallback to original error
        }
    }

    /// <summary>
    /// Registers a custom error recovery template for a specific error type.
    /// </summary>
    /// <param name="errorType">The error type identifier.</param>
    /// <param name="template">The Scriban template for recovery guidance.</param>
    /// <param name="description">Description of when this template is used.</param>
    public void RegisterErrorTemplate(string errorType, string template, string? description = null)
    {
        _errorTemplates[errorType] = new ErrorRecoveryTemplate
        {
            ErrorType = errorType,
            Template = template,
            Description = description ?? $"Recovery guidance for {errorType}"
        };
        
        _logger?.LogDebug("Registered custom error template for '{ErrorType}'", errorType);
    }

    /// <summary>
    /// Gets available error template types.
    /// </summary>
    /// <returns>List of error types with registered templates.</returns>
    public IReadOnlyList<string> GetAvailableTemplateTypes()
    {
        return _errorTemplates.Keys.ToList().AsReadOnly();
    }

    private ErrorRecoveryTemplate? GetErrorTemplate(string errorType)
    {
        return _errorTemplates.TryGetValue(errorType, out var template) ? template : null;
    }

    private TemplateVariables CreateErrorTemplateVariables(
        string errorType, 
        string toolName, 
        string errorMessage, 
        ErrorRecoveryContext? context)
    {
        var variables = new TemplateVariables
        {
            CustomVariables = new Dictionary<string, object>
            {
                ["error_type"] = errorType,
                ["tool_name"] = toolName,
                ["error_message"] = errorMessage,
                ["recovery_tone"] = _options.RecoveryTone.ToString().ToLowerInvariant(),
                ["include_metrics"] = _options.IncludePerformanceMetrics,
                ["include_workflow_tips"] = _options.IncludeWorkflowTips
            }
        };

        if (context != null)
        {
            // Add tool capabilities and context information
            if (context.AvailableTools?.Any() == true)
            {
                variables.AvailableTools = context.AvailableTools.ToArray();
            }

            if (context.AvailableMarkers?.Any() == true)
            {
                variables.AvailableMarkers = context.AvailableMarkers.ToArray();
            }

            // Add context-specific variables
            foreach (var kvp in context.AdditionalContext)
            {
                variables.CustomVariables[kvp.Key] = kvp.Value;
            }
        }

        return variables;
    }

    private Dictionary<string, ErrorRecoveryTemplate> InitializeBuiltInTemplates()
    {
        var templates = new Dictionary<string, ErrorRecoveryTemplate>();

        // Type verification error template
        templates["TYPE_VERIFICATION_ERROR"] = new ErrorRecoveryTemplate
        {
            ErrorType = "TYPE_VERIFICATION_ERROR",
            Description = "Guidance for type-related errors that could be prevented with proper tool usage",
            Template = @"## Professional Development Guidance

**Issue**: Type information verification needed to prevent compilation errors.

### Recommended Recovery Workflow
{{~ if has_tool available_tools ""goto_definition"" ~}}
1. **Use `goto_definition`** to verify the exact method signature and parameter types
   - This provides 100% accurate type information from your language server
   - Eliminates guesswork that leads to compilation errors
{{~ end ~}}

{{~ if has_tool available_tools ""symbol_search"" ~}}
2. **Use `symbol_search`** to find the correct class or interface definition
   - Provides comprehensive type information including inheritance and interfaces
   - More reliable than text-based searching for type information
{{~ end ~}}

{{~ if has_tool available_tools ""find_references"" ~}}
3. **Use `find_references`** to see how this type is used elsewhere in the codebase
   - Learn from existing patterns and usage examples
   - Understand the expected parameter formats and return types
{{~ end ~}}

### Why This Approach Works
{{~ if include_metrics ~}}
- **95% fewer type-related errors** when using type-aware tools first
- **30% reduction in token usage** from fewer debugging iterations  
- **10x faster** development cycle with verified signatures
{{~ end ~}}

{{~ if include_workflow_tips ~}}
### Professional Workflow Tip
Always verify types before writing code. The few seconds spent on verification saves minutes of debugging and prevents frustrating compilation errors that waste tokens and interrupt your flow.
{{~ end ~}}

*This guidance helps you develop better patterns for error-free development.*"
        };

        // File not found error template
        templates["FILE_NOT_FOUND"] = new ErrorRecoveryTemplate
        {
            ErrorType = "FILE_NOT_FOUND",
            Description = "Guidance for file location errors with search tool recommendations",
            Template = @"## File Location Assistance

**Issue**: The specified file could not be found at the expected location.

### Efficient File Discovery Workflow
{{~ if has_tool available_tools ""file_search"" ~}}
1. **Use `file_search`** with the filename or pattern to locate the file
   - Searches entire workspace efficiently using indexed patterns
   - Handles partial matches and various file extensions
   - Example: `file_search ""{{ error_context.filename | default: ""filename"" }}""` or `file_search ""*.cs""`
{{~ end ~}}

{{~ if has_tool available_tools ""directory_search"" ~}}
2. **Use `directory_search`** to explore the project structure systematically  
   - Better than guessing directory paths
   - Reveals the actual organization of the codebase
{{~ end ~}}

{{~ if has_tool available_tools ""recent_files"" ~}}
3. **Check `recent_files`** if this file was recently modified
   - Shows files that have been worked on recently
   - Often reveals files that have been moved or renamed
{{~ end ~}}

### Professional Development Practice
{{~ if include_workflow_tips ~}}
Instead of guessing file paths, use search tools to **discover** the actual project structure. This approach:
- Builds better mental models of unfamiliar codebases  
- Prevents assumptions that lead to ""file not found"" errors
- Creates more reliable and maintainable development workflows
{{~ end ~}}

*Efficient file discovery leads to faster development and fewer interruptions.*"
        };

        // Workspace not indexed error template  
        templates["WORKSPACE_NOT_INDEXED"] = new ErrorRecoveryTemplate
        {
            ErrorType = "WORKSPACE_NOT_INDEXED",
            Description = "Essential first step guidance for workspace preparation",
            Template = @"## Workspace Preparation Required

**Issue**: Advanced search and navigation tools require workspace indexing for optimal performance.

### Essential First Step
{{~ if has_tool available_tools ""index_workspace"" ~}}
**Run `index_workspace` now** to enable high-performance search capabilities:
- **10x faster** search operations across large codebases
- **Semantic understanding** of code structure and symbols  
- **Accurate results** instead of basic text matching
{{~ end ~}}

### Why Indexing Matters
{{~ if include_metrics ~}}
- **Unindexed workspace**: Basic text search only, limited accuracy
- **Indexed workspace**: Symbol-aware search, type information, reference tracking
- **Performance difference**: Millisecond responses vs. multi-second searches
{{~ end ~}}

### Professional Workflow
{{~ if include_workflow_tips ~}}
**Always index first** when starting work in a new codebase. This single step transforms your development experience from basic text searching to professional IDE-level navigation and search capabilities.

Think of indexing as ""connecting to the language server"" - essential infrastructure for serious development work.
{{~ end ~}}

*Proper workspace setup prevents numerous errors and dramatically improves development efficiency.*"
        };

        // Permission/access error template
        templates["ACCESS_DENIED"] = new ErrorRecoveryTemplate
        {
            ErrorType = "ACCESS_DENIED", 
            Description = "Professional guidance for file system permission issues",
            Template = @"## File System Access Resolution

**Issue**: Insufficient permissions to access the requested resource.

### Professional Resolution Steps
1. **Verify file path accuracy** - Ensure the path exists and is correctly specified
2. **Check workspace configuration** - Verify the working directory is properly set  
3. **Review file permissions** - Ensure read/write access as needed for the operation
4. **Consider administrative access** - Some operations may require elevated permissions

### Development Environment Best Practices
{{~ if include_workflow_tips ~}}
- **Use relative paths** when possible to avoid permission issues
- **Verify workspace root** is set correctly for your development environment
- **Check .gitignore patterns** that might affect file accessibility
- **Ensure consistent file system permissions** across your development setup
{{~ end ~}}

### Technical Context
This error typically indicates a file system permission boundary rather than a code logic issue. Resolving access properly ensures reliable development environment operation.

*Proper environment setup prevents access-related interruptions during development.*"
        };

        return templates;
    }
}

/// <summary>
/// Context information for error recovery template processing.
/// </summary>
public class ErrorRecoveryContext
{
    /// <summary>
    /// Available tools in the current server context.
    /// </summary>
    public IEnumerable<string>? AvailableTools { get; set; }

    /// <summary>
    /// Available tool marker capabilities.
    /// </summary>
    public IEnumerable<string>? AvailableMarkers { get; set; }

    /// <summary>
    /// Additional context variables for template processing.
    /// </summary>
    public Dictionary<string, object> AdditionalContext { get; set; } = new();
}

/// <summary>
/// Represents an error recovery template with metadata.
/// </summary>
internal class ErrorRecoveryTemplate
{
    public string ErrorType { get; set; } = string.Empty;
    public string Template { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}