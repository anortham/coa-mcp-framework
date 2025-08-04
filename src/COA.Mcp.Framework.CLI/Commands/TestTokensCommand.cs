using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using COA.Mcp.Framework.TokenOptimization;
using Spectre.Console;

namespace COA.Mcp.Framework.CLI.Commands;

public class TestTokensCommand : Command
{
    public TestTokensCommand() : base("test-tokens", "Test token estimation for various inputs")
    {
        var inputOption = new Option<string?>(
            new[] { "--input", "-i" },
            "Input text to estimate tokens for");
        
        var fileOption = new Option<string?>(
            new[] { "--file", "-f" },
            "File to estimate tokens for");
        
        var jsonOption = new Option<bool>(
            new[] { "--json", "-j" },
            "Treat input as JSON and estimate object tokens");
        
        var compareOption = new Option<bool>(
            new[] { "--compare", "-c" },
            "Compare different estimation strategies");

        AddOption(inputOption);
        AddOption(fileOption);
        AddOption(jsonOption);
        AddOption(compareOption);

        this.SetHandler(async (InvocationContext context) =>
        {
            var input = context.ParseResult.GetValueForOption(inputOption);
            var file = context.ParseResult.GetValueForOption(fileOption);
            var isJson = context.ParseResult.GetValueForOption(jsonOption);
            var compare = context.ParseResult.GetValueForOption(compareOption);

            await TestTokenEstimation(input, file, isJson, compare);
        });
    }

    private async Task TestTokenEstimation(string? input, string? file, bool isJson, bool compare)
    {
        AnsiConsole.MarkupLine("[blue]Token Estimation Test[/]");
        AnsiConsole.WriteLine();

        string? content = null;

        // Get content from input or file
        if (!string.IsNullOrEmpty(input))
        {
            content = input;
        }
        else if (!string.IsNullOrEmpty(file))
        {
            if (!File.Exists(file))
            {
                AnsiConsole.MarkupLine($"[red]File not found:[/] {file}");
                return;
            }
            content = await File.ReadAllTextAsync(file);
        }
        else
        {
            // Interactive mode
            content = AnsiConsole.Ask<string>("Enter text to estimate tokens for:");
        }

        if (string.IsNullOrEmpty(content))
        {
            AnsiConsole.MarkupLine("[yellow]No content provided.[/]");
            return;
        }

        // Perform estimation
        if (compare)
        {
            await CompareEstimationStrategies(content, isJson);
        }
        else
        {
            await EstimateSingle(content, isJson);
        }
    }

    private async Task EstimateSingle(string content, bool isJson)
    {
        int tokens;
        
        if (isJson)
        {
            try
            {
                var obj = JsonSerializer.Deserialize<JsonElement>(content);
                tokens = TokenEstimator.EstimateObject(obj);
                
                AnsiConsole.MarkupLine("[green]JSON Object Analysis:[/]");
                AnalyzeJsonStructure(obj);
            }
            catch (JsonException ex)
            {
                AnsiConsole.MarkupLine($"[red]Invalid JSON:[/] {ex.Message}");
                return;
            }
        }
        else
        {
            tokens = TokenEstimator.EstimateString(content);
        }

        // Display results
        var table = new Table();
        table.AddColumn("Metric");
        table.AddColumn("Value");

        table.AddRow("Content Length", content.Length.ToString("N0"));
        table.AddRow("Estimated Tokens", tokens.ToString("N0"));
        table.AddRow("Characters per Token", (content.Length / (double)tokens).ToString("F2"));
        table.AddRow("Token Density", (tokens / (double)content.Length * 100).ToString("F2") + "%");

        // Safety limits
        table.AddRow("", "");
        table.AddRow("[yellow]Safety Limits[/]", "");
        table.AddRow("Conservative Limit", $"{tokens}/{TokenEstimator.CONSERVATIVE_SAFETY_LIMIT} ({(tokens / (double)TokenEstimator.CONSERVATIVE_SAFETY_LIMIT * 100):F1}%)");
        table.AddRow("Default Limit", $"{tokens}/{TokenEstimator.DEFAULT_SAFETY_LIMIT} ({(tokens / (double)TokenEstimator.DEFAULT_SAFETY_LIMIT * 100):F1}%)");

        AnsiConsole.Write(table);

        // Warnings
        if (tokens > TokenEstimator.DEFAULT_SAFETY_LIMIT)
        {
            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("[red]⚠ Warning: Content exceeds default safety limit![/]");
            AnsiConsole.MarkupLine("[yellow]Consider using progressive reduction or response modes.[/]");
        }
        else if (tokens > TokenEstimator.CONSERVATIVE_SAFETY_LIMIT)
        {
            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("[yellow]⚠ Warning: Content exceeds conservative safety limit.[/]");
        }

        await Task.CompletedTask;
    }

