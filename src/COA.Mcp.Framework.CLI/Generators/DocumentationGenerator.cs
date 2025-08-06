using System.Reflection;
using System.Text;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using ComponentModel = System.ComponentModel;

namespace COA.Mcp.Framework.CLI.Generators;

public class DocumentationGenerator
{
    public async Task<string> GenerateMarkdownDocumentation(Assembly assembly)
    {
        var sb = new StringBuilder();
        
        // Find all tool types that inherit from McpToolBase<,> (v1.1.0 pattern)
        var toolTypes = assembly.GetTypes()
            .Where(t => IsToolType(t))
            .OrderBy(t => t.Name)
            .ToList();

        if (toolTypes.Count == 0)
        {
            return "No MCP tools found in assembly.";
        }

        // Header
        sb.AppendLine($"# {assembly.GetName().Name} - MCP Tools Documentation");
        sb.AppendLine();
        sb.AppendLine("Auto-generated documentation for MCP tools.");
        sb.AppendLine();
        sb.AppendLine("## Table of Contents");
        sb.AppendLine();

        // TOC
        foreach (var toolType in toolTypes)
        {
            var toolName = GetToolName(toolType);
            if (!string.IsNullOrEmpty(toolName))
            {
                sb.AppendLine($"- [{toolName}](#{toolName.Replace("_", "-")})");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Tools");
        sb.AppendLine();

        // Tool details
        foreach (var toolType in toolTypes)
        {
            await GenerateToolDocumentation(sb, toolType);
        }

        return sb.ToString();
    }

    private async Task GenerateToolDocumentation(StringBuilder sb, Type toolType)
    {
        var toolName = GetToolName(toolType);
        var toolDescription = GetToolDescription(toolType);
        var toolCategory = GetToolCategory(toolType);

        if (string.IsNullOrEmpty(toolName))
            return;

        sb.AppendLine($"### {toolName}");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(toolDescription))
        {
            // Parse multi-line description
            var lines = toolDescription.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("Returns:") || 
                    trimmedLine.StartsWith("Prerequisites:") || 
                    trimmedLine.StartsWith("Use cases:") ||
                    trimmedLine.StartsWith("Error handling:") ||
                    trimmedLine.StartsWith("Not for:"))
                {
                    sb.AppendLine($"**{trimmedLine}**");
                }
                else
                {
                    sb.AppendLine(trimmedLine);
                }
            }
        }
        else
        {
            sb.AppendLine("*No description available*");
        }

        sb.AppendLine();

        // Tool info
        sb.AppendLine($"**Category:** {toolCategory}");
        sb.AppendLine($"**Type:** `{toolType.Name}`");
        sb.AppendLine();

        // Parameters
        var (paramType, resultType) = GetToolGenericTypes(toolType);
        if (paramType != null)
        {
            sb.AppendLine("#### Parameters");
            sb.AppendLine();
            
            var props = paramType.GetProperties();
            if (props.Length > 0)
            {
                sb.AppendLine("| Parameter | Type | Required | Description |");
                sb.AppendLine("|-----------|------|----------|-------------|");

                foreach (var prop in props)
                {
                    var desc = prop.GetCustomAttribute<ComponentModel.DescriptionAttribute>();
                    var required = prop.GetCustomAttribute<ComponentModel.DataAnnotations.RequiredAttribute>() != null;
                    var typeName = GetFriendlyTypeName(prop.PropertyType);
                    var description = desc?.Description ?? "*No description*";

                    sb.AppendLine($"| {prop.Name} | {typeName} | {(required ? "Yes" : "No")} | {description} |");
                }
            }
            else
            {
                sb.AppendLine("*No parameters*");
            }
            sb.AppendLine();
        }

        // Return type
        if (resultType != null)
        {
            sb.AppendLine("#### Returns");
            sb.AppendLine();
            sb.AppendLine($"`{GetFriendlyTypeName(resultType)}`");
            sb.AppendLine();

            // Document result properties if it's a custom type
            var resultProps = resultType.GetProperties()
                .Where(p => p.DeclaringType != typeof(ToolResultBase))
                .ToArray();
            
            if (resultProps.Any())
            {
                sb.AppendLine("**Result Properties:**");
                sb.AppendLine();
                sb.AppendLine("| Property | Type | Description |");
                sb.AppendLine("|----------|------|-------------|");

                foreach (var prop in resultProps)
                {
                    var desc = prop.GetCustomAttribute<ComponentModel.DescriptionAttribute>();
                    var typeName = GetFriendlyTypeName(prop.PropertyType);
                    var description = desc?.Description ?? "*No description*";
                    
                    sb.AppendLine($"| {prop.Name} | {typeName} | {description} |");
                }
                sb.AppendLine();
            }
        }

