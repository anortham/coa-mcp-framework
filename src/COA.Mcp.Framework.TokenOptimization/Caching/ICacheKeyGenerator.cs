using System.Collections.Generic;

namespace COA.Mcp.Framework.TokenOptimization.Caching;

public interface ICacheKeyGenerator
{
    string GenerateKey(string toolName, object parameters);
    
    string GenerateKey(string toolName, Dictionary<string, object?> parameters);
}

public interface ICacheEvictionPolicy
{
    bool ShouldEvict(CacheEntry entry, CacheStatistics statistics);
    
    IEnumerable<CacheEntry> GetEvictionCandidates(IEnumerable<CacheEntry> entries, int targetCount);
}

public class CacheEntry
{
    public string Key { get; set; } = string.Empty;
    public object Value { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime LastAccessedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public long AccessCount { get; set; }
    public long SizeInBytes { get; set; }
    public CachePriority Priority { get; set; }
    public string[]? Tags { get; set; }
}