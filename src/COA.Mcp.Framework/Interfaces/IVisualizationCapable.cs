using COA.Mcp.Framework.Models;

namespace COA.Mcp.Framework.Interfaces;

/// <summary>
/// Interface for tools that support visualization in VS Code
/// </summary>
public interface IVisualizationCapable
{
    /// <summary>
    /// Gets the default visualization configuration for this tool
    /// </summary>
    /// <returns>Visualization configuration</returns>
    VisualizationConfig GetDefaultVisualizationConfig();
}