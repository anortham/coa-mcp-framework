using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using COA.Mcp.Framework.TokenOptimization.Storage;
using COA.Mcp.Framework.TokenOptimization.Storage.Strategies;

namespace COA.Mcp.Framework.TokenOptimization.Benchmarks;

[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[RankColumn]
public class StorageBenchmarks
{
    private ResourceStorageService _storageService = null!;
    private InMemoryStorageStrategy _memoryStrategy = null!;
    private CompressedStorageStrategy _compressedStrategy = null!;
    private List<TestData> _testData = null!;
    private List<ResourceUri> _storedUris = null!;
    
    [Params(10, 100, 1000)]
    public int DataSize { get; set; }
    
    [GlobalSetup]
    public void Setup()
    {
        _storageService = new ResourceStorageService();
        _memoryStrategy = new InMemoryStorageStrategy();
        _compressedStrategy = new CompressedStorageStrategy(_memoryStrategy);
        
        _storageService.RegisterStrategy(_compressedStrategy);
        
        _testData = new List<TestData>(DataSize);
        _storedUris = new List<ResourceUri>(DataSize);
        
        for (int i = 0; i < DataSize; i++)
        {
            var data = new TestData
            {
                Id = Guid.NewGuid(),
                Name = $"Test Data {i}",
                Description = string.Join(" ", Enumerable.Repeat($"Description {i}", 100)),
                Values = Enumerable.Range(1, 100).ToList(),
                Properties = Enumerable.Range(1, 50)
                    .ToDictionary(j => $"Prop{j}", j => (object)$"Value{j}")
            };
            _testData.Add(data);
            
            // Pre-store some data
            var uri = _storageService.StoreAsync(data).Result;
            _storedUris.Add(uri);
        }
    }
    
    [Benchmark]
    public async Task<ResourceUri> StoreUncompressed()
    {
        var index = Random.Shared.Next(DataSize);
        return await _storageService.StoreAsync(_testData[index], new ResourceStorageOptions
        {
            Compress = false,
            Category = "benchmark"
        });
    }
    
    [Benchmark]
    public async Task<ResourceUri> StoreCompressed()
    {
        var index = Random.Shared.Next(DataSize);
        return await _storageService.StoreAsync(_testData[index], new ResourceStorageOptions
        {
            Compress = true,
            Category = "benchmark"
        });
    }
    
    [Benchmark]
    public async Task<TestData?> RetrieveData()
    {
        var index = Random.Shared.Next(_storedUris.Count);
        return await _storageService.RetrieveAsync<TestData>(_storedUris[index]);
    }
    
    [Benchmark]
    public async Task<bool> ExistsCheck()
    {
        var index = Random.Shared.Next(_storedUris.Count);
        return await _storageService.ExistsAsync(_storedUris[index]);
    }
    
    [Benchmark]
    public async Task<ResourceStorageInfo> GetInfo()
    {
        var index = Random.Shared.Next(_storedUris.Count);
        return await _storageService.GetInfoAsync(_storedUris[index]);
    }
    
    [Benchmark]
    public async Task CleanupExpired()
    {
        await _storageService.CleanupExpiredAsync();
    }
    
    [Benchmark]
    public ResourceUri ParseUri()
    {
        var index = Random.Shared.Next(_storedUris.Count);
        var uriString = _storedUris[index].ToString();
        return new ResourceUri(uriString);
    }
    
    public class TestData
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<int> Values { get; set; } = new();
        public Dictionary<string, object> Properties { get; set; } = new();
    }
}