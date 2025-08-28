using COA.Mcp.Framework.Server;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;

// Minimal MCP server - entire application in one file!
// This is the simplest possible MCP server that actually works.

// 1. Define your tool
public class HelloTool : McpToolBase<HelloParams, HelloResult>
{
    public override string Name => "hello";
    public override string Description => "Says hello to someone";
    
    protected override async Task<HelloResult> ExecuteInternalAsync(
        HelloParams parameters, CancellationToken cancellationToken)
    {
        // Just return a greeting - no complexity needed
        await Task.CompletedTask; // Make it async-compliant
        
        return new HelloResult 
        { 
            Success = true, 
            Greeting = $"Hello, {parameters.Name ?? "World"}!" 
        };
    }
}

// 2. Define your input parameters
public class HelloParams 
{ 
    public string? Name { get; set; }
}

// 3. Define your result
public class HelloResult : ToolResultBase 
{ 
    public override string Operation => "hello";
    public string Greeting { get; set; } = "";
}

// 4. Run the server
class Program
{
    static async Task Main(string[] args)
    {
        // Create server with minimal configuration
        var builder = new McpServerBuilder()
            .WithServerInfo("Hello MCP Server", "1.0.0");
        
        // Register your tool
        builder.RegisterToolType<HelloTool>();
        
        // Start the server (uses STDIO transport by default for Claude Desktop)
        Console.WriteLine("🚀 Hello MCP Server starting...");
        Console.WriteLine("💡 This server will run until you stop it (Ctrl+C)");
        
        await builder.RunAsync();
    }
}