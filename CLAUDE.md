# COA MCP Framework - Claude AI Assistant Guide

## 🚨 CRITICAL WARNINGS - READ FIRST

### 1. **Framework vs Implementation Testing**

```
⚠️  FRAMEWORK CODE vs MCP SERVER EXECUTION:
    1. Framework changes require NuGet package rebuild
    2. Consuming projects must update package references
    3. MCP servers must restart to use new framework version
    4. NO EXCEPTIONS TO THIS RULE
```

- **Framework Development**: Changes to COA.Mcp.Framework.* projects
- **Testing Changes**: Build → Pack → Update consuming project → Restart MCP server
- **Example**: Changing `TokenEstimator.cs` won't affect running servers until package update

### 2. **Build Configuration**

- **During Development**: Use `dotnet build -c Debug` for framework projects
- **Package Creation**: Use `dotnet pack -c Release` for NuGet packages
- **Version Management**: Update version in .csproj before packing
- **Local Testing**: Use local NuGet source for testing before publishing

### 3. **Core Dependencies**

- **COA.Mcp.Protocol**: Will be moved into this framework as the base package
- **Dependency Chain**: Protocol → Framework → TokenOptimization → Testing
- **Version Alignment**: All packages should version together initially

### 4. **Development Best Practices**

- **NEVER** make assumptions about what properties or methods are available on a type, go look it up and see
- **ALWAYS** use the search tools to understand existing patterns before implementing
- **ALWAYS** build and test before committing: BUILD → TEST → COMMIT
- **NEVER** check in broken builds or failing tests
- **ALWAYS** maintain backward compatibility in minor versions

## 🏗️ Framework Architecture (v1.0.0 - SIMPLIFIED!)

### Package Structure

```
COA.Mcp.Framework (v1.0.0) - COMPLETE MCP SOLUTION
├── Includes COA.Mcp.Protocol (v1.3.x) as dependency
├── McpServer - Full server implementation
├── McpToolBase<TParams, TResult> - Type-safe tools
├── McpToolRegistry - Single unified registry
└── McpServerBuilder - Fluent configuration

Optional Extensions:
├── COA.Mcp.Framework.TokenOptimization (v1.0.0) - Token management
├── COA.Mcp.Framework.Testing (v1.0.0) - Testing infrastructure
├── COA.Mcp.Framework.Migration (v1.0.0) - Migration utilities
├── COA.Mcp.Framework.Templates (v1.0.0) - Project templates
└── COA.Mcp.Framework.CLI (v1.0.0) - Command-line tools
```

**IMPORTANT**: Users only need to reference `COA.Mcp.Framework` - it includes everything needed to build an MCP server!

### Design Principles

1. **Separation of Concerns**: Each package has a clear, focused responsibility
2. **Extensibility First**: Design for extensibility without modification
3. **Performance by Default**: Token optimization and caching built-in
4. **Developer Experience**: Intuitive APIs with comprehensive IntelliSense
5. **Testability**: Every component designed for easy testing

## 📦 Package Development Guidelines

### COA.Mcp.Protocol (Integrated into Framework)

The foundation MCP protocol implementation, now integrated into the framework solution with separate versioning (1.3.x).

```csharp
// Core protocol types
- McpServer
- ToolRegistry  
- ResourceRegistry
- Protocol messages and contracts
- TypedJsonRpc implementation
- Server capabilities management
```

### COA.Mcp.Framework

Core abstractions for building MCP tools:

