using COA.Mcp.Framework;
using COA.Mcp.Framework.Registration;
using COA.Mcp.Framework.TokenOptimization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
#if (UseOpenTelemetry)
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
#endif

var builder = McpServerBuilder.Create(args);

// Configure logging
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Add configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

#if (UseOpenTelemetry)
// Configure OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "McpServerTemplate"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddRuntimeInstrumentation()
        .AddConsoleExporter())
    .WithLogging(logging => logging
        .AddConsoleExporter());
#endif

// Add MCP Framework
builder.Services.AddMcpFramework(options =>
{
    // Automatic tool discovery
    options.DiscoverToolsFromAssembly(typeof(Program).Assembly);
    
    // Token optimization settings
    options.UseTokenOptimization(TokenOptimizationLevel.Aggressive);
    options.SetDefaultTokenLimit(10000);
    options.EnableProgressiveReduction(true);
    
    // Response building
    options.UseAIOptimizedResponses(true);
    options.ConfigureInsights(insights =>
    {
        insights.MinInsights = 3;
        insights.MaxInsights = 5;
        insights.IncludeUsageHints = true;
    });
    
    // JSON settings
    options.ConfigureJsonOptions(json =>
    {
        json.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        json.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });
});

// Add your services here
// builder.Services.AddSingleton<IMyService, MyService>();

// Build and run the server
var server = builder.Build();

try
{
    var logger = server.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Starting McpServerTemplate MCP Server...");
    
    await server.RunAsync();
}
catch (Exception ex)
{
    var logger = server.Services.GetRequiredService<ILogger<Program>>();
    logger.LogCritical(ex, "An unhandled exception occurred while running the MCP server");
    throw;
}