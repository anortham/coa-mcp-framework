#if (IncludeFramework)
using COA.Mcp.Framework.Server;
#endif
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
#if (IncludeFramework && IncludeExampleTools)
using McpServerTemplate.Tools;
#endif
#if (UseOpenTelemetry)
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
#endif

#if (IncludeFramework)
// Build and run the MCP server using COA Framework
var builder = new McpServerBuilder()
    .WithServerInfo("McpServerTemplate", "1.0.0")
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        // MCP protocol requires that only stderr is used for logging
        // stdout is reserved for JSON-RPC communication
        Console.SetOut(Console.Error);
        
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
    });

// Configure services
builder.Services.AddSingleton(builder.Configuration);

#if (UseOpenTelemetry)
// Configure OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "McpServerTemplate"))
    .WithTracing(tracing => tracing
        .AddSource("McpServerTemplate")
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddRuntimeInstrumentation()
        .AddConsoleExporter());
#endif

// Register your services here
// builder.Services.AddSingleton<IMyService, MyService>();

#if (IncludeExampleTools)
// Register example tools using the recommended pattern
builder.RegisterToolType<HelloWorldTool>();
builder.RegisterToolType<SystemInfoTool>();
#endif

// ==================== TOOL REGISTRATION PATTERNS ====================
// The COA MCP Framework supports three patterns for registering tools:
//
// PATTERN 1: McpToolBase + Manual Registration (RECOMMENDED)
// - Inherit from McpToolBase<TParams, TResult>
// - Provides built-in validation, error handling, and token management
// - Register explicitly with RegisterToolType<T>()
// - Best for production code with full framework features
// Example:
// builder.RegisterToolType<MyCustomTool>();
//
// PATTERN 2: Interface Implementation + Auto-Discovery
// - Implement IMcpTool<TParams, TResult> interface directly
// - Auto-discovered when calling DiscoverTools()
// - Good for bulk registration of many tools
// Example:
// builder.DiscoverTools(typeof(Program).Assembly);
//
// PATTERN 3: Attribute-Based (Legacy but supported)
// - Mark class with [McpServerToolType]
// - Mark methods with [McpServerTool("tool_name")]
// - Auto-discovered when calling DiscoverTools()
// - Legacy pattern, maintained for backward compatibility
// Example:
// [McpServerToolType]
// public class MyToolClass
// {
//     [McpServerTool("my_tool")]
//     public async Task<object> ExecuteAsync(MyParams parameters) { ... }
// }
// =====================================================================

// Register your custom tools here using your preferred pattern:

// Register prompts if needed
// builder.RegisterPromptType<MyCustomPrompt>();

// Build and run
try
{
    await builder.RunAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Fatal error: {ex.Message}");
    Environment.Exit(1);
}

#else
// Build and run without COA Framework
var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging((context, logging) =>
    {
        logging.ClearProviders();
        // MCP protocol requires that only stderr is used for logging
        // stdout is reserved for JSON-RPC communication
        Console.SetOut(Console.Error);
        
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
    })
    .ConfigureServices((context, services) =>
    {
        // Add configuration
        services.AddSingleton(context.Configuration);
        
#if (UseOpenTelemetry)
        // Configure OpenTelemetry
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: "McpServerTemplate"))
            .WithTracing(tracing => tracing
                .AddSource("McpServerTemplate")
                .AddConsoleExporter())
            .WithMetrics(metrics => metrics
                .AddRuntimeInstrumentation()
                .AddConsoleExporter());
#endif

        // NOTE: Without the COA Framework, you'll need to implement:
        // 1. McpServer class that handles JSON-RPC communication
        // 2. ToolRegistry to manage your tools
        // 3. Tool discovery mechanism (manual or attribute-based)
        
        // Add your services here
        // services.AddSingleton<IMyService, MyService>();
        // services.AddHostedService<McpServer>();
    })
    .UseConsoleLifetime(options =>
    {
        options.SuppressStatusMessages = true;
    })
    .Build();

// Example startup code - adjust based on your needs
using (var scope = host.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Starting McpServerTemplate MCP Server...");
    
    // Add your tool registration logic here
}

try
{
    await host.RunAsync();
}
catch (Exception ex)
{
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogCritical(ex, "An unhandled exception occurred while running the MCP server");
    throw;
}
#endif