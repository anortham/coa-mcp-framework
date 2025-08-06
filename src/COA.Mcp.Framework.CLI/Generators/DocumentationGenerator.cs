using System.Reflection;
using System.Text;
using COA.Mcp.Framework.Attributes;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Interfaces;
using ComponentModel = System.ComponentModel;

namespace COA.Mcp.Framework.CLI.Generators;

public class DocumentationGenerator
{
    public async Task<string> GenerateMarkdownDocumentation(Assembly assembly)
    {
        var sb = new StringBuilder();
        
        // Find all tool types
        var toolTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
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
            var toolMethods = GetToolMethods(toolType);
            foreach (var method in toolMethods)
            {
                var attr = method.GetCustomAttribute<McpServerToolAttribute>()!;
                sb.AppendLine($"- [{attr.Name}](#{attr.Name.Replace("_", "-")})");
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
        var toolMethods = GetToolMethods(toolType);

        foreach (var method in toolMethods)
        {
            var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>()!;
            var descAttr = method.GetCustomAttribute<COA.Mcp.Framework.Attributes.DescriptionAttribute>();

            sb.AppendLine($"### {toolAttr.Name}");
            sb.AppendLine();

            if (descAttr != null)
            {
                // Parse multi-line description
                var lines = descAttr.Description.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (trimmedLine.StartsWith("Returns:") || 
                        trimmedLine.StartsWith("Prerequisites:") || 
                        trimmedLine.StartsWith("Use cases:"))
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
            if (toolType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMcpTool<,>)))
            {
                var instance = Activator.CreateInstance(toolType, GetConstructorArgs(toolType));
                if (instance is IMcpTool toolBase)
                {
                    sb.AppendLine($"**Category:** {toolBase.Category}");
                    sb.AppendLine();
                }
            }

            // Parameters
            var parameters = method.GetParameters();
            if (parameters.Length > 0)
            {
                var paramType = parameters[0].ParameterType;
                sb.AppendLine("#### Parameters");
                sb.AppendLine();
                
                var props = paramType.GetProperties();
                if (props.Length > 0)
                {
                    sb.AppendLine("| Parameter | Type | Required | Description |");
                    sb.AppendLine("|-----------|------|----------|-------------|");

                    foreach (var prop in props)
                    {
                        var propDesc = prop.GetCustomAttribute<COA.Mcp.Framework.Attributes.DescriptionAttribute>();
                        var isNullable = Nullable.GetUnderlyingType(prop.PropertyType) != null || 
                                        !prop.PropertyType.IsValueType ||
                                        prop.PropertyType == typeof(string);
                        
                        var typeName = GetFriendlyTypeName(prop.PropertyType);
                        var required = isNullable ? "No" : "Yes";
                        var description = propDesc?.Description ?? "*No description*";

                        sb.AppendLine($"| {prop.Name} | {typeName} | {required} | {description} |");
                    }
                }
                else
                {
                    sb.AppendLine("*No parameters*");
                }
                sb.AppendLine();
            }

            // Example
            sb.AppendLine("#### Example");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine($"  \"tool\": \"{toolAttr.Name}\",");
            sb.AppendLine("  \"parameters\": {");

            if (parameters.Length > 0)
            {
                var paramType = parameters[0].ParameterType;
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
        }

        await Task.CompletedTask;
    }

    private List<MethodInfo> GetToolMethods(Type toolType)
    {
        return toolType.GetMethods()
            .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null)
            .OrderBy(m => m.GetCustomAttribute<McpServerToolAttribute>()!.Name)
            .ToList();
    }

    private object?[] GetConstructorArgs(Type type)
    {
        var constructors = type.GetConstructors();
        if (constructors.Length == 0) return Array.Empty<object>();

        var constructor = constructors[0];
        var parameters = constructor.GetParameters();
        return parameters.Select(p => (object?)null).ToArray();
    }

    private string GetFriendlyTypeName(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            return GetFriendlyTypeName(type.GetGenericArguments()[0]) + "?";
        }

