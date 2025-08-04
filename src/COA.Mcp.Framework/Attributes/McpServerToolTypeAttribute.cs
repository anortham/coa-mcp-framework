using System;

namespace COA.Mcp.Framework.Attributes;

/// <summary>
/// Marks a class as containing MCP server tools. Classes with this attribute
/// will be scanned for methods decorated with <see cref="McpServerToolAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class McpServerToolTypeAttribute : Attribute
{
    /// <summary>
    /// Optional description of the tool group.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional category for grouping related tools.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Whether to automatically register this tool type during discovery.
    /// Default is true.
    /// </summary>
    public bool AutoRegister { get; set; } = true;
}