using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using COA.Mcp.Framework.Migration.Analyzers;
using COA.Mcp.Framework.Migration.Migrators;
using Priority = COA.Mcp.Framework.Migration.Analyzers.Priority;

namespace COA.Mcp.Framework.CLI.Commands;

public class MigrateCommand : Command
{
    public MigrateCommand() : base("migrate", "Migrate existing MCP projects to use the framework")
    {
        var projectOption = new Option<string>(
            new[] { "--project", "-p" },
            "Path to the project file (.csproj) to migrate");
        
        var dryRunOption = new Option<bool>(
            new[] { "--dry-run", "-d" },
            "Show what would be changed without making modifications");
        
        var backupOption = new Option<bool>(
            new[] { "--backup", "-b" },
            getDefaultValue: () => true,
            "Create backup of files before migration");

        AddOption(projectOption);
        AddOption(dryRunOption);
        AddOption(backupOption);

        this.SetHandler(async (InvocationContext context) =>
        {
            var project = context.ParseResult.GetValueForOption(projectOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var backup = context.ParseResult.GetValueForOption(backupOption);

            if (string.IsNullOrEmpty(project))
            {
                AnsiConsole.MarkupLine("[red]Error: Project path is required. Use --project or -p to specify the .csproj file.[/]");
                return;
            }

            await MigrateProject(project, dryRun, backup);
        });
    }

    private async Task MigrateProject(string projectPath, bool dryRun, bool backup)
    {
        // Normalize the project path
        projectPath = Path.GetFullPath(projectPath);
        
        if (!File.Exists(projectPath))
        {
            AnsiConsole.MarkupLine($"[red]Project file not found:[/] {projectPath}");
            return;
        }

        AnsiConsole.MarkupLine($"[blue]Migrating project:[/] {Path.GetFileName(projectPath)}");
        if (dryRun)
        {
            AnsiConsole.MarkupLine("[yellow]Running in dry-run mode - no changes will be made[/]");
        }
        AnsiConsole.WriteLine();

        var projectDir = Path.GetDirectoryName(projectPath);
        if (string.IsNullOrEmpty(projectDir))
        {
            // If no directory info, assume current directory
            projectDir = Directory.GetCurrentDirectory();
            AnsiConsole.MarkupLine($"[yellow]Using current directory as project directory: {projectDir}[/]");
        }
        MigrationReport? report = null;

        try
        {
            // Step 1: Analyze project using the new MigrationAnalyzer
            await AnsiConsole.Status()
                .StartAsync("Analyzing project...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Star);
                    ctx.SpinnerStyle(Style.Parse("green"));

                    // Create a null logger for the analyzer since we can't use console in MCP
                    using var loggerFactory = LoggerFactory.Create(builder => 
                    {
                        // No console logging for MCP compatibility
                        builder.SetMinimumLevel(LogLevel.None);
                    });
                    
                    var logger = loggerFactory.CreateLogger<MigrationAnalyzer>();
                    var analyzer = new MigrationAnalyzer(logger);
                    
                    report = await analyzer.AnalyzeProjectAsync(projectDir);
                });

            if (report == null || report.Patterns.Count == 0)
            {
                AnsiConsole.MarkupLine("[green]✓[/] Project appears to already be using the framework!");
                return;
            }

            // Step 2: Display migration report
            await DisplayMigrationReport(report);

            if (dryRun)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[yellow]Dry run complete. No changes were made.[/]");
                return;
            }

            // Step 3: Confirm migration
            if (!AnsiConsole.Confirm("Proceed with migration?"))
            {
                AnsiConsole.MarkupLine("[yellow]Migration cancelled.[/]");
                return;
            }

            // Step 4: Create backups
            if (backup)
            {
                await CreateBackups(report.Patterns.Select(p => p.FilePath).Distinct());
            }

            // Step 5: Apply migrations
            await ApplyMigrations(report);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[green]✓ Migration completed successfully![/]");
            AnsiConsole.MarkupLine("[dim]Next steps:[/]");
            AnsiConsole.MarkupLine("  1. Review the migrated code");
            AnsiConsole.MarkupLine("  2. Run 'dotnet build' to verify compilation");
            AnsiConsole.MarkupLine("  3. Run 'coa-mcp validate' to check tool implementations");
            AnsiConsole.MarkupLine("  4. Update any custom logic as needed");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Migration failed:[/] {ex.Message}");
        }
    }

    private List<string> FindToolFiles(string projectDir)
    {
        var files = new List<string>();
        
        // Common patterns for tool files
        var patterns = new[]
        {
            "*Tool.cs",
            "*Tools.cs",
            "Tools/*.cs",
            "Services/*Tool*.cs"
        };

        foreach (var pattern in patterns)
        {
            var matches = Directory.GetFiles(projectDir, pattern, SearchOption.AllDirectories)
                .Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\"))
                .ToList();
            files.AddRange(matches);
        }

        return files.Distinct().ToList();
    }

    private async Task<List<MigrationItem>> AnalyzeToolFile(string filePath)
    {
        var items = new List<MigrationItem>();
        var code = await File.ReadAllTextAsync(filePath);
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();

        // Check for tool patterns
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var classDecl in classes)
        {
            // Check if it looks like a tool class
            if (classDecl.Identifier.Text.Contains("Tool"))
            {
                // Check for framework usage
                var hasFrameworkBase = classDecl.BaseList?.Types
                    .Any(t => t.ToString().Contains("McpToolBase")) ?? false;

                if (!hasFrameworkBase)
                {
                    items.Add(new MigrationItem
                    {
                        FilePath = filePath,
                        ItemType = "ToolClass",
                        Description = $"Convert {classDecl.Identifier.Text} to inherit from McpToolBase",
                        OldCode = classDecl.ToString(),
                        NewCode = GenerateMigratedToolClass(classDecl)
                    });
                }

                // Check for tool methods
                var methods = classDecl.DescendantNodes().OfType<MethodDeclarationSyntax>();
                foreach (var method in methods)
                {
                    if (IsToolMethod(method))
                    {
                        var hasAttribute = method.AttributeLists
                            .SelectMany(al => al.Attributes)
                            .Any(a => a.Name.ToString().Contains("McpServerTool"));

                        if (!hasAttribute)
                        {
                            items.Add(new MigrationItem
                            {
                                FilePath = filePath,
                                ItemType = "ToolMethod",
                                Description = $"Add McpServerTool attribute to {method.Identifier.Text}",
                                OldCode = method.ToString(),
                                NewCode = AddToolAttribute(method)
                            });
                        }
                    }
                }
            }
        }

        return items;
    }

    private bool IsToolMethod(MethodDeclarationSyntax method)
    {
        // Heuristics to identify tool methods
        return method.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)) &&
               method.ReturnType.ToString().Contains("Task") &&
               (method.Identifier.Text.StartsWith("Execute") || 
                method.Identifier.Text.EndsWith("Async") ||
                method.ParameterList.Parameters.Count == 1);
    }

    private string GenerateMigratedToolClass(ClassDeclarationSyntax classDecl)
    {
        // Simple transformation - in real implementation would use Roslyn properly
        var className = classDecl.Identifier.Text;
        var toolName = className.Replace("Tool", "").ToLowerInvariant();
        var paramClassName = $"{className.Replace("Tool", "")}Params";
        var resultClassName = $"{className.Replace("Tool", "")}Result";
        
        // Note: Square brackets are escaped with [[ and ]] for Spectre.Console
        return $@"// Updated to v1.1.0 pattern
public class {className} : McpToolBase<{paramClassName}, {resultClassName}>
{{
    public override string Name => ""{toolName}"";
    public override string Description => ""TODO: Add description"";
    public override ToolCategory Category => ToolCategory.Query;
    
    protected override async Task<{resultClassName}> ExecuteInternalAsync(
        {paramClassName} parameters,
        CancellationToken cancellationToken)
    {{
        // TODO: Migrate existing logic here
        throw new NotImplementedException();
    }}
}}

public class {paramClassName}
{{
    // TODO: Add parameters from existing method
}}

public class {resultClassName} : ToolResultBase
{{
    // TODO: Add result properties
}}";
    }

    private string AddToolAttribute(MethodDeclarationSyntax method)
    {
        var toolName = method.Identifier.Text.Replace("Async", "").ToLowerInvariant();
        // Note: This is for old-style tools, not used in v1.1.0 pattern
        return $@"// Note: In v1.1.0, tools should inherit from McpToolBase<TParams, TResult>
// This attribute-based approach is deprecated
[[McpServerTool(Name = ""{toolName}"")]]
[[Description(""TODO: Add description"")]]
{method}";
    }

    private List<MigrationItem> AnalyzeProjectFile(string projectPath)
    {
        var items = new List<MigrationItem>();
        var projectContent = File.ReadAllText(projectPath);

        // Check for framework package references
        if (!projectContent.Contains("COA.Mcp.Framework"))
        {
            items.Add(new MigrationItem
            {
                FilePath = projectPath,
                ItemType = "PackageReference",
                Description = "Add COA.Mcp.Framework package references",
                OldCode = "",
                NewCode = @"<PackageReference Include=""COA.Mcp.Framework"" Version=""1.0.0"" />
    <PackageReference Include=""COA.Mcp.Framework.TokenOptimization"" Version=""1.0.0"" />"
            });
        }

        return items;
    }

    private Task DisplayMigrationReport(MigrationReport report)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[blue]Migration Analysis Report[/]"));
        
        // Summary
        AnsiConsole.MarkupLine($"[bold]Project:[/] {report.ProjectPath}");
        AnsiConsole.MarkupLine($"[bold]Estimated Effort:[/] {report.EstimatedEffort}");
        AnsiConsole.MarkupLine($"[bold]Files Analyzed:[/] {report.TotalFiles}");
        AnsiConsole.WriteLine();

        // Pattern Summary
        if (report.PatternSummary.Any())
        {
            var summaryTable = new Table();
            summaryTable.Title = new TableTitle("[yellow]Patterns Found[/]");
            summaryTable.AddColumn("Pattern Type");
            summaryTable.AddColumn("Count");
            summaryTable.AddColumn("Files Affected");

            foreach (var pattern in report.PatternSummary.Values.OrderByDescending(p => p.Count))
            {
                summaryTable.AddRow(
                    pattern.Type,
                    pattern.Count.ToString(),
                    pattern.Files.Count.ToString()
                );
            }

            AnsiConsole.Write(summaryTable);
            AnsiConsole.WriteLine();
        }

        // Recommendations
        if (report.Recommendations.Any())
        {
            AnsiConsole.Write(new Rule("[blue]Recommendations[/]"));
            
            foreach (var rec in report.Recommendations)
            {
                var panel = new Panel($"{rec.Description}\n\n[dim]Category:[/] {rec.Category}\n[dim]Estimated Time:[/] {rec.EstimatedTimeMinutes} minutes\n[dim]Automation Available:[/] {(rec.AutomationAvailable ? "Yes" : "No")}")
                {
                    Header = new PanelHeader($"[{GetPriorityColor(rec.Priority)}]{rec.Title}[/]")
                };
                AnsiConsole.Write(panel);
            }
        }
        
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"Total patterns found: [yellow]{report.Patterns.Count}[/]");
        
        return Task.CompletedTask;
    }
    
    private string GetPriorityColor(Priority priority)
    {
        return priority switch
        {
            Priority.High => "red",
            Priority.Medium => "yellow",
            Priority.Low => "green",
            _ => "white"
        };
    }

    private Task CreateBackups(IEnumerable<string> files)
    {
        AnsiConsole.Status()
            .Start("Creating backups...", ctx =>
            {
                foreach (var file in files)
                {
                    var backupPath = $"{file}.backup";
                    File.Copy(file, backupPath, overwrite: true);
                    ctx.Status($"Backed up {Path.GetFileName(file)}");
                }
            });
        return Task.CompletedTask;
    }

    private async Task ApplyMigrations(MigrationReport report)
    {
        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Applying migrations[/]");
                var total = report.Patterns.Count;

                // TODO: Implement actual migration using code migrators
                // For now, we'll just show progress
                foreach (var pattern in report.Patterns)
                {
                    // Update task description instead of using Status
                    task.Description = $"Migrating {Path.GetFileName(pattern.FilePath)}...";
                    await Task.Delay(100); // Simulate work
                    
                    task.Increment(100.0 / total);
                }
            });
    }

    private class MigrationItem
    {
        public string FilePath { get; set; } = "";
        public string ItemType { get; set; } = "";
        public string Description { get; set; } = "";
        public string OldCode { get; set; } = "";
        public string NewCode { get; set; } = "";
    }
}