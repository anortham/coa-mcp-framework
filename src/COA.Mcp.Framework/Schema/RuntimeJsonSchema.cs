using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace COA.Mcp.Framework.Schema
{
    /// <summary>
    /// Runtime implementation of JSON schema for non-generic scenarios.
    /// Used when type information is only available at runtime.
    /// </summary>
    public class RuntimeJsonSchema : IJsonSchema
    {
        private readonly Dictionary<string, object> _schema;
        private readonly Lazy<string> _jsonRepresentation;
        private readonly Lazy<JsonElement> _jsonElement;

        /// <summary>
        /// Creates a new runtime JSON schema from a schema dictionary.
        /// </summary>
        public RuntimeJsonSchema(Dictionary<string, object> schema)
        {
            _schema = schema ?? throw new ArgumentNullException(nameof(schema));
            _jsonRepresentation = new Lazy<string>(() => JsonSerializer.Serialize(_schema));
            _jsonElement = new Lazy<JsonElement>(() => JsonSerializer.SerializeToElement(_schema));
        }

        /// <inheritdoc/>
        public Dictionary<string, object> ToDictionary() => new(_schema);

        /// <inheritdoc/>
        public string ToJson() => _jsonRepresentation.Value;

        /// <inheritdoc/>
        public JsonElement ToJsonElement() => _jsonElement.Value;

        /// <inheritdoc/>
        public string SchemaType => _schema.TryGetValue("type", out var type) 
            ? type?.ToString() ?? "object" 
            : "object";

        /// <inheritdoc/>
        public IReadOnlyList<string> RequiredProperties
        {
            get
            {
                if (_schema.TryGetValue("required", out var required) && required is List<string> list)
                {
                    return list.AsReadOnly();
                }
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Gets the properties schema if this is an object type.
        /// </summary>
        public IReadOnlyDictionary<string, object>? Properties
        {
            get
            {
                if (_schema.TryGetValue("properties", out var properties) && 
                    properties is Dictionary<string, object> dict)
                {
                    return dict;
                }
                return null;
            }
        }

        /// <summary>
        /// Implicit conversion to dictionary for backward compatibility.
        /// </summary>
        public static implicit operator Dictionary<string, object>(RuntimeJsonSchema schema)
        {
            return schema?.ToDictionary() ?? new Dictionary<string, object>();
        }
    }
}