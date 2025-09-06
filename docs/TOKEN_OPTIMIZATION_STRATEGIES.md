# Token Optimization Strategies - Best of Both Worlds

## Overview

This document details the comprehensive token optimization strategies for the COA MCP Framework, combining the best ideas from both CodeSearch and CodeNav MCP projects. Our goal is to provide a robust, flexible system that prevents context window overflow while maintaining excellent developer and AI agent experiences.

## Core Principles

### 1. **Prevention Over Recovery**
- Pre-estimate token usage BEFORE building responses
- Apply limits proactively rather than reactively
- Design data structures for efficient token usage

### 2. **Progressive Disclosure**
- Start with essential information
- Provide clear paths to more detail
- Use resource storage for complete data

### 3. **Intelligent Reduction**
- Preserve the most valuable information
- Maintain data relationships and context
- Provide clear indicators when data is truncated

### 4. **AI-Friendly Responses**
- Structure responses for easy AI parsing
- Include actionable next steps
- Provide contextual insights

## Token Budget Management

### Safety Limits (From CodeNav)

```csharp
public static class TokenLimits
{
    // Conservative limits based on context window percentages
    public const int DEFAULT_SAFETY_LIMIT = 10000;      // ~5% of 200K context
    public const int CONSERVATIVE_SAFETY_LIMIT = 5000;  // ~2.5% of context
    public const int MINIMAL_SAFETY_LIMIT = 2000;       // ~1% of context
    
    // Response mode budgets (from CodeSearch)
    public const int SUMMARY_TOKEN_BUDGET = 5000;       // For summary responses
    public const int FULL_TOKEN_BUDGET = 50000;         // For detailed responses
    
    // Per-item estimates for common types
    public const int BASE_RESPONSE_OVERHEAD = 500;      // JSON structure, metadata
    public const int TYPICAL_ITEM_TOKENS = 100;         // Average item size
    public const int COMPLEX_ITEM_TOKENS = 300;         // Items with documentation
}
```

### Dynamic Token Estimation

#### Sampling-Based Estimation (From CodeNav)

```csharp
public static class TokenEstimator
{
    /// <summary>
    /// Estimates tokens for a collection using sampling for accuracy
    /// </summary>
    public static int EstimateCollection<T>(
        IEnumerable<T> items, 
        Func<T, int> itemEstimator,
        int baseTokens = TokenLimits.BASE_RESPONSE_OVERHEAD,
        int sampleSize = 5)
    {
        var itemList = items as IList<T> ?? items.ToList();
        if (!itemList.Any()) return baseTokens;

        // Sample first few items for accurate per-item estimate
        var sample = itemList.Take(Math.Min(sampleSize, itemList.Count)).ToList();
        var avgTokensPerItem = sample.Average(itemEstimator);
        
        // Add variance buffer for safety (10% padding)
        var estimatedTotal = baseTokens + (int)(itemList.Count * avgTokensPerItem * 1.1);
        
        // Account for response metadata
        estimatedTotal += EstimateResponseMetadata(itemList.Count);
        
        return estimatedTotal;
    }
    
    private static int EstimateResponseMetadata(int itemCount)
    {
        // Insights: ~50 tokens per insight, typically 3-5 insights
        var insightsTokens = 200;
        
        // Actions: ~100 tokens per action, typically 2-3 actions
        var actionsTokens = 250;
        
        // Distribution/summary data: varies by size
        var summaryTokens = Math.Min(500, itemCount * 5);
        
        return insightsTokens + actionsTokens + summaryTokens;
    }
}
```

#### Type-Specific Estimators

