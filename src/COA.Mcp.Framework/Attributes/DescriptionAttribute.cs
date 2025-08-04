using System;

namespace COA.Mcp.Framework.Attributes;

/// <summary>
/// Provides a description for tools and parameters that will be exposed to MCP clients.
/// This helps AI agents understand how to use the tool effectively.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Class, Inherited = true)]
public sealed class DescriptionAttribute : Attribute
{
    /// <summary>
    /// Gets the description text.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DescriptionAttribute"/> class.
    /// </summary>
    /// <param name="description">The description text.</param>
    public DescriptionAttribute(string description)
    {
        Description = description ?? throw new ArgumentNullException(nameof(description));
    }

    /// <summary>
    /// Optional examples of how to use the tool or parameter.
    /// </summary>
    public string[]? Examples { get; set; }

    /// <summary>
    /// Optional prerequisites or requirements for using this tool.
    /// </summary>
    public string? Prerequisites { get; set; }

    /// <summary>
    /// Optional use cases where this tool is most applicable.
    /// </summary>
    public string? UseCases { get; set; }

    /// <summary>
    /// Optional warnings or limitations.
    /// </summary>
    public string? Warnings { get; set; }
}