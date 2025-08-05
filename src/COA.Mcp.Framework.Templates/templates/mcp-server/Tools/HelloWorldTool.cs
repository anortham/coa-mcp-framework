using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace McpServerTemplate.Tools;

/// <summary>
/// Example tool showing basic MCP tool structure.
/// In a real implementation, you would:
/// 1. Add attributes for tool discovery (if using attribute-based discovery)
/// 2. Register this tool with your ToolRegistry
/// 3. Handle JSON-RPC calls to execute the tool
/// </summary>
public class HelloWorldTool
{
    private readonly ILogger<HelloWorldTool> _logger;

    public HelloWorldTool(ILogger<HelloWorldTool> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Example tool execution method.
    /// </summary>
    public async Task<object> ExecuteAsync(HelloWorldParams parameters)
    {
        _logger.LogInformation("Executing hello_world tool for {Name}", parameters.Name);

        // Validate parameters
        var name = parameters.Name?.Trim() ?? "World";
        var includeTime = parameters.IncludeTime ?? false;

        // Simulate some async work
        await Task.Delay(100);

        var greeting = $"Hello, {name}!";
        if (includeTime)
        {
            greeting += $" The current time is {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC.";
        }

        // Return response in your preferred format
        // This example shows an AI-friendly response structure
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
            Meta = new
            {
                ExecutionTime = "100ms",
                ToolVersion = "1.0.0"
            }
        };
    }
}

/// <summary>
/// Parameters for the hello_world tool.
/// </summary>
public class HelloWorldParams
{
    /// <summary>
    /// Name of the person to greet (optional, defaults to 'World').
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Include current UTC time in the greeting (optional, defaults to false).
    /// </summary>
    public bool? IncludeTime { get; set; }
}