using System.Text.Json.Serialization;

namespace COA.Mcp.Framework.TokenOptimization.Models;

/// <summary>
/// Represents an insight generated from data analysis.
/// </summary>
public class Insight
{
    /// <summary>
    /// Gets or sets the insight text.
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the insight type/category.
    /// </summary>
    [JsonPropertyName("type")]
    public InsightType Type { get; set; } = InsightType.General;
    
    /// <summary>
    /// Gets or sets the confidence level (0.0 to 1.0).
    /// </summary>
    [JsonPropertyName("confidence")]
    public double Confidence { get; set; } = 0.8;
    
    /// <summary>
    /// Gets or sets the priority/importance (0-100).
    /// </summary>
    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 50;
    
    /// <summary>
    /// Gets or sets the importance level of the insight.
    /// </summary>
    [JsonPropertyName("importance")]
    public InsightImportance Importance { get; set; } = InsightImportance.Medium;
    
    /// <summary>
    /// Gets or sets additional metadata for the insight.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
    
    /// <summary>
    /// Gets or sets supporting data for the insight.
    /// </summary>
    [JsonPropertyName("supportingData")]
    public Dictionary<string, object>? SupportingData { get; set; }
    
    /// <summary>
    /// Gets or sets related action identifiers.
    /// </summary>
    [JsonPropertyName("relatedActions")]
    public List<string>? RelatedActions { get; set; }
}

/// <summary>
/// Types of insights that can be generated.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InsightType
{
    /// <summary>
    /// General observation or finding.
    /// </summary>
    General,
    
    /// <summary>
    /// Information or status update.
    /// </summary>
    Information,
    
    /// <summary>
    /// Pattern or trend identified in the data.
    /// </summary>
    Pattern,
    
    /// <summary>
    /// Potential issue or problem detected.
    /// </summary>
    Issue,
    
    /// <summary>
    /// Warning about potential problems.
    /// </summary>
    Warning,
    
    /// <summary>
    /// Error or critical issue.
    /// </summary>
    Error,
    
    /// <summary>
    /// Optimization opportunity.
    /// </summary>
    Optimization,
    
    /// <summary>
    /// Security-related insight.
    /// </summary>
    Security,
    
    /// <summary>
    /// Performance-related insight.
    /// </summary>
    Performance,
    
    /// <summary>
    /// Data quality observation.
    /// </summary>
    DataQuality,
    
    /// <summary>
    /// Architectural or design insight.
    /// </summary>
    Architecture,
    
    /// <summary>
    /// Next steps or recommendations.
    /// </summary>
    Recommendation,
    
    /// <summary>
    /// Suggestion for improvement.
    /// </summary>
    Suggestion,
    
    /// <summary>
    /// Analysis result or finding.
    /// </summary>
    Analysis
}

/// <summary>
/// Importance levels for insights.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InsightImportance
{
    /// <summary>
    /// Low importance.
    /// </summary>
    Low,
    
    /// <summary>
    /// Medium importance.
    /// </summary>
    Medium,
    
    /// <summary>
    /// High importance.
    /// </summary>
    High,
    
    /// <summary>
    /// Critical importance.
    /// </summary>
    Critical
}

/// <summary>
/// Builder for creating insights fluently.
/// </summary>
public class InsightBuilder
{
    private readonly Insight _insight = new();
    
    /// <summary>
    /// Sets the insight text.
    /// </summary>
    public InsightBuilder WithText(string text)
    {
        _insight.Text = text;
        return this;
    }
    
    /// <summary>
    /// Sets the insight type.
    /// </summary>
    public InsightBuilder WithType(InsightType type)
    {
        _insight.Type = type;
        return this;
    }
    
    /// <summary>
    /// Sets the confidence level.
    /// </summary>
    public InsightBuilder WithConfidence(double confidence)
    {
        _insight.Confidence = Math.Max(0, Math.Min(1, confidence));
        return this;
    }
    
    /// <summary>
    /// Sets the priority.
    /// </summary>
    public InsightBuilder WithPriority(int priority)
    {
        _insight.Priority = Math.Max(0, Math.Min(100, priority));
        return this;
    }
    
    /// <summary>
    /// Adds supporting data.
    /// </summary>
    public InsightBuilder WithSupportingData(string key, object value)
    {
        _insight.SupportingData ??= new Dictionary<string, object>();
        _insight.SupportingData[key] = value;
        return this;
    }
    
    /// <summary>
    /// Adds a related action.
    /// </summary>
    public InsightBuilder WithRelatedAction(string actionId)
    {
        _insight.RelatedActions ??= new List<string>();
        _insight.RelatedActions.Add(actionId);
        return this;
    }
    
    /// <summary>
    /// Builds the insight.
    /// </summary>
    public Insight Build() => _insight;
}