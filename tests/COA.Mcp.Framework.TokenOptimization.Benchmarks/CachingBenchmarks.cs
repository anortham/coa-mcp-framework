using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using COA.Mcp.Framework.TokenOptimization.Caching;

namespace COA.Mcp.Framework.TokenOptimization.Benchmarks;

[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[RankColumn]
public class CachingBenchmarks
{
    private ResponseCacheService _cacheService = null!;
    private CacheKeyGenerator _keyGenerator = null!;
    private List<string> _keys = null!;
    private List<TestResponse> _responses = null!;
    
    [Params(100, 1000, 10000)]
    public int CacheSize { get; set; }
    
    [GlobalSetup]
    public void Setup()
    {
        _cacheService = new ResponseCacheService(new LruEvictionPolicy());
        _keyGenerator = new CacheKeyGenerator();
        
        _keys = new List<string>(CacheSize);
        _responses = new List<TestResponse>(CacheSize);
        
        // Pre-populate cache and generate test data
        for (int i = 0; i < CacheSize; i++)
        {
            var key = _keyGenerator.GenerateKey("test_tool", new { id = i, query = $"query_{i}" });
            _keys.Add(key);
            
            var response = new TestResponse
            {
                Id = i,
                Data = $"Response data for item {i}",
                Items = Enumerable.Range(1, 10).Select(j => $"Item {j}").ToList()
            };
            _responses.Add(response);
            
            _cacheService.SetAsync(key, response).Wait();
        }
    }
    
    [Benchmark]
    public string GenerateCacheKey()
    {
        var index = Random.Shared.Next(CacheSize);
        return _keyGenerator.GenerateKey("test_tool", new { id = index, query = $"query_{index}" });
    }
    
    [Benchmark]
    public async Task<TestResponse?> CacheHit()
    {
        var index = Random.Shared.Next(CacheSize);
        return await _cacheService.GetAsync<TestResponse>(_keys[index]);
    }
    
    [Benchmark]
    public async Task<TestResponse?> CacheMiss()
    {
        var key = _keyGenerator.GenerateKey("test_tool", new { id = int.MaxValue, query = "non_existent" });
        return await _cacheService.GetAsync<TestResponse>(key);
    }
    
    [Benchmark]
    public async Task CacheSet()
    {
        var index = Random.Shared.Next(CacheSize);
        var key = $"new_key_{Guid.NewGuid():N}";
        await _cacheService.SetAsync(key, _responses[index]);
    }
    
    [Benchmark]
    public async Task CacheSetWithExpiration()
    {
        var index = Random.Shared.Next(CacheSize);
        var key = $"expiring_key_{Guid.NewGuid():N}";
        var options = new CacheEntryOptions
        {
            AbsoluteExpiration = TimeSpan.FromMinutes(5),
            Priority = CachePriority.Normal
        };
        await _cacheService.SetAsync(key, _responses[index], options);
    }
    
    [Benchmark]
    public async Task<CacheStatistics> GetStatistics()
    {
        return await _cacheService.GetStatisticsAsync();
    }
    
    [GlobalCleanup]
    public void Cleanup()
    {
        _cacheService?.Dispose();
    }
    
    public class TestResponse
    {
        public int Id { get; set; }
        public string Data { get; set; } = string.Empty;
        public List<string> Items { get; set; } = new();
    }
}