        if (type == typeof(string)) return "string";
        if (type == typeof(int)) return "int";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(DateTime)) return "DateTime";
        if (type == typeof(decimal)) return "decimal";
        if (type == typeof(double)) return "double";
        if (type == typeof(float)) return "float";
        
        if (type.IsArray)
        {
            return GetFriendlyTypeName(type.GetElementType()!) + "[]";
        }

        if (type.IsGenericType)
        {
            var genericType = type.GetGenericTypeDefinition();
            if (genericType == typeof(List<>))
            {
                return GetFriendlyTypeName(type.GetGenericArguments()[0]) + "[]";
            }
            if (genericType == typeof(Dictionary<,>))
            {
                var args = type.GetGenericArguments();
                return $"Dictionary<{GetFriendlyTypeName(args[0])}, {GetFriendlyTypeName(args[1])}>";
            }
        }

        return type.Name;
    }

    private string GetExampleValue(Type type)
    {
        if (type == typeof(string) || Nullable.GetUnderlyingType(type) == typeof(string))
            return "\"example\"";
        if (type == typeof(int) || type == typeof(int?))
            return "123";
        if (type == typeof(bool) || type == typeof(bool?))
            return "true";
        if (type == typeof(DateTime) || type == typeof(DateTime?))
            return "\"2024-01-01T00:00:00Z\"";
        if (type == typeof(decimal) || type == typeof(decimal?) ||
            type == typeof(double) || type == typeof(double?) ||
            type == typeof(float) || type == typeof(float?))
            return "123.45";
        
        if (type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)))
            return "[]";

        return "{}";
    }

    private string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    public async Task<string> GenerateOpenApiSpec(Assembly assembly)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("{");
        sb.AppendLine("  \"openapi\": \"3.0.0\",");
        sb.AppendLine("  \"info\": {");
        sb.AppendLine($"    \"title\": \"{assembly.GetName().Name} MCP API\",");
        sb.AppendLine("    \"version\": \"1.0.0\",");
        sb.AppendLine("    \"description\": \"Model Context Protocol tools API\"");
        sb.AppendLine("  },");
        sb.AppendLine("  \"paths\": {");

        var toolTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
            .ToList();

        var toolCount = 0;
        foreach (var toolType in toolTypes)
        {
            var toolMethods = GetToolMethods(toolType);
            foreach (var method in toolMethods)
            {
                if (toolCount > 0) sb.AppendLine(",");
                await GenerateOpenApiPath(sb, method);
                toolCount++;
            }
        }

        sb.AppendLine();
        sb.AppendLine("  }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private async Task GenerateOpenApiPath(StringBuilder sb, MethodInfo method)
    {
        var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>()!;
        var descAttr = method.GetCustomAttribute<COA.Mcp.Framework.Attributes.DescriptionAttribute>();

        sb.AppendLine($"    \"/tools/{toolAttr.Name}\": {{");
        sb.AppendLine("      \"post\": {");
        sb.AppendLine($"        \"summary\": \"{toolAttr.Name}\",");
        if (descAttr != null)
        {
            var desc = descAttr.Description.Replace("\"", "\\\"").Replace("\n", " ");
            sb.AppendLine($"        \"description\": \"{desc}\",");
        }
        sb.AppendLine("        \"requestBody\": {");
        sb.AppendLine("          \"required\": true,");
        sb.AppendLine("          \"content\": {");
        sb.AppendLine("            \"application/json\": {");
        sb.AppendLine("              \"schema\": {");
        
        var parameters = method.GetParameters();
        if (parameters.Length > 0)
        {
            await GenerateJsonSchema(sb, parameters[0].ParameterType, indent: 16);
        }
        else
        {
            sb.AppendLine("                \"type\": \"object\"");
        }

        sb.AppendLine("              }");
        sb.AppendLine("            }");
        sb.AppendLine("          }");
        sb.AppendLine("        },");
        sb.AppendLine("        \"responses\": {");
        sb.AppendLine("          \"200\": {");
        sb.AppendLine("            \"description\": \"Success\"");
        sb.AppendLine("          }");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.Append("    }");
    }

    private async Task GenerateJsonSchema(StringBuilder sb, Type type, int indent)
    {
        var padding = new string(' ', indent);
        
        sb.AppendLine($"{padding}\"type\": \"object\",");
        sb.AppendLine($"{padding}\"properties\": {{");

        var props = type.GetProperties();
        for (int i = 0; i < props.Length; i++)
        {
            var prop = props[i];
            var propDesc = prop.GetCustomAttribute<COA.Mcp.Framework.Attributes.DescriptionAttribute>();
            
            sb.AppendLine($"{padding}  \"{ToCamelCase(prop.Name)}\": {{");
            sb.Append($"{padding}    \"type\": \"{GetJsonSchemaType(prop.PropertyType)}\"");
            if (propDesc != null)
            {
                sb.AppendLine(",");
                sb.AppendLine($"{padding}    \"description\": \"{propDesc.Description}\"");
            }
            else
            {
                sb.AppendLine();
            }
            
            if (i < props.Length - 1)
            {
                sb.AppendLine($"{padding}  }},");
            }
            else
            {
                sb.AppendLine($"{padding}  }}");
            }
        }

        sb.Append($"{padding}}}");
        
        await Task.CompletedTask;
    }

    private string GetJsonSchemaType(Type type)
    {
        if (type == typeof(string)) return "string";
        if (type == typeof(int) || type == typeof(int?)) return "integer";
        if (type == typeof(bool) || type == typeof(bool?)) return "boolean";
        if (type == typeof(decimal) || type == typeof(double) || type == typeof(float) ||
            type == typeof(decimal?) || type == typeof(double?) || type == typeof(float?))
            return "number";
        if (type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)))
            return "array";
        return "object";
    }
}