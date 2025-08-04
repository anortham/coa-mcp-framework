using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace COA.Mcp.Framework.TokenOptimization.Caching;

public class ResponseCacheService : IResponseCacheService, IDisposable
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly ICacheEvictionPolicy _evictionPolicy;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Timer _cleanupTimer;
    private long _totalRequests;
    private long _cacheHits;
    private long _cacheMisses;
    private DateTime _lastCleanup = DateTime.UtcNow;
    
    public ResponseCacheService(ICacheEvictionPolicy? evictionPolicy = null)
    {
        _evictionPolicy = evictionPolicy ?? new LruEvictionPolicy();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        
        // Run cleanup every 5 minutes
        _cleanupTimer = new Timer(
            _ => Task.Run(CleanupExpiredAsync),
            null,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5));
    }
    
    public Task<T?> GetAsync<T>(string key) where T : class
    {
        Interlocked.Increment(ref _totalRequests);
        
        if (_cache.TryGetValue(key, out var entry))
        {
            // Check expiration
            if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value <= DateTime.UtcNow)
            {
                _cache.TryRemove(key, out _);
                Interlocked.Increment(ref _cacheMisses);
                return Task.FromResult<T?>(null);
            }
            
            // Update access time and count
            entry.LastAccessedAt = DateTime.UtcNow;
            entry.AccessCount++;
            
            Interlocked.Increment(ref _cacheHits);
            
            if (entry.Value is T typedValue)
            {
                return Task.FromResult<T?>(typedValue);
            }
            
            // Try to deserialize if stored as JSON string
            if (entry.Value is string json)
            {
                var deserialized = JsonSerializer.Deserialize<T>(json, _jsonOptions);
                return Task.FromResult(deserialized);
            }
        }
        
        Interlocked.Increment(ref _cacheMisses);
        return Task.FromResult<T?>(null);
    }
    
    public Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null) where T : class
    {
        options ??= new CacheEntryOptions();
        
        var now = DateTime.UtcNow;
        DateTime? expiresAt = null;
        
        if (options.AbsoluteExpiration.HasValue)
        {
            expiresAt = now.Add(options.AbsoluteExpiration.Value);
        }
        else if (options.SlidingExpiration.HasValue)
        {
            expiresAt = now.Add(options.SlidingExpiration.Value);
        }
        
        // Calculate size
        var json = JsonSerializer.Serialize(value, _jsonOptions);
        var sizeInBytes = Encoding.UTF8.GetByteCount(json);
        
        var entry = new CacheEntry
        {
            Key = key,
            Value = value,
            CreatedAt = now,
            LastAccessedAt = now,
            ExpiresAt = expiresAt,
            AccessCount = 0,
            SizeInBytes = sizeInBytes,
            Priority = options.Priority,
            Tags = options.Tags
        };
        
        _cache[key] = entry;
        
        // Check if we need to evict
        var stats = GetStatisticsAsync().Result;
        if (_evictionPolicy.ShouldEvict(entry, stats))
        {
            Task.Run(PerformEvictionAsync);
        }
        
        return Task.CompletedTask;
    }
    
    public Task<bool> ExistsAsync(string key)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value <= DateTime.UtcNow)
            {
                _cache.TryRemove(key, out _);
                return Task.FromResult(false);
            }
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
    
    public Task RemoveAsync(string key)
    {
        _cache.TryRemove(key, out _);
        return Task.CompletedTask;
    }
    
    public Task<CacheStatistics> GetStatisticsAsync()
    {
        var totalMemory = _cache.Values.Sum(e => e.SizeInBytes);
        
        var stats = new CacheStatistics
        {
            TotalRequests = _totalRequests,
            CacheHits = _cacheHits,
            CacheMisses = _cacheMisses,
            CurrentItemCount = _cache.Count,
            TotalMemoryBytes = totalMemory,
            LastCleanup = _lastCleanup
        };
        
        return Task.FromResult(stats);
    }
    
    public Task ClearAsync()
    {
        _cache.Clear();
        _totalRequests = 0;
        _cacheHits = 0;
        _cacheMisses = 0;
        return Task.CompletedTask;
    }
    
    private async Task CleanupExpiredAsync()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _cache
            .Where(kvp => kvp.Value.ExpiresAt.HasValue && kvp.Value.ExpiresAt.Value <= now)
            .Select(kvp => kvp.Key)
            .ToList();
            
        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }
        
        _lastCleanup = now;
        
        // Also perform eviction if needed
        var stats = await GetStatisticsAsync();
        var entries = _cache.Values.ToList();
        
        foreach (var entry in entries)
        {
            if (_evictionPolicy.ShouldEvict(entry, stats))
            {
                await PerformEvictionAsync();
                break;
            }
        }
    }
    
    private Task PerformEvictionAsync()
    {
        var targetEvictionCount = Math.Max(1, _cache.Count / 10); // Evict 10% at a time
        var candidates = _evictionPolicy.GetEvictionCandidates(_cache.Values, targetEvictionCount);
        
        foreach (var entry in candidates)
        {
            _cache.TryRemove(entry.Key, out _);
        }
        
        return Task.CompletedTask;
    }
    
    public void Dispose()
    {
        _cleanupTimer?.Dispose();
    }
}