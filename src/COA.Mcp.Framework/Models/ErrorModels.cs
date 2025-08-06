using System.Text.Json.Serialization;

namespace COA.Mcp.Framework.Models;

/// <summary>
/// AI-friendly error information with actionable recovery steps
/// </summary>
public class ErrorInfo
{
    [JsonPropertyName("code")]
    public required string Code { get; set; }
    
    [JsonPropertyName("message")]
    public string? Message { get; set; }
    
    [JsonPropertyName("recovery")]
    public RecoveryInfo? Recovery { get; set; }
}

/// <summary>
/// Recovery information with steps and suggested actions
/// </summary>
public class RecoveryInfo
{
    [JsonPropertyName("steps")]
    public string[] Steps { get; set; } = Array.Empty<string>();
    
    [JsonPropertyName("suggestedActions")]
    public List<SuggestedAction> SuggestedActions { get; set; } = new();
}

/// <summary>
/// Suggested action for error recovery
/// </summary>
public class SuggestedAction
{
    [JsonPropertyName("tool")]
    public required string Tool { get; set; }
    
    [JsonPropertyName("description")]
    public required string Description { get; set; }
    
    [JsonPropertyName("parameters")]
    public object? Parameters { get; set; }
}

/// <summary>
/// Base error codes common to all MCP servers
/// </summary>
public static class BaseErrorCodes
{
    public const string INTERNAL_ERROR = "INTERNAL_ERROR";
    public const string INVALID_PARAMETERS = "INVALID_PARAMETERS";
    public const string NOT_FOUND = "NOT_FOUND";
    public const string TIMEOUT = "TIMEOUT";
    public const string RESOURCE_LIMIT_EXCEEDED = "RESOURCE_LIMIT_EXCEEDED";
    public const string OPERATION_CANCELLED = "OPERATION_CANCELLED";
}