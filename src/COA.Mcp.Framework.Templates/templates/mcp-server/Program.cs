using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using McpServerTemplate.Services;
using McpServerTemplate.Tools;
#if (UseOpenTelemetry)
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
#endif

// Build and run the host
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

        // NOTE: This template provides a starting structure.
        // You'll need to implement the following based on your needs:
        // 1. McpServer class that handles JSON-RPC communication
        // 2. ToolRegistry to manage your tools
        // 3. Tool discovery mechanism (manual or attribute-based)
        
        // Example of what you might add:
        // services.AddSingleton<ToolRegistry>();
        // services.AddSingleton<McpServer>();
        // services.AddHostedService<McpServer>(provider => provider.GetRequiredService<McpServer>());

        // Register your tool classes for dependency injection
        services.AddSingleton<HelloWorldTool>();
        services.AddSingleton<SystemInfoTool>();

        // Add your services here
        // services.AddSingleton<IMyService, MyService>();
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
    // Example:
    // var toolRegistry = scope.ServiceProvider.GetRequiredService<ToolRegistry>();
    // toolRegistry.RegisterTool(...);
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