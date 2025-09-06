using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using COA.Mcp.Framework.Configuration;
using COA.Mcp.Framework.Interfaces;

namespace COA.Mcp.Framework.Services;

/// <summary>
/// Default implementation of IToolDescriptionProvider that provides context-aware
/// tool descriptions without manipulative language.
/// </summary>
public class DefaultToolDescriptionProvider : IToolDescriptionProvider
{
    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _descriptionOverrides;
    private readonly Dictionary<string, Func<ToolDescriptionContext?, string?>> _contextHandlers;
    private readonly ToolManagementConfiguration? _config;

    /// <summary>
    /// Initializes a new instance of the DefaultToolDescriptionProvider class.
    /// </summary>
    public DefaultToolDescriptionProvider()
    {
        _descriptionOverrides = new ConcurrentDictionary<string, Dictionary<string, string>>();
        _contextHandlers = new Dictionary<string, Func<ToolDescriptionContext?, string?>>();
        
        // Register default context-aware descriptions
        RegisterDefaultDescriptions();
    }

    /// <summary>
    /// Initializes a new instance of the DefaultToolDescriptionProvider class with configuration.
    /// </summary>
    /// <param name="config">Tool management configuration.</param>
    public DefaultToolDescriptionProvider(ToolManagementConfiguration config) : this()
    {
        _config = config;
    }

    /// <summary>
    /// Gets a context-enhanced description for the specified tool.
    /// </summary>
    /// <param name="toolName">The name of the tool to describe.</param>
    /// <param name="context">Optional context information for customizing the description.</param>
    /// <returns>Enhanced description or null to use the default tool description.</returns>
    public string? GetEnhancedDescription(string toolName, ToolDescriptionContext? context = null)
    {
        if (string.IsNullOrEmpty(toolName))
            return null;

        // First check for context-specific overrides
        if (context?.Scenario != null && _descriptionOverrides.TryGetValue(toolName, out var overrides))
        {
            if (overrides.TryGetValue(context.Scenario, out var contextDescription))
            {
                return contextDescription;
            }
        }

        // Check for general overrides
        if (_descriptionOverrides.TryGetValue(toolName, out var generalOverrides))
        {
            if (generalOverrides.TryGetValue("*", out var generalDescription))
            {
                return generalDescription;
            }
        }

        // Check for context-aware handlers
        if (_contextHandlers.TryGetValue(toolName, out var handler))
        {
            return handler(context);
        }

        return null; // Use default description
    }

    /// <summary>
    /// Registers a description override for a specific tool in a given context.
    /// </summary>
    /// <param name="toolName">The tool name to override.</param>
    /// <param name="description">The enhanced description to use.</param>
    /// <param name="context">Optional context where this override applies.</param>
    public void RegisterDescriptionOverride(string toolName, string description, string? context = null)
    {
        if (string.IsNullOrEmpty(toolName) || string.IsNullOrEmpty(description))
            return;

        var contextKey = context ?? "*";
        
        _descriptionOverrides.AddOrUpdate(toolName, 
            new Dictionary<string, string> { { contextKey, description } },
            (key, existing) =>
            {
                existing[contextKey] = description;
                return existing;
            });
    }

    /// <summary>
    /// Determines if a tool description should be enhanced based on current context.
    /// </summary>
    /// <param name="toolName">The tool name to check.</param>
    /// <param name="context">The current context.</param>
    /// <returns>True if the description should be enhanced, false to use default.</returns>
    public bool ShouldEnhanceDescription(string toolName, ToolDescriptionContext? context = null)
    {
        if (string.IsNullOrEmpty(toolName))
            return false;

        // Check configuration setting if available
        if (_config?.UseDefaultDescriptionProvider == false)
            return false;

        // Enhance if we have context-specific overrides
        if (context?.Scenario != null && _descriptionOverrides.TryGetValue(toolName, out var overrides))
        {
            if (overrides.ContainsKey(context.Scenario))
                return true;
        }

        // Enhance if we have general overrides or handlers
        return _descriptionOverrides.ContainsKey(toolName) || _contextHandlers.ContainsKey(toolName);
    }

    /// <summary>
    /// Registers a dynamic context handler for a tool that generates descriptions based on context.
    /// </summary>
    /// <param name="toolName">The tool name to register a handler for.</param>
    /// <param name="handler">Function that generates context-aware descriptions.</param>
    public void RegisterContextHandler(string toolName, Func<ToolDescriptionContext?, string?> handler)
    {
        if (string.IsNullOrEmpty(toolName) || handler == null)
            return;

        _contextHandlers[toolName] = handler;
    }

    private void RegisterDefaultDescriptions()
    {
        // Example: Context-aware description for search tools
        RegisterContextHandler("text_search", context =>
        {
            if (context?.AvailableTools?.Contains("symbol_search") == true)
            {
                return "Searches for text patterns in files. For better accuracy when looking for specific classes or methods, consider using symbol_search first to locate exact definitions.";
            }
            return null; // Use default description
        });

        RegisterContextHandler("file_search", context =>
        {
            if (context?.PreferConciseDescriptions == true)
            {
                return "Locates files by name pattern. Fast alternative to directory traversal.";
            }
            return null;
        });

        // Example: Expertise-level aware descriptions
        RegisterContextHandler("index_workspace", context =>
        {
            return context?.ExpertiseLevel switch
            {
                "beginner" => "Builds a searchable index of your workspace. This is usually the first step before using search tools and dramatically improves search speed and accuracy.",
                "expert" => "Initializes Lucene.NET index for the workspace. Required for optimal search performance.",
                _ => null
            };
        });
    }

    /// <summary>
    /// Gets all registered overrides for debugging and configuration purposes.
    /// </summary>
    /// <returns>Dictionary mapping tool names to their context-specific overrides.</returns>
    public Dictionary<string, Dictionary<string, string>> GetAllOverrides()
    {
        return _descriptionOverrides.ToDictionary(
            kvp => kvp.Key,
            kvp => new Dictionary<string, string>(kvp.Value)
        );
    }

    /// <summary>
    /// Clears all registered overrides and handlers.
    /// </summary>
    public void ClearAllOverrides()
    {
        _descriptionOverrides.Clear();
        _contextHandlers.Clear();
        RegisterDefaultDescriptions();
    }

    /// <summary>
    /// Transforms passive descriptions to imperative language.
    /// Example: "Searches for text" → "USE FIRST - Search for existing implementations"
    /// </summary>
    /// <param name="passiveDescription">The passive description to transform.</param>
    /// <param name="priority">Priority level (1-100) that determines the urgency prefix.</param>
    /// <returns>Transformed imperative description with appropriate urgency prefix.</returns>
    public static string TransformToImperative(string passiveDescription, int priority = 50)
    {
        if (string.IsNullOrWhiteSpace(passiveDescription))
            return passiveDescription;

        var prefix = priority switch
        {
            >= 90 => "CRITICAL - ",
            >= 80 => "USE FIRST - ",
            >= 70 => "RECOMMENDED - ",
            >= 60 => "PREFER - ",
            _ => ""
        };

        return $"{prefix}{passiveDescription}";
    }
}