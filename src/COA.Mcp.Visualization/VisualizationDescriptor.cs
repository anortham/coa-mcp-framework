using System.Text.Json.Serialization;

namespace COA.Mcp.Visualization;

/// <summary>
/// Describes visualization data and hints for display clients
/// </summary>
public class VisualizationDescriptor
{
    /// <summary>
    /// The type of visualization (e.g., "search-results", "hierarchy", "metrics")
    /// This is used by the client to select the appropriate renderer
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; set; }
    
    /// <summary>
    /// Version of the visualization format for this type
    /// Allows for backward compatibility as formats evolve
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";
    
    /// <summary>
    /// The actual data to visualize
    /// Structure depends on the Type
    /// </summary>
    [JsonPropertyName("data")]
    public required object Data { get; set; }
    
    /// <summary>
    /// Optional hints for how to display the data
    /// </summary>
    [JsonPropertyName("hint")]
    public VisualizationHint? Hint { get; set; }
    
    /// <summary>
    /// Optional metadata for the visualization
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}