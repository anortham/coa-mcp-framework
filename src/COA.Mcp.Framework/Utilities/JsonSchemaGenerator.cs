using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

namespace COA.Mcp.Framework.Utilities;

/// <summary>
/// Generates JSON Schema from .NET types for MCP tool parameter validation.
/// </summary>
public static class JsonSchemaGenerator
{
    /// <summary>
    /// Generates a JSON Schema for the specified type.
    /// </summary>
    /// <typeparam name="T">The type to generate schema for.</typeparam>
    /// <returns>A dictionary representing the JSON Schema.</returns>
    public static Dictionary<string, object> GenerateSchema<T>()
    {
        return GenerateSchema(typeof(T));
    }

    /// <summary>
    /// Generates a JSON Schema for the specified type.
    /// </summary>
    /// <param name="type">The type to generate schema for.</param>
    /// <returns>A dictionary representing the JSON Schema.</returns>
    public static Dictionary<string, object> GenerateSchema(Type type)
    {
        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["additionalProperties"] = false
        };

        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            // Skip properties marked with JsonIgnore
            if (property.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                continue;

            var propertySchema = GeneratePropertySchema(property);
            var propertyName = GetJsonPropertyName(property);
            
            properties[propertyName] = propertySchema;

            // Check if property is required
            if (IsRequired(property))
            {
                required.Add(propertyName);
            }
        }

        if (properties.Count > 0)
        {
            schema["properties"] = properties;
        }

        if (required.Count > 0)
        {
            schema["required"] = required;
        }

