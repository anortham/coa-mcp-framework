using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace COA.Mcp.Protocol;

/// <summary>
/// Marker type indicating tool capability support.
/// Per MCP specification, this should serialize as an empty object {}.
/// </summary>
public sealed class ToolsCapabilityMarker 
{ 
    // Empty marker class is intentional per MCP spec
    // Serializes to {} to indicate capability is supported
}

/// <summary>
/// Marker type indicating prompts capability support.
/// Per MCP specification, this should serialize as an empty object {}.
/// </summary>
public sealed class PromptsCapabilityMarker 
{ 
    // Empty marker class is intentional per MCP spec
    // Serializes to {} to indicate capability is supported
}

/// <summary>
/// Marker type indicating sampling capability support.
/// Per MCP specification, this should serialize as an empty object {}.
/// </summary>
public sealed class SamplingCapabilityMarker 
{ 
    // Empty marker class is intentional per MCP spec
    // Serializes to {} to indicate capability is supported
}

/// <summary>
/// Custom JSON converter that ensures capability markers serialize to empty objects.
/// This maintains backward compatibility with existing code using anonymous objects.
/// </summary>
internal class CapabilityMarkerConverter<T> : JsonConverter<T> where T : class, new()
{
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            reader.Skip(); // Skip the empty object
            return new T();
        }
        return null;
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteEndObject();
    }
}