```csharp
public static class TypeEstimators
{
    // For code analysis tools (Roslyn types)
    public static int EstimateDiagnostic(dynamic diagnostic)
    {
        var tokens = 50; // Base structure
        tokens += EstimateString(diagnostic.Message);
        tokens += EstimateString(diagnostic.FilePath) / 2; // Paths repeat
        tokens += diagnostic.Properties?.Count * 20 ?? 0;
        tokens += diagnostic.Tags?.Count * 5 ?? 0;
        return tokens;
    }
    
    // For symbol information
    public static int EstimateSymbol(dynamic symbol)
    {
        var tokens = 80; // Base structure
        tokens += EstimateString(symbol.Name);
        tokens += EstimateString(symbol.FullName);
        tokens += EstimateString(symbol.Documentation) / 3; // Compressed
        tokens += symbol.Parameters?.Count * 30 ?? 0;
        return tokens;
    }
    
    // For file/search results
    public static int EstimateSearchResult(dynamic result)
    {
        var tokens = 60; // Base structure
        tokens += EstimateString(result.FilePath) / 2;
        tokens += result.Context?.Lines * 20 ?? 0; // Context lines
        tokens += result.Matches?.Count * 10 ?? 0;
        return tokens;
    }
    
    // String estimation (1 token ≈ 4 characters)
    public static int EstimateString(string? text)
        => (text?.Length ?? 0) / 4;
}
```

## Progressive Reduction Strategies

### Standard Reduction (From CodeNav)

```csharp
public class StandardProgressiveReduction
{
    // Proven reduction steps that maintain usefulness
    private static readonly int[] DefaultSteps = { 100, 75, 50, 30, 20, 10, 5 };
    
    public static List<T> ApplyReduction<T>(
        List<T> items,
        Func<List<T>, int> estimator,
        int tokenLimit,
        int[] steps = null)
    {
        steps = steps ?? DefaultSteps;
        
        // First check if we're already under limit
        if (estimator(items) <= tokenLimit)
            return items;
        
        // Try each reduction step
        foreach (var count in steps)
        {
            if (count >= items.Count)
                continue;
                
            var candidateItems = items.Take(count).ToList();
            if (estimator(candidateItems) <= tokenLimit)
                return candidateItems;
        }
        
        // Last resort: minimal set
        return items.Take(Math.Min(3, items.Count)).ToList();
    }
}
```

### Smart Reduction Strategies

#### Priority-Based Reduction

```csharp
public class PriorityBasedReduction<T>
{
    private readonly Func<T, double> _priorityCalculator;
    
    public List<T> Reduce(
        List<T> items, 
        int tokenLimit,
        Func<List<T>, int> estimator)
    {
        // Sort by priority (highest first)
        var prioritized = items
            .Select(item => new { Item = item, Priority = _priorityCalculator(item) })
            .OrderByDescending(x => x.Priority)
            .Select(x => x.Item)
            .ToList();
        
        // Binary search for optimal count
        int left = 1, right = prioritized.Count;
        int bestCount = 1;
        
        while (left <= right)
        {
            int mid = (left + right) / 2;
            var candidate = prioritized.Take(mid).ToList();
            
            if (estimator(candidate) <= tokenLimit)
            {
                bestCount = mid;
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }
        
        return prioritized.Take(bestCount).ToList();
    }
}

// Example priority calculators
public static class PriorityCalculators
{
    // For search results - prioritize by relevance score
    public static double SearchResultPriority(dynamic result)
        => result.Score * (result.IsExactMatch ? 2.0 : 1.0);
    
    // For diagnostics - prioritize errors over warnings
    public static double DiagnosticPriority(dynamic diagnostic)
        => diagnostic.Severity == "Error" ? 1000.0 : 
           diagnostic.Severity == "Warning" ? 100.0 : 10.0;
    
    // For symbols - prioritize public API
    public static double SymbolPriority(dynamic symbol)
        => symbol.IsPublic ? 100.0 : 
           symbol.IsProtected ? 50.0 : 
           symbol.IsInternal ? 20.0 : 10.0;
}
```

#### Clustering-Based Reduction