        // Example
        sb.AppendLine("#### Example");
        sb.AppendLine();
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine($"  \"tool\": \"{toolName}\",");
        sb.AppendLine("  \"parameters\": {");

        if (paramType != null)
        {
            var props = paramType.GetProperties();
            for (int i = 0; i < props.Length; i++)
            {
                var prop = props[i];
                var example = GetExampleValue(prop.PropertyType);
                var comma = i < props.Length - 1 ? "," : "";
                sb.AppendLine($"    \"{ToCamelCase(prop.Name)}\": {example}{comma}");
            }
        }

        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("---");
        sb.AppendLine();

        await Task.CompletedTask;
    }

    private bool IsToolType(Type type)
    {
        if (type.IsAbstract || type.IsInterface)
            return false;

        var baseType = type.BaseType;
        while (baseType != null)
        {
            if (baseType.IsGenericType && 
                baseType.GetGenericTypeDefinition().Name == "McpToolBase`2")
            {
                return true;
            }
            baseType = baseType.BaseType;
        }

        return false;
    }

    private string? GetToolName(Type toolType)
    {
        try
        {
            var nameProp = toolType.GetProperty("Name");
            if (nameProp != null)
            {
                var instance = Activator.CreateInstance(toolType, GetConstructorArgs(toolType));
                return nameProp.GetValue(instance) as string;
            }
        }
        catch { }
        return null;
    }

    private string? GetToolDescription(Type toolType)
    {
        try
        {
            var descProp = toolType.GetProperty("Description");
            if (descProp != null)
            {
                var instance = Activator.CreateInstance(toolType, GetConstructorArgs(toolType));
                return descProp.GetValue(instance) as string;
            }
        }
        catch { }
        return null;
    }

    private string GetToolCategory(Type toolType)
    {
        try
        {
            var categoryProp = toolType.GetProperty("Category");
            if (categoryProp != null)
            {
                var instance = Activator.CreateInstance(toolType, GetConstructorArgs(toolType));
                var category = categoryProp.GetValue(instance);
                return category?.ToString() ?? "Unknown";
            }
        }
        catch { }
        return "Unknown";
    }

    private (Type? paramType, Type? resultType) GetToolGenericTypes(Type toolType)
    {
        var baseType = toolType.BaseType;
        while (baseType != null && baseType.IsGenericType)
        {
            if (baseType.GetGenericTypeDefinition().Name == "McpToolBase`2")
            {
                var genericArgs = baseType.GetGenericArguments();
                if (genericArgs.Length == 2)
                {
                    return (genericArgs[0], genericArgs[1]);
                }
            }
            baseType = baseType.BaseType;
        }
        return (null, null);
    }

    private object?[] GetConstructorArgs(Type type)
    {
        var constructors = type.GetConstructors();
        if (constructors.Length == 0)
            return Array.Empty<object?>();

        var constructor = constructors[0];
        var parameters = constructor.GetParameters();
        var args = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            // Try to provide default values for common types
            var paramType = parameters[i].ParameterType;
            if (paramType.IsInterface || paramType.IsAbstract)
            {
                args[i] = null;
            }
            else
            {
                try
                {
                    args[i] = Activator.CreateInstance(paramType);
                }
                catch
                {
                    args[i] = null;
                }
            }
        }

        return args;
    }

    private string GetFriendlyTypeName(Type type)
    {
        if (type.IsGenericType)
        {
            var name = type.Name.Substring(0, type.Name.IndexOf('`'));
            var genericArgs = type.GetGenericArguments();
            var genericNames = string.Join(", ", genericArgs.Select(GetFriendlyTypeName));
            return $"{name}<{genericNames}>";
        }

        var underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType != null)
        {
            return $"{GetFriendlyTypeName(underlyingType)}?";
        }

        return type.Name switch
        {
            "String" => "string",
            "Int32" => "int",
            "Int64" => "long",
            "Boolean" => "bool",
            "Double" => "double",
            "Single" => "float",
            "Decimal" => "decimal",
            "DateTime" => "DateTime",
            "Guid" => "Guid",
            _ => type.Name
        };
    }

    private string GetExampleValue(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        if (underlyingType == typeof(string))
            return "\"example\"";
        if (underlyingType == typeof(int) || underlyingType == typeof(long))
            return "123";
        if (underlyingType == typeof(bool))
            return "true";
        if (underlyingType == typeof(double) || underlyingType == typeof(float) || underlyingType == typeof(decimal))
            return "123.45";
        if (underlyingType.IsArray || (underlyingType.IsGenericType && underlyingType.GetGenericTypeDefinition() == typeof(List<>)))
            return "[]";
        if (underlyingType.IsClass)
            return "{}";

        return "null";
    }

    private string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }
}