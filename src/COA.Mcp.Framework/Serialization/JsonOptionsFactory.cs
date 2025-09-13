using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace COA.Mcp.Framework.Serialization;

/// <summary>
/// Factory for creating consistent JsonSerializerOptions across the MCP framework.
/// Provides centralized configuration without dependency injection complexity.
/// </summary>
public static class JsonOptionsFactory
{
    /// <summary>
    /// Creates standard JsonSerializerOptions for general MCP framework usage.
    /// Configured for consistent camelCase output with emoji support.
    /// </summary>
    /// <returns>JsonSerializerOptions configured with framework standards</returns>
    public static JsonSerializerOptions CreateStandard()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>
    /// Creates indented JsonSerializerOptions for human-readable output.
    /// Uses same standards as CreateStandard() but with formatting.
    /// </summary>
    /// <returns>JsonSerializerOptions configured for readable output</returns>
    public static JsonSerializerOptions CreateIndented()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>
    /// Creates JsonSerializerOptions for case-insensitive deserialization.
    /// Used when deserializing JSON from external sources with varying property casing.
    /// </summary>
    /// <returns>JsonSerializerOptions configured for case-insensitive property matching</returns>
    public static JsonSerializerOptions CreateForDeserialization()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }
}