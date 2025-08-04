using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.TokenOptimization.Reduction;

/// <summary>
/// Engine that orchestrates progressive reduction of collections using various strategies.
/// </summary>
public class ProgressiveReductionEngine
{
    private readonly ILogger<ProgressiveReductionEngine>? _logger;
    private readonly Dictionary<string, IReductionStrategy> _strategies = new();
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressiveReductionEngine"/> class.
    /// </summary>
    /// <param name="logger">Optional logger for debugging.</param>
    public ProgressiveReductionEngine(ILogger<ProgressiveReductionEngine>? logger = null)
    {
        _logger = logger;
        RegisterDefaultStrategies();
    }
    
    /// <summary>
    /// Registers a reduction strategy.
    /// </summary>
    /// <param name="strategy">The strategy to register.</param>
    public void RegisterStrategy(IReductionStrategy strategy)
    {
        if (strategy == null)
            throw new ArgumentNullException(nameof(strategy));
        
        _strategies[strategy.Name] = strategy;
        _logger?.LogDebug("Registered reduction strategy: {StrategyName}", strategy.Name);
    }
    
    /// <summary>
    /// Reduces a collection using the specified strategy.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <param name="items">The collection to reduce.</param>
    /// <param name="itemEstimator">Function to estimate tokens for a single item.</param>
    /// <param name="tokenLimit">The maximum allowed tokens.</param>
    /// <param name="strategyName">The name of the strategy to use (default: "standard").</param>
    /// <param name="context">Optional context for the reduction operation.</param>
    /// <returns>A reduced collection that fits within the token limit.</returns>
    public ReductionResult<T> Reduce<T>(
        IEnumerable<T> items,
        Func<T, int> itemEstimator,
        int tokenLimit,
        string strategyName = "standard",
        ReductionContext? context = null)
    {
        var itemsList = items as IList<T> ?? items.ToList();
        
        if (!_strategies.TryGetValue(strategyName, out var strategy))
        {
            _logger?.LogWarning("Strategy {StrategyName} not found, falling back to standard", strategyName);
            strategy = _strategies.GetValueOrDefault("standard") 
                ?? throw new InvalidOperationException("Standard reduction strategy not available");
        }
        
        var startTime = DateTime.UtcNow;
        var result = strategy.Reduce(itemsList, itemEstimator, tokenLimit, context);
        var duration = DateTime.UtcNow - startTime;
        
        _logger?.LogInformation(
            "Reduced collection from {OriginalCount} to {ReducedCount} items " +
            "({ReductionPercentage:F1}% reduction) using {Strategy} strategy in {Duration}ms",
            result.OriginalCount,
            result.Items.Count,
            result.ReductionPercentage,
            strategyName,
            duration.TotalMilliseconds);
        
        return result;
    }
    
    /// <summary>
    /// Attempts multiple strategies to find the best reduction.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <param name="items">The collection to reduce.</param>
    /// <param name="itemEstimator">Function to estimate tokens for a single item.</param>
    /// <param name="tokenLimit">The maximum allowed tokens.</param>
    /// <param name="strategyNames">Strategies to try in order.</param>
    /// <param name="context">Optional context for the reduction operation.</param>
    /// <returns>The best reduction result (most items retained within limit).</returns>
    public ReductionResult<T> ReduceWithBestStrategy<T>(
        IEnumerable<T> items,
        Func<T, int> itemEstimator,
        int tokenLimit,
        string[]? strategyNames = null,
        ReductionContext? context = null)
    {
        var itemsList = items as IList<T> ?? items.ToList();
        strategyNames ??= new[] { "standard", "adaptive", "priority" };
        
        ReductionResult<T>? bestResult = null;
        var bestItemCount = 0;
        
        foreach (var strategyName in strategyNames)
        {
            if (!_strategies.ContainsKey(strategyName))
                continue;
            
            try
            {
                var result = Reduce(itemsList, itemEstimator, tokenLimit, strategyName, context);
                
                // Choose the result that retains the most items within the limit
                if (result.EstimatedTokens <= tokenLimit && result.Items.Count > bestItemCount)
                {
                    bestResult = result;
                    bestItemCount = result.Items.Count;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error applying {Strategy} reduction strategy", strategyName);
            }
        }
        
        return bestResult ?? new ReductionResult<T>
        {
            Items = new List<T>(),
            OriginalCount = itemsList.Count,
            EstimatedTokens = 0,
            Metadata = new Dictionary<string, object> { ["error"] = "No suitable reduction strategy found" }
        };
    }
    
    private void RegisterDefaultStrategies()
    {
        RegisterStrategy(new StandardReductionStrategy());
        RegisterStrategy(new PriorityBasedReductionStrategy());
        // Additional strategies will be registered as they are implemented
    }
}