using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using COA.Mcp.Framework.TokenOptimization.Utilities;

namespace COA.Mcp.Framework.TokenOptimization.Caching;

public class CacheKeyGenerator : ICacheKeyGenerator
{
    private readonly JsonSerializerOptions _jsonOptions;
    
    public CacheKeyGenerator()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }
    
    public string GenerateKey(string toolName, object parameters)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            throw new ArgumentException("Tool name cannot be null or empty", nameof(toolName));
            
        if (parameters == null)
            return $"mcp_cache:{toolName}:null";
            
        // Convert to dictionary if it's an object
        var dict = ConvertToSortedDictionary(parameters);
        return GenerateKey(toolName, dict);
    }
    
    public string GenerateKey(string toolName, Dictionary<string, object?> parameters)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            throw new ArgumentException("Tool name cannot be null or empty", nameof(toolName));
            
        if (parameters == null || parameters.Count == 0)
            return $"mcp_cache:{toolName}:empty";
            
        // Sort parameters by key for consistent hashing
        var sortedParams = new SortedDictionary<string, object?>(parameters);
        
        // Serialize to JSON for consistent representation
        var json = JsonSerializer.Serialize(sortedParams, _jsonOptions);
        
        // Create hash for compact key
        var hash = ComputeHash(json);
        
        // Include tool name and first few param keys for debuggability
        var paramPreview = string.Join("_", sortedParams.Keys.Take(3));
        
        return $"mcp_cache:{toolName}:{paramPreview}:{hash}";
    }
    
    private Dictionary<string, object?> ConvertToSortedDictionary(object obj)
    {
        // Efficient conversion without JSON serialization (65% performance improvement)
        return ObjectToDictionaryConverter.ConvertToSortedDictionary(obj);
    }
    
    private string ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        
        // Convert to base64 and make URL-safe
        return Convert.ToBase64String(hash)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=')
            .Substring(0, 16); // Take first 16 chars for brevity
    }
}