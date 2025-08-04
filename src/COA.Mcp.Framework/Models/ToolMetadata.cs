using System;
using System.Collections.Generic;
using System.Reflection;
using COA.Mcp.Framework.Interfaces;

namespace COA.Mcp.Framework;

/// <summary>
/// Metadata about an MCP tool.
/// </summary>
public class ToolMetadata
{
    /// <summary>
    /// Gets or sets the tool name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the tool description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets or sets the tool category.
    /// </summary>
    public ToolCategory Category { get; init; } = ToolCategory.General;

    /// <summary>
    /// Gets or sets the tool version.
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// Gets or sets whether the tool is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets or sets the timeout in milliseconds (0 for no timeout).
    /// </summary>
    public int TimeoutMs { get; init; } = 0;

    /// <summary>
    /// Gets or sets the type containing the tool.
    /// </summary>
    public Type? DeclaringType { get; init; }

    /// <summary>
    /// Gets or sets the method info for the tool.
    /// </summary>
    public MethodInfo? Method { get; init; }

    /// <summary>
    /// Gets or sets the parameter type for the tool.
    /// </summary>
    public Type? ParameterType { get; init; }

    /// <summary>
    /// Gets or sets the parameter schema.
    /// </summary>
    public ParameterSchema? Parameters { get; init; }

    /// <summary>
    /// Gets or sets additional metadata.
    /// </summary>
    public Dictionary<string, object?> AdditionalMetadata { get; init; } = new();

    /// <summary>
    /// Gets or sets tool examples.
    /// </summary>
    public List<string> Examples { get; init; } = new();

    /// <summary>
    /// Gets or sets tool prerequisites.
    /// </summary>
    public string? Prerequisites { get; init; }

    /// <summary>
    /// Gets or sets tool use cases.
    /// </summary>
    public string? UseCases { get; init; }

    /// <summary>
    /// Gets or sets tool warnings.
    /// </summary>
    public string? Warnings { get; init; }

    /// <summary>
    /// Gets or sets the tool instance (if already instantiated).
    /// </summary>
    public ITool? ToolInstance { get; init; }
}

/// <summary>
/// Schema information for tool parameters.
/// </summary>
public class ParameterSchema
{
    /// <summary>
    /// Gets or sets the schema format (e.g., "json-schema").
    /// </summary>
    public string Format { get; init; } = "json-schema";

    /// <summary>
    /// Gets or sets the schema definition.
    /// </summary>
    public Dictionary<string, object?> Schema { get; set; } = new();

    /// <summary>
    /// Gets or sets parameter properties.
    /// </summary>
    public Dictionary<string, ParameterProperty> Properties { get; init; } = new();

    /// <summary>
    /// Gets or sets required parameter names.
    /// </summary>
    public List<string> Required { get; init; } = new();
}

/// <summary>
/// Information about a tool parameter property.
/// </summary>
public class ParameterProperty
{
    /// <summary>
    /// Gets or sets the property name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the property type.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets or sets the property description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets or sets whether the property is required.
    /// </summary>
    public bool Required { get; init; }

    /// <summary>
    /// Gets or sets the default value.
    /// </summary>
    public object? DefaultValue { get; init; }

    /// <summary>
    /// Gets or sets validation constraints.
    /// </summary>
    public Dictionary<string, object?> Constraints { get; init; } = new();

    /// <summary>
    /// Gets or sets examples for this property.
    /// </summary>
    public List<object?> Examples { get; init; } = new();
}