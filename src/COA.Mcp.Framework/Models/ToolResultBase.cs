using System.Text.Json.Serialization;

namespace COA.Mcp.Framework.Models;

/// <summary>
/// Base class for all tool results following the CodeSearch MCP pattern
/// </summary>
public abstract class ToolResultBase
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("operation")]
    public abstract string Operation { get; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("error")]
    public ErrorInfo? Error { get; set; }

    [JsonPropertyName("insights")]
    public List<string>? Insights { get; set; }

    [JsonPropertyName("actions")]
    public List<AIAction>? Actions { get; set; }

    [JsonPropertyName("meta")]
    public ToolExecutionMetadata? Meta { get; set; }

    [JsonPropertyName("resourceUri")]
    public string? ResourceUri { get; set; }
}

/// <summary>
/// Metadata about the tool execution
/// </summary>
public class ToolExecutionMetadata
{
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "full";

    [JsonPropertyName("truncated")]
    public bool Truncated { get; set; }

    [JsonPropertyName("tokens")]
    public int? Tokens { get; set; }

    [JsonPropertyName("cached")]
    public string? Cached { get; set; }

    [JsonPropertyName("executionTime")]
    public string? ExecutionTime { get; set; }
}

/// <summary>
/// Standard query information
/// </summary>
public class QueryInfo
{
    [JsonPropertyName("workspace")]
    public string? Workspace { get; set; }

    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }

    [JsonPropertyName("position")]
    public PositionInfo? Position { get; set; }

    [JsonPropertyName("targetSymbol")]
    public string? TargetSymbol { get; set; }

    [JsonPropertyName("generationType")]
    public string? GenerationType { get; set; }

    [JsonPropertyName("additionalParams")]
    public Dictionary<string, object>? AdditionalParams { get; set; }
}

/// <summary>
/// Position information for location-based queries
/// </summary>
public class PositionInfo
{
    [JsonPropertyName("line")]
    public int Line { get; set; }

    [JsonPropertyName("column")]
    public int Column { get; set; }
}

/// <summary>
/// Standard summary information
/// </summary>
public class SummaryInfo
{
    [JsonPropertyName("totalFound")]
    public int TotalFound { get; set; }

    [JsonPropertyName("returned")]
    public int Returned { get; set; }

    [JsonPropertyName("executionTime")]
    public string? ExecutionTime { get; set; }

    [JsonPropertyName("symbolInfo")]
    public SymbolSummary? SymbolInfo { get; set; }
}

/// <summary>
/// Symbol summary information
/// </summary>
public class SymbolSummary
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("containingType")]
    public string? ContainingType { get; set; }

    [JsonPropertyName("namespace")]
    public string? Namespace { get; set; }
}

/// <summary>
/// Standard results summary
/// </summary>
public class ResultsSummary
{
    [JsonPropertyName("included")]
    public int Included { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("hasMore")]
    public bool HasMore { get; set; }
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