using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using COA.Mcp.Framework.Attributes;
using COA.Mcp.Framework.Interfaces;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.Registration;

/// <summary>
/// Service for discovering MCP tools through reflection.
/// </summary>
public class ToolDiscoveryService : IToolDiscovery
{
    private readonly ILogger<ToolDiscoveryService>? _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolDiscoveryService"/> class.
    /// </summary>
    public ToolDiscoveryService(ILogger<ToolDiscoveryService>? logger = null)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    /// <inheritdoc/>
    public IEnumerable<ToolMetadata> DiscoverTools(Assembly assembly)
    {
        if (assembly == null)
            throw new ArgumentNullException(nameof(assembly));

        _logger?.LogDebug("Discovering tools in assembly {AssemblyName}", assembly.GetName().Name);

        var toolMetadata = new List<ToolMetadata>();

        try
        {
            var toolTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && 
                           t.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
                .ToList();

            foreach (var type in toolTypes)
            {
                var typeAttribute = type.GetCustomAttribute<McpServerToolTypeAttribute>()!;
                
                if (!typeAttribute.AutoRegister)
                {
                    _logger?.LogDebug("Skipping type {TypeName} - AutoRegister is false", type.FullName);
                    continue;
                }

                var validation = ValidateToolType(type);
                if (!validation.IsValid)
                {
                    _logger?.LogWarning("Type {TypeName} validation failed: {Errors}",
                        type.FullName, validation.GetFormattedMessage());
                    continue;
                }

                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null)
                    .ToList();

                foreach (var method in methods)
                {
                    var metadata = ExtractToolMetadata(type, method);
                    if (metadata != null)
                    {
                        toolMetadata.Add(metadata);
                    }
                }
            }

            _logger?.LogInformation("Discovered {Count} tools in assembly {AssemblyName}",
                toolMetadata.Count, assembly.GetName().Name);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error discovering tools in assembly {AssemblyName}", 
                assembly.GetName().Name);
        }

        return toolMetadata;
    }

    /// <inheritdoc/>
    public IEnumerable<ToolMetadata> DiscoverTools(params Assembly[] assemblies)
    {
        var allMetadata = new List<ToolMetadata>();

        foreach (var assembly in assemblies)
        {
            allMetadata.AddRange(DiscoverTools(assembly));
        }

        return allMetadata;
    }

    /// <inheritdoc/>
    public IEnumerable<ToolMetadata> DiscoverToolsInCurrentAssembly()
    {
        return DiscoverTools(Assembly.GetCallingAssembly());
    }

    /// <inheritdoc/>
    public IEnumerable<ToolMetadata> DiscoverToolsInAllAssemblies(bool includeSystemAssemblies = false)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => includeSystemAssemblies || !IsSystemAssembly(a))
            .ToArray();

        return DiscoverTools(assemblies);
    }

    /// <inheritdoc/>
    public ToolValidationResult ValidateToolType(Type type)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (!type.IsClass)
        {
            errors.Add($"Type '{type.FullName}' is not a class.");
        }

        if (type.IsAbstract)
        {
            errors.Add($"Type '{type.FullName}' is abstract and cannot be instantiated.");
        }

        var toolTypeAttr = type.GetCustomAttribute<McpServerToolTypeAttribute>();
        if (toolTypeAttr == null)
        {
            errors.Add($"Type '{type.FullName}' is missing the McpServerToolType attribute.");
        }

        // Check if type has parameterless constructor or can be dependency injected
        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        if (!constructors.Any())
        {
            errors.Add($"Type '{type.FullName}' has no public constructors.");
        }

        // Check if type implements ITool
        if (typeof(ITool).IsAssignableFrom(type))
        {
            warnings.Add($"Type '{type.FullName}' implements ITool directly. Consider inheriting from McpToolBase instead.");
        }

        // Check for tool methods
        var toolMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null)
            .ToList();

        if (!toolMethods.Any())
        {
            warnings.Add($"Type '{type.FullName}' has no methods marked with McpServerTool attribute.");
        }

        return errors.Any() 
            ? ToolValidationResult.FailureWithWarnings(errors.ToArray(), warnings.ToArray())
            : warnings.Any()
                ? ToolValidationResult.SuccessWithWarnings(warnings.ToArray())
                : ToolValidationResult.Success();
    }

    /// <inheritdoc/>
    public ToolValidationResult ValidateToolMethod(MethodInfo method)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
        if (toolAttr == null)
        {
            errors.Add($"Method '{method.Name}' is missing the McpServerTool attribute.");
        }

        // Check return type
        var returnType = method.ReturnType;
        if (returnType != typeof(Task<object>) && 
            returnType != typeof(ValueTask<object>))
        {
            errors.Add($"Method '{method.Name}' must return Task<object> or ValueTask<object>.");
        }

        // Check parameters
        var parameters = method.GetParameters();
        if (parameters.Length != 1)
        {
            errors.Add($"Method '{method.Name}' must have exactly one parameter.");
        }
        else
        {
            var param = parameters[0];
            if (param.ParameterType == typeof(object))
            {
                warnings.Add($"Method '{method.Name}' uses object parameter type. Consider using a strongly-typed parameter class.");
            }
        }

        // Check for async naming convention
        if (!method.Name.EndsWith("Async") && 
            (returnType == typeof(Task<object>) || returnType == typeof(ValueTask<object>)))
        {
            warnings.Add($"Async method '{method.Name}' should end with 'Async' suffix.");
        }

        return errors.Any()
            ? ToolValidationResult.FailureWithWarnings(errors.ToArray(), warnings.ToArray())
            : warnings.Any()
                ? ToolValidationResult.SuccessWithWarnings(warnings.ToArray())
                : ToolValidationResult.Success();
    }

    private ToolMetadata? ExtractToolMetadata(Type type, MethodInfo method)
    {
        try
        {
            var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
            if (toolAttr == null || !toolAttr.Enabled)
                return null;

            var descAttr = method.GetCustomAttribute<DescriptionAttribute>();
            var typeDescAttr = type.GetCustomAttribute<DescriptionAttribute>();
            var typeToolAttr = type.GetCustomAttribute<McpServerToolTypeAttribute>();

            var parameterType = method.GetParameters().FirstOrDefault()?.ParameterType;
            var parameterSchema = parameterType != null 
                ? ExtractParameterSchema(parameterType) 
                : null;

            var metadata = new ToolMetadata
            {
                Name = toolAttr.Name,
                Description = descAttr?.Description ?? $"Executes {toolAttr.Name}",
                Category = DetermineCategory(type, method),
                Version = toolAttr.Version,
                Enabled = toolAttr.Enabled,
                TimeoutMs = toolAttr.TimeoutMs,
                DeclaringType = type,
                Method = method,
                ParameterType = parameterType,
                Parameters = parameterSchema,
                Prerequisites = descAttr?.Prerequisites,
                UseCases = descAttr?.UseCases,
                Warnings = descAttr?.Warnings
            };

            if (descAttr?.Examples != null)
            {
                metadata.Examples.AddRange(descAttr.Examples);
            }

            if (typeToolAttr?.Category != null)
            {
                metadata.AdditionalMetadata["GroupCategory"] = typeToolAttr.Category;
            }

            _logger?.LogDebug("Extracted metadata for tool '{ToolName}' from {Type}.{Method}",
                metadata.Name, type.Name, method.Name);

            return metadata;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error extracting metadata for method {Method} in type {Type}",
                method.Name, type.FullName);
            return null;
        }
    }

    private ParameterSchema? ExtractParameterSchema(Type parameterType)
    {
        if (parameterType == typeof(object))
            return null;

        var schema = new ParameterSchema
        {
            Format = "json-schema",
            Properties = new Dictionary<string, ParameterProperty>(),
            Required = new List<string>()
        };

        var properties = parameterType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        
        foreach (var property in properties)
        {
            var propSchema = ExtractPropertySchema(property);
            if (propSchema != null)
            {
                schema.Properties[property.Name] = propSchema;
                
                if (propSchema.Required)
                {
                    schema.Required.Add(property.Name);
                }
            }
        }

        // Build JSON schema
        schema.Schema = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = schema.Properties.ToDictionary(
                p => JsonNamingPolicy.CamelCase.ConvertName(p.Key),
                p => BuildJsonSchemaForProperty(p.Value)),
            ["required"] = schema.Required.Select(r => JsonNamingPolicy.CamelCase.ConvertName(r)).ToList(),
            ["additionalProperties"] = false
        };

        return schema;
    }

    private ParameterProperty? ExtractPropertySchema(PropertyInfo property)
    {
        var descAttr = property.GetCustomAttribute<DescriptionAttribute>();
        var requiredAttr = property.GetCustomAttribute<RequiredAttribute>();
        var rangeAttr = property.GetCustomAttribute<RangeAttribute>();
        var lengthAttr = property.GetCustomAttribute<StringLengthAttribute>();

        var propSchema = new ParameterProperty
        {
            Name = property.Name,
            Type = GetJsonType(property.PropertyType),
            Description = descAttr?.Description,
            Required = requiredAttr != null || IsRequiredType(property.PropertyType),
            DefaultValue = GetDefaultValue(property.PropertyType)
        };

        // Add constraints
        if (rangeAttr != null)
        {
            propSchema.Constraints["minimum"] = rangeAttr.Minimum;
            propSchema.Constraints["maximum"] = rangeAttr.Maximum;
        }

        if (lengthAttr != null)
        {
            if (lengthAttr.MinimumLength > 0)
                propSchema.Constraints["minLength"] = lengthAttr.MinimumLength;
            propSchema.Constraints["maxLength"] = lengthAttr.MaximumLength;
        }

        // Add examples from description attribute
        if (descAttr?.Examples != null)
        {
            propSchema.Examples.AddRange(descAttr.Examples.Cast<object?>());
        }

        return propSchema;
    }

    private Dictionary<string, object?> BuildJsonSchemaForProperty(ParameterProperty property)
    {
        var schema = new Dictionary<string, object?>
        {
            ["type"] = property.Type,
            ["description"] = property.Description
        };

        foreach (var constraint in property.Constraints)
        {
            schema[constraint.Key] = constraint.Value;
        }

        if (property.DefaultValue != null)
        {
            schema["default"] = property.DefaultValue;
        }

        if (property.Examples.Any())
        {
            schema["examples"] = property.Examples;
        }

        return schema;
    }

    private static string GetJsonType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        return Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean => "boolean",
            TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or
            TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 => "integer",
            TypeCode.Single or TypeCode.Double or TypeCode.Decimal => "number",
            TypeCode.String or TypeCode.Char => "string",
            TypeCode.DateTime => "string", // with format date-time
            _ => type.IsArray || typeof(System.Collections.IEnumerable).IsAssignableFrom(type) ? "array" : "object"
        };
    }

    private static bool IsRequiredType(Type type)
    {
        // Value types are required unless nullable
        return type.IsValueType && Nullable.GetUnderlyingType(type) == null;
    }

    private static object? GetDefaultValue(Type type)
    {
        if (type.IsValueType)
        {
            return Activator.CreateInstance(type);
        }
        return null;
    }

    private static ToolCategory DetermineCategory(Type type, MethodInfo method)
    {
        // Check for category in type name or namespace
        var typeName = type.Name.ToLowerInvariant();
        var namespaceParts = type.Namespace?.ToLowerInvariant() ?? "";

        if (typeName.Contains("query") || typeName.Contains("search") || 
            namespaceParts.Contains("query") || namespaceParts.Contains("search"))
            return ToolCategory.Query;

        if (typeName.Contains("analysis") || typeName.Contains("analyze") ||
            namespaceParts.Contains("analysis"))
            return ToolCategory.Analysis;

        if (typeName.Contains("modify") || typeName.Contains("refactor") ||
            typeName.Contains("update") || typeName.Contains("change"))
            return ToolCategory.Modification;

        if (typeName.Contains("report") || typeName.Contains("document"))
            return ToolCategory.Reporting;

        if (typeName.Contains("system") || typeName.Contains("admin"))
            return ToolCategory.System;

        return ToolCategory.General;
    }

    private static bool IsSystemAssembly(Assembly assembly)
    {
        var name = assembly.GetName().Name ?? "";
        return name.StartsWith("System.") ||
               name.StartsWith("Microsoft.") ||
               name.StartsWith("mscorlib") ||
               name.StartsWith("netstandard");
    }
}