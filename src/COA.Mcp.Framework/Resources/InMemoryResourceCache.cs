using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Protocol;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace COA.Mcp.Framework.Resources;

/// <summary>
/// In-memory implementation of resource cache with automatic expiration and size limits.
/// Thread-safe and suitable for singleton lifetime.
/// </summary>
public class InMemoryResourceCache : IResourceCache
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<InMemoryResourceCache> _logger;
    private readonly ResourceCacheOptions _options;
    private readonly object _statsLock = new();
    
    private long _totalHits;
    private long _totalMisses;
    private long _totalEvictions;
    private readonly Dictionary<string, int> _hitsByScheme = new();
    private DateTime _lastEviction = DateTime.MinValue;

    public InMemoryResourceCache(
        IMemoryCache cache,
        ILogger<InMemoryResourceCache> logger,
        IOptions<ResourceCacheOptions> options)
    {
        _cache = cache;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    public Task<ReadResourceResult?> GetAsync(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return Task.FromResult<ReadResourceResult?>(null);
        }

        var result = _cache.Get<ReadResourceResult>(GetCacheKey(uri));
        
        lock (_statsLock)
        {
            if (result != null)
            {
                _totalHits++;
                var scheme = ExtractScheme(uri);
                _hitsByScheme[scheme] = _hitsByScheme.GetValueOrDefault(scheme) + 1;
                _logger.LogDebug("Cache hit for resource: {Uri}", uri);
            }
            else
            {
                _totalMisses++;
                _logger.LogDebug("Cache miss for resource: {Uri}", uri);
            }
        }

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task SetAsync(string uri, ReadResourceResult result, TimeSpan? expiry = null)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            throw new ArgumentException("URI cannot be null or empty", nameof(uri));
        }

        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        var cacheKey = GetCacheKey(uri);
        var effectiveExpiry = expiry ?? _options.DefaultExpiration;

        var cacheEntryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = effectiveExpiry,
            SlidingExpiration = _options.SlidingExpiration,
            Priority = _options.Priority,
            Size = EstimateSize(result)
        };

        // Register eviction callback for statistics
        cacheEntryOptions.RegisterPostEvictionCallback((key, value, reason, state) =>
        {
            lock (_statsLock)
            {
                _totalEvictions++;
                _lastEviction = DateTime.UtcNow;
            }
            
            _logger.LogDebug("Resource evicted from cache: {Key}, Reason: {Reason}", key, reason);
        });

        _cache.Set(cacheKey, result, cacheEntryOptions);
        _logger.LogDebug("Cached resource: {Uri} with expiration: {Expiry}", uri, effectiveExpiry);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return Task.FromResult(false);
        }

        var exists = _cache.TryGetValue(GetCacheKey(uri), out _);
        return Task.FromResult(exists);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string uri)
    {
        if (!string.IsNullOrWhiteSpace(uri))
        {
            _cache.Remove(GetCacheKey(uri));
            _logger.LogDebug("Removed resource from cache: {Uri}", uri);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearAsync()
    {
        // IMemoryCache doesn't have a clear method, so we'll need to track keys
        // For now, this is a limitation of the in-memory implementation
        _logger.LogWarning("Clear operation requested but not fully supported by IMemoryCache. " +
                          "Consider restarting the application for a full cache clear.");
        
        lock (_statsLock)
        {
            _totalHits = 0;
            _totalMisses = 0;
            _totalEvictions = 0;
            _hitsByScheme.Clear();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<CacheStatistics> GetStatisticsAsync()
    {
        lock (_statsLock)
        {
            var stats = new CacheStatistics
            {
                ItemCount = (int)(_cache.GetCurrentStatistics()?.CurrentEntryCount ?? 0),
                TotalHits = _totalHits,
                TotalMisses = _totalMisses,
                TotalEvictions = _totalEvictions,
                EstimatedSizeBytes = _cache.GetCurrentStatistics()?.CurrentEstimatedSize ?? 0,
                LastEviction = _lastEviction,
                HitsByScheme = new Dictionary<string, int>(_hitsByScheme)
            };

            return Task.FromResult(stats);
        }
    }

    private string GetCacheKey(string uri)
    {
        return $"resource:{uri}";
    }

    private string ExtractScheme(string uri)
    {
        var schemeEnd = uri.IndexOf("://");
        return schemeEnd > 0 ? uri.Substring(0, schemeEnd) : "unknown";
    }

    private long EstimateSize(ReadResourceResult result)
    {
        // Rough estimation of object size in bytes
        try
        {
            var json = JsonSerializer.Serialize(result);
            return json.Length * 2; // Unicode characters are 2 bytes
        }
        catch
        {
            // Default size if serialization fails
            return 1024;
        }
    }
}

/// <summary>
/// Configuration options for resource caching.
/// </summary>
public class ResourceCacheOptions
{
    /// <summary>
    /// Default expiration time for cached resources.
    /// </summary>
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Sliding expiration time. Resource expiration is extended by this amount on each access.
    /// </summary>
    public TimeSpan? SlidingExpiration { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Cache priority for resource entries.
    /// </summary>
    public CacheItemPriority Priority { get; set; } = CacheItemPriority.Normal;

    /// <summary>
    /// Maximum cache size in bytes. 0 means no limit.
    /// </summary>
    public long MaxSizeBytes { get; set; } = 100 * 1024 * 1024; // 100 MB default

    /// <summary>
    /// Whether to enable cache statistics tracking.
    /// </summary>
    public bool EnableStatistics { get; set; } = true;
}