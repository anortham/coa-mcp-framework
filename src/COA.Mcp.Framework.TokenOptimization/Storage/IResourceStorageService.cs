using System;
using System.Threading.Tasks;

namespace COA.Mcp.Framework.TokenOptimization.Storage;

public interface IResourceStorageService
{
    Task<ResourceUri> StoreAsync<T>(T resource, ResourceStorageOptions? options = null);
    
    Task<T?> RetrieveAsync<T>(ResourceUri uri);
    
    Task<bool> ExistsAsync(ResourceUri uri);
    
    Task<bool> DeleteAsync(ResourceUri uri);
    
    Task<ResourceStorageInfo> GetInfoAsync(ResourceUri uri);
    
    Task CleanupExpiredAsync();
}

public class ResourceStorageOptions
{
    public TimeSpan? Expiration { get; set; }
    public bool Compress { get; set; }
    public string? Category { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

public class ResourceStorageInfo
{
    public ResourceUri Uri { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public long SizeInBytes { get; set; }
    public bool IsCompressed { get; set; }
    public string? Category { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}