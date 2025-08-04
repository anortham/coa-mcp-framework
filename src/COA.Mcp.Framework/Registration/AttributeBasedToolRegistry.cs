using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using COA.Mcp.Framework.Exceptions;
using COA.Mcp.Framework.Interfaces;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.Registration;

/// <summary>
/// Implementation of tool registry that supports attribute-based tool discovery.
/// </summary>
public class AttributeBasedToolRegistry : IToolRegistry
{
    private readonly ConcurrentDictionary<string, ToolMetadata> _tools;
    private readonly ILogger<AttributeBasedToolRegistry>? _logger;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AttributeBasedToolRegistry"/> class.
    /// </summary>
    public AttributeBasedToolRegistry(ILogger<AttributeBasedToolRegistry>? logger = null)
    {
        _tools = new ConcurrentDictionary<string, ToolMetadata>(StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }

    /// <inheritdoc/>
    public void RegisterTool(ITool tool)
    {
        if (tool == null)
            throw new ArgumentNullException(nameof(tool));

        var metadata = new ToolMetadata
        {
            Name = tool.ToolName,
            Description = tool.Description,
            Category = tool.Category,
            ToolInstance = tool
        };

        RegisterTool(metadata);
    }

    /// <inheritdoc/>
    public void RegisterTool(ToolMetadata toolMetadata)
    {
        if (toolMetadata == null)
            throw new ArgumentNullException(nameof(toolMetadata));

        if (string.IsNullOrWhiteSpace(toolMetadata.Name))
            throw new ToolRegistrationException("Tool name cannot be null or empty.");

        lock (_lock)
        {
            if (_tools.ContainsKey(toolMetadata.Name))
            {
                throw new ToolRegistrationException(
                    $"Tool with name '{toolMetadata.Name}' is already registered.");
            }

            if (!_tools.TryAdd(toolMetadata.Name, toolMetadata))
            {
                throw new ToolRegistrationException(
                    $"Failed to register tool '{toolMetadata.Name}'.");
            }

            _logger?.LogInformation("Registered tool '{ToolName}' of type '{Type}'",
                toolMetadata.Name,
                toolMetadata.DeclaringType?.FullName ?? "Unknown");
        }
    }

    /// <inheritdoc/>
    public ITool? GetTool(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            return null;

        if (_tools.TryGetValue(toolName, out var metadata))
        {
            return metadata.ToolInstance;
        }

        return null;
    }

    /// <inheritdoc/>
    public ToolMetadata? GetToolMetadata(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            return null;

        return _tools.GetValueOrDefault(toolName);
    }

    /// <inheritdoc/>
    public IEnumerable<ITool> GetAllTools()
    {
        return _tools.Values
            .Where(m => m.ToolInstance != null)
            .Select(m => m.ToolInstance!)
            .ToList();
    }

    /// <inheritdoc/>
    public IEnumerable<ToolMetadata> GetAllToolMetadata()
    {
        return _tools.Values.ToList();
    }

    /// <inheritdoc/>
    public bool IsToolRegistered(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            return false;

        return _tools.ContainsKey(toolName);
    }

    /// <inheritdoc/>
    public bool UnregisterTool(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            return false;

        var removed = _tools.TryRemove(toolName, out var metadata);
        
        if (removed)
        {
            _logger?.LogInformation("Unregistered tool '{ToolName}'", toolName);
        }

        return removed;
    }

    /// <inheritdoc/>
    public IEnumerable<ITool> GetToolsByCategory(ToolCategory category)
    {
        return _tools.Values
            .Where(m => m.Category == category && m.ToolInstance != null)
            .Select(m => m.ToolInstance!)
            .ToList();
    }

    /// <summary>
    /// Registers multiple tools from metadata.
    /// </summary>
    public void RegisterTools(IEnumerable<ToolMetadata> toolMetadata)
    {
        foreach (var metadata in toolMetadata)
        {
            try
            {
                RegisterTool(metadata);
            }
            catch (ToolRegistrationException ex)
            {
                _logger?.LogWarning(ex, "Failed to register tool '{ToolName}'", metadata.Name);
            }
        }
    }

    /// <summary>
    /// Gets statistics about registered tools.
    /// </summary>
    public ToolRegistryStatistics GetStatistics()
    {
        var tools = _tools.Values.ToList();
        
        return new ToolRegistryStatistics
        {
            TotalTools = tools.Count,
            EnabledTools = tools.Count(t => t.Enabled),
            DisabledTools = tools.Count(t => !t.Enabled),
            ToolsByCategory = tools
                .GroupBy(t => t.Category)
                .ToDictionary(g => g.Key, g => g.Count()),
            ToolsWithInstances = tools.Count(t => t.ToolInstance != null),
            ToolsWithoutInstances = tools.Count(t => t.ToolInstance == null)
        };
    }

    /// <summary>
    /// Clears all registered tools.
    /// </summary>
    public void Clear()
    {
        _tools.Clear();
        _logger?.LogInformation("Cleared all registered tools");
    }
}

/// <summary>
/// Statistics about the tool registry.
/// </summary>
public class ToolRegistryStatistics
{
    /// <summary>
    /// Gets or sets the total number of registered tools.
    /// </summary>
    public int TotalTools { get; init; }

    /// <summary>
    /// Gets or sets the number of enabled tools.
    /// </summary>
    public int EnabledTools { get; init; }

    /// <summary>
    /// Gets or sets the number of disabled tools.
    /// </summary>
    public int DisabledTools { get; init; }

    /// <summary>
    /// Gets or sets the tool count by category.
    /// </summary>
    public Dictionary<ToolCategory, int> ToolsByCategory { get; init; } = new();

    /// <summary>
    /// Gets or sets the number of tools with instances.
    /// </summary>
    public int ToolsWithInstances { get; init; }

    /// <summary>
    /// Gets or sets the number of tools without instances.
    /// </summary>
    public int ToolsWithoutInstances { get; init; }
}