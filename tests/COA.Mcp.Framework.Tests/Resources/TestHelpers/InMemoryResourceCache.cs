using System.Collections.Concurrent;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Protocol;

namespace COA.Mcp.Framework.Tests.Resources.TestHelpers;

/// <summary>
/// Simple in-memory resource cache implementation for testing.
/// Tests real caching behavior instead of mock interactions.
/// </summary>
#pragma warning disable CS0618 // Type or member is obsolete - testing backward compatibility
public class TestInMemoryResourceCache : IResourceCache
{
    private readonly ConcurrentDictionary<string, ReadResourceResult> _cache = new();
    private long _hits;
    private long _misses;
    
    public Task<ReadResourceResult?> GetAsync(string uri)
    {
        var found = _cache.TryGetValue(uri, out var result);
        if (found) 
            Interlocked.Increment(ref _hits);
        else 
            Interlocked.Increment(ref _misses);
        return Task.FromResult(result);
    }

    public Task SetAsync(string uri, ReadResourceResult result, TimeSpan? expiration = null)
    {
        _cache[uri] = result;
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string uri)
    {
        return Task.FromResult(_cache.ContainsKey(uri));
    }

    public Task RemoveAsync(string uri)
    {
        _cache.TryRemove(uri, out _);
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        _cache.Clear();
        return Task.CompletedTask;
    }

    public Task<CacheStatistics> GetStatisticsAsync()
    {
        return Task.FromResult(new CacheStatistics
        {
            ItemCount = _cache.Count,
            TotalHits = _hits,
            TotalMisses = _misses,
            TotalEvictions = 0,
            EstimatedSizeBytes = _cache.Count * 1024 // Rough estimate
        });
    }

    /// <summary>
    /// Helper method for tests to verify cache contents.
    /// </summary>
    public bool ContainsKey(string uri) => _cache.ContainsKey(uri);
    
    /// <summary>
    /// Helper method for tests to get cache size.
    /// </summary>
    public int Count => _cache.Count;
}
#pragma warning restore CS0618 // Type or member is obsolete