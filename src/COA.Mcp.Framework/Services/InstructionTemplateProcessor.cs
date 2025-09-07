using System;
using System.Collections.Generic;
using System.Linq;
using COA.Mcp.Framework.Configuration;
using COA.Mcp.Framework.Interfaces;
using Microsoft.Extensions.Logging;
using Scriban;
using Scriban.Runtime;

namespace COA.Mcp.Framework.Services;

/// <summary>
/// Processes Scriban templates for dynamic instruction generation based on available
/// tools and their capabilities. This enables sophisticated conditional guidance
/// without hardcoded assumptions about tool availability.
/// </summary>
public class InstructionTemplateProcessor
{
    private readonly ILogger<InstructionTemplateProcessor>? _logger;
    private readonly Dictionary<string, Template> _templateCache;

    /// <summary>
    /// Initializes a new instance of the InstructionTemplateProcessor class.
    /// </summary>
    /// <param name="logger">Optional logger for template processing events.</param>
    public InstructionTemplateProcessor(ILogger<InstructionTemplateProcessor>? logger = null)
    {
        _logger = logger;
        _templateCache = new Dictionary<string, Template>();
    }

    /// <summary>
    /// Processes a template string with the provided template variables.
    /// </summary>
    /// <param name="templateText">The Scriban template text to process.</param>
    /// <param name="variables">Variables to make available in the template.</param>
    /// <param name="cacheKey">Optional cache key for template compilation optimization.</param>
    /// <returns>The processed template result or error message.</returns>
    public string ProcessTemplate(string templateText, TemplateVariables variables, string? cacheKey = null)
    {
        if (string.IsNullOrWhiteSpace(templateText))
            return string.Empty;

        try
        {
            var template = GetOrCompileTemplate(templateText, cacheKey);
            var model = CreateTemplateModel(variables);
            
            return template.Render(model);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to process template: {Template}", templateText);
            return $"<!-- Template processing failed: {ex.Message} -->";
        }
    }

    /// <summary>
    /// Processes a pre-compiled template with the provided variables.
    /// </summary>
    /// <param name="template">The compiled Scriban template.</param>
    /// <param name="variables">Variables to make available in the template.</param>
    /// <returns>The processed template result or error message.</returns>
    public string ProcessTemplate(Template template, TemplateVariables variables)
    {
        try
        {
            var model = CreateTemplateModel(variables);
            return template.Render(model);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to process compiled template");
            return $"<!-- Template processing failed: {ex.Message} -->";
        }
    }

    /// <summary>
    /// Compiles a template string for repeated use.
    /// </summary>
    /// <param name="templateText">The template text to compile.</param>
    /// <param name="cacheKey">Optional cache key for the compiled template.</param>
    /// <returns>The compiled template or null if compilation failed.</returns>
    public Template? CompileTemplate(string templateText, string? cacheKey = null)
    {
        if (string.IsNullOrWhiteSpace(templateText))
            return null;

        try
        {
            return GetOrCompileTemplate(templateText, cacheKey);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to compile template: {Template}", templateText);
            return null;
        }
    }

    /// <summary>
    /// Clears the template compilation cache.
    /// </summary>
    public void ClearCache()
    {
        _templateCache.Clear();
        _logger?.LogDebug("Template cache cleared");
    }

    /// <summary>
    /// Gets the number of cached templates.
    /// </summary>
    public int CachedTemplateCount => _templateCache.Count;

    private Template GetOrCompileTemplate(string templateText, string? cacheKey)
    {
        if (!string.IsNullOrEmpty(cacheKey) && _templateCache.TryGetValue(cacheKey, out var cachedTemplate))
        {
            return cachedTemplate;
        }

        var template = Template.Parse(templateText);
        
        if (!string.IsNullOrEmpty(cacheKey))
        {
            _templateCache[cacheKey] = template;
        }

        return template;
    }

    private object CreateTemplateModel(TemplateVariables variables)
    {
        var model = new ScriptObject();
        
        // Import Scriban's built-in functions (array, object, string, etc.)
        model.Import(typeof(Scriban.Functions.ArrayFunctions));
        model.Import(typeof(Scriban.Functions.ObjectFunctions));
        model.Import(typeof(Scriban.Functions.StringFunctions));
        
        // Add available tools
        if (variables.AvailableTools != null)
        {
            model["available_tools"] = variables.AvailableTools;
        }

        // Add available markers  
        if (variables.AvailableMarkers != null)
        {
            model["available_markers"] = variables.AvailableMarkers;
        }

        // Add tool priorities
        if (variables.ToolPriorities != null)
        {
            model["tool_priorities"] = variables.ToolPriorities;
        }

        // Add workflow suggestions
        if (variables.WorkflowSuggestions != null)
        {
            model["workflow_suggestions"] = variables.WorkflowSuggestions;
        }

        // Add server information
        if (variables.ServerInfo != null)
        {
            model["server_info"] = variables.ServerInfo;
        }