```csharp
// Attributes for tool discovery
[McpServerToolType] // Marks a class as containing MCP tools
[McpServerTool(Name = "tool_name")] // Marks a method as an MCP tool
[Description("Tool description")] // Provides tool documentation

// Base classes
public abstract class McpToolBase : ITool
{
    // Validation helpers
    protected T ValidateRequired<T>(T? value, string paramName);
    protected int ValidatePositive(int value, string paramName);
    protected void ValidateRange(int value, int min, int max, string paramName);
    
    // Token-aware execution
    protected Task<TResult> ExecuteWithTokenManagement<TResult>(
        Func<Task<TResult>> operation);
    
    // Enhanced error handling (NEW)
    protected ToolResultBase CreateErrorResult(string operation, string error, 
        string? recoveryStep = null);
    protected ToolResultBase CreateValidationErrorResult(string operation, 
        string paramName, string requirement);
}

// Interfaces
public interface ITool
{
    string ToolName { get; }
    string Description { get; }
    ToolCategory Category { get; }
}

// NEW: Resource Provider Infrastructure
public interface IResourceProvider
{
    bool CanHandle(string uri);
    Task<ResourceContent> GetResourceAsync(string uri);
}

public interface IResourceRegistry
{
    void RegisterProvider(IResourceProvider provider);
    Task<ResourceContent> GetResourceAsync(string uri);
}

// NEW: Standardized Result Models
public abstract class ToolResultBase
{
    public bool Success { get; set; }
    public abstract string Operation { get; }
    public ErrorInfo? Error { get; set; }
    public Dictionary<string, object>? Meta { get; set; }
}

// NEW: AI-Friendly Error Models
public class ErrorInfo
{
    public required string Code { get; set; }
    public string? Message { get; set; }
    public RecoveryInfo? Recovery { get; set; }
}
```

### COA.Mcp.Framework.TokenOptimization

Advanced token management from both projects:

```csharp
// From CodeNav - Enhanced token estimation
public static class TokenEstimator
{
    public const int DEFAULT_SAFETY_LIMIT = 10000;  // 5% of context
    public const int CONSERVATIVE_SAFETY_LIMIT = 5000;
    
    // Estimation methods
    public static int EstimateString(string? text);
    public static int EstimateObject(object obj);
    public static int EstimateCollection<T>(items, itemEstimator);
    
    // Progressive reduction
    public static List<T> ApplyProgressiveReduction<T>(
        items, estimator, tokenLimit, reductionSteps);
}

// From CodeSearch - AI-optimized responses
public class AIOptimizedResponse
{
    public string Format { get; set; } // "ai-optimized"
    public AIResponseData Data { get; set; }
    public List<AIAction> Actions { get; set; }
    public List<string> Insights { get; set; }
    public AIResponseMeta Meta { get; set; }
}

// Response builders
public abstract class BaseResponseBuilder<T>
{
    protected const int SummaryTokenBudget = 5000;
    protected const int FullTokenBudget = 50000;
    
    protected abstract List<string> GenerateInsights(data, mode);
    protected abstract List<dynamic> GenerateActions(data, budget);
}
```

## 🛠️ Implementation Patterns

### Creating a New MCP Tool (Consumer Perspective)

```csharp
[McpServerToolType]
public class WeatherTool : McpToolBase
{
    private readonly IWeatherService _weatherService;
    private readonly ILogger<WeatherTool> _logger;
    
    public override string ToolName => "get_weather";
    public override ToolCategory Category => ToolCategory.Query;
    
    public WeatherTool(IWeatherService weatherService, ILogger<WeatherTool> logger)
    {
        _weatherService = weatherService;
        _logger = logger;
    }
    
    [McpServerTool(Name = "get_weather")]
    [Description(@"Gets current weather for a location.
    Returns: Weather data including temperature, conditions, and forecast.
    Prerequisites: None.
    Use cases: Weather queries, travel planning, outdoor activity decisions.")]
    public async Task<object> ExecuteAsync(WeatherParams parameters)
    {
        // Built-in validation from base class
        var location = ValidateRequired(parameters.Location, nameof(parameters.Location));
        var days = ValidateRange(parameters.ForecastDays ?? 1, 1, 10, "ForecastDays");
        
        // Token-aware execution from base class
        return await ExecuteWithTokenManagement(async () =>
        {
            var weather = await _weatherService.GetWeatherAsync(location, days);
            
            // Use framework's response building
            return new WeatherResult
            {
                Success = true,
                Location = location,
                Current = weather.Current,
                Forecast = weather.Forecast,
                Insights = GenerateWeatherInsights(weather),
                Actions = GenerateWeatherActions(weather),
                Meta = new ToolMetadata
                {
                    ExecutionTime = $"{sw.ElapsedMilliseconds}ms",
                    Truncated = weather.Forecast.Count > 5
                }
            };
        });
    }
}

public class WeatherParams
{
    [Description("Location to get weather for (city name or coordinates)")]
    public string? Location { get; set; }
    
    [Description("Number of forecast days (1-10, default: 1)")]
    public int? ForecastDays { get; set; }
}
```

### Framework Registration Pattern

