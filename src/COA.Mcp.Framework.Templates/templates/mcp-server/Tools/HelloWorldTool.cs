using COA.Mcp.Framework;
using COA.Mcp.Framework.Attributes;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using System.ComponentModel;

namespace McpServerTemplate.Tools;

[McpServerToolType]
public class HelloWorldTool : McpToolBase
{
    private readonly ILogger<HelloWorldTool> _logger;

    public override string ToolName => "hello_world";
    public override ToolCategory Category => ToolCategory.Information;

    public HelloWorldTool(ILogger<HelloWorldTool> logger)
    {
        _logger = logger;
    }

    [McpServerTool(Name = "hello_world")]
    [Description(@"Simple greeting tool that demonstrates basic MCP tool structure.
    Returns: Personalized greeting message with execution metadata.
    Prerequisites: None.
    Use cases: Testing MCP server, understanding tool structure, basic examples.")]
    public async Task<object> ExecuteAsync(HelloWorldParams parameters)
    {
        _logger.LogInformation("Executing hello_world tool for {Name}", parameters.Name);

        // Use built-in validation from base class
        var name = ValidateRequired(parameters.Name, nameof(parameters.Name))?.Trim() ?? "World";
        var includeTime = parameters.IncludeTime ?? false;

        // Token-aware execution from base class
        return await ExecuteWithTokenManagement(async () =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            // Simulate some async work
            await Task.Delay(100);

            var greeting = $"Hello, {name}!";
            if (includeTime)
            {
                greeting += $" The current time is {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC.";
            }

            sw.Stop();

            return new
            {
                Success = true,
                Message = greeting,
                Insights = new[]
                {
                    $"Greeted {name} successfully",
                    includeTime ? "Time information was included" : "Time information was not requested",
                    "This is an example tool showing basic MCP patterns"
                },
                Actions = new[]
                {
                    new { Tool = "get_system_info", Description = "Get more detailed system information" },
                    new { Tool = "hello_world", Description = "Try greeting someone else", Parameters = new { name = "Alice", includeTime = true } }
                },
                Meta = new ToolMetadata
                {
                    ExecutionTime = $"{sw.ElapsedMilliseconds}ms",
                    TokensEstimated = EstimateTokens(greeting),
                    ToolVersion = "1.0.0"
                }
            };
        });
    }

    private int EstimateTokens(string text)
    {
        // Simple estimation - in real usage, use TokenEstimator from framework
        return (text.Length / 4) + 10; // Rough approximation + overhead
    }
}

public class HelloWorldParams
{
    [Description("Name of the person to greet (optional, defaults to 'World')")]
    public string? Name { get; set; }

    [Description("Include current UTC time in the greeting (optional, defaults to false)")]
    public bool? IncludeTime { get; set; }
}