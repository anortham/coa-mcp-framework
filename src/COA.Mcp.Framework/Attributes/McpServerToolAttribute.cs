using System;

namespace COA.Mcp.Framework.Attributes;

/// <summary>
/// Marks a method as an MCP server tool. The method must be public and return a Task&lt;object&gt;.
/// </summary>
/// <remarks>
/// This is a legacy pattern maintained for backward compatibility.
/// For new implementations, prefer inheriting from McpToolBase&lt;TParams, TResult&gt;
/// for better type safety, validation, and framework integration.
/// 
/// Methods marked with this attribute are automatically discovered when the containing
/// class is marked with [McpServerToolType] and builder.DiscoverTools() is called.
/// </remarks>
/// <example>
/// <code>
/// [McpServerToolType]
/// public class MyToolService
/// {
///     [McpServerTool("calculate")]
///     [Description("Performs calculations")]
///     public async Task&lt;object&gt; CalculateAsync(CalculateParams parameters)
///     {
///         // Implementation
///         return new { result = 42 };
///     }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class McpServerToolAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the tool that will be exposed to MCP clients.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="McpServerToolAttribute"/> class.
    /// </summary>
    /// <param name="name">The name of the tool.</param>
    public McpServerToolAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Tool name cannot be null or whitespace.", nameof(name));
        }

        Name = name;
    }

    /// <summary>
    /// Optional version of the tool. Defaults to "1.0.0".
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Whether this tool is enabled. Can be used to temporarily disable tools.
    /// Default is true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum execution time in milliseconds. 0 means no timeout.
    /// </summary>
    public int TimeoutMs { get; set; } = 0;
}