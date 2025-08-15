using System.ComponentModel;

namespace COA.Mcp.Framework.Models;

/// <summary>
/// Base class for parameters that support visualization control
/// </summary>
public abstract class VisualizableParameters
{
    /// <summary>
    /// Override tool default: whether to show in VS Code (null = use tool default)
    /// </summary>
    [Description("Override tool default: whether to show in VS Code (null = use tool default)")]
    public bool? ShowInVSCode { get; set; }
    
    /// <summary>
    /// Override tool default: preferred view type (null = use tool default)
    /// "auto", "grid", "chart", "markdown", "tree", "timeline"
    /// </summary>
    [Description("Override tool default: preferred view type (auto, grid, chart, markdown, tree, timeline)")]
    public string? VSCodeView { get; set; }
    
    /// <summary>
    /// Navigate to first result automatically when showing in VS Code
    /// </summary>
    [Description("Navigate to first result automatically when showing in VS Code")]
    public bool NavigateToFirstResult { get; set; } = false;
}