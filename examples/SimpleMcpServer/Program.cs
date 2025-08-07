using COA.Mcp.Framework.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SimpleMcpServer.Prompts;
using SimpleMcpServer.Tools;

// Build and run the MCP server
var builder = new McpServerBuilder()
    .WithServerInfo("Simple MCP Server Example", "1.0.0")
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
    });

// Register services
builder.Services.AddSingleton<IDataService, InMemoryDataService>();

// Register tools
builder.RegisterToolType<CalculatorTool>();
builder.RegisterToolType<StringManipulationTool>();
builder.RegisterToolType<DataStoreTool>();
builder.RegisterToolType<SystemInfoTool>();

// Register prompts
builder.RegisterPromptType<GreetingPrompt>();
builder.RegisterPromptType<CodeGeneratorPrompt>();

// Build and run
await builder.RunAsync();