        return schema;
    }

    private static Dictionary<string, object> GeneratePropertySchema(PropertyInfo property)
    {
        var schema = new Dictionary<string, object>();
        var propertyType = property.PropertyType;

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(propertyType);
        if (underlyingType != null)
        {
            propertyType = underlyingType;
        }

        // Add description - prefer XML documentation over Description attribute
        var description = GetPropertyDescription(property);
        if (!string.IsNullOrEmpty(description))
        {
            schema["description"] = description;
        }

        // Handle different types
        if (propertyType == typeof(string))
        {
            schema["type"] = "string";
            AddStringValidation(schema, property);
        }
        else if (propertyType == typeof(int) || propertyType == typeof(long) || 
                 propertyType == typeof(short) || propertyType == typeof(byte))
        {
            schema["type"] = "integer";
            AddNumericValidation(schema, property);
        }
        else if (propertyType == typeof(float) || propertyType == typeof(double) || 
                 propertyType == typeof(decimal))
        {
            schema["type"] = "number";
            AddNumericValidation(schema, property);
        }
        else if (propertyType == typeof(bool))
        {
            schema["type"] = "boolean";
        }
        else if (propertyType.IsEnum)
        {
            schema["type"] = "string";
            schema["enum"] = Enum.GetNames(propertyType);
        }
        else if (typeof(IEnumerable).IsAssignableFrom(propertyType) && propertyType != typeof(string))
        {
            schema["type"] = "array";
            
            // Get the element type
            var elementType = propertyType.IsArray 
                ? propertyType.GetElementType()
                : propertyType.GetGenericArguments().FirstOrDefault();
                
            if (elementType != null)
            {
                schema["items"] = GenerateTypeSchema(elementType);
            }
        }
        else if (propertyType.IsClass)
        {
            // For complex types, generate nested schema
            return GenerateSchema(propertyType);
        }
        else
        {
            // Default to string for unknown types
            schema["type"] = "string";
        }

        return schema;
    }

    private static Dictionary<string, object> GenerateTypeSchema(Type type)
    {
        var schema = new Dictionary<string, object>();

        if (type == typeof(string))
        {
            schema["type"] = "string";
        }
        else if (type == typeof(int) || type == typeof(long) || 
                 type == typeof(short) || type == typeof(byte))
        {
            schema["type"] = "integer";
        }
        else if (type == typeof(float) || type == typeof(double) || 
                 type == typeof(decimal))
        {
            schema["type"] = "number";
        }
        else if (type == typeof(bool))
        {
            schema["type"] = "boolean";
        }
        else if (type.IsEnum)
        {
            schema["type"] = "string";
            schema["enum"] = Enum.GetNames(type);
        }
        else if (type.IsClass && type != typeof(object))
        {
            return GenerateSchema(type);
        }
        else
        {
            schema["type"] = "string";
        }

        return schema;
    }

    private static void AddStringValidation(Dictionary<string, object> schema, PropertyInfo property)
    {
        var minLengthAttr = property.GetCustomAttribute<MinLengthAttribute>();
        if (minLengthAttr != null)
        {
            schema["minLength"] = minLengthAttr.Length;
        }

        var maxLengthAttr = property.GetCustomAttribute<MaxLengthAttribute>();
        if (maxLengthAttr != null)
        {
            schema["maxLength"] = maxLengthAttr.Length;
        }

        var stringLengthAttr = property.GetCustomAttribute<StringLengthAttribute>();
        if (stringLengthAttr != null)
        {
            if (stringLengthAttr.MinimumLength > 0)
            {
                schema["minLength"] = stringLengthAttr.MinimumLength;
            }
            schema["maxLength"] = stringLengthAttr.MaximumLength;
        }

        var regexAttr = property.GetCustomAttribute<RegularExpressionAttribute>();
        if (regexAttr != null)
        {
            schema["pattern"] = regexAttr.Pattern;
        }
    }

    private static void AddNumericValidation(Dictionary<string, object> schema, PropertyInfo property)
    {
        var rangeAttr = property.GetCustomAttribute<RangeAttribute>();
        if (rangeAttr != null)
        {
            if (rangeAttr.Minimum != null)
            {
                schema["minimum"] = Convert.ToDouble(rangeAttr.Minimum);
            }
            if (rangeAttr.Maximum != null)
            {
                schema["maximum"] = Convert.ToDouble(rangeAttr.Maximum);
            }
        }
    }

    private static bool IsRequired(PropertyInfo property)
    {
        // Check for Required attribute
        if (property.GetCustomAttribute<RequiredAttribute>() != null)
            return true;

        // Check for JsonRequired attribute
        var jsonPropertyAttr = property.GetCustomAttribute<JsonPropertyNameAttribute>();
        if (jsonPropertyAttr != null)
        {
            // Note: JsonPropertyNameAttribute doesn't have a Required property in System.Text.Json
            // You might need to use a custom attribute for this
        }

        // Non-nullable value types are implicitly required (unless they have a default value)
        if (property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) == null)
        {
            // Check if it has a default value attribute
            var defaultValueAttr = property.GetCustomAttribute<DefaultValueAttribute>();
            if (defaultValueAttr == null)
            {
                return true;
            }
        }

        return false;
    }

    private static string GetJsonPropertyName(PropertyInfo property)
    {
        var jsonPropertyAttr = property.GetCustomAttribute<JsonPropertyNameAttribute>();
        if (jsonPropertyAttr != null)
        {
            return jsonPropertyAttr.Name;
        }

        // Convert to camelCase by default (matching our JSON serialization options)
        var name = property.Name;
        if (string.IsNullOrEmpty(name))
            return name;

        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    /// <summary>
    /// Gets the description for a property, preferring XML documentation over Description attribute.
    /// </summary>
    /// <param name="property">The property to get description for.</param>
    /// <returns>The description text or null if not found.</returns>
    private static string? GetPropertyDescription(PropertyInfo property)
    {
        // Try XML documentation first
        var xmlDoc = XmlDocumentationExtractor.GetPropertyDocumentation(property);
        if (xmlDoc?.Summary != null)
        {
            var description = xmlDoc.Summary;

            // Append examples if available
            if (xmlDoc.Examples.Any())
            {
                description += $" Examples: {string.Join(", ", xmlDoc.Examples)}";
            }

            return description;
        }

        // Fall back to Description attribute
        var descriptionAttr = property.GetCustomAttribute<DescriptionAttribute>();
        return descriptionAttr?.Description;
    }
}