    private async Task CompareEstimationStrategies(string content, bool isJson)
    {
        AnsiConsole.MarkupLine("[blue]Comparing Estimation Strategies[/]");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.AddColumn("Strategy");
        table.AddColumn("Tokens");
        table.AddColumn("Time (ms)");
        table.AddColumn("Accuracy");

        // Test different strategies
        var strategies = new (string name, Func<string, int> strategy)[]
        {
            ("Simple (length/4)", (string s) => s.Length / 4),
            ("Whitespace-based", (string s) => s.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length),
            ("Framework Default", (string s) => TokenEstimator.EstimateString(s)),
            ("Conservative (+20%)", (string s) => (int)(TokenEstimator.EstimateString(s) * 1.2))
        };

        foreach (var (name, strategy) in strategies)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var tokens = strategy(content);
            sw.Stop();

            table.AddRow(
                name,
                tokens.ToString("N0"),
                sw.ElapsedMilliseconds.ToString(),
                "N/A" // Would need actual API comparison for accuracy
            );
        }

        AnsiConsole.Write(table);

        // Progressive reduction demo
        if (isJson)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[blue]Progressive Reduction Simulation:[/]");
            
            var steps = new[] { 100, 75, 50, 30, 20, 10, 5 };
            var reductionTable = new Table();
            reductionTable.AddColumn("Reduction %");
            reductionTable.AddColumn("Estimated Tokens");
            reductionTable.AddColumn("Within Limit");

            var originalTokens = TokenEstimator.EstimateString(content);
            
            foreach (var step in steps)
            {
                var reducedLength = (int)(content.Length * (step / 100.0));
                var reducedContent = content.Substring(0, Math.Min(reducedLength, content.Length));
                var reducedTokens = TokenEstimator.EstimateString(reducedContent);
                
                reductionTable.AddRow(
                    $"{step}%",
                    reducedTokens.ToString("N0"),
                    reducedTokens <= TokenEstimator.DEFAULT_SAFETY_LIMIT ? "[green]✓[/]" : "[red]✗[/]"
                );
            }

            AnsiConsole.Write(reductionTable);
        }

        await Task.CompletedTask;
    }

    private void AnalyzeJsonStructure(JsonElement element, int depth = 0)
    {
        if (depth > 3) return; // Limit depth for display

        var tree = new Tree("JSON Structure");
        AnalyzeJsonElement(tree, element, depth);
        AnsiConsole.Write(tree);
    }

    private void AnalyzeJsonElement(IHasTreeNodes parent, JsonElement element, int depth)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var node = parent.AddNode($"[yellow]{prop.Name}[/]: {prop.Value.ValueKind}");
                    if (depth < 2)
                    {
                        AnalyzeJsonElement(node, prop.Value, depth + 1);
                    }
                }
                break;
                
            case JsonValueKind.Array:
                parent.AddNode($"[blue]Array[/] ({element.GetArrayLength()} items)");
                break;
                
            default:
                // Leaf nodes
                break;
        }
    }
}