```csharp
public class ClusteringReduction<T>
{
    // Reduce items while maintaining diversity
    public List<T> ReduceWithClustering(
        List<T> items,
        Func<T, T, double> similarity,
        int targetCount)
    {
        if (items.Count <= targetCount)
            return items;
        
        // Simple k-means style clustering
        var selected = new List<T>();
        var remaining = new List<T>(items);
        
        // Start with most unique item
        var first = remaining[0];
        selected.Add(first);
        remaining.RemoveAt(0);
        
        // Iteratively add most dissimilar items
        while (selected.Count < targetCount && remaining.Any())
        {
            var mostDissimilar = remaining
                .OrderByDescending(item => 
                    selected.Min(s => similarity(item, s)))
                .First();
                
            selected.Add(mostDissimilar);
            remaining.Remove(mostDissimilar);
        }
        
        return selected;
    }
}
```

## Response Building Patterns

### AI-Optimized Response Structure (From CodeSearch)

```csharp
public class AIOptimizedResponseBuilder<T>
{
    private readonly ResponseConfig _config;
    
    public AIOptimizedResponse BuildResponse(
        List<T> allItems,
        Func<T, int> itemEstimator,
        ResponseContext context)
    {
        // Determine response mode based on data size
        var mode = DetermineResponseMode(allItems, context);
        
        // Apply token management
        var tokenAware = ApplyTokenManagement(allItems, itemEstimator, mode);
        
        // Build response structure
        return new AIOptimizedResponse
        {
            Format = "ai-optimized",
            Data = BuildResponseData(tokenAware.Items, context),
            Insights = GenerateInsights(tokenAware, context),
            Actions = GenerateActions(tokenAware, context),
            Meta = new AIResponseMeta
            {
                Mode = mode.ToString().ToLower(),
                EstimatedTokens = tokenAware.EstimatedTokens,
                TokenBudget = GetTokenBudget(mode),
                AutoModeSwitch = tokenAware.AutoSwitchedMode,
                Truncated = tokenAware.WasTruncated,
                ResourceUri = tokenAware.ResourceUri
            }
        };
    }
    
    private ResponseMode DetermineResponseMode(List<T> items, ResponseContext context)
    {
        // Honor explicit mode if requested
        if (context.RequestedMode.HasValue)
            return context.RequestedMode.Value;
        
        // Auto-switch based on estimated size
        var roughEstimate = items.Count * TypeEstimators.TYPICAL_ITEM_TOKENS;
        return roughEstimate > TokenLimits.SUMMARY_TOKEN_BUDGET 
            ? ResponseMode.Summary 
            : ResponseMode.Full;
    }
    
    private TokenAwareResult<T> ApplyTokenManagement(
        List<T> items, 
        Func<T, int> estimator,
        ResponseMode mode)
    {
        var budget = mode == ResponseMode.Summary 
            ? TokenLimits.SUMMARY_TOKEN_BUDGET 
            : TokenLimits.FULL_TOKEN_BUDGET;
        
        // Pre-estimate with sampling
        var preEstimate = TokenEstimator.EstimateCollection(items, estimator);
        
        if (preEstimate <= budget)
        {
            return new TokenAwareResult<T>
            {
                Items = items,
                EstimatedTokens = preEstimate,
                WasTruncated = false
            };
        }
        
        // Apply progressive reduction
        var reduced = StandardProgressiveReduction.ApplyReduction(
            items, 
            list => TokenEstimator.EstimateCollection(list, estimator),
            budget);
        
        // Store full results if truncated
        string? resourceUri = null;
        if (reduced.Count < items.Count)
        {
            resourceUri = StoreFullResults(items);
        }
        
        return new TokenAwareResult<T>
        {
            Items = reduced,
            EstimatedTokens = TokenEstimator.EstimateCollection(reduced, estimator),
            WasTruncated = true,
            OriginalCount = items.Count,
            ResourceUri = resourceUri,
            AutoSwitchedMode = mode == ResponseMode.Summary && 
                              preEstimate > TokenLimits.FULL_TOKEN_BUDGET
        };
    }
}
```

### Insight Generation Strategies

