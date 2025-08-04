using System.Collections;

namespace COA.Mcp.Framework.TokenOptimization.EstimationStrategies;

/// <summary>
/// Token estimation strategy for collection types.
/// </summary>
public class CollectionEstimationStrategy : IEstimationStrategy
{
    private readonly IEstimationStrategyFactory _strategyFactory;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="CollectionEstimationStrategy"/> class.
    /// </summary>
    /// <param name="strategyFactory">The strategy factory for estimating collection items.</param>
    public CollectionEstimationStrategy(IEstimationStrategyFactory strategyFactory)
    {
        _strategyFactory = strategyFactory ?? throw new ArgumentNullException(nameof(strategyFactory));
    }
    
    /// <inheritdoc/>
    public int Priority => 80;
    
    /// <inheritdoc/>
    public bool CanHandle(Type type)
    {
        return typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string);
    }
    
    /// <inheritdoc/>
    public int Estimate(object? value)
    {
        if (value is not IEnumerable enumerable)
            return 0;
        
        var items = enumerable.Cast<object>().ToList();
        
        // Use TokenEstimator's collection estimation with sampling
        return TokenEstimator.EstimateCollection(items, item =>
        {
            if (item == null)
                return 0;
            
            var strategy = _strategyFactory.GetStrategy(item.GetType());
            return strategy?.Estimate(item) ?? TokenEstimator.EstimateObject(item);
        });
    }
}