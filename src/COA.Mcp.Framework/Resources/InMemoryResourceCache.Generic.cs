using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Protocol;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace COA.Mcp.Framework.Resources;

/// <summary>
/// Generic in-memory implementation of resource cache with automatic expiration and size limits.
/// Thread-safe and suitable for singleton lifetime.
/// </summary>
/// <typeparam name="TResource">The type of resource to cache.</typeparam>
public class InMemoryResourceCache<TResource> : IResourceCache<TResource> 
    where TResource : class
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<InMemoryResourceCache<TResource>>? _logger;
    private readonly ResourceCacheOptions _options;
    private readonly object _statsLock = new();
    
    private long _totalHits;
    private long _totalMisses;
    private long _totalEvictions;
    private readonly Dictionary<string, int> _hitsByScheme = new();
    private DateTime _lastEviction = DateTime.MinValue;

    /// <summary>
    /// Initializes a new instance of the generic InMemoryResourceCache.
    /// </summary>
    public InMemoryResourceCache(
        IMemoryCache cache,
        ILogger<InMemoryResourceCache<TResource>>? logger = null,
        IOptions<ResourceCacheOptions>? options = null)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger;
        _options = options?.Value ?? new ResourceCacheOptions();
    }

    /// <inheritdoc />
    public Task<TResource?> GetAsync(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return Task.FromResult<TResource?>(null);
        }

        var result = _cache.Get<TResource>(GetCacheKey(uri));
        
        lock (_statsLock)
        {
            if (result != null)
            {
                _totalHits++;
                var scheme = ExtractScheme(uri);
                _hitsByScheme[scheme] = _hitsByScheme.GetValueOrDefault(scheme) + 1;
                _logger?.LogDebug("Cache hit for resource: {Uri}", uri);
            }
            else
            {
                _totalMisses++;
                _logger?.LogDebug("Cache miss for resource: {Uri}", uri);
            }
        }

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task SetAsync(string uri, TResource resource, TimeSpan? expiry = null)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            throw new ArgumentException("URI cannot be null or empty", nameof(uri));
        }

        if (resource == null)
        {
            throw new ArgumentNullException(nameof(resource));
        }

        var cacheKey = GetCacheKey(uri);
        var effectiveExpiry = expiry ?? _options.DefaultExpiration;

        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = effectiveExpiry,
            Size = EstimateSize(resource),
            Priority = DeterminePriority(uri),
        };

        // Set up eviction callback
        cacheOptions.PostEvictionCallbacks.Add(new PostEvictionCallbackRegistration
        {
            EvictionCallback = OnEviction,
            State = uri
        });

        _cache.Set(cacheKey, resource, cacheOptions);
        
        _logger?.LogDebug("Cached resource: {Uri} with expiry: {Expiry}", uri, effectiveExpiry);
        
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
        if (string.IsNullOrWhiteSpace(uri))
        {
            return Task.CompletedTask;
        }

        _cache.Remove(GetCacheKey(uri));
        _logger?.LogDebug("Removed resource from cache: {Uri}", uri);
        
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearAsync()
    {
        if (_cache is MemoryCache memoryCache)
        {
            memoryCache.Compact(1.0); // Compact 100% to clear all entries
        }
        
        lock (_statsLock)
        {
            _totalHits = 0;
            _totalMisses = 0;
            _totalEvictions = 0;
            _hitsByScheme.Clear();
        }
        
        _logger?.LogInformation("Cache cleared");
        
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<CacheStatistics> GetStatisticsAsync()
    {
        lock (_statsLock)
        {
            var stats = new CacheStatistics
            {
                TotalHits = _totalHits,
                TotalMisses = _totalMisses,
                TotalEvictions = _totalEvictions,
                LastEviction = _lastEviction,
                HitsByScheme = new Dictionary<string, int>(_hitsByScheme)
            };

            // Estimate item count and size
            if (_cache is MemoryCache memoryCache)
            {
                var cacheStats = memoryCache.GetCurrentStatistics();
                if (cacheStats != null)
                {
                    stats.ItemCount = (int)cacheStats.CurrentEntryCount;
                    stats.EstimatedSizeBytes = cacheStats.CurrentEstimatedSize ?? 0;
                }
            }

            return Task.FromResult(stats);
        }
    }

    private string GetCacheKey(string uri)
    {
        var typeName = typeof(TResource).Name;
        return $"resource:{typeName}:{uri.ToLowerInvariant()}";
    }

    private string ExtractScheme(string uri)
    {
        var colonIndex = uri.IndexOf(':');
        return colonIndex > 0 ? uri.Substring(0, colonIndex) : "unknown";
    }

    private long EstimateSize(TResource resource)
    {
        // Rough estimation based on JSON serialization
        try
        {
            var json = JsonSerializer.Serialize(resource);
            return json.Length * 2; // Approximate memory usage (Unicode chars)
        }
        catch
        {
            return 1024; // Default 1KB if serialization fails
        }
    }

    private CacheItemPriority DeterminePriority(string uri)
    {
        // Could be customized based on URI patterns or resource types
        var scheme = ExtractScheme(uri);
        return scheme switch
        {
            "file" => CacheItemPriority.High,
            "http" => CacheItemPriority.Normal,
            "https" => CacheItemPriority.Normal,
            _ => CacheItemPriority.Low
        };
    }

    private void OnEviction(object key, object? value, EvictionReason reason, object? state)
    {
        lock (_statsLock)
        {
            _totalEvictions++;
            _lastEviction = DateTime.UtcNow;
        }
        
        _logger?.LogDebug("Resource evicted from cache: {Uri}, Reason: {Reason}", 
            state?.ToString() ?? key.ToString(), reason);
    }
}

/// <summary>
/// Non-generic implementation that specifically handles ReadResourceResult for backward compatibility.
/// </summary>
#pragma warning disable CS0618 // Type or member is obsolete - backward compatibility
public class InMemoryResourceCacheTyped : InMemoryResourceCache<ReadResourceResult>, IResourceCache
#pragma warning restore CS0618 // Type or member is obsolete
{
    /// <summary>
    /// Initializes a new instance for ReadResourceResult caching.
    /// </summary>
    public InMemoryResourceCacheTyped(
        IMemoryCache cache,
        ILogger<InMemoryResourceCache> logger,
        IOptions<ResourceCacheOptions> options)
        : base(cache, logger as ILogger<InMemoryResourceCache<ReadResourceResult>>, options)
    {
    }
}