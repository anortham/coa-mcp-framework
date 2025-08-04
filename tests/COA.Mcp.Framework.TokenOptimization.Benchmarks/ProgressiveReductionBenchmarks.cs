using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using COA.Mcp.Framework.TokenOptimization;
using COA.Mcp.Framework.TokenOptimization.Reduction;

namespace COA.Mcp.Framework.TokenOptimization.Benchmarks;

[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[RankColumn]
public class ProgressiveReductionBenchmarks
{
    private List<TestItem> _items = null!;
    private ProgressiveReductionEngine _engine = null!;
    private StandardReductionStrategy _standardStrategy = null!;
    private PriorityBasedReductionStrategy _priorityStrategy = null!;
    
    [Params(100, 1000, 10000)]
    public int ItemCount { get; set; }
    
    [Params(1000, 5000, 10000)]
    public int TokenLimit { get; set; }
    
    [GlobalSetup]
    public void Setup()
    {
        _items = Enumerable.Range(1, ItemCount)
            .Select(i => new TestItem
            {
                Id = i,
                Name = $"Item {i}",
                Description = string.Join(" ", Enumerable.Repeat($"Description for item {i}", 10)),
                Priority = i % 3 // 0, 1, or 2
            })
            .ToList();
            
        _standardStrategy = new StandardReductionStrategy();
        _priorityStrategy = new PriorityBasedReductionStrategy();
        
        _engine = new ProgressiveReductionEngine();
        _engine.RegisterStrategy(_standardStrategy);
        _engine.RegisterStrategy(_priorityStrategy);
    }
    
    [Benchmark(Baseline = true)]
    public List<TestItem> StandardReduction()
    {
        var result = _engine.Reduce(
            _items,
            item => TokenEstimator.EstimateObject(item),
            TokenLimit,
            "standard");
        return result.Items.ToList();
    }
    
    [Benchmark]
    public List<TestItem> PriorityBasedReduction()
    {
        var result = _engine.Reduce(
            _items,
            item => TokenEstimator.EstimateObject(item),
            TokenLimit,
            "priority",
            new ReductionContext
            {
                PriorityFunction = item => ((TestItem)item).Priority
            });
        return result.Items.ToList();
    }
    
    [Benchmark]
    public List<TestItem> EngineReduction()
    {
        var result = _engine.Reduce(
            _items,
            item => TokenEstimator.EstimateObject(item),
            TokenLimit,
            "standard",
            new ReductionContext
            {
                PriorityFunction = item => ((TestItem)item).Priority
            });
            
        return result.Items.ToList();
    }
    
    [Benchmark]
    public List<TestItem> TokenEstimatorApplyReduction()
    {
        return TokenEstimator.ApplyProgressiveReduction(
            _items,
            item => TokenEstimator.EstimateObject(item),
            TokenLimit);
    }
    
    public class TestItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Priority { get; set; }
    }
}