using System;

namespace COA.Mcp.Framework.Attributes;

/// <summary>
/// Marks a class as containing MCP server tools. Classes with this attribute
/// will be scanned for methods decorated with <see cref="McpServerToolAttribute"/>.
/// </summary>
/// <remarks>
/// This is a legacy pattern maintained for backward compatibility.
/// For new implementations, prefer inheriting from McpToolBase&lt;TParams, TResult&gt;
/// for better type safety, validation, and framework integration.
/// 
/// This attribute is automatically discovered when calling builder.DiscoverTools().
/// </remarks>
/// <example>
/// <code>
/// [McpServerToolType]
/// public class MyToolService
/// {
///     [McpServerTool("calculate")]
///     public async Task&lt;object&gt; CalculateAsync(CalculateParams parameters)
///     {
///         // Implementation
///     }
/// }
/// </code>
/// </example>
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