using System.Collections.Generic;
using System.Text.Json;

namespace COA.Mcp.Framework.Schema
{
    /// <summary>
    /// Interface for type-safe JSON schema representation.
    /// </summary>
    public interface IJsonSchema
    {
        /// <summary>
        /// Converts the schema to a dictionary representation.
        /// </summary>
        Dictionary<string, object> ToDictionary();

        /// <summary>
        /// Converts the schema to a JSON string.
        /// </summary>
        string ToJson();

        /// <summary>
        /// Converts the schema to a JsonElement for protocol compatibility.
        /// </summary>
        JsonElement ToJsonElement();

        /// <summary>
        /// Gets the schema type (e.g., "object", "string", "number").
        /// </summary>
        string SchemaType { get; }

        /// <summary>
        /// Gets the list of required properties if this is an object schema.
        /// </summary>
        IReadOnlyList<string> RequiredProperties { get; }
    }
}