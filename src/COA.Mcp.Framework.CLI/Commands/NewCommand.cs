using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Spectre.Console;

namespace COA.Mcp.Framework.CLI.Commands;

public class NewCommand : Command
{
    public NewCommand() : base("new", "Create new MCP components")
    {
        var subCommand = new Command("tool", "Create a new MCP tool");
        
        var nameArgument = new Argument<string>("name", "Name of the tool");
        var outputOption = new Option<string?>(
            new[] { "--output", "-o" },
            "Output directory (defaults to current directory)");
        var categoryOption = new Option<string>(
            new[] { "--category", "-c" },
            getDefaultValue: () => "Query",
            "Tool category (Query, Command, Information)");

        subCommand.AddArgument(nameArgument);
        subCommand.AddOption(outputOption);
        subCommand.AddOption(categoryOption);

        subCommand.SetHandler(async (InvocationContext context) =>
        {
            var name = context.ParseResult.GetValueForArgument(nameArgument);
            var output = context.ParseResult.GetValueForOption(outputOption) ?? Environment.CurrentDirectory;
            var category = context.ParseResult.GetValueForOption(categoryOption);

            await CreateNewTool(name, output, category);
        });

        AddCommand(subCommand);
    }

    private async Task CreateNewTool(string name, string outputDir, string? category)
    {
        AnsiConsole.MarkupLine($"[green]Creating new tool:[/] {name}");

        // Ensure name ends with "Tool"
        if (!name.EndsWith("Tool"))
        {
            name += "Tool";
        }

        // Generate tool code
        var code = GenerateToolCode(name, category ?? "Query");

        // Write to file
        var fileName = Path.Combine(outputDir, $"{name}.cs");
        
        if (File.Exists(fileName))
        {
            if (!AnsiConsole.Confirm($"File {fileName} already exists. Overwrite?"))
            {
                AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
                return;
            }
        }

        await File.WriteAllTextAsync(fileName, code);

        AnsiConsole.MarkupLine($"[green]✓[/] Created {fileName}");
        AnsiConsole.MarkupLine($"[dim]Next steps:[/]");
        AnsiConsole.MarkupLine($"  1. Update the tool implementation in {name}.cs");
        AnsiConsole.MarkupLine($"  2. Define your parameter class");
        AnsiConsole.MarkupLine($"  3. Add business logic to ExecuteAsync method");
        AnsiConsole.MarkupLine($"  4. The tool will be automatically discovered at startup");
    }

    private string GenerateToolCode(string name, string category)
    {
        var toolNameLower = name.Replace("Tool", "").ToLowerInvariant();
        var paramClassName = $"{name.Replace("Tool", "")}Params";

        return $@"using COA.Mcp.Framework;
using COA.Mcp.Framework.Attributes;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework.TokenOptimization;
using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace MyMcpServer.Tools;

[McpServerToolType]
public class {name} : McpToolBase
{{
    private readonly ILogger<{name}> _logger;
    private readonly ITokenEstimator _tokenEstimator;

    public override string ToolName => ""{toolNameLower}"";
    public override ToolCategory Category => ToolCategory.{category};

    public {name}(ILogger<{name}> logger, ITokenEstimator tokenEstimator)
    {{
        _logger = logger;
        _tokenEstimator = tokenEstimator;
    }}

    [McpServerTool(Name = ""{toolNameLower}"")]
    [Description(@""TODO: Add tool description here.
    Returns: TODO: Describe what this tool returns.
    Prerequisites: TODO: List any prerequisites.
    Use cases: TODO: List common use cases."")]
    public async Task<object> ExecuteAsync({paramClassName} parameters)
    {{
        _logger.LogInformation(""Executing {toolNameLower} tool"");

        // Validate parameters using base class helpers
        // var requiredParam = ValidateRequired(parameters.RequiredParam, nameof(parameters.RequiredParam));

        return await ExecuteWithTokenManagement(async () =>
        {{
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            // TODO: Implement your tool logic here
            await Task.Delay(100); // Simulate work

            var result = new
            {{
                Success = true,
                Message = ""TODO: Replace with actual result"",
                // Add your data here
            }};

            sw.Stop();

            // Generate insights about the operation
            var insights = new List<string>
            {{
                ""TODO: Add meaningful insights about the operation"",
                ""TODO: Add another insight"",
                $""Operation completed in {{sw.ElapsedMilliseconds}}ms""
            }};

            // Suggest next actions
            var actions = new List<AIAction>
            {{
                new AIAction
                {{
                    Tool = ""another_tool"",
                    Description = ""TODO: Suggest a logical next action""
                }}
            }};

            return new
            {{
                Success = true,
                Data = result,
                Insights = insights,
                Actions = actions,
                Meta = new ToolMetadata
                {{
                    ExecutionTime = $""{{sw.ElapsedMilliseconds}}ms"",
                    TokensEstimated = _tokenEstimator.EstimateObject(result),
                    ToolVersion = ""1.0.0""
                }}
            }};
        }});
    }}
}}

public class {paramClassName}
{{
    // TODO: Define your parameters here
    // [Description(""Description of this parameter"")]
    // public string? ExampleParam {{ get; set; }}
}}";
    }
}