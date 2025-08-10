using COA.Mcp.Protocol;

namespace COA.Mcp.Framework.Interfaces;

/// <summary>
/// Generic cache service for storing and retrieving typed resources.
/// </summary>
/// <typeparam name="TResource">The type of resource to cache.</typeparam>
public interface IResourceCache<TResource> where TResource : class
{
    /// <summary>
    /// Gets a cached resource by URI.
    /// </summary>
    /// <param name="uri">The resource URI.</param>
    /// <returns>The cached resource, or null if not found or expired.</returns>
    Task<TResource?> GetAsync(string uri);

    /// <summary>
    /// Sets a resource in the cache with optional expiration.
    /// </summary>
    /// <param name="uri">The resource URI.</param>
    /// <param name="resource">The resource to cache.</param>
    /// <param name="expiry">Optional expiration time. If null, uses default expiration.</param>
    Task SetAsync(string uri, TResource resource, TimeSpan? expiry = null);

    /// <summary>
    /// Checks if a resource exists in the cache and is not expired.
    /// </summary>
    /// <param name="uri">The resource URI to check.</param>
    /// <returns>True if the resource exists and is not expired, false otherwise.</returns>
    Task<bool> ExistsAsync(string uri);

    /// <summary>
    /// Removes a resource from the cache.
    /// </summary>
    /// <param name="uri">The resource URI to remove.</param>
    Task RemoveAsync(string uri);

    /// <summary>
    /// Clears all cached resources.
    /// </summary>
    Task ClearAsync();

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    /// <returns>Cache statistics including hit rate, size, and item count.</returns>
    Task<CacheStatistics> GetStatisticsAsync();
}

/// <summary>
/// Provides a singleton cache service for resource providers to store and retrieve resources.
/// Resolves the lifetime mismatch between scoped providers and singleton registry.
/// Non-generic version for backward compatibility.
/// </summary>
public interface IResourceCache : IResourceCache<ReadResourceResult>
{
    // All methods are inherited from IResourceCache<ReadResourceResult>
    // This interface exists for backward compatibility
}

/// <summary>
/// Cache statistics for monitoring and debugging.
/// </summary>
public class CacheStatistics
{
    public int ItemCount { get; set; }
    public long TotalHits { get; set; }
    public long TotalMisses { get; set; }
    public long TotalEvictions { get; set; }
    public double HitRate => TotalHits + TotalMisses > 0 
        ? (double)TotalHits / (TotalHits + TotalMisses) 
        : 0;
    public long EstimatedSizeBytes { get; set; }
    public DateTime LastEviction { get; set; }
    public Dictionary<string, int> HitsByScheme { get; set; } = new();
}