```csharp
public class InsightGenerator
{
    // Generate contextual insights based on data patterns
    public List<string> GenerateInsights<T>(
        TokenAwareResult<T> result,
        ResponseContext context)
    {
        var insights = new List<string>();
        
        // Always indicate truncation if it occurred
        if (result.WasTruncated)
        {
            insights.Add($"⚠️ Response size limit applied ({result.EstimatedTokens:N0} tokens). " +
                        $"Showing {result.Items.Count} of {result.OriginalCount} results.");
        }
        
        // Add data-specific insights
        insights.AddRange(GenerateDataInsights(result.Items, context));
        
        // Add pattern insights
        insights.AddRange(GeneratePatternInsights(result.Items, context));
        
        // Add recommendation insights
        if (result.WasTruncated)
        {
            insights.Add(GenerateTruncationRecommendation(result, context));
        }
        
        // Limit to 5 most valuable insights
        return insights
            .OrderByDescending(i => CalculateInsightValue(i, context))
            .Take(5)
            .ToList();
    }
    
    private double CalculateInsightValue(string insight, ResponseContext context)
    {
        double value = 1.0;
        
        // Prioritize actionable insights
        if (insight.Contains("Try") || insight.Contains("Consider"))
            value *= 2.0;
            
        // Prioritize warnings
        if (insight.StartsWith("⚠️"))
            value *= 1.5;
            
        // Prioritize insights relevant to user's goal
        if (context.UserGoal != null && 
            insight.ToLower().Contains(context.UserGoal.ToLower()))
            value *= 3.0;
            
        return value;
    }
}
```

### Action Generation Patterns

```csharp
public class ActionGenerator
{
    public List<AIAction> GenerateActions<T>(
        TokenAwareResult<T> result,
        ResponseContext context)
    {
        var actions = new List<AIAction>();
        
        // Always provide "get more results" if truncated
        if (result.WasTruncated)
        {
            actions.Add(new AIAction
            {
                Id = "get_more_results",
                Description = $"Get all {result.OriginalCount} results",
                Command = new AIActionCommand
                {
                    Tool = context.ToolName,
                    Parameters = new Dictionary<string, object>
                    {
                        ["maxResults"] = Math.Min(result.OriginalCount, 500),
                        ["responseMode"] = "full"
                    }
                },
                EstimatedTokens = result.OriginalCount * 100,
                Priority = ActionPriority.High,
                Context = ActionContext.ManyResults
            });
        }
        
        // Add contextual actions based on data
        actions.AddRange(GenerateContextualActions(result.Items, context));
        
        // Add workflow actions
        actions.AddRange(GenerateWorkflowActions(result, context));
        
        // Limit and prioritize
        return actions
            .OrderByDescending(a => a.Priority)
            .ThenBy(a => a.EstimatedTokens)
            .Take(5)
            .ToList();
    }
    
    private List<AIAction> GenerateWorkflowActions<T>(
        TokenAwareResult<T> result,
        ResponseContext context)
    {
        var actions = new List<AIAction>();
        
        // Example: After search, suggest refinement
        if (context.ToolName.Contains("search") && result.Items.Count > 20)
        {
            actions.Add(new AIAction
            {
                Id = "refine_search",
                Description = "Refine search with more specific criteria",
                Command = new AIActionCommand
                {
                    Tool = context.ToolName,
                    Parameters = new Dictionary<string, object>
                    {
                        ["query"] = context.OriginalQuery + " AND specific_term",
                        ["maxResults"] = 50
                    }
                },
                Priority = ActionPriority.Medium,
                Context = ActionContext.ManyResults
            });
        }
        
        return actions;
    }
}
```

## Resource Storage Strategy

