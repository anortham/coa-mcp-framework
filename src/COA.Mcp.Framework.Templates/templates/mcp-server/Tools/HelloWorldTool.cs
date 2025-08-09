#if (IncludeFramework)
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using COA.Mcp.Framework;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Models;

namespace McpServerTemplate.Tools;

/// <summary>
/// A simple greeting tool that demonstrates basic MCP tool structure.
/// </summary>
public class HelloWorldTool : McpToolBase<HelloWorldParameters, HelloWorldResult>
{
    public override string Name => "hello_world";
    public override string Description => "Simple greeting tool that demonstrates basic MCP tool structure";
    public override ToolCategory Category => ToolCategory.Utility;

    protected override async Task<HelloWorldResult> ExecuteInternalAsync(
        HelloWorldParameters parameters, 
        CancellationToken cancellationToken)
    {
        var name = parameters.Name ?? "World";
        var greeting = $"Hello, {name}!";
        
        if (parameters.IncludeTime == true)
        {
            greeting += $" The current UTC time is {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";
        }

        return await Task.FromResult(new HelloWorldResult
        {
            Greeting = greeting,
            Timestamp = DateTime.UtcNow
        });
    }
}

/// <summary>
/// Parameters for the HelloWorld tool.
/// </summary>
public class HelloWorldParameters : IToolParameters
{
    /// <summary>
    /// Name of the person to greet
    /// </summary>
    [Description("Name of the person to greet")]
    public string? Name { get; set; }

    /// <summary>
    /// Include current UTC time in greeting
    /// </summary>
    [Description("Include current UTC time in greeting")]
    public bool? IncludeTime { get; set; }
}

/// <summary>
/// Result from the HelloWorld tool.
/// </summary>
public class HelloWorldResult : ToolResultBase
{
    /// <summary>
    /// The greeting message
    /// </summary>
    public string Greeting { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when greeting was generated
    /// </summary>
    public DateTime Timestamp { get; set; }

    public override string GetDisplayText()
    {
        return Greeting;
    }
}

#else
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
    private readonly ILogger<HelloWorldTool>? _logger;

    public HelloWorldTool(ILogger<HelloWorldTool>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Example tool execution method.
    /// </summary>
    public async Task<object> ExecuteAsync(HelloWorldParams parameters)
    {
        _logger?.LogInformation("Executing hello_world tool for {Name}", parameters.Name);

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
            Timestamp = DateTime.UtcNow,
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
#endif