using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using COA.Mcp.Framework.TokenOptimization.Storage.Strategies;
using COA.Mcp.Framework.Utilities;

namespace COA.Mcp.Framework.TokenOptimization.Storage;

public class ResourceStorageService : IResourceStorageService
{
    private readonly ConcurrentDictionary<string, IStorageStrategy> _strategies = new();
    private readonly ConcurrentDictionary<string, ResourceStorageInfo> _metadata = new();
    private readonly JsonSerializerOptions _jsonOptions;
    
    public ResourceStorageService()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        
        // Register default strategies
        RegisterStrategy(new InMemoryStorageStrategy());
    }
    
    public void RegisterStrategy(IStorageStrategy strategy)
    {
        _strategies[strategy.StorageType] = strategy;
    }
    
    public async Task<ResourceUri> StoreAsync<T>(T resource, ResourceStorageOptions? options = null)
    {
        options ??= new ResourceStorageOptions();
        
        var storageType = options.Compress ? "memory-compressed" : "memory";
        var category = options.Category ?? typeof(T).Name.ToLowerInvariant();
        var uri = ResourceUri.Create(storageType, category);
        
        // Serialize resource
        var json = JsonSerializer.Serialize(resource, _jsonOptions);
        var data = Encoding.UTF8.GetBytes(json);
        
        // Get appropriate strategy
        var strategy = GetStrategy(storageType);
        if (strategy == null)
        {
            // Create compressed strategy on demand
            if (storageType == "memory-compressed")
            {
                var innerStrategy = GetStrategy("memory") ?? new InMemoryStorageStrategy();
                strategy = new CompressedStorageStrategy(innerStrategy);
                RegisterStrategy(strategy);
            }
            else
            {
                throw new InvalidOperationException($"No storage strategy registered for type: {storageType}");
            }
        }
        
        // Store the data
        await strategy.StoreAsync(data, uri);
        
        // Store metadata
        var info = new ResourceStorageInfo
        {
            Uri = uri,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = options.Expiration.HasValue ? DateTime.UtcNow.Add(options.Expiration.Value) : null,
            SizeInBytes = data.Length,
            IsCompressed = options.Compress,
            Category = category,
            Metadata = options.Metadata
        };
        _metadata[uri.Id] = info;
        
        return uri;
    }
    
    public async Task<T?> RetrieveAsync<T>(ResourceUri uri)
    {
        var strategy = GetStrategyForUri(uri);
        if (strategy == null)
            return default;
            
        var data = await strategy.RetrieveAsync(uri);
        if (data == null)
            return default;
            
        var json = Encoding.UTF8.GetString(data);
        return JsonSerializer.Deserialize<T>(json, _jsonOptions);
    }
    
    public async Task<bool> ExistsAsync(ResourceUri uri)
    {
        var strategy = GetStrategyForUri(uri);
        if (strategy == null)
            return false;
            
        return await strategy.ExistsAsync(uri);
    }
    
    public async Task<bool> DeleteAsync(ResourceUri uri)
    {
        var strategy = GetStrategyForUri(uri);
        if (strategy == null)
            return false;
            
        _metadata.TryRemove(uri.Id, out _);
        return await strategy.DeleteAsync(uri);
    }
    
    public Task<ResourceStorageInfo> GetInfoAsync(ResourceUri uri)
    {
        if (_metadata.TryGetValue(uri.Id, out var info))
        {
            return Task.FromResult(info);
        }
        
        throw new InvalidOperationException($"Resource not found: {uri}");
    }
    
    public async Task CleanupExpiredAsync()
    {
        var now = DateTime.UtcNow;
        
        // Find expired resources to clean up
        var expiredResources = _metadata
            .Where(kvp => kvp.Value.ExpiresAt.HasValue && kvp.Value.ExpiresAt.Value <= now)
            .ToArray();
        
        // Clean up expired metadata concurrently with limited concurrency to avoid overwhelming storage
        if (expiredResources.Any())
        {
            await ConcurrentAsyncUtilities.ExecuteConcurrentlyAsync(
                expiredResources,
                async kvp =>
                {
                    _metadata.TryRemove(kvp.Key, out _);
                    await DeleteAsync(kvp.Value.Uri);
                },
                maxConcurrency: 5); // Limit to 5 concurrent deletions to avoid overwhelming storage
        }
        
        // Clean up each strategy concurrently
        await ConcurrentAsyncUtilities.ExecuteConcurrentlyAsync(
            _strategies.Values,
            async strategy => await strategy.CleanupAsync());
    }
    
    private IStorageStrategy? GetStrategy(string storageType)
    {
        return _strategies.TryGetValue(storageType, out var strategy) ? strategy : null;
    }
    
    private IStorageStrategy? GetStrategyForUri(ResourceUri uri)
    {
        return GetStrategy(uri.StorageType);
    }
}