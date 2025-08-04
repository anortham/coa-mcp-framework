namespace COA.Mcp.Framework.TokenOptimization.Reduction;

/// <summary>
/// Interface for reduction strategies that determine how to reduce collections to fit within token limits.
/// </summary>
public interface IReductionStrategy
{
    /// <summary>
    /// Gets the name of this reduction strategy.
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Reduces a collection to fit within the specified token limit.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <param name="items">The collection to reduce.</param>
    /// <param name="itemEstimator">Function to estimate tokens for a single item.</param>
    /// <param name="tokenLimit">The maximum allowed tokens.</param>
    /// <param name="context">Optional context for the reduction operation.</param>
    /// <returns>A reduced collection that fits within the token limit.</returns>
    ReductionResult<T> Reduce<T>(
        IList<T> items,
        Func<T, int> itemEstimator,
        int tokenLimit,
        ReductionContext? context = null);
}

/// <summary>
/// Context information for reduction operations.
/// </summary>
public class ReductionContext
{
    /// <summary>
    /// Gets or sets the priority function for items (higher values are kept).
    /// </summary>
    public Func<object, double>? PriorityFunction { get; set; }
    
    /// <summary>
    /// Gets or sets whether to preserve the original order of items.
    /// </summary>
    public bool PreserveOrder { get; set; } = true;
    
    /// <summary>
    /// Gets or sets custom metadata for the reduction operation.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Result of a reduction operation.
/// </summary>
/// <typeparam name="T">The type of items in the collection.</typeparam>
public class ReductionResult<T>
{
    /// <summary>
    /// Gets the reduced collection.
    /// </summary>
    public List<T> Items { get; init; } = new();
    
    /// <summary>
    /// Gets the estimated token count for the reduced collection.
    /// </summary>
    public int EstimatedTokens { get; init; }
    
    /// <summary>
    /// Gets the original item count before reduction.
    /// </summary>
    public int OriginalCount { get; init; }
    
    /// <summary>
    /// Gets the reduction percentage applied.
    /// </summary>
    public double ReductionPercentage => OriginalCount > 0 
        ? (1.0 - (double)Items.Count / OriginalCount) * 100 
        : 0;
    
    /// <summary>
    /// Gets whether the collection was truncated.
    /// </summary>
    public bool WasTruncated => Items.Count < OriginalCount;
    
    /// <summary>
    /// Gets additional metadata about the reduction.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}