using System;
using System.Collections.Generic;
using System.Linq;

namespace COA.Mcp.Framework.TokenOptimization.Caching;

public class LruEvictionPolicy : ICacheEvictionPolicy
{
    private readonly long _maxMemoryBytes;
    private readonly double _targetMemoryUsageRatio;
    
    public LruEvictionPolicy(long maxMemoryBytes = 100_000_000, double targetMemoryUsageRatio = 0.8)
    {
        _maxMemoryBytes = maxMemoryBytes;
        _targetMemoryUsageRatio = targetMemoryUsageRatio;
    }
    
    public bool ShouldEvict(CacheEntry entry, CacheStatistics statistics)
    {
        // Check if expired
        if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value <= DateTime.UtcNow)
            return true;
            
        // Check memory pressure
        if (statistics.TotalMemoryBytes > _maxMemoryBytes)
        {
            // Keep high priority items unless severe pressure
            if (entry.Priority == CachePriority.NeverRemove)
                return false;
                
            if (entry.Priority == CachePriority.High && 
                statistics.TotalMemoryBytes < _maxMemoryBytes * 1.2)
                return false;
                
            return true;
        }
        
        return false;
    }
    
    public IEnumerable<CacheEntry> GetEvictionCandidates(IEnumerable<CacheEntry> entries, int targetCount)
    {
        var now = DateTime.UtcNow;
        
        // First, get all expired entries
        var expired = entries
            .Where(e => e.ExpiresAt.HasValue && e.ExpiresAt.Value <= now)
            .ToList();
            
        if (expired.Count >= targetCount)
            return expired.Take(targetCount);
            
        // Then sort by LRU with priority consideration
        var candidates = entries
            .Where(e => !expired.Contains(e) && e.Priority != CachePriority.NeverRemove)
            .OrderBy(e => GetEvictionScore(e))
            .Take(targetCount - expired.Count);
            
        return expired.Concat(candidates);
    }
    
    private double GetEvictionScore(CacheEntry entry)
    {
        var age = (DateTime.UtcNow - entry.CreatedAt).TotalMinutes;
        var lastAccess = (DateTime.UtcNow - entry.LastAccessedAt).TotalMinutes;
        var accessRate = entry.AccessCount / Math.Max(1, age);
        
        // Lower score = more likely to evict
        var score = accessRate * 1000; // Access frequency weight
        score += (1.0 / Math.Max(1, lastAccess)) * 100; // Recency weight
        score *= GetPriorityMultiplier(entry.Priority); // Priority weight
        score *= Math.Log(Math.Max(1, entry.SizeInBytes)) / Math.Log(1024); // Size penalty
        
        return score;
    }
    
    private double GetPriorityMultiplier(CachePriority priority)
    {
        return priority switch
        {
            CachePriority.Low => 0.5,
            CachePriority.Normal => 1.0,
            CachePriority.High => 2.0,
            CachePriority.NeverRemove => double.MaxValue,
            _ => 1.0
        };
    }
}

public class SizeBasedEvictionPolicy : ICacheEvictionPolicy
{
    private readonly long _maxItemSize;
    
    public SizeBasedEvictionPolicy(long maxItemSize = 10_000_000) // 10MB default
    {
        _maxItemSize = maxItemSize;
    }
    
    public bool ShouldEvict(CacheEntry entry, CacheStatistics statistics)
    {
        return entry.SizeInBytes > _maxItemSize;
    }
    
    public IEnumerable<CacheEntry> GetEvictionCandidates(IEnumerable<CacheEntry> entries, int targetCount)
    {
        // Evict largest items first
        return entries
            .Where(e => e.Priority != CachePriority.NeverRemove)
            .OrderByDescending(e => e.SizeInBytes)
            .Take(targetCount);
    }
}