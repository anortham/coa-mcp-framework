using System.Text.Json.Serialization;

namespace COA.Mcp.Framework.TokenOptimization.Models;

/// <summary>
/// Common metadata for all response types.
/// </summary>
public class ResponseMetadata
{
    /// <summary>
    /// Gets or sets the unique identifier for this response.
    /// </summary>
    [JsonPropertyName("responseId")]
    public string ResponseId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Gets or sets the timestamp when the response was generated.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Gets or sets the execution duration in milliseconds.
    /// </summary>
    [JsonPropertyName("executionTimeMs")]
    public double ExecutionTimeMs { get; set; }
    
    /// <summary>
    /// Gets or sets the response mode used (e.g., "summary", "full").
    /// </summary>
    [JsonPropertyName("responseMode")]
    public string ResponseMode { get; set; } = "full";
    
    /// <summary>
    /// Gets or sets whether this response can be cached.
    /// </summary>
    [JsonPropertyName("cacheable")]
    public bool Cacheable { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the cache key if this response was cached.
    /// </summary>
    [JsonPropertyName("cacheKey")]
    public string? CacheKey { get; set; }
    
    /// <summary>
    /// Gets or sets whether this response was served from cache.
    /// </summary>
    [JsonPropertyName("fromCache")]
    public bool FromCache { get; set; }
    
    /// <summary>
    /// Gets or sets the tool name that generated this response.
    /// </summary>
    [JsonPropertyName("toolName")]
    public string? ToolName { get; set; }
    
    /// <summary>
    /// Gets or sets the tool version.
    /// </summary>
    [JsonPropertyName("toolVersion")]
    public string? ToolVersion { get; set; }
}

/// <summary>
/// Extended metadata for responses that include insights.
/// </summary>
public class InsightfulResponseMetadata : ResponseMetadata
{
    /// <summary>
    /// Gets or sets the number of insights generated.
    /// </summary>
    [JsonPropertyName("insightCount")]
    public int InsightCount { get; set; }
    
    /// <summary>
    /// Gets or sets the insight generation strategy used.
    /// </summary>
    [JsonPropertyName("insightStrategy")]
    public string InsightStrategy { get; set; } = "contextual";
    
    /// <summary>
    /// Gets or sets the confidence scores for generated insights.
    /// </summary>
    [JsonPropertyName("insightConfidence")]
    public Dictionary<string, double>? InsightConfidence { get; set; }
}