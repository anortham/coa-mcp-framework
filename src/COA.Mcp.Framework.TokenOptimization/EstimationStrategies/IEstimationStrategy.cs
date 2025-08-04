namespace COA.Mcp.Framework.TokenOptimization.EstimationStrategies;

/// <summary>
/// Interface for token estimation strategies.
/// </summary>
public interface IEstimationStrategy
{
    /// <summary>
    /// Determines if this strategy can handle the given type.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if this strategy can handle the type; otherwise, false.</returns>
    bool CanHandle(Type type);
    
    /// <summary>
    /// Estimates the number of tokens for the given value.
    /// </summary>
    /// <param name="value">The value to estimate.</param>
    /// <returns>Estimated token count.</returns>
    int Estimate(object? value);
    
    /// <summary>
    /// Gets the priority of this strategy (higher values are checked first).
    /// </summary>
    int Priority { get; }
}