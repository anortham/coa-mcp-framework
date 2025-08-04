using System;
using System.Threading.Tasks;

namespace COA.Mcp.Framework.TokenOptimization.Caching;

public interface IResponseCacheService
{
    Task<T?> GetAsync<T>(string key) where T : class;
    
    Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null) where T : class;
    
    Task<bool> ExistsAsync(string key);
    
    Task RemoveAsync(string key);
    
    Task<CacheStatistics> GetStatisticsAsync();
    
    Task ClearAsync();
}

public class CacheEntryOptions
{
    public TimeSpan? AbsoluteExpiration { get; set; }
    public TimeSpan? SlidingExpiration { get; set; }
    public CachePriority Priority { get; set; } = CachePriority.Normal;
    public string[]? Tags { get; set; }
}

public enum CachePriority
{
    Low,
    Normal,
    High,
    NeverRemove
}

public class CacheStatistics
{
    public long TotalRequests { get; set; }
    public long CacheHits { get; set; }
    public long CacheMisses { get; set; }
    public double HitRate => TotalRequests > 0 ? (double)CacheHits / TotalRequests : 0;
    public long CurrentItemCount { get; set; }
    public long TotalMemoryBytes { get; set; }
    public DateTime LastCleanup { get; set; }
}