namespace COA.Mcp.Framework.TokenOptimization.Reduction;

/// <summary>
/// Standard reduction strategy using fixed percentage steps.
/// </summary>
public class StandardReductionStrategy : IReductionStrategy
{
    private static readonly int[] DefaultReductionSteps = { 100, 75, 50, 30, 20, 10, 5 };
    
    /// <inheritdoc/>
    public string Name => "standard";
    
    /// <inheritdoc/>
    public ReductionResult<T> Reduce<T>(
        IList<T> items,
        Func<T, int> itemEstimator,
        int tokenLimit,
        ReductionContext? context = null)
    {
        if (items.Count == 0)
        {
            return new ReductionResult<T>
            {
                Items = new List<T>(),
                OriginalCount = 0,
                EstimatedTokens = 0
            };
        }
        
        var originalCount = items.Count;
        var reductionSteps = GetReductionSteps(context);
        
        foreach (var percentage in reductionSteps)
        {
            var itemCount = Math.Max(1, (originalCount * percentage) / 100);
            var subset = items.Take(itemCount).ToList();
            var estimatedTokens = TokenEstimator.EstimateCollection(subset, itemEstimator);
            
            if (estimatedTokens <= tokenLimit)
            {
                return new ReductionResult<T>
                {
                    Items = subset,
                    OriginalCount = originalCount,
                    EstimatedTokens = estimatedTokens,
                    Metadata = new Dictionary<string, object>
                    {
                        ["strategy"] = Name,
                        ["percentage_retained"] = percentage,
                        ["step_index"] = Array.IndexOf(reductionSteps, percentage)
                    }
                };
            }
        }
        
        // If even the smallest reduction doesn't fit, return just one item
        var singleItem = items.Take(1).ToList();
        return new ReductionResult<T>
        {
            Items = singleItem,
            OriginalCount = originalCount,
            EstimatedTokens = itemEstimator(singleItem[0]),
            Metadata = new Dictionary<string, object>
            {
                ["strategy"] = Name,
                ["percentage_retained"] = (100.0 / originalCount),
                ["forced_single_item"] = true
            }
        };
    }
    
    private int[] GetReductionSteps(ReductionContext? context)
    {
        if (context?.Metadata.TryGetValue("reduction_steps", out var stepsObj) == true 
            && stepsObj is int[] customSteps)
        {
            return customSteps;
        }
        
        return DefaultReductionSteps;
    }
}