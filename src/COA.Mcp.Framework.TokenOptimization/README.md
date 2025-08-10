# COA.Mcp.Framework.TokenOptimization

Token optimization and intelligent response building for MCP tools. This package provides automatic token estimation, progressive reduction strategies, and AI-optimized response formatting to help MCP tools stay within token limits while maximizing information value.

## Features

- **Token Estimation**: Accurate token counting for strings, objects, and collections
- **Progressive Reduction**: Intelligent content reduction to fit token budgets
- **Response Intelligence**: Automatic insight and action generation
- **Response Caching**: Built-in caching with eviction policies
- **Resource Storage**: Store full results with compression support
- **AI-Optimized Formats**: Response structures designed for AI consumption

## Quick Start

### 1. Add Package Reference

```xml
<PackageReference Include="COA.Mcp.Framework.TokenOptimization" Version="1.4.0" />
```

### 2. Register Services

```csharp
using COA.Mcp.Framework.TokenOptimization;
using Microsoft.Extensions.DependencyInjection;

// In your service configuration
services.AddTokenOptimization(); // Coming soon - see manual registration below

// Manual registration (current approach)
services.AddSingleton<ITokenEstimator, DefaultTokenEstimator>();
services.AddSingleton<IInsightGenerator, InsightGenerator>();
services.AddSingleton<IActionGenerator, ActionGenerator>();
services.AddSingleton<IResponseCacheService, ResponseCacheService>();
services.AddSingleton<IResourceStorageService, ResourceStorageService>();
```

### 3. Basic Token Estimation

```csharp
using COA.Mcp.Framework.TokenOptimization;

// Estimate tokens for strings
int tokens = TokenEstimator.EstimateString("Hello, world!");

// Estimate tokens for objects
var data = new { Name = "Test", Items = new[] { 1, 2, 3 } };
int objectTokens = TokenEstimator.EstimateObject(data);

// Estimate tokens for collections
var list = new List<string> { "item1", "item2", "item3" };
int collectionTokens = TokenEstimator.EstimateCollection(list);
```

## Building Token-Aware MCP Tools

### Inherit from McpToolBase with Token Optimization

```csharp
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.TokenOptimization;
using COA.Mcp.Framework.TokenOptimization.Models;
using COA.Mcp.Framework.TokenOptimization.ResponseBuilders;

public class SearchTool : McpToolBase<SearchParams, TokenAwareResponse>
{
    private readonly ITokenEstimator _tokenEstimator;
    private readonly SearchResponseBuilder _responseBuilder;
    
    public SearchTool(
        ITokenEstimator tokenEstimator,
        ILogger<SearchTool> logger)
    {
        _tokenEstimator = tokenEstimator;
        _responseBuilder = new SearchResponseBuilder(logger);
    }
    
    public override string Name => "search";
    public override string Description => "Search with token-aware results";
    
    protected override async Task<TokenAwareResponse> ExecuteAsync(
        SearchParams parameters,
        CancellationToken cancellationToken)
    {
        // Perform search
        var results = await PerformSearchAsync(parameters);
        
        // Build token-aware response
        var context = new ResponseContext
        {
            ResponseMode = parameters.ResponseMode ?? "summary",
            TokenLimit = parameters.MaxTokens,
            ToolName = Name
        };
        
        var response = await _responseBuilder.BuildResponseAsync(results, context);
        return response;
    }
}
```

### Create a Custom Response Builder

```csharp
using COA.Mcp.Framework.TokenOptimization.ResponseBuilders;
using COA.Mcp.Framework.TokenOptimization.Models;

public class SearchResponseBuilder : BaseResponseBuilder<SearchResults>
{
    public SearchResponseBuilder(ILogger logger) : base(logger) { }
    
    public override async Task<object> BuildResponseAsync(
        SearchResults data,
        ResponseContext context)
    {
        var tokenBudget = CalculateTokenBudget(context);
        var startTime = DateTime.UtcNow;
        
        // Reduce results to fit token budget
        var reducedResults = ReduceResults(data.Items, tokenBudget * 0.7);
        
        // Generate insights and actions
        var insights = GenerateInsights(data, context.ResponseMode);
        var actions = GenerateActions(data, (int)(tokenBudget * 0.15));
        
        // Build response
        var response = new AIOptimizedResponse
        {
            Format = "ai-optimized",
            Data = new AIResponseData
            {
                Summary = $"Found {data.TotalCount} results",
                Results = reducedResults,
                Count = reducedResults.Count
            },
            Insights = ReduceInsights(insights, (int)(tokenBudget * 0.15)),
            Actions = ReduceActions(actions, (int)(tokenBudget * 0.15)),
            Meta = CreateMetadata(startTime, reducedResults.Count < data.TotalCount)
        };
        
        // Update token estimate
        response.Meta.TokenInfo.Estimated = TokenEstimator.EstimateObject(response);
        
        return response;
    }
    
    protected override List<string> GenerateInsights(
        SearchResults data,
        string responseMode)
    {
        var insights = new List<string>();
        
        if (data.TotalCount == 0)
            insights.Add("No results found - try broadening search criteria");
        else if (data.TotalCount > 100)
            insights.Add($"Large result set ({data.TotalCount} items) - consider filtering");
        
        if (responseMode == "summary")
            insights.Add("Showing summary view - use 'full' mode for complete results");
        
        return insights;
    }
    
    protected override List<AIAction> GenerateActions(
        SearchResults data,
        int tokenBudget)
    {
        var actions = new List<AIAction>();
        
        if (data.TotalCount > 10)
        {
            actions.Add(new AIAction
            {
                Id = "filter",
                Description = "Apply filters to narrow results",
                Category = "filter",
                Priority = 10,
                Tokens = 50
            });
        }
        
        if (data.HasMore)
        {
            actions.Add(new AIAction
            {
                Id = "next_page",
                Description = "Load next page of results",
                Category = "navigate",
                Priority = 8,
                Tokens = 100
            });
        }
        
        return actions;
    }
}
```

