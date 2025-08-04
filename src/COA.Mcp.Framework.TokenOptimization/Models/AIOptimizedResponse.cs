using System.Text.Json.Serialization;

namespace COA.Mcp.Framework.TokenOptimization.Models;

/// <summary>
/// AI-optimized response format from CodeSearch implementation.
/// Provides structured data with insights and suggested actions.
/// </summary>
public class AIOptimizedResponse
{
    /// <summary>
    /// Gets or sets the response format identifier.
    /// </summary>
    [JsonPropertyName("format")]
    public string Format { get; set; } = "ai-optimized";
    
    /// <summary>
    /// Gets or sets the main response data.
    /// </summary>
    [JsonPropertyName("data")]
    public AIResponseData Data { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the list of insights about the data.
    /// </summary>
    [JsonPropertyName("insights")]
    public List<string> Insights { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the suggested next actions.
    /// </summary>
    [JsonPropertyName("actions")]
    public List<AIAction> Actions { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the response metadata.
    /// </summary>
    [JsonPropertyName("meta")]
    public AIResponseMeta Meta { get; set; } = new();
}

/// <summary>
/// Container for the main response data.
/// </summary>
public class AIResponseData
{
    /// <summary>
    /// Gets or sets the response summary.
    /// </summary>
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }
    
    /// <summary>
    /// Gets or sets the main results.
    /// </summary>
    [JsonPropertyName("results")]
    public object? Results { get; set; }
    
    /// <summary>
    /// Gets or sets the result count.
    /// </summary>
    [JsonPropertyName("count")]
    public int Count { get; set; }
    
    /// <summary>
    /// Gets or sets additional data properties.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}

/// <summary>
/// Represents a suggested action for the AI to take next.
/// </summary>
public class AIAction
{
    /// <summary>
    /// Gets or sets the action identifier.
    /// </summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the tool name for the action (alias for Action).
    /// </summary>
    [JsonIgnore]
    public string Tool 
    { 
        get => Action; 
        set => Action = value; 
    }
    
    /// <summary>
    /// Gets or sets the human-readable description of the action.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the rationale for suggesting this action.
    /// </summary>
    [JsonPropertyName("rationale")]
    public string? Rationale { get; set; }
    
    /// <summary>
    /// Gets or sets the category of this action.
    /// </summary>
    [JsonPropertyName("category")]
    public string? Category { get; set; }
    
    /// <summary>
    /// Gets or sets the parameters for the action.
    /// </summary>
    [JsonPropertyName("parameters")]
    public Dictionary<string, object>? Parameters { get; set; }
    
    /// <summary>
    /// Gets or sets the priority of this action (higher is more important).
    /// </summary>
    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 50;
}

/// <summary>
/// Metadata about the response.
/// </summary>
public class AIResponseMeta
{
    /// <summary>
    /// Gets or sets the execution time.
    /// </summary>
    [JsonPropertyName("executionTime")]
    public string? ExecutionTime { get; set; }
    
    /// <summary>
    /// Gets or sets whether the response was truncated.
    /// </summary>
    [JsonPropertyName("truncated")]
    public bool Truncated { get; set; }
    
    /// <summary>
    /// Gets or sets the resource URI for full results.
    /// </summary>
    [JsonPropertyName("resourceUri")]
    public string? ResourceUri { get; set; }
    
    /// <summary>
    /// Gets or sets the token count information.
    /// </summary>
    [JsonPropertyName("tokenInfo")]
    public TokenInfo? TokenInfo { get; set; }
    
    /// <summary>
    /// Gets or sets additional metadata.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}

/// <summary>
/// Information about token usage.
/// </summary>
public class TokenInfo
{
    /// <summary>
    /// Gets or sets the estimated token count.
    /// </summary>
    [JsonPropertyName("estimated")]
    public int Estimated { get; set; }
    
    /// <summary>
    /// Gets or sets the token limit that was applied.
    /// </summary>
    [JsonPropertyName("limit")]
    public int Limit { get; set; }
    
    /// <summary>
    /// Gets or sets the reduction strategy used.
    /// </summary>
    [JsonPropertyName("reductionStrategy")]
    public string? ReductionStrategy { get; set; }
}