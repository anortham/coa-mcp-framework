using System;
using System.Text.Json.Serialization;
using COA.Mcp.Framework.Models;

namespace COA.Mcp.Framework.TokenOptimization.Models;

/// <summary>
/// Generic AI-optimized response format with strongly typed results.
/// Provides structured data with insights and suggested actions.
/// </summary>
/// <typeparam name="T">The type of the results data.</typeparam>
public class AIOptimizedResponse<T> : ToolResultBase
{
    /// <summary>
    /// Gets or sets the response format identifier.
    /// </summary>
    [JsonPropertyName("format")]
    public string Format { get; set; } = "ai-optimized";
    
    /// <summary>
    /// Gets or sets the main response data with typed results.
    /// </summary>
    [JsonPropertyName("data")]
    public AIResponseData<T> Data { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the list of insights about the data.
    /// </summary>
    [JsonPropertyName("insights")]
    public new List<string> Insights { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the suggested next actions.
    /// </summary>
    [JsonPropertyName("actions")]
    public new List<AIAction> Actions { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the response metadata.
    /// </summary>
    [JsonPropertyName("responseMeta")]
    public AIResponseMeta ResponseMeta { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the response metadata (backward compatibility alias).
    /// </summary>
    [JsonIgnore]
    public new AIResponseMeta Meta 
    { 
        get => ResponseMeta; 
        set => ResponseMeta = value; 
    }
    
    /// <inheritdoc/>
    public override string Operation => "ai-optimized-response";
}

/// <summary>
/// Non-generic AI-optimized response for backward compatibility.
/// Provides structured data with insights and suggested actions.
/// </summary>
[Obsolete("Use AIOptimizedResponse<T> instead for better type safety. This non-generic version will be removed in version 2.0.0.", false)]
public class AIOptimizedResponse : AIOptimizedResponse<object>
{
    /// <summary>
    /// Creates a generic version of this response with typed results.
    /// </summary>
    /// <typeparam name="T">The type to convert the results to.</typeparam>
    /// <returns>A generic AIOptimizedResponse with typed results.</returns>
    public AIOptimizedResponse<T> ToGeneric<T>() where T : class
    {
        return new AIOptimizedResponse<T>
        {
            Format = Format,
            Data = Data.ToGeneric<T>(),
            Insights = new List<string>(Insights),
            Actions = new List<AIAction>(Actions),
            ResponseMeta = ResponseMeta,
            Success = Success,
            Message = Message,
            Error = Error,
            ResourceUri = ResourceUri
        };
    }
}

/// <summary>
/// Generic container for the main response data with typed results.
/// </summary>
/// <typeparam name="T">The type of the results.</typeparam>
public class AIResponseData<T>
{
    /// <summary>
    /// Gets or sets the response summary.
    /// </summary>
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }
    
    /// <summary>
    /// Gets or sets the main results with strong typing.
    /// </summary>
    [JsonPropertyName("results")]
    public T? Results { get; set; }
    
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
    
    /// <summary>
    /// Creates a generic version with different result type.
    /// </summary>
    internal AIResponseData<TNew> ToGeneric<TNew>() where TNew : class
    {
        return new AIResponseData<TNew>
        {
            Summary = Summary,
            Results = Results as TNew,
            Count = Count,
            ExtensionData = ExtensionData
        };
    }
}

/// <summary>
/// Non-generic container for backward compatibility.
/// </summary>
[Obsolete("Use AIResponseData<T> instead for better type safety. This non-generic version will be removed in version 2.0.0.", false)]
public class AIResponseData : AIResponseData<object>
{
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