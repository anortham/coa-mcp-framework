using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using COA.Mcp.Framework.TokenOptimization;

namespace COA.Mcp.Framework.TokenOptimization.Benchmarks;

[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[RankColumn]
public class TokenEstimatorBenchmarks
{
    private string _shortString = null!;
    private string _mediumString = null!;
    private string _longString = null!;
    private List<string> _smallCollection = null!;
    private List<string> _largeCollection = null!;
    private ComplexObject _complexObject = null!;
    
    [GlobalSetup]
    public void Setup()
    {
        _shortString = "Hello, World!";
        _mediumString = string.Join(" ", Enumerable.Repeat("Lorem ipsum dolor sit amet", 100));
        _longString = string.Join(" ", Enumerable.Repeat("Lorem ipsum dolor sit amet", 10000));
        
        _smallCollection = Enumerable.Range(1, 10)
            .Select(i => $"Item {i}: {Guid.NewGuid()}")
            .ToList();
            
        _largeCollection = Enumerable.Range(1, 10000)
            .Select(i => $"Item {i}: {Guid.NewGuid()}")
            .ToList();
            
        _complexObject = new ComplexObject
        {
            Id = Guid.NewGuid(),
            Name = "Complex Object",
            Values = Enumerable.Range(1, 100).ToList(),
            Properties = Enumerable.Range(1, 50)
                .ToDictionary(i => $"Prop{i}", i => (object)$"Value{i}"),
            NestedObjects = Enumerable.Range(1, 10)
                .Select(i => new NestedObject { Id = i, Data = $"Nested {i}" })
                .ToList()
        };
    }
    
    [Benchmark]
    public int EstimateShortString()
    {
        return TokenEstimator.EstimateString(_shortString);
    }
    
    [Benchmark]
    public int EstimateMediumString()
    {
        return TokenEstimator.EstimateString(_mediumString);
    }
    
    [Benchmark]
    public int EstimateLongString()
    {
        return TokenEstimator.EstimateString(_longString);
    }
    
    [Benchmark]
    public int EstimateSmallCollection()
    {
        return TokenEstimator.EstimateCollection(_smallCollection, TokenEstimator.EstimateString);
    }
    
    [Benchmark]
    public int EstimateLargeCollection()
    {
        return TokenEstimator.EstimateCollection(_largeCollection, TokenEstimator.EstimateString);
    }
    
    [Benchmark]
    public int EstimateComplexObject()
    {
        return TokenEstimator.EstimateObject(_complexObject);
    }
    
    [Benchmark]
    public int CalculateBudgetDefault()
    {
        return TokenEstimator.CalculateTokenBudget(200000, 5000);
    }
    
    [Benchmark]
    public int CalculateBudgetConservative()
    {
        return TokenEstimator.CalculateTokenBudget(200000, TokenEstimator.CONSERVATIVE_SAFETY_LIMIT);
    }
    
    private class ComplexObject
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<int> Values { get; set; } = new();
        public Dictionary<string, object> Properties { get; set; } = new();
        public List<NestedObject> NestedObjects { get; set; } = new();
    }
    
    private class NestedObject
    {
        public int Id { get; set; }
        public string Data { get; set; } = string.Empty;
    }
}