```csharp
// In Program.cs of consuming project
var builder = McpServerBuilder.Create(args);

// Add framework with options
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

// Add services
builder.Services.AddSingleton<IWeatherService, WeatherService>();

var server = builder.Build();
await server.RunAsync();
```

## 🧪 Testing Patterns

### Unit Testing Tools

```csharp
[TestFixture]
public class WeatherToolTests : McpToolTestBase<WeatherTool>
{
    private Mock<IWeatherService> _weatherServiceMock;
    
    protected override WeatherTool CreateTool()
    {
        _weatherServiceMock = new Mock<IWeatherService>();
        return new WeatherTool(_weatherServiceMock.Object, Logger);
    }
    
    [Test]
    public async Task GetWeather_WithValidLocation_ReturnsWeatherData()
    {
        // Arrange - Use builders from framework
        var parameters = new ToolParameterBuilder<WeatherParams>()
            .WithLocation("Seattle")
            .WithForecastDays(3)
            .Build();
            
        var expectedWeather = new WeatherDataBuilder()
            .WithTemperature(72)
            .WithConditions("Partly Cloudy")
            .WithForecastDays(3)
            .Build();
            
        _weatherServiceMock
            .Setup(x => x.GetWeatherAsync("Seattle", 3))
            .ReturnsAsync(expectedWeather);
        
        // Act
        var result = await Tool.ExecuteAsync(parameters);
        
        // Assert - Use framework assertions
        result.Should().BeSuccessful()
            .And.HaveTokenCountLessThan(5000)
            .And.HaveInsightCount(3, 5)
            .And.HaveNextAction("get_extended_forecast");
            
        result.As<WeatherResult>()
            .Current.Temperature.Should().Be(72);
    }
    
    [Test]
    public async Task GetWeather_WithLargeForecast_AppliesTokenLimits()
    {
        // Arrange - Create scenario that triggers token limits
        var parameters = new ToolParameterBuilder<WeatherParams>()
            .WithLocation("Seattle")
            .WithForecastDays(100) // Excessive
            .Build();
            
        var largeForecast = GenerateLargeForecast(100);
        
        _weatherServiceMock
            .Setup(x => x.GetWeatherAsync("Seattle", 10)) // Clamped
            .ReturnsAsync(largeForecast);
        
        // Act
        var result = await Tool.ExecuteAsync(parameters);
        
        // Assert
        result.Should().BeTokenOptimized()
            .And.HaveTruncationMessage()
            .And.HaveResourceUri()
            .And.HaveNextAction("get_more_forecast_days");
            
        result.As<WeatherResult>()
            .Forecast.Should().HaveCountLessOrEqualTo(5); // Reduced
    }
}
```

### Integration Testing

```csharp
[TestFixture] 
public class WeatherToolIntegrationTests : McpIntegrationTestBase
{
    [Test]
    public async Task WeatherTool_FullWorkflow_Success()
    {
        // Arrange
        var server = CreateTestServer(builder =>
        {
            builder.Services.AddMcpFramework(options =>
            {
                options.DiscoverToolsFromAssembly(typeof(WeatherTool).Assembly);
            });
            builder.Services.AddSingleton<IWeatherService, RealWeatherService>();
        });
        
        // Act - Simulate MCP client calls
        var tools = await server.ListToolsAsync();
        var weatherTool = tools.Should().ContainToolNamed("get_weather");
        
        var result = await server.CallToolAsync("get_weather", new
        {
            location = "Seattle",
            forecastDays = 3
        });
        
        // Assert
        result.Should().BeSuccessful()
            .And.CompleteWithinMs(1000)
            .And.ProduceValidJson();
    }
}
```

## 🚀 Performance Guidelines

### Token Estimation Best Practices

1. **Pre-estimate Early**: Always estimate tokens BEFORE building responses
2. **Sample for Accuracy**: Use first 5-10 items to estimate collection costs
3. **Cache Estimations**: Reuse estimates for similar objects
4. **Profile Real Usage**: Measure actual token usage vs estimates

### Progressive Reduction Strategies

```csharp
// Standard reduction steps (from successful implementations)
int[] standardSteps = { 100, 75, 50, 30, 20, 10, 5 };

// Adaptive reduction (learns from usage)
var adaptiveSteps = _reductionLearner.GetOptimalSteps(toolName, dataType);

// Priority-based reduction (keeps important items)
var priorityReducer = new PriorityReducer<T>(item => item.Importance);
var reducedItems = priorityReducer.Reduce(allItems, tokenLimit);
```

