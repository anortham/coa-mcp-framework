using System.Collections.Generic;

namespace COA.Mcp.Framework.Interfaces;

/// <summary>
/// Registry for managing MCP tools.
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// Registers a tool with the registry.
    /// </summary>
    /// <param name="tool">The tool to register.</param>
    void RegisterTool(ITool tool);

    /// <summary>
    /// Registers a tool with metadata.
    /// </summary>
    /// <param name="toolMetadata">The tool metadata to register.</param>
    void RegisterTool(ToolMetadata toolMetadata);

    /// <summary>
    /// Gets a tool by name.
    /// </summary>
    /// <param name="toolName">The name of the tool.</param>
    /// <returns>The tool if found; otherwise, null.</returns>
    ITool? GetTool(string toolName);

    /// <summary>
    /// Gets tool metadata by name.
    /// </summary>
    /// <param name="toolName">The name of the tool.</param>
    /// <returns>The tool metadata if found; otherwise, null.</returns>
    ToolMetadata? GetToolMetadata(string toolName);

    /// <summary>
    /// Gets all registered tools.
    /// </summary>
    /// <returns>A collection of all registered tools.</returns>
    IEnumerable<ITool> GetAllTools();

    /// <summary>
    /// Gets all registered tool metadata.
    /// </summary>
    /// <returns>A collection of all registered tool metadata.</returns>
    IEnumerable<ToolMetadata> GetAllToolMetadata();

    /// <summary>
    /// Checks if a tool is registered.
    /// </summary>
    /// <param name="toolName">The name of the tool.</param>
    /// <returns>True if the tool is registered; otherwise, false.</returns>
    bool IsToolRegistered(string toolName);

    /// <summary>
    /// Unregisters a tool.
    /// </summary>
    /// <param name="toolName">The name of the tool to unregister.</param>
    /// <returns>True if the tool was unregistered; otherwise, false.</returns>
    bool UnregisterTool(string toolName);

    /// <summary>
    /// Gets tools by category.
    /// </summary>
    /// <param name="category">The category to filter by.</param>
    /// <returns>Tools in the specified category.</returns>
    IEnumerable<ITool> GetToolsByCategory(ToolCategory category);
}