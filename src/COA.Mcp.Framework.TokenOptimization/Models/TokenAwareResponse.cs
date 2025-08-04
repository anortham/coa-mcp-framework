using System.Text.Json.Serialization;

namespace COA.Mcp.Framework.TokenOptimization.Models;

/// <summary>
/// Token-aware response wrapper that provides token management information.
/// Based on CodeNav's token optimization patterns.
/// </summary>
/// <typeparam name="T">The type of the response data.</typeparam>
public class TokenAwareResponse<T>
{
    /// <summary>
    /// Gets or sets whether the operation was successful.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the response data.
    /// </summary>
    [JsonPropertyName("data")]
    public T? Data { get; set; }
    
    /// <summary>
    /// Gets or sets error information if the operation failed.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }
    
    /// <summary>
    /// Gets or sets token management metadata.
    /// </summary>
    [JsonPropertyName("tokenMetadata")]
    public TokenMetadata TokenMetadata { get; set; } = new();
    
    /// <summary>
    /// Gets or sets additional response metadata.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Metadata about token usage and management.
/// </summary>
public class TokenMetadata
{
    /// <summary>
    /// Gets or sets the original estimated token count before reduction.
    /// </summary>
    [JsonPropertyName("originalTokens")]
    public int OriginalTokens { get; set; }
    
    /// <summary>
    /// Gets or sets the final token count after reduction.
    /// </summary>
    [JsonPropertyName("finalTokens")]
    public int FinalTokens { get; set; }
    
    /// <summary>
    /// Gets or sets the token limit that was enforced.
    /// </summary>
    [JsonPropertyName("tokenLimit")]
    public int TokenLimit { get; set; }
    
    /// <summary>
    /// Gets or sets whether reduction was applied.
    /// </summary>
    [JsonPropertyName("wasReduced")]
    public bool WasReduced { get; set; }
    
    /// <summary>
    /// Gets or sets the reduction percentage if reduction was applied.
    /// </summary>
    [JsonPropertyName("reductionPercentage")]
    public double? ReductionPercentage { get; set; }
    
    /// <summary>
    /// Gets or sets the reduction strategy used.
    /// </summary>
    [JsonPropertyName("reductionStrategy")]
    public string? ReductionStrategy { get; set; }
    
    /// <summary>
    /// Gets or sets the safety mode used for token calculation.
    /// </summary>
    [JsonPropertyName("safetyMode")]
    public string SafetyMode { get; set; } = "Default";
}