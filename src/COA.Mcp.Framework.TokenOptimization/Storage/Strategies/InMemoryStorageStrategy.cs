using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace COA.Mcp.Framework.TokenOptimization.Storage.Strategies;

public class InMemoryStorageStrategy : IStorageStrategy
{
    private readonly ConcurrentDictionary<string, StoredResource> _storage = new();
    private readonly TimeSpan _defaultExpiration = TimeSpan.FromHours(1);
    
    public string StorageType => "memory";
    
    public Task<string> StoreAsync(byte[] data, ResourceUri uri)
    {
        var resource = new StoredResource
        {
            Data = data,
            Uri = uri,
            StoredAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(_defaultExpiration)
        };
        
        _storage[uri.Id] = resource;
        return Task.FromResult(uri.Id);
    }
    
    public Task<byte[]?> RetrieveAsync(ResourceUri uri)
    {
        if (_storage.TryGetValue(uri.Id, out var resource))
        {
            if (resource.ExpiresAt > DateTime.UtcNow)
            {
                return Task.FromResult<byte[]?>(resource.Data);
            }
            
            // Remove expired resource
            _storage.TryRemove(uri.Id, out _);
        }
        
        return Task.FromResult<byte[]?>(null);
    }
    
    public Task<bool> ExistsAsync(ResourceUri uri)
    {
        if (_storage.TryGetValue(uri.Id, out var resource))
        {
            if (resource.ExpiresAt > DateTime.UtcNow)
            {
                return Task.FromResult(true);
            }
            
            // Remove expired resource
            _storage.TryRemove(uri.Id, out _);
        }
        
        return Task.FromResult(false);
    }
    
    public Task<bool> DeleteAsync(ResourceUri uri)
    {
        return Task.FromResult(_storage.TryRemove(uri.Id, out _));
    }
    
    public Task<long> GetSizeAsync(ResourceUri uri)
    {
        if (_storage.TryGetValue(uri.Id, out var resource))
        {
            return Task.FromResult((long)resource.Data.Length);
        }
        
        return Task.FromResult(0L);
    }
    
    public Task CleanupAsync()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _storage
            .Where(kvp => kvp.Value.ExpiresAt <= now)
            .Select(kvp => kvp.Key)
            .ToList();
            
        foreach (var key in expiredKeys)
        {
            _storage.TryRemove(key, out _);
        }
        
        return Task.CompletedTask;
    }
    
    private class StoredResource
    {
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public ResourceUri Uri { get; set; } = null!;
        public DateTime StoredAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}