### Response Building Performance

1. **Lazy Evaluation**: Build only what's needed for the response mode
2. **Streaming Support**: Enable streaming for large responses
3. **Resource Storage**: Store full results for later retrieval
4. **Caching**: Cache responses for identical queries

## 📋 Quality Checklist

### Before Committing Code

- [ ] All projects build successfully: `dotnet build`
- [ ] All tests pass: `dotnet test`
- [ ] Test coverage ≥ 85%: `dotnet test /p:CollectCoverage=true`
- [ ] No build warnings
- [ ] XML documentation on all public APIs
- [ ] Examples in documentation
- [ ] Version numbers updated if needed
- [ ] CHANGELOG.md updated

### Before Creating Package

- [ ] Version number incremented appropriately
- [ ] Package metadata updated
- [ ] Dependencies versions correct
- [ ] Release notes prepared
- [ ] Breaking changes documented
- [ ] Migration guide updated (if needed)
- [ ] Package tested locally
- [ ] Cross-platform compatibility verified

### Before Publishing

- [ ] Final quality check passed
- [ ] Documentation website updated
- [ ] Example projects updated
- [ ] Performance benchmarks run
- [ ] Security scan completed
- [ ] License files included
- [ ] NuGet tags appropriate
- [ ] Announcement prepared

## 🎯 Success Metrics

### Framework Adoption
- Both CodeSearch and CodeNav successfully migrated
- 5+ new MCP servers built with framework
- 80%+ reduction in boilerplate code
- <15 minutes to create new MCP server

### Performance Targets
- Token estimation accuracy: 95%+
- Framework overhead: <5% vs direct implementation  
- Memory usage: <50MB for typical usage
- Startup time: <500ms

### Developer Satisfaction
- Clear, intuitive APIs
- Comprehensive IntelliSense
- Helpful error messages
- Rich documentation and examples

## 🔍 Debugging Tips

### Token Estimation Issues
```csharp
// Enable detailed token logging
services.AddMcpFramework(options =>
{
    options.EnableTokenLogging(LogLevel.Debug);
    options.LogTokenEstimates = true;
    options.LogActualTokens = true;
});
```

### Tool Registration Problems
```csharp
// List all discovered tools
var tools = serviceProvider.GetRequiredService<IToolRegistry>().GetAllTools();
foreach (var tool in tools)
{
    logger.LogInformation("Found tool: {Name} in {Type}", 
        tool.Name, tool.DeclaringType);
}
```

### Response Building Debugging
```csharp
// Enable response building traces
services.Configure<ResponseBuildingOptions>(options =>
{
    options.EnableTracing = true;
    options.LogInsightGeneration = true;
    options.LogActionSuggestions = true;
});
```

## 🌟 Innovation Opportunities

### Future Enhancements
1. **ML-Based Token Prediction**: Learn from actual usage patterns
2. **Semantic Compression**: Compress responses while preserving meaning
3. **Adaptive Insights**: Generate insights based on user behavior
4. **Cross-Tool Orchestration**: Coordinate multiple tools automatically
5. **Visual Studio Integration**: Design-time tool validation

### Research Areas
1. **Token Budget Negotiation**: Dynamic budget based on context
2. **Semantic Caching**: Cache based on query meaning
3. **Progressive Enhancement**: Add detail as token budget allows
4. **Collaborative Filtering**: Learn from community usage patterns

## 📚 Additional Resources

### Documentation
- [Implementation Plan](IMPLEMENTATION_PLAN.md) - Detailed development roadmap
- [Architecture Decisions](docs/ADR/) - Key design choices
- [API Reference](docs/API/) - Complete API documentation
- [Migration Guide](docs/MIGRATION.md) - For existing projects

### Examples
- [Simple MCP Server](examples/SimpleMcpServer/) - Minimal example
- [Weather Service](examples/WeatherService/) - Real-world example
- [Code Analysis](examples/CodeAnalysis/) - Complex tool example
- [Testing Patterns](examples/TestingPatterns/) - Test examples

### Community
- GitHub Issues - Bug reports and features
- Discussions - Questions and ideas
- Wiki - Community contributions
- Discord - Real-time help

---

Remember: The goal is to make MCP development so easy and reliable that developers can focus on their domain logic rather than infrastructure concerns. Every decision should support this goal.