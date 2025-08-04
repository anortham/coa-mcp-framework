using System.Threading.Tasks;

namespace COA.Mcp.Framework.Interfaces;

/// <summary>
/// Represents an MCP tool that can be executed.
/// </summary>
public interface ITool
{
    /// <summary>
    /// Gets the name of the tool.
    /// </summary>
    string ToolName { get; }

    /// <summary>
    /// Gets the description of the tool.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the category of the tool.
    /// </summary>
    ToolCategory Category { get; }

    /// <summary>
    /// Executes the tool with the provided parameters.
    /// </summary>
    /// <param name="parameters">The parameters for the tool execution.</param>
    /// <returns>The result of the tool execution.</returns>
    Task<object> ExecuteAsync(object parameters);
}

/// <summary>
/// Tool categories for grouping and organization.
/// </summary>
public enum ToolCategory
{
    /// <summary>
    /// General purpose tools.
    /// </summary>
    General,

    /// <summary>
    /// Query and search tools.
    /// </summary>
    Query,

    /// <summary>
    /// Analysis and inspection tools.
    /// </summary>
    Analysis,

    /// <summary>
    /// Modification and refactoring tools.
    /// </summary>
    Modification,

    /// <summary>
    /// Reporting and documentation tools.
    /// </summary>
    Reporting,

    /// <summary>
    /// System and infrastructure tools.
    /// </summary>
    System,

    /// <summary>
    /// Custom category for specialized tools.
    /// </summary>
    Custom
}