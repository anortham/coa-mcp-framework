namespace COA.Mcp.Framework.TokenOptimization.Reduction;

/// <summary>
/// Reduction strategy that keeps items based on priority scores.
/// </summary>
public class PriorityBasedReductionStrategy : IReductionStrategy
{
    /// <inheritdoc/>
    public string Name => "priority";
    
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
        var priorityFunction = context?.PriorityFunction ?? DefaultPriorityFunction;
        
        // Create items with priorities
        var itemsWithPriority = items
            .Select((item, index) => new
            {
                Item = item,
                Priority = priorityFunction(item!),
                OriginalIndex = index,
                TokenCost = itemEstimator(item)
            })
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.OriginalIndex) // Maintain original order for equal priorities
            .ToList();
        
        var selectedItems = new List<T>();
        var totalTokens = 0;
        var structureOverhead = 50; // JSON structure overhead
        
        foreach (var itemInfo in itemsWithPriority)
        {
            var newTotal = totalTokens + itemInfo.TokenCost + structureOverhead;
            if (newTotal > tokenLimit && selectedItems.Count > 0)
                break;
            
            selectedItems.Add(itemInfo.Item);
            totalTokens = newTotal;
        }
        
        // If nothing fits, take the highest priority item
        if (selectedItems.Count == 0 && items.Count > 0)
        {
            var highestPriorityItem = itemsWithPriority.First();
            selectedItems.Add(highestPriorityItem.Item);
            totalTokens = highestPriorityItem.TokenCost;
        }
        
        // Restore original order if requested
        if (context?.PreserveOrder == true)
        {
            var itemSet = new HashSet<T>(selectedItems);
            selectedItems = items.Where(item => itemSet.Contains(item)).ToList();
        }
        
        return new ReductionResult<T>
        {
            Items = selectedItems,
            OriginalCount = originalCount,
            EstimatedTokens = totalTokens,
            Metadata = new Dictionary<string, object>
            {
                ["strategy"] = Name,
                ["items_retained"] = selectedItems.Count,
                ["highest_priority_excluded"] = itemsWithPriority
                    .Where(x => !selectedItems.Contains(x.Item))
                    .Select(x => x.Priority)
                    .DefaultIfEmpty(0)
                    .Max()
            }
        };
    }
    
    private double DefaultPriorityFunction(object item)
    {
        // Default: all items have equal priority
        // Can be overridden via context.PriorityFunction
        return 1.0;
    }
}