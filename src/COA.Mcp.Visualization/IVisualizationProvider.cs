namespace COA.Mcp.Visualization;

/// <summary>
/// Interface for MCP tools that can provide visualization hints for their data
/// </summary>
public interface IVisualizationProvider
{
    /// <summary>
    /// Gets the visualization descriptor for the tool's output
    /// </summary>
    /// <returns>Visualization descriptor with data and display hints</returns>
    VisualizationDescriptor? GetVisualizationDescriptor();
}

/// <summary>
/// Interface for parameters that can override visualization settings
/// </summary>
public interface IVisualizableParameters
{
    /// <summary>
    /// Whether to show the results in VS Code (null = use tool default)
    /// </summary>
    bool? ShowInVSCode { get; set; }
    
    /// <summary>
    /// Preferred view type for visualization (null = use tool default)
    /// Options: "grid", "tree", "chart", "markdown", "timeline", etc.
    /// </summary>
    string? VSCodeView { get; set; }
    
    /// <summary>
    /// Whether to navigate to the first result automatically
    /// </summary>
    bool? NavigateToFirstResult { get; set; }
}