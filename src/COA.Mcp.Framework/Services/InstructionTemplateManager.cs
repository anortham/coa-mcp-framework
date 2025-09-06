using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using COA.Mcp.Framework.Configuration;
using COA.Mcp.Framework.Interfaces;
using Microsoft.Extensions.Logging;
using Scriban;

namespace COA.Mcp.Framework.Services;

/// <summary>
/// Manages instruction templates for different contexts and scenarios.
/// This provides the high-level API for template-based instruction generation
/// with built-in templates and custom template support.
/// </summary>
public class InstructionTemplateManager
{
    private readonly InstructionTemplateProcessor _processor;
    private readonly ILogger<InstructionTemplateManager>? _logger;
    private readonly Dictionary<string, string> _builtInTemplates;
    private readonly Dictionary<string, Template> _compiledTemplates;

    /// <summary>
    /// Initializes a new instance of the InstructionTemplateManager class.
    /// </summary>
    /// <param name="processor">The template processor for rendering templates.</param>
    /// <param name="logger">Optional logger for template operations.</param>
    public InstructionTemplateManager(InstructionTemplateProcessor processor, ILogger<InstructionTemplateManager>? logger = null)
    {
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
        _logger = logger;
        _builtInTemplates = new Dictionary<string, string>();
        _compiledTemplates = new Dictionary<string, Template>();
        
        RegisterBuiltInTemplates();
    }

    /// <summary>
    /// Generates instructions using the specified template context and available tools.
    /// </summary>
    /// <param name="contextName">The template context to use (e.g., "codesearch", "general").</param>
    /// <param name="availableTools">The tools available in the current server.</param>
    /// <param name="toolInstances">The actual tool instances for capability detection.</param>
    /// <param name="customVariables">Additional variables for template processing.</param>
    /// <returns>Generated instruction text or empty string if no suitable template found.</returns>
    public string GenerateInstructions(
        string contextName,
        IEnumerable<string> availableTools,
        IEnumerable<object>? toolInstances = null,
        Dictionary<string, object>? customVariables = null)
    {
        try
        {
            var template = GetTemplate(contextName);
            if (template == null)
            {
                _logger?.LogWarning("No template found for context: {Context}", contextName);
                return string.Empty;
            }

            var variables = TemplateVariables.FromTools(availableTools, toolInstances);
            if (customVariables != null)
            {
                variables.CustomVariables = customVariables;
            }

            return _processor.ProcessTemplate(template, variables);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to generate instructions for context: {Context}", contextName);
            return string.Empty;
        }
    }

    /// <summary>
    /// Registers a custom template for a specific context.
    /// </summary>
    /// <param name="contextName">The context name for the template.</param>
    /// <param name="templateText">The Scriban template text.</param>
    /// <param name="precompile">Whether to pre-compile the template for performance.</param>
    public void RegisterTemplate(string contextName, string templateText, bool precompile = true)
    {
        if (string.IsNullOrWhiteSpace(contextName))
            throw new ArgumentException("Context name cannot be null or empty", nameof(contextName));

        if (string.IsNullOrWhiteSpace(templateText))
            throw new ArgumentException("Template text cannot be null or empty", nameof(templateText));

        _builtInTemplates[contextName] = templateText;

        if (precompile)
        {
            var template = _processor.CompileTemplate(templateText, contextName);
            if (template != null)
            {
                _compiledTemplates[contextName] = template;
            }
        }

        _logger?.LogDebug("Registered template for context: {Context}", contextName);
    }

