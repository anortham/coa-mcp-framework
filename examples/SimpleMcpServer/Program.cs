using COA.Mcp.Framework.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SimpleMcpServer.Prompts;
using SimpleMcpServer.Tools;

// Build and run the MCP server with full behavioral adoption
var builder = new McpServerBuilder()
    .WithServerInfo("Simple MCP Server Example", "1.0.0")
    
    // ===== BEHAVIORAL ADOPTION FEATURES =====
    // These features guide Claude toward optimal tool usage patterns
    
    // Template-based instructions that adapt to available tools
    .WithTemplateInstructions(options =>
    {
        options.ContextName = "general"; // Use built-in general template
        options.EnableConditionalLogic = true;
        options.CustomVariables["ProjectType"] = "MCP Server Example";
        options.CustomVariables["TeamName"] = "COA Framework Team";
        options.CustomVariables["Purpose"] = "Demonstrate behavioral adoption features";
    })
    
    // Tool management with priority and workflow suggestions
    .ConfigureToolManagement(config =>
    {
        config.EnableWorkflowSuggestions = true;  // Suggests optimal tool sequences
        config.EnableToolPriority = true;        // Promotes high-priority tools
        config.EnableDescriptionEnhancement = true; // Enriches tool descriptions
    })
    
    // Advanced error recovery with educational guidance
    .WithAdvancedErrorRecovery(options =>
    {
        options.EnableRecoveryGuidance = true;
        options.Tone = ErrorRecoveryTone.Tutorial; // Educational for examples
        options.IncludeOriginalError = true;
        options.IncludePreventionTips = true;  // Teaches error prevention
        options.IncludeProTips = true;         // Advanced usage tips
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
    })
    .ConfigureFramework(options =>
    {
        // Reduce framework logging noise for cleaner example output
        options.FrameworkLogLevel = LogLevel.Warning;
        options.EnableDetailedToolLogging = false;
        options.EnableDetailedMiddlewareLogging = false;
    });

// Register services
builder.Services.AddSingleton<IDataService, InMemoryDataService>();

// Register tools - enhanced with behavioral adoption markers
builder.RegisterToolType<CalculatorTool>();        // Implements IToolPriority, INoActiveProject
builder.RegisterToolType<StringManipulationTool>(); 
builder.RegisterToolType<DataStoreTool>();          // Implements IToolPriority, ICanEdit, IBulkOperation
builder.RegisterToolType<SystemInfoTool>();
builder.RegisterToolType<LifecycleExampleTool>();   // Demonstrates lifecycle hooks
builder.RegisterToolType<SearchDemoTool>();         // Demonstrates visualization capabilities
builder.RegisterToolType<MetricsDemoTool>();        // Demonstrates chart visualization

// Register prompts
builder.RegisterPromptType<GreetingPrompt>();
builder.RegisterPromptType<CodeGeneratorPrompt>();

// Build and run
await builder.RunAsync();