        // Add custom variables
        if (variables.CustomVariables != null)
        {
            foreach (var kvp in variables.CustomVariables)
            {
                model[kvp.Key] = kvp.Value;
            }
        }

        // Add built-in tools awareness
        if (variables.BuiltInTools != null)
        {
            model["builtin_tools"] = variables.BuiltInTools;
        }

        // Add tool comparisons
        if (variables.ToolComparisons != null)
        {
            model["tool_comparisons"] = variables.ToolComparisons;
        }

        // Add enforcement level
        model["enforcement_level"] = variables.EnforcementLevel.ToString().ToLower();

        // Import template helper functions using Scriban's native Import mechanism
        model.Import(typeof(TemplateHelperFunctions));

        // Note: array_length removed - use array.size instead
        // Note: string_join removed - use string.join instead (from built-in functions)

        return model;
    }
}

/// <summary>
/// Contains variables that can be used in instruction templates for dynamic content generation.
/// </summary>
public class TemplateVariables
{
    /// <summary>
    /// Gets or sets the list of available tool names in the current server.
    /// </summary>
    public string[]? AvailableTools { get; set; }

    /// <summary>
    /// Gets or sets the list of available tool marker names (capabilities).
    /// </summary>
    public string[]? AvailableMarkers { get; set; }

    /// <summary>
    /// Gets or sets tool priority information for conditional recommendations.
    /// </summary>
    public Dictionary<string, int>? ToolPriorities { get; set; }

    /// <summary>
    /// Gets or sets workflow suggestions for the available tools.
    /// </summary>
    public WorkflowSuggestion[]? WorkflowSuggestions { get; set; }

    /// <summary>
    /// Gets or sets server information (name, version) for template customization.
    /// </summary>
    public object? ServerInfo { get; set; }

    /// <summary>
    /// Gets or sets custom variables for extended template functionality.
    /// </summary>
    public Dictionary<string, object>? CustomVariables { get; set; }

    /// <summary>
    /// Gets or sets the list of built-in Claude tools that may compete with server tools.
    /// Used for generating professional guidance about tool selection.
    /// </summary>
    public string[]? BuiltInTools { get; set; } = new[] { "Read", "Grep", "Bash", "Search", "WebSearch" };

    /// <summary>
    /// Gets or sets tool comparison data for generating professional guidance.
    /// Key = task description, Value = comparison info
    /// </summary>
    public Dictionary<string, ToolComparison>? ToolComparisons { get; set; }

    /// <summary>
    /// Gets or sets the workflow enforcement level for instructions.
    /// </summary>
    public WorkflowEnforcement EnforcementLevel { get; set; } = WorkflowEnforcement.Recommend;

    /// <summary>
    /// Creates template variables from available tools and their capabilities.
    /// </summary>
    /// <param name="tools">The available tools.</param>
    /// <param name="toolInstances">The actual tool instances for capability detection.</param>
    /// <returns>Template variables populated with tool information.</returns>
    public static TemplateVariables FromTools(IEnumerable<string> tools, IEnumerable<object>? toolInstances = null)
    {
        var variables = new TemplateVariables
        {
            AvailableTools = tools?.ToArray() ?? Array.Empty<string>()
        };

        if (toolInstances != null)
        {
            variables.AvailableMarkers = DetectAvailableMarkers(toolInstances);
            variables.ToolPriorities = ExtractToolPriorities(toolInstances);
        }

        return variables;
    }

    private static string[] DetectAvailableMarkers(IEnumerable<object> toolInstances)
    {
        var markers = new HashSet<string>();
        var markerTypes = typeof(IToolMarker).Assembly
            .GetTypes()
            .Where(t => t.IsInterface && typeof(IToolMarker).IsAssignableFrom(t))
            .Where(t => t != typeof(IToolMarker))
            .ToList();

        foreach (var tool in toolInstances)
        {
            foreach (var markerType in markerTypes)
            {
                if (markerType.IsAssignableFrom(tool.GetType()))
                {
                    markers.Add(markerType.Name);
                }
            }
        }

        return markers.ToArray();
    }

    private static Dictionary<string, int> ExtractToolPriorities(IEnumerable<object> toolInstances)
    {
        var priorities = new Dictionary<string, int>();

        foreach (var tool in toolInstances)
        {
            // Support both IToolPriority (existing) and IPrioritizedTool (new)
            int? priority = null;
            if (tool is IToolPriority legacyPriorityTool)
            {
                priority = legacyPriorityTool.Priority;
            }
            else if (tool is IPrioritizedTool prioritizedTool)
            {
                priority = prioritizedTool.Priority;
            }

            if (priority.HasValue)
            {
                var toolName = tool.GetType().Name;
                if (toolName.EndsWith("Tool", StringComparison.OrdinalIgnoreCase))
                {
                    toolName = toolName.Substring(0, toolName.Length - 4);
                }
                
                priorities[toolName] = priority.Value;
            }
        }

        return priorities;
    }
}