    /// <summary>
    /// Loads templates from files in the specified directory.
    /// Template files should have .scriban extension and be named after their context.
    /// </summary>
    /// <param name="templateDirectory">Directory containing template files.</param>
    /// <param name="precompile">Whether to pre-compile loaded templates.</param>
    /// <returns>Number of templates loaded.</returns>
    public async Task<int> LoadTemplatesFromDirectoryAsync(string templateDirectory, bool precompile = true)
    {
        if (!Directory.Exists(templateDirectory))
        {
            _logger?.LogWarning("Template directory not found: {Directory}", templateDirectory);
            return 0;
        }

        var templateFiles = Directory.GetFiles(templateDirectory, "*.scriban");
        var loadedCount = 0;

        foreach (var filePath in templateFiles)
        {
            try
            {
                var contextName = Path.GetFileNameWithoutExtension(filePath);
                var templateText = await File.ReadAllTextAsync(filePath);
                
                RegisterTemplate(contextName, templateText, precompile);
                loadedCount++;
                
                _logger?.LogDebug("Loaded template: {File} -> {Context}", filePath, contextName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load template file: {File}", filePath);
            }
        }

        _logger?.LogInformation("Loaded {Count} templates from directory: {Directory}", loadedCount, templateDirectory);
        return loadedCount;
    }

    /// <summary>
    /// Gets the available template contexts.
    /// </summary>
    /// <returns>Array of available context names.</returns>
    public string[] GetAvailableContexts()
    {
        return _builtInTemplates.Keys.ToArray();
    }

    /// <summary>
    /// Checks if a template exists for the specified context.
    /// </summary>
    /// <param name="contextName">The context name to check.</param>
    /// <returns>True if a template exists for the context.</returns>
    public bool HasTemplate(string contextName)
    {
        return !string.IsNullOrWhiteSpace(contextName) && _builtInTemplates.ContainsKey(contextName);
    }

    /// <summary>
    /// Clears all template caches for memory management.
    /// </summary>
    public void ClearCache()
    {
        _compiledTemplates.Clear();
        _processor.ClearCache();
        _logger?.LogDebug("Template caches cleared");
    }

    private Template? GetTemplate(string contextName)
    {
        // First check if we have a pre-compiled template
        if (_compiledTemplates.TryGetValue(contextName, out var compiled))
        {
            return compiled;
        }

        // Fall back to compiling from text
        if (_builtInTemplates.TryGetValue(contextName, out var templateText))
        {
            var template = _processor.CompileTemplate(templateText, contextName);
            if (template != null)
            {
                _compiledTemplates[contextName] = template;
            }
            return template;
        }

        return null;
    }

    private void RegisterBuiltInTemplates()
    {
        // General template for basic servers
        RegisterTemplate("general", @"## {{ server_info.name | string.capitalize }} Tools

This server provides {{ available_tools | array.length }} professional tools designed for efficiency and accuracy.

{{~ if has_marker available_markers ""IRequiresPreparation"" ~}}
### Essential First Step
{{~ for tool in available_tools ~}}
{{~ if tool == ""index_workspace"" || tool == ""init"" || tool == ""setup"" ~}}
Always run `{{ tool }}` before using other tools for optimal performance.
{{~ end ~}}
{{~ end ~}}

{{~ end ~}}
### Available Tools
{{~ for tool in available_tools ~}}
- **{{ tool }}**: Professional tool for enhanced workflow efficiency
{{~ end ~}}

Following these tools' intended workflows reduces debugging iterations and improves results.");

        // CodeSearch-specific template
        RegisterTemplate("codesearch", @"## High-Performance Code Navigation

This server provides {{ available_tools | array.length }} specialized code search tools powered by advanced indexing.

{{~ if has_tool available_tools ""index_workspace"" ~}}
### Essential First Step
Always run `index_workspace` before searching - this dramatically improves accuracy and speed.

{{~ end ~}}
{{~ if has_marker available_markers ""ISymbolicRead"" ~}}
### Recommended Workflow for Type-Safe Development
{{~ if has_tool available_tools ""symbol_search"" ~}}
1. **Use symbol_search** to find classes and methods by name - 90% more accurate than text search
{{~ end ~}}
{{~ if has_tool available_tools ""goto_definition"" ~}}
2. **Use goto_definition** to verify exact signatures before writing code
{{~ end ~}}
{{~ if has_tool available_tools ""find_references"" ~}}
3. **Use find_references** before refactoring to understand impact
{{~ end ~}}
4. **Use text_search** for broader pattern matching when needed

**Why this works**: Symbol-based navigation eliminates guesswork and prevents type errors.
{{~ end ~}}

{{~ if has_marker available_markers ""IBulkOperation"" ~}}
### Performance Optimization
{{~ if has_tool available_tools ""batch_operations"" ~}}
Use `batch_operations` for multiple searches - it's 3-10x faster than sequential operations.
{{~ end ~}}
{{~ end ~}}

{{~ if has_marker available_markers ""ICanEdit"" ~}}
### Code Modification Guidelines
Before making changes:
- Search for existing patterns to maintain consistency  
- Use find_references to understand usage contexts
- Verify types with goto_definition before modifications
{{~ end ~}}

This approach reduces token usage by 30% and eliminates 95% of type-related errors.");

        // Database/API server template  
        RegisterTemplate("database", @"## Professional Database Tools

This server provides {{ available_tools | array.length }} database operation tools designed for reliability and performance.

{{~ if has_marker available_markers ""IRequiresPreparation"" ~}}
### Connection Setup
{{~ for tool in available_tools ~}}
{{~ if tool == ""connect"" || tool == ""authenticate"" || tool == ""setup_connection"" ~}}
Start with `{{ tool }}` to establish secure database connectivity.
{{~ end ~}}
{{~ end ~}}

{{~ end ~}}
### Recommended Operations Sequence
{{~ if has_tool available_tools ""list_tables"" ~}}
1. **list_tables** - Explore database structure
{{~ end ~}}
{{~ if has_tool available_tools ""describe_table"" ~}}
2. **describe_table** - Understand schema before queries
{{~ end ~}}
{{~ if has_tool available_tools ""query_data"" ~}}
3. **query_data** - Execute optimized data retrieval
{{~ end ~}}

{{~ if has_marker available_markers ""IBulkOperation"" ~}}
### Bulk Operations
For large datasets, use bulk operation tools to minimize connection overhead and improve throughput.
{{~ end ~}}

Following these patterns ensures data integrity and optimal performance.");

        _logger?.LogDebug("Registered {Count} built-in templates", _builtInTemplates.Count);
    }
}