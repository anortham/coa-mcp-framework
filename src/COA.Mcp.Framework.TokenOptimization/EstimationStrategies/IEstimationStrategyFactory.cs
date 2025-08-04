namespace COA.Mcp.Framework.TokenOptimization.EstimationStrategies;

/// <summary>
/// Factory for creating estimation strategies based on type.
/// </summary>
public interface IEstimationStrategyFactory
{
    /// <summary>
    /// Gets the appropriate estimation strategy for the given type.
    /// </summary>
    /// <param name="type">The type to get a strategy for.</param>
    /// <returns>The estimation strategy, or null if no suitable strategy is found.</returns>
    IEstimationStrategy? GetStrategy(Type type);
    
    /// <summary>
    /// Registers an estimation strategy.
    /// </summary>
    /// <param name="strategy">The strategy to register.</param>
    void RegisterStrategy(IEstimationStrategy strategy);
}