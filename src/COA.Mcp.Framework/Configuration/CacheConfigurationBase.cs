namespace COA.Mcp.Framework.Configuration;

/// <summary>
/// Base configuration for caching functionality
/// </summary>
public abstract class CacheConfigurationBase
{
    /// <summary>
    /// Default time-to-live for cache entries
    /// </summary>
    public string DefaultTTL { get; set; } = "00:05:00";

    /// <summary>
    /// Maximum cache size (e.g., "100MB", "1GB")
    /// </summary>
    public string MaxCacheSize { get; set; } = "100MB";

    /// <summary>
    /// Cache eviction policy (LRU, LFU, FIFO)
    /// </summary>
    public string EvictionPolicy { get; set; } = "LRU";

    /// <summary>
    /// Whether caching is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Cleanup interval for expired entries
    /// </summary>
    public string CleanupInterval { get; set; } = "00:10:00";
}

/// <summary>
/// Base settings for operation-specific cache configuration
/// </summary>
public class OperationCacheSettingsBase
{
    /// <summary>
    /// Time-to-live for this operation's cache entries
    /// </summary>
    public string TTL { get; set; } = "00:05:00";

    /// <summary>
    /// Maximum number of entries for this operation
    /// </summary>
    public int MaxEntries { get; set; } = 500;

    /// <summary>
    /// Cache priority (Low, Normal, High)
    /// </summary>
    public string Priority { get; set; } = "Normal";

    /// <summary>
    /// Whether this operation's cache is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;
}