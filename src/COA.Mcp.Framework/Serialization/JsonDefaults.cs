using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace COA.Mcp.Framework.Serialization;

/// <summary>
/// Centralized JSON serialization defaults to ensure consistent behavior across the MCP framework.
/// All JSON operations should use these pre-configured options to ensure UTF-8 compatibility
/// and consistent serialization behavior.
/// </summary>
public static class JsonDefaults
{
    // Singleton instances to avoid repeated allocations and ensure consistent configuration
    private static readonly Lazy<JsonSerializerOptions> _standard = new(() => CreateStandard());
    private static readonly Lazy<JsonSerializerOptions> _indented = new(() => CreateIndented());
    private static readonly Lazy<JsonSerializerOptions> _strict = new(() => CreateStrict());
    
    /// <summary>
    /// Standard JSON serialization options with UTF-8 support.
    /// Use this for most internal serialization scenarios.
    /// </summary>
    public static JsonSerializerOptions Standard => _standard.Value;
    
    /// <summary>
    /// Indented JSON serialization options for human-readable output.
    /// Use this for configuration files, logs, or debug output.
    /// </summary>
    public static JsonSerializerOptions Indented => _indented.Value;
    
    /// <summary>
    /// Strict JSON serialization options for external APIs.
    /// Uses default encoder which escapes non-ASCII characters.
    /// </summary>
    public static JsonSerializerOptions Strict => _strict.Value;
    
    private static JsonSerializerOptions CreateStandard() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // Critical: UTF-8 emoji and Unicode support
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        PropertyNameCaseInsensitive = true, // More forgiving deserialization
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };
    
    private static JsonSerializerOptions CreateIndented()
    {
        var options = new JsonSerializerOptions(Standard);
        options.WriteIndented = true;
        return options;
    }
    
    private static JsonSerializerOptions CreateStrict()
    {
        var options = new JsonSerializerOptions(Standard);
        options.Encoder = JavaScriptEncoder.Default; // Escapes non-ASCII for external APIs
        options.AllowTrailingCommas = false;
        options.ReadCommentHandling = JsonCommentHandling.Disallow;
        options.PropertyNameCaseInsensitive = false;
        return options;
    }
}