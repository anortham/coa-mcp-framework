using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using COA.Mcp.Framework.Utilities;

namespace COA.Mcp.Framework.Schema
{
    /// <summary>
    /// Generic implementation of JSON schema with type safety.
    /// </summary>
    /// <typeparam name="T">The type this schema represents.</typeparam>
    public class JsonSchema<T> : IJsonSchema where T : class
    {
        private readonly Dictionary<string, object> _schema;
        private readonly Lazy<string> _jsonRepresentation;
        private readonly Lazy<JsonElement> _jsonElement;

        /// <summary>
        /// Creates a new JSON schema for the specified type.
        /// </summary>
        public JsonSchema()
        {
            _schema = JsonSchemaGenerator.GenerateSchema<T>();
            _jsonRepresentation = new Lazy<string>(() => JsonSerializer.Serialize(_schema));
            _jsonElement = new Lazy<JsonElement>(() => JsonSerializer.SerializeToElement(_schema));
        }

        /// <summary>
        /// Creates a JSON schema from an existing schema dictionary.
        /// </summary>
        public JsonSchema(Dictionary<string, object> schema)
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
        /// Gets the parameter type this schema represents.
        /// </summary>
        public Type ParameterType => typeof(T);

        /// <summary>
        /// Validates that an object conforms to this schema.
        /// </summary>
        public bool IsValid(T obj)
        {
            // This could be expanded with actual JSON schema validation
            if (obj == null)
            {
                return !RequiredProperties.Any();
            }

            // Basic validation - check required properties exist
            var objType = obj.GetType();
            foreach (var required in RequiredProperties)
            {
                var prop = objType.GetProperty(required);
                if (prop == null || prop.GetValue(obj) == null)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Implicit conversion to dictionary for backward compatibility.
        /// </summary>
        public static implicit operator Dictionary<string, object>(JsonSchema<T> schema)
        {
            return schema?.ToDictionary() ?? new Dictionary<string, object>();
        }
    }
}