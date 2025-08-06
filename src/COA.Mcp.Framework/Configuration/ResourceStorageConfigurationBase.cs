namespace COA.Mcp.Framework.Configuration;

/// <summary>
/// Base configuration for resource storage functionality
/// </summary>
public abstract class ResourceStorageConfigurationBase
{
    /// <summary>
    /// Default expiration time for stored resources
    /// </summary>
    public string DefaultExpiration { get; set; } = "01:00:00"; // 1 hour

    /// <summary>
    /// Whether to enable compression for stored resources
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Maximum storage size in megabytes
    /// </summary>
    public long MaxStorageSizeMB { get; set; } = 500; // 500MB max

    /// <summary>
    /// Interval for cleaning up expired resources
    /// </summary>
    public string CleanupInterval { get; set; } = "00:30:00"; // Every 30 minutes

    /// <summary>
    /// Whether storage is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Compression level (0-9, where 0 is no compression and 9 is maximum)
    /// </summary>
    public int CompressionLevel { get; set; } = 6;
}

/// <summary>
/// Base settings for category-specific storage configuration
/// </summary>
public class CategorySettingsBase
{
    /// <summary>
    /// Expiration time for this category
    /// </summary>
    public string Expiration { get; set; } = "01:00:00";

    /// <summary>
    /// Whether to compress items in this category
    /// </summary>
    public bool Compress { get; set; } = true;

    /// <summary>
    /// Maximum number of items in this category
    /// </summary>
    public int MaxItemCount { get; set; } = 1000;

    /// <summary>
    /// Priority for this category (Low, Normal, High)
    /// </summary>
    public string Priority { get; set; } = "Normal";
}