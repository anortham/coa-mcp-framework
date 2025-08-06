namespace COA.Mcp.Framework;

/// <summary>
/// Defines the category of an MCP tool for organization and discovery.
/// </summary>
public enum ToolCategory
{
    /// <summary>
    /// General purpose tools that don't fit other categories.
    /// </summary>
    General = 0,

    /// <summary>
    /// Tools that perform queries or searches.
    /// </summary>
    Query = 1,

    /// <summary>
    /// Tools that analyze code or data.
    /// </summary>
    Analysis = 2,

    /// <summary>
    /// Tools that generate code, content, or artifacts.
    /// </summary>
    Generation = 3,

    /// <summary>
    /// Tools that transform or refactor code.
    /// </summary>
    Refactoring = 4,

    /// <summary>
    /// Tools that perform validation or verification.
    /// </summary>
    Validation = 5,

    /// <summary>
    /// Tools that handle documentation.
    /// </summary>
    Documentation = 6,

    /// <summary>
    /// Tools that manage configuration.
    /// </summary>
    Configuration = 7,

    /// <summary>
    /// Tools that provide diagnostics or debugging capabilities.
    /// </summary>
    Diagnostics = 8,

    /// <summary>
    /// Tools that handle testing functionality.
    /// </summary>
    Testing = 9,

    /// <summary>
    /// Tools that manage deployment or publishing.
    /// </summary>
    Deployment = 10,

    /// <summary>
    /// Tools that handle security-related operations.
    /// </summary>
    Security = 11,

    /// <summary>
    /// Tools that manage resources or storage.
    /// </summary>
    Resources = 12,

    /// <summary>
    /// Tools that handle integration with external systems.
    /// </summary>
    Integration = 13,

    /// <summary>
    /// Tools that provide monitoring or observability.
    /// </summary>
    Monitoring = 14,

    /// <summary>
    /// Tools that handle utility or helper functions.
    /// </summary>
    Utility = 15
}