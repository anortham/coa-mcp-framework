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
        AnsiConsole.MarkupLine($"  1. Update the parameter class ({name.Replace("Tool", "")}Params)");
        AnsiConsole.MarkupLine($"  2. Update the result class ({name.Replace("Tool", "")}Result)");
        AnsiConsole.MarkupLine($"  3. Add business logic to ExecuteInternalAsync method");
        AnsiConsole.MarkupLine($"  4. Register the tool with builder.RegisterToolType<{name}>() in your server");
    }

    private string GenerateToolCode(string name, string category)
    {
        var toolNameLower = name.Replace("Tool", "").ToLowerInvariant();
        var paramClassName = $"{name.Replace("Tool", "")}Params";
        var resultClassName = $"{name.Replace("Tool", "")}Result";

        return $@"using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;

namespace MyMcpServer.Tools;

/// <summary>
/// TODO: Add tool description here.
/// Returns: TODO: Describe what this tool returns.
/// Prerequisites: TODO: List any prerequisites.
/// Use cases: TODO: List common use cases.
/// </summary>
public class {name} : McpToolBase<{paramClassName}, {resultClassName}>
{{
    private readonly ILogger<{name}> _logger;

    public override string Name => ""{toolNameLower}"";
    public override string Description => @""TODO: Add tool description here.
    Returns: TODO: Describe what this tool returns.
    Prerequisites: TODO: List any prerequisites.
    Use cases: TODO: List common use cases."";
    public override ToolCategory Category => ToolCategory.{category};

    public {name}(ILogger<{name}> logger)
    {{
        _logger = logger;
    }}

    protected override async Task<{resultClassName}> ExecuteInternalAsync(
        {paramClassName} parameters,
        CancellationToken cancellationToken)
    {{
        _logger.LogInformation(""Executing {toolNameLower} tool"");

        try
        {{
            // TODO: Implement your tool logic here
            await Task.Delay(100, cancellationToken); // Simulate work

            return new {resultClassName}
            {{
                Success = true,
                Operation = ""{toolNameLower}"",
                Message = ""TODO: Replace with actual result"",
                // Add your data properties here
            }};
        }}
        catch (Exception ex)
        {{
            _logger.LogError(ex, ""Error executing {toolNameLower}"");
            
            return new {resultClassName}
            {{
                Success = false,
                Operation = ""{toolNameLower}"",
                Error = new ErrorInfo
                {{
                    Code = ""ERROR_CODE"",
                    Message = ex.Message,
                    Recovery = new RecoveryInfo
                    {{
                        Steps = new[] {{ ""Step 1: Check X"", ""Step 2: Try Y"" }},
                        SuggestedActions = new[]
                        {{
                            new SuggestedAction
                            {{
                                Id = ""retry"",
                                Description = ""Retry the operation"",
                                Priority = ""high""
                            }}
                        }}
                    }}
                }}
            }};
        }}
    }}
}}

/// <summary>
/// Parameters for the {toolNameLower} tool
/// </summary>
public class {paramClassName}
{{
    // TODO: Define your parameters here
    // [Required]
    // [Description(""Description of this parameter"")]
    // public string? ExampleParam {{ get; set; }}
    
    // [Range(1, 100)]
    // [Description(""An optional numeric parameter"")]
    // public int? MaxResults {{ get; set; }}
}}

/// <summary>
/// Result from the {toolNameLower} tool
/// </summary>
public class {resultClassName} : ToolResultBase
{{
    // TODO: Add your result properties here
    public string? Message {{ get; set; }}
    
    // Example: public List<string>? Items {{ get; set; }}
    // Example: public int Count {{ get; set; }}
}}";
    }
}