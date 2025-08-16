using System.Text.Json.Serialization;

namespace COA.Mcp.Visualization;

/// <summary>
/// Provides hints to the visualization client about how to display data
/// </summary>
public class VisualizationHint
{
    /// <summary>
    /// Preferred view type (e.g., "grid", "tree", "chart", "markdown", "timeline")
    /// The client may choose a different view if the preferred one is not available
    /// </summary>
    [JsonPropertyName("preferredView")]
    public string? PreferredView { get; set; }
    
    /// <summary>
    /// Fallback format if the client doesn't understand the visualization type
    /// Options: "json", "csv", "markdown", "text"
    /// </summary>
    [JsonPropertyName("fallbackFormat")]
    public string FallbackFormat { get; set; } = "json";
    
    /// <summary>
    /// Whether the visualization should be interactive (allow user actions)
    /// </summary>
    [JsonPropertyName("interactive")]
    public bool Interactive { get; set; } = true;
    
    /// <summary>
    /// Whether to consolidate multiple visualizations of the same type into one tab
    /// </summary>
    [JsonPropertyName("consolidateTabs")]
    public bool ConsolidateTabs { get; set; } = true;
    
    /// <summary>
    /// Priority for showing this visualization
    /// </summary>
    [JsonPropertyName("priority")]
    public VisualizationPriority Priority { get; set; } = VisualizationPriority.Normal;
    
    /// <summary>
    /// Maximum number of concurrent tabs for this visualization type
    /// </summary>
    [JsonPropertyName("maxConcurrentTabs")]
    public int MaxConcurrentTabs { get; set; } = 3;
    
    /// <summary>
    /// Additional options specific to the visualization type
    /// </summary>
    [JsonPropertyName("options")]
    public Dictionary<string, object>? Options { get; set; }
}

/// <summary>
/// Priority levels for visualizations
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum VisualizationPriority
{
    /// <summary>
    /// Only show when explicitly requested
    /// </summary>
    OnRequest = 0,
    
    /// <summary>
    /// Low priority - show only if important
    /// </summary>
    Low = 1,
    
    /// <summary>
    /// Normal priority - show by default
    /// </summary>
    Normal = 2,
    
    /// <summary>
    /// High priority - always show
    /// </summary>
    High = 3,
    
    /// <summary>
    /// Critical - must be shown immediately
    /// </summary>
    Critical = 4
}