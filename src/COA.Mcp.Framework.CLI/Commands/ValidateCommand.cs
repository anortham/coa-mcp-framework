using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Reflection;
using COA.Mcp.Framework.Attributes;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework.CLI.Generators;
using Microsoft.Extensions.FileSystemGlobbing;
using Spectre.Console;
using ComponentModel = System.ComponentModel;

namespace COA.Mcp.Framework.CLI.Commands;

public class ValidateCommand : Command
{
    public ValidateCommand() : base("validate", "Validate MCP tools in a project")
    {
        var pathOption = new Option<string?>(
            new[] { "--path", "-p" },
            "Path to project or assembly to validate");
        
        var verboseOption = new Option<bool>(
            new[] { "--verbose", "-v" },
            "Show detailed validation information");
        
        var generateDocsOption = new Option<bool>(
            new[] { "--generate-docs", "-d" },
            "Generate documentation for validated tools");

        AddOption(pathOption);
        AddOption(verboseOption);
        AddOption(generateDocsOption);

        this.SetHandler(async (InvocationContext context) =>
        {
            var path = context.ParseResult.GetValueForOption(pathOption) ?? Environment.CurrentDirectory;
            var verbose = context.ParseResult.GetValueForOption(verboseOption);
            var generateDocs = context.ParseResult.GetValueForOption(generateDocsOption);

            await ValidateProject(path, verbose, generateDocs);
        });
    }

    private async Task ValidateProject(string path, bool verbose, bool generateDocs)
    {
        AnsiConsole.MarkupLine($"[blue]Validating MCP tools in:[/] {path}");
        AnsiConsole.WriteLine();

        try
        {
            // Find assemblies to validate
            var assemblies = FindAssemblies(path);
            
            if (assemblies.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No assemblies found to validate.[/]");
                return;
            }

            var totalTools = 0;
            var totalIssues = 0;

            foreach (var assemblyPath in assemblies)
            {
                AnsiConsole.MarkupLine($"[dim]Checking:[/] {Path.GetFileName(assemblyPath)}");
                
                try
                {
                    var assembly = Assembly.LoadFrom(assemblyPath);
                    var (tools, issues) = await ValidateAssembly(assembly, verbose);
                    
                    totalTools += tools;
                    totalIssues += issues;
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Failed to load assembly:[/] {ex.Message}");
                    totalIssues++;
                }
                
                AnsiConsole.WriteLine();
            }

            // Summary
            AnsiConsole.Write(new Rule("[blue]Validation Summary[/]"));
            AnsiConsole.MarkupLine($"Total tools found: [green]{totalTools}[/]");
            
            if (totalIssues == 0)
            {
                AnsiConsole.MarkupLine("[green]✓ All validations passed![/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]✗ Found {totalIssues} issue(s)[/]");
            }

            // Generate documentation if requested
            if (generateDocs && totalTools > 0)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Rule("[blue]Documentation Generation[/]"));
                
                var docGen = new DocumentationGenerator();
                foreach (var assemblyPath in assemblies)
                {
                    try
                    {
                        var assembly = Assembly.LoadFrom(assemblyPath);
                        var markdown = await docGen.GenerateMarkdownDocumentation(assembly);
                        
                        var docPath = Path.Combine(
                            Path.GetDirectoryName(assemblyPath)!,
                            $"{Path.GetFileNameWithoutExtension(assemblyPath)}-tools.md"
                        );
                        
                        await File.WriteAllTextAsync(docPath, markdown);
                        AnsiConsole.MarkupLine($"[green]✓[/] Generated documentation: {Path.GetFileName(docPath)}");
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]Failed to generate docs:[/] {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Validation failed:[/] {ex.Message}");
        }
    }

    private List<string> FindAssemblies(string path)
    {
        var assemblies = new List<string>();

        if (File.Exists(path) && Path.GetExtension(path).ToLower() == ".dll")
        {
            assemblies.Add(path);
        }
        else if (Directory.Exists(path))
        {
            // Look for built assemblies
            var matcher = new Matcher();
            matcher.AddInclude("**/bin/**/*.dll");
            matcher.AddExclude("**/bin/**/Microsoft.*.dll");
            matcher.AddExclude("**/bin/**/System.*.dll");
            matcher.AddExclude("**/bin/**/COA.Mcp.Framework*.dll");

            var matches = matcher.Execute(new Microsoft.Extensions.FileSystemGlobbing.Abstractions.DirectoryInfoWrapper(new DirectoryInfo(path)));
            
            foreach (var match in matches.Files)
            {
                var fullPath = Path.Combine(path, match.Path);
                assemblies.Add(fullPath);
            }
        }

        return assemblies;
    }

    private async Task<(int tools, int issues)> ValidateAssembly(Assembly assembly, bool verbose)
    {
        var tools = 0;
        var issues = 0;

        // Find all types that inherit from McpToolBase<,> (v1.1.0 pattern)
        var toolTypes = assembly.GetTypes()
            .Where(t => IsToolType(t))
            .ToList();

        foreach (var toolType in toolTypes)
        {
            tools++;
            var typeIssues = ValidateToolType(toolType, verbose);
            issues += typeIssues.Count;

            if (verbose || typeIssues.Count > 0)
            {
                DisplayToolTypeValidation(toolType, typeIssues);
            }
        }

        await Task.CompletedTask;
        return (tools, issues);
    }

    private bool IsToolType(Type type)
    {
        if (type.IsAbstract || type.IsInterface)
            return false;

        // Check if it inherits from McpToolBase<,>
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

    private List<string> ValidateToolType(Type type, bool verbose)
    {
        var issues = new List<string>();

        // Check if inherits from McpToolBase<,>
        if (!IsToolType(type))
        {
            issues.Add($"Does not inherit from McpToolBase<TParams, TResult>");
            return issues;
        }

        // Check for Name property override
        var nameProp = type.GetProperty("Name");
        if (nameProp == null || !nameProp.GetGetMethod()!.IsVirtual)
        {
            issues.Add("Missing Name property override");
        }

        // Check for Description property override
        var descProp = type.GetProperty("Description");
        if (descProp == null || !descProp.GetGetMethod()!.IsVirtual)
        {
            issues.Add("Missing Description property override");
        }

        // Check for Category property override
        var categoryProp = type.GetProperty("Category");
        if (categoryProp == null || !categoryProp.GetGetMethod()!.IsVirtual)
        {
            issues.Add("Missing Category property override");
        }

        // Check for ExecuteInternalAsync method override
        var executeMethod = type.GetMethod("ExecuteInternalAsync", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (executeMethod == null || !executeMethod.IsVirtual)
        {
            issues.Add("Missing ExecuteInternalAsync method override");
        }

        // Check for proper constructor
        var constructors = type.GetConstructors();
        if (constructors.Length == 0)
        {
            issues.Add("No public constructors found");
        }

        // Validate parameter and result types
        var baseType = type.BaseType;
        while (baseType != null && baseType.IsGenericType)
        {
            if (baseType.GetGenericTypeDefinition().Name == "McpToolBase`2")
            {
                var genericArgs = baseType.GetGenericArguments();
                if (genericArgs.Length == 2)
                {
                    var paramType = genericArgs[0];
                    var resultType = genericArgs[1];

                    // Check if result type inherits from ToolResultBase
                    if (!typeof(ToolResultBase).IsAssignableFrom(resultType))
                    {
                        issues.Add($"Result type {resultType.Name} does not inherit from ToolResultBase");
                    }

                    // Check parameter properties for descriptions (if verbose)
                    if (verbose)
                    {
                        var props = paramType.GetProperties();
                        foreach (var prop in props)
                        {
                            if (prop.GetCustomAttribute<ComponentModel.DescriptionAttribute>() == null)
                            {
                                issues.Add($"Parameter property '{prop.Name}' missing Description attribute");
                            }
                        }
                    }
                }
                break;
            }
            baseType = baseType.BaseType;
        }

        return issues;
    }


    private void DisplayToolTypeValidation(Type type, List<string> issues)
    {
        var tree = new Tree($"[yellow]{type.Name}[/]");

        // Try to get the tool name
        var nameProp = type.GetProperty("Name");
        if (nameProp != null)
        {
            try
            {
                var instance = Activator.CreateInstance(type, GetConstructorArgs(type));
                var name = nameProp.GetValue(instance) as string;
                if (!string.IsNullOrEmpty(name))
                {
                    tree.AddNode($"[green]Tool name:[/] {name}");
                }
            }
            catch
            {
                // Unable to instantiate, skip
            }
        }

        if (issues.Count == 0)
        {
            tree.AddNode($"[green]✓[/] All validations passed");
        }
        else
        {
            foreach (var issue in issues)
            {
                tree.AddNode($"[red]✗[/] {issue}");
            }
        }

        AnsiConsole.Write(tree);
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
}