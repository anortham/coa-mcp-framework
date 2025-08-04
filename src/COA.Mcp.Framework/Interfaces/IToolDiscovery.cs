using System.Collections.Generic;
using System.Reflection;

namespace COA.Mcp.Framework.Interfaces;

/// <summary>
/// Service for discovering MCP tools through reflection.
/// </summary>
public interface IToolDiscovery
{
    /// <summary>
    /// Discovers tools in the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan for tools.</param>
    /// <returns>Collection of discovered tool metadata.</returns>
    IEnumerable<ToolMetadata> DiscoverTools(Assembly assembly);

    /// <summary>
    /// Discovers tools in multiple assemblies.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan for tools.</param>
    /// <returns>Collection of discovered tool metadata.</returns>
    IEnumerable<ToolMetadata> DiscoverTools(params Assembly[] assemblies);

    /// <summary>
    /// Discovers tools in the calling assembly.
    /// </summary>
    /// <returns>Collection of discovered tool metadata.</returns>
    IEnumerable<ToolMetadata> DiscoverToolsInCurrentAssembly();

    /// <summary>
    /// Discovers tools in all loaded assemblies.
    /// </summary>
    /// <param name="includeSystemAssemblies">Whether to include system assemblies in the scan.</param>
    /// <returns>Collection of discovered tool metadata.</returns>
    IEnumerable<ToolMetadata> DiscoverToolsInAllAssemblies(bool includeSystemAssemblies = false);

    /// <summary>
    /// Validates that a type contains valid MCP tools.
    /// </summary>
    /// <param name="type">The type to validate.</param>
    /// <returns>Validation results.</returns>
    ToolValidationResult ValidateToolType(Type type);

    /// <summary>
    /// Validates that a method is a valid MCP tool.
    /// </summary>
    /// <param name="method">The method to validate.</param>
    /// <returns>Validation results.</returns>
    ToolValidationResult ValidateToolMethod(MethodInfo method);
}