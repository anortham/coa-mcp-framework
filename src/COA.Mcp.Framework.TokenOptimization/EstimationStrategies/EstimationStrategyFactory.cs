namespace COA.Mcp.Framework.TokenOptimization.EstimationStrategies;

/// <summary>
/// Default implementation of the estimation strategy factory.
/// </summary>
public class EstimationStrategyFactory : IEstimationStrategyFactory
{
    private readonly List<IEstimationStrategy> _strategies = new();
    
    /// <summary>
    /// Initializes a new instance of the <see cref="EstimationStrategyFactory"/> class.
    /// </summary>
    public EstimationStrategyFactory()
    {
        // Register default strategies
        RegisterDefaultStrategies();
    }
    
    /// <inheritdoc/>
    public IEstimationStrategy? GetStrategy(Type type)
    {
        // Return the first strategy that can handle the type, ordered by priority
        return _strategies
            .OrderByDescending(s => s.Priority)
            .FirstOrDefault(s => s.CanHandle(type));
    }
    
    /// <inheritdoc/>
    public void RegisterStrategy(IEstimationStrategy strategy)
    {
        if (strategy == null)
            throw new ArgumentNullException(nameof(strategy));
        
        _strategies.Add(strategy);
    }
    
    private void RegisterDefaultStrategies()
    {
        RegisterStrategy(new StringEstimationStrategy());
        RegisterStrategy(new CollectionEstimationStrategy(this));
        RegisterStrategy(new ObjectEstimationStrategy());
    }
}