## Progressive Reduction

The package includes intelligent reduction strategies that preserve the most important information when token limits are exceeded:

```csharp
using COA.Mcp.Framework.TokenOptimization.Reduction;

var engine = new ProgressiveReductionEngine();

// Reduce with standard strategy (keeps first N items)
var reduced = engine.Reduce(
    largeCollection,
    item => TokenEstimator.EstimateObject(item),
    tokenBudget: 5000,
    strategy: "standard");

// Reduce with priority strategy (keeps highest priority items)
var priorityReduced = engine.Reduce(
    largeCollection,
    item => TokenEstimator.EstimateObject(item),
    tokenBudget: 5000,
    strategy: "priority",
    context: new ReductionContext
    {
        PriorityFunction = item => item.Score // Higher scores kept
    });
```

## Response Caching

Cache responses to avoid recomputation:

```csharp
public class CachedSearchTool : McpToolBase<SearchParams, object>
{
    private readonly IResponseCacheService _cache;
    private readonly ICacheKeyGenerator _keyGenerator;
    
    public CachedSearchTool(
        IResponseCacheService cache,
        ICacheKeyGenerator keyGenerator)
    {
        _cache = cache;
        _keyGenerator = keyGenerator;
    }
    
    protected override async Task<object> ExecuteAsync(
        SearchParams parameters,
        CancellationToken cancellationToken)
    {
        var cacheKey = _keyGenerator.GenerateKey(Name, parameters);
        
        // Try cache first
        var cached = await _cache.GetAsync(cacheKey);
        if (cached != null)
            return cached;
        
        // Compute response
        var response = await ComputeResponseAsync(parameters);
        
        // Cache for future use
        await _cache.SetAsync(cacheKey, response, TimeSpan.FromMinutes(15));
        
        return response;
    }
}
```

## Resource Storage

Store full results when responses are truncated:

```csharp
public class StorageAwareSearchTool : McpToolBase<SearchParams, object>
{
    private readonly IResourceStorageService _storage;
    
    protected override async Task<object> ExecuteAsync(
        SearchParams parameters,
        CancellationToken cancellationToken)
    {
        var fullResults = await GetFullResultsAsync(parameters);
        
        if (TokenEstimator.EstimateObject(fullResults) > 5000)
        {
            // Store full results and return summary with URI
            var resourceUri = await _storage.StoreAsync(
                fullResults,
                new StorageOptions
                {
                    Compress = true,
                    ExpiresIn = TimeSpan.FromHours(1)
                });
            
            return new TokenAwareResponse
            {
                Summary = "Large result set stored",
                ResourceUri = resourceUri,
                Meta = new { FullResultCount = fullResults.Count }
            };
        }
        
        return fullResults;
    }
}
```

## Intelligent Insights and Actions

Generate contextual insights and suggested actions:

```csharp
// Configure insight generation
var insightContext = new InsightContext
{
    OperationName = "search",
    MinInsights = 2,
    MaxInsights = 5,
    UserQuery = parameters.Query,
    Parameters = new Dictionary<string, object>
    {
        { "resultCount", results.Count },
        { "executionTime", executionTime }
    }
};

var insights = await _insightGenerator.GenerateInsightsAsync(
    results,
    insightContext,
    tokenBudget: 500);

// Configure action generation
var actionContext = new ActionContext
{
    OperationName = "search",
    MaxActions = 3,
    UserIntent = parameters.Query,
    RelatedInsights = insights
};

var actions = await _actionGenerator.GenerateActionsAsync(
    results,
    actionContext,
    tokenBudget: 300);
```

