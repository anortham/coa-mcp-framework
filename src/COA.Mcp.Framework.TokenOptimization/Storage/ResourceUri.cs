using System;

namespace COA.Mcp.Framework.TokenOptimization.Storage;

public class ResourceUri
{
    private const string UriPrefix = "mcp-resource://";
    
    public string Id { get; }
    public string StorageType { get; }
    public string Category { get; }
    public DateTime CreatedAt { get; }
    
    public ResourceUri(string id, string storageType, string category)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        StorageType = storageType ?? throw new ArgumentNullException(nameof(storageType));
        Category = category ?? throw new ArgumentNullException(nameof(category));
        CreatedAt = DateTime.UtcNow;
    }
    
    public ResourceUri(string uriString)
    {
        if (string.IsNullOrWhiteSpace(uriString))
            throw new ArgumentException("URI string cannot be null or empty", nameof(uriString));
            
        if (!uriString.StartsWith(UriPrefix))
            throw new ArgumentException($"Invalid resource URI format. Must start with {UriPrefix}", nameof(uriString));
            
        var parts = uriString.Substring(UriPrefix.Length).Split('/');
        if (parts.Length < 3)
            throw new ArgumentException("Invalid resource URI format", nameof(uriString));
            
        StorageType = parts[0];
        Category = parts[1];
        Id = parts[2];
        
        if (parts.Length > 3 && long.TryParse(parts[3], out var ticks))
        {
            CreatedAt = new DateTime(ticks, DateTimeKind.Utc);
        }
        else
        {
            CreatedAt = DateTime.UtcNow;
        }
    }
    
    public override string ToString()
    {
        return $"{UriPrefix}{StorageType}/{Category}/{Id}/{CreatedAt.Ticks}";
    }
    
    public static ResourceUri Create(string storageType, string category)
    {
        var id = Guid.NewGuid().ToString("N");
        return new ResourceUri(id, storageType, category);
    }
    
    public override bool Equals(object? obj)
    {
        return obj is ResourceUri uri && Id == uri.Id;
    }
    
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}