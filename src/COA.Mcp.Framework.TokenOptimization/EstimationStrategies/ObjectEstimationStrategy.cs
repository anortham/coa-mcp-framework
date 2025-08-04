namespace COA.Mcp.Framework.TokenOptimization.EstimationStrategies;

/// <summary>
/// Token estimation strategy for general objects using JSON serialization.
/// </summary>
public class ObjectEstimationStrategy : IEstimationStrategy
{
    /// <inheritdoc/>
    public int Priority => 10; // Lowest priority, used as fallback
    
    /// <inheritdoc/>
    public bool CanHandle(Type type)
    {
        // This strategy can handle any type as a fallback
        return true;
    }
    
    /// <inheritdoc/>
    public int Estimate(object? value)
    {
        return TokenEstimator.EstimateObject(value);
    }
}