## Token Safety Modes

Control token budget calculation with safety modes:

```csharp
// Conservative mode (80% of limit)
var conservativeBudget = TokenEstimator.CalculateTokenBudget(
    10000,              // Base limit
    2000,               // Already used
    TokenSafetyMode.Conservative);

// Aggressive mode (95% of limit)
var aggressiveBudget = TokenEstimator.CalculateTokenBudget(
    10000,
    2000,
    TokenSafetyMode.Aggressive);

// Default mode (90% of limit)
var defaultBudget = TokenEstimator.CalculateTokenBudget(
    10000,
    2000,
    TokenSafetyMode.Default);
```

## Response Models

### TokenAwareResponse<T>
Generic token-aware response wrapper:
```csharp
public class TokenAwareResponse<T>
{
    public T? Data { get; set; }
    public TokenMetadata TokenMetadata { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}
```

### AIOptimizedResponse
AI-optimized response format with insights and actions (standalone class, does not inherit from ToolResultBase):
```csharp
public class AIOptimizedResponse
{
    public string Format => "ai-optimized";
    public AIResponseData Data { get; set; }
    public List<string> Insights { get; set; }
    public List<AIAction> Actions { get; set; }
    public AIResponseMeta Meta { get; set; }
}
```

## Best Practices

1. **Always estimate before sending**: Check token counts before returning responses
2. **Use progressive reduction**: Implement fallback strategies for large responses
3. **Cache aggressively**: Cache both full and reduced responses
4. **Provide insights**: Help AI agents understand the data context
5. **Suggest actions**: Guide AI agents to productive next steps
6. **Store full results**: Use resource storage for complete data access
7. **Set appropriate limits**: Use safety modes based on your use case

## Advanced Features

### Custom Estimation Strategies

Create custom estimation strategies for domain-specific objects:

```csharp
public class MyCustomEstimationStrategy : IEstimationStrategy
{
    public bool CanEstimate(Type type)
    {
        return type == typeof(MyCustomType);
    }
    
    public int Estimate(object obj, ITokenEstimator estimator)
    {
        var custom = (MyCustomType)obj;
        // Custom estimation logic
        return custom.ComplexityScore * 10;
    }
}

// Register the strategy
services.AddSingleton<IEstimationStrategy, MyCustomEstimationStrategy>();
```

### Custom Reduction Strategies

Implement domain-specific reduction logic:

```csharp
public class SmartReductionStrategy : IReductionStrategy
{
    public string Name => "smart";
    
    public ReductionResult<T> Reduce<T>(
        IList<T> items,
        Func<T, int> tokenEstimator,
        int tokenBudget,
        ReductionContext? context)
    {
        // Smart reduction logic based on item importance
        var sortedByImportance = items
            .OrderByDescending(item => CalculateImportance(item))
            .ToList();
        
        var result = new List<T>();
        var currentTokens = 0;
        
        foreach (var item in sortedByImportance)
        {
            var itemTokens = tokenEstimator(item);
            if (currentTokens + itemTokens <= tokenBudget)
            {
                result.Add(item);
                currentTokens += itemTokens;
            }
        }
        
        return new ReductionResult<T>
        {
            Items = result,
            OriginalCount = items.Count,
            RemovedCount = items.Count - result.Count,
            EstimatedTokens = currentTokens
        };
    }
}
```

## Integration with MCP Framework

The TokenOptimization package integrates seamlessly with the COA MCP Framework:

1. **Automatic validation**: Token limits can be validated in parameters
2. **Built-in error handling**: Token limit exceeded errors with recovery steps
3. **Tool metadata**: Advertise token limits in tool descriptions
4. **Prompt support**: Token-aware prompt responses

## Performance Considerations

- Token estimation is fast but not free - cache estimates for repeated objects
- Use sampling for very large collections (>1000 items)
- Progressive reduction is iterative - set reasonable initial budgets
- Resource storage has I/O cost - use appropriate expiration times

## Troubleshooting

### Token estimates seem inaccurate
- The estimator uses approximations (4 chars/token average)
- For exact counts, integrate with actual tokenizer libraries
- Adjust safety modes for your specific use case

### Responses are over-truncated
- Check your safety mode settings
- Verify token budget calculations
- Consider using resource storage for full results

### Cache misses are frequent
- Review cache key generation logic
- Increase cache expiration times
- Check for parameter variations causing different keys

## Examples

See the `examples/` directory for complete examples:
- `TokenAwareSearchTool`: Search tool with progressive reduction
- `CachedAnalysisTool`: Analysis with response caching
- `SmartSummaryTool`: Intelligent summarization with insights

## Contributing

Contributions are welcome! Please ensure:
- All tests pass
- New features have tests
- Documentation is updated
- Follow existing code style

## License

Part of the COA MCP Framework. See LICENSE for details.