```csharp
public interface IResourceStorageService
{
    /// <summary>
    /// Stores full results for later retrieval
    /// </summary>
    string StoreResults<T>(List<T> results, TimeSpan? expiration = null);
    
    /// <summary>
    /// Retrieves stored results
    /// </summary>
    List<T> RetrieveResults<T>(string resourceUri);
}

public class CompressedResourceStorage : IResourceStorageService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger _logger;
    
    public string StoreResults<T>(List<T> results, TimeSpan? expiration = null)
    {
        var resourceId = GenerateResourceId();
        var resourceUri = $"mcp://resource/{typeof(T).Name.ToLower()}/{resourceId}";
        
        // Serialize and compress
        var json = JsonSerializer.Serialize(results);
        var compressed = Compress(json);
        
        // Store with appropriate expiration
        _cache.Set(resourceUri, compressed, new MemoryCacheEntryOptions
        {
            SlidingExpiration = expiration ?? TimeSpan.FromMinutes(30),
            Size = compressed.Length,
            Priority = CacheItemPriority.High
        });
        
        _logger.LogDebug("Stored {Count} results at {Uri} ({Size} bytes compressed)",
            results.Count, resourceUri, compressed.Length);
        
        return resourceUri;
    }
    
    private byte[] Compress(string data)
    {
        var bytes = Encoding.UTF8.GetBytes(data);
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
        {
            gzip.Write(bytes, 0, bytes.Length);
        }
        return output.ToArray();
    }
}
```

## Adaptive Learning System

```csharp
public class TokenOptimizationLearner
{
    private readonly ITokenUsageRepository _repository;
    
    /// <summary>
    /// Learns optimal reduction steps based on actual usage
    /// </summary>
    public int[] GetOptimalReductionSteps(string toolName, string dataType)
    {
        var history = _repository.GetUsageHistory(toolName, dataType);
        
        if (history.Count < 10)
        {
            // Not enough data, use defaults
            return new[] { 100, 75, 50, 30, 20, 10, 5 };
        }
        
        // Analyze successful reductions
        var successfulReductions = history
            .Where(h => h.WasSuccessful && h.AppliedReduction)
            .Select(h => h.FinalItemCount)
            .OrderByDescending(c => c)
            .ToList();
        
        // Generate optimal steps based on history
        return GenerateOptimalSteps(successfulReductions);
    }
    
    /// <summary>
    /// Records actual token usage for learning
    /// </summary>
    public void RecordUsage(TokenUsageRecord record)
    {
        // Calculate estimation accuracy
        record.EstimationAccuracy = record.ActualTokens > 0
            ? (double)record.EstimatedTokens / record.ActualTokens
            : 1.0;
        
        // Store for future learning
        _repository.SaveUsageRecord(record);
        
        // Update estimator calibration if needed
        if (Math.Abs(record.EstimationAccuracy - 1.0) > 0.2)
        {
            _logger.LogWarning(
                "Token estimation off by {Percent:P} for {Tool}/{Type}",
                Math.Abs(record.EstimationAccuracy - 1.0),
                record.ToolName,
                record.DataType);
        }
    }
}
```

## Configuration and Tuning

```csharp
public class TokenOptimizationOptions
{
    /// <summary>
    /// Global token limit for all responses
    /// </summary>
    public int DefaultTokenLimit { get; set; } = 10000;
    
    /// <summary>
    /// Optimization level
    /// </summary>
    public TokenOptimizationLevel Level { get; set; } = TokenOptimizationLevel.Balanced;
    
    /// <summary>
    /// Enable adaptive learning
    /// </summary>
    public bool EnableAdaptiveLearning { get; set; } = true;
    
    /// <summary>
    /// Enable resource storage for truncated results
    /// </summary>
    public bool EnableResourceStorage { get; set; } = true;
    
    /// <summary>
    /// Custom reduction strategies
    /// </summary>
    public Dictionary<string, IReductionStrategy> CustomStrategies { get; set; } = new();
    
    /// <summary>
    /// Per-tool token limits
    /// </summary>
    public Dictionary<string, int> ToolTokenLimits { get; set; } = new();
}

public enum TokenOptimizationLevel
{
    /// <summary>
    /// Minimal optimization - larger responses allowed
    /// </summary>
    Minimal,
    
    /// <summary>
    /// Balanced optimization - good performance/size tradeoff
    /// </summary>
    Balanced,
    
    /// <summary>
    /// Aggressive optimization - minimize token usage
    /// </summary>
    Aggressive,
    
    /// <summary>
    /// Custom optimization - use provided strategies
    /// </summary>
    Custom
}
```

