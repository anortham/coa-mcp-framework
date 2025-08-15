namespace COA.Mcp.Framework.Models;

/// <summary>
/// Configuration for tool visualization behavior
/// </summary>
public class VisualizationConfig
{
    /// <summary>
    /// Whether this tool should show visualizations by default
    /// </summary>
    public bool ShowByDefault { get; set; } = false;
    
    /// <summary>
    /// Preferred visualization type: "auto", "grid", "chart", "markdown", "tree", "timeline"
    /// </summary>
    public string PreferredView { get; set; } = "auto";
    
    /// <summary>
    /// Priority level for showing visualizations
    /// </summary>
    public VisualizationPriority Priority { get; set; } = VisualizationPriority.OnRequest;
    
    /// <summary>
    /// Whether to consolidate multiple visualizations into one tab
    /// </summary>
    public bool ConsolidateTabs { get; set; } = true;
    
    /// <summary>
    /// Maximum number of concurrent visualization tabs
    /// </summary>
    public int? MaxConcurrentTabs { get; set; } = 1;
    
    /// <summary>
    /// Whether to navigate to the first result automatically
    /// </summary>
    public bool NavigateToFirstResult { get; set; } = false;
}

/// <summary>
/// Priority levels for tool visualizations
/// </summary>
public enum VisualizationPriority
{
    /// <summary>
    /// Never visualize - data-only tools
    /// </summary>
    Never,
    
    /// <summary>
    /// Only when explicitly requested by user
    /// </summary>
    OnRequest,
    
    /// <summary>
    /// Based on result count, context, or data significance
    /// </summary>
    Contextual,
    
    /// <summary>
    /// Always visualize when VS Code is connected
    /// </summary>
    Always
}