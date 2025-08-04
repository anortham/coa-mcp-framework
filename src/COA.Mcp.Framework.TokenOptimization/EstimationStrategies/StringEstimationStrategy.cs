namespace COA.Mcp.Framework.TokenOptimization.EstimationStrategies;

/// <summary>
/// Token estimation strategy for string values.
/// </summary>
public class StringEstimationStrategy : IEstimationStrategy
{
    /// <inheritdoc/>
    public int Priority => 100;
    
    /// <inheritdoc/>
    public bool CanHandle(Type type)
    {
        return type == typeof(string);
    }
    
    /// <inheritdoc/>
    public int Estimate(object? value)
    {
        if (value is not string str)
            return 0;
        
        return TokenEstimator.EstimateString(str);
    }
}