## Testing Token Optimization

```csharp
[TestFixture]
public class TokenOptimizationTests
{
    [Test]
    public void EstimateCollection_WithTypicalItems_EstimatesAccurately()
    {
        // Arrange
        var items = GenerateTestItems(100);
        
        // Act
        var estimate = TokenEstimator.EstimateCollection(
            items,
            item => TokenEstimators.EstimateSearchResult(item));
        
        // Assert - Should be within 10% of actual
        var actual = CalculateActualTokens(items);
        estimate.Should().BeCloseTo(actual, (int)(actual * 0.1));
    }
    
    [Test]
    public void ProgressiveReduction_WithLargeDataset_StaysUnderLimit()
    {
        // Arrange
        var items = GenerateTestItems(1000);
        var limit = 5000;
        
        // Act
        var reduced = StandardProgressiveReduction.ApplyReduction(
            items,
            list => TokenEstimator.EstimateCollection(list, EstimateItem),
            limit);
        
        // Assert
        var actualTokens = TokenEstimator.EstimateCollection(reduced, EstimateItem);
        actualTokens.Should().BeLessOrEqualTo(limit);
        reduced.Should().NotBeEmpty();
    }
    
    [Test]
    public void PriorityReduction_PreservesHighPriorityItems()
    {
        // Arrange
        var items = GeneratePrioritizedItems(100);
        var reducer = new PriorityBasedReduction<TestItem>(i => i.Priority);
        var limit = 2000;
        
        // Act
        var reduced = reducer.Reduce(items, limit, EstimateItems);
        
        // Assert
        var minPriority = reduced.Min(i => i.Priority);
        var excludedMaxPriority = items
            .Except(reduced)
            .DefaultIfEmpty(new TestItem { Priority = 0 })
            .Max(i => i.Priority);
            
        minPriority.Should().BeGreaterThan(excludedMaxPriority);
    }
}
```

## Performance Benchmarks

```csharp
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class TokenOptimizationBenchmarks
{
    private List<object> _smallDataset;
    private List<object> _largeDataset;
    
    [GlobalSetup]
    public void Setup()
    {
        _smallDataset = GenerateData(100);
        _largeDataset = GenerateData(10000);
    }
    
    [Benchmark]
    public int EstimateSmallDataset()
        => TokenEstimator.EstimateCollection(_smallDataset, EstimateItem);
    
    [Benchmark]
    public int EstimateLargeDataset()
        => TokenEstimator.EstimateCollection(_largeDataset, EstimateItem);
    
    [Benchmark]
    public List<object> ReduceLargeDataset()
        => StandardProgressiveReduction.ApplyReduction(
            _largeDataset,
            list => TokenEstimator.EstimateCollection(list, EstimateItem),
            10000);
}
```

## Best Practices Summary

### DO:
✅ Pre-estimate tokens before building responses
✅ Use sampling for accurate collection estimates
✅ Apply progressive reduction when over limits
✅ Store full results when truncating
✅ Provide clear next actions for more data
✅ Include truncation warnings in insights
✅ Test token optimization with real data
✅ Monitor estimation accuracy in production

### DON'T:
❌ Build full response then check tokens
❌ Use fixed item counts without token checks
❌ Truncate without indicating it happened
❌ Forget to provide resource URIs
❌ Ignore token optimization in tests
❌ Assume all items have same token cost
❌ Skip validation of token estimates

## Conclusion

The token optimization system in COA MCP Framework combines:
- **From CodeSearch**: AI-optimized response structure, response builders, insights/actions
- **From CodeNav**: Token estimation, progressive reduction, safety limits
- **New Innovations**: Adaptive learning, priority-based reduction, clustering strategies

This comprehensive approach ensures MCP tools never overwhelm the context window while providing maximum value to AI agents and developers.