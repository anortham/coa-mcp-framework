using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Reflection;
using COA.Mcp.Framework.Attributes;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.CLI.Generators;
using Microsoft.Extensions.FileSystemGlobbing;
using Spectre.Console;

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

        // Find all types with McpServerToolType attribute
        var toolTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
            .ToList();

        foreach (var toolType in toolTypes)
        {
            var typeIssues = ValidateToolType(toolType, verbose);
            issues += typeIssues.Count;

            // Find tool methods
            var toolMethods = toolType.GetMethods()
                .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null)
                .ToList();

            tools += toolMethods.Count;

            foreach (var method in toolMethods)
            {
                var methodIssues = ValidateToolMethod(method, verbose);
                issues += methodIssues.Count;
            }

            if (verbose || typeIssues.Count > 0)
            {
                DisplayToolTypeValidation(toolType, toolMethods, typeIssues);
            }
        }

        await Task.CompletedTask;
        return (tools, issues);
    }

    private List<string> ValidateToolType(Type type, bool verbose)
    {
        var issues = new List<string>();

        // Check if inherits from McpToolBase
        if (!typeof(McpToolBase).IsAssignableFrom(type))
        {
            issues.Add($"Does not inherit from McpToolBase");
        }

        // Check for parameterless constructor or DI constructor
        var constructors = type.GetConstructors();
        if (constructors.Length == 0)
        {
            issues.Add("No public constructors found");
        }

        // Check for ToolName property override
        var toolNameProp = type.GetProperty("ToolName");
        if (toolNameProp == null || !toolNameProp.GetGetMethod()!.IsVirtual)
        {
            issues.Add("Missing ToolName property override");
        }

        // Check for Category property override
        var categoryProp = type.GetProperty("Category");
        if (categoryProp == null || !categoryProp.GetGetMethod()!.IsVirtual)
        {
            issues.Add("Missing Category property override");
        }

        return issues;
    }

    private List<string> ValidateToolMethod(MethodInfo method, bool verbose)
    {
        var issues = new List<string>();

        // Check method signature
        if (!method.Name.EndsWith("Async"))
        {
            issues.Add("Method name should end with 'Async'");
        }

        // Check return type
        var returnType = method.ReturnType;
        if (!typeof(Task).IsAssignableFrom(returnType))
        {
            issues.Add("Method must return Task or Task<T>");
        }

        // Check for Description attribute
        if (method.GetCustomAttribute<COA.Mcp.Framework.Attributes.DescriptionAttribute>() == null)
        {
            issues.Add("Missing Description attribute");
        }

        // Check parameters
        var parameters = method.GetParameters();
        if (parameters.Length != 1)
        {
            issues.Add("Method should have exactly one parameter");
        }
        else
        {
            var paramType = parameters[0].ParameterType;
            // Validate parameter type has properties with descriptions
            var props = paramType.GetProperties();
            foreach (var prop in props)
            {
                if (prop.GetCustomAttribute<COA.Mcp.Framework.Attributes.DescriptionAttribute>() == null && verbose)
                {
                    issues.Add($"Parameter property '{prop.Name}' missing Description attribute");
                }
            }
        }

        return issues;
    }

    private void DisplayToolTypeValidation(Type type, List<MethodInfo> methods, List<string> issues)
    {
        var tree = new Tree($"[yellow]{type.Name}[/]");

        foreach (var method in methods)
        {
            var attr = method.GetCustomAttribute<McpServerToolAttribute>()!;
            var node = tree.AddNode($"[green]✓[/] {attr.Name}");
        }

        foreach (var issue in issues)
        {
            tree.AddNode($"[red]✗[/] {issue}");
        }

        AnsiConsole.Write(tree);
    }
}