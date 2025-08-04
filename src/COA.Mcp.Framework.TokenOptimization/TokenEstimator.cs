using System.Collections;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace COA.Mcp.Framework.TokenOptimization;

/// <summary>
/// Provides token estimation functionality for strings, objects, and collections.
/// Based on best practices from CodeNav and CodeSearch implementations.
/// </summary>
public static class TokenEstimator
{
    /// <summary>
    /// Default safety limit (5% of typical context window).
    /// </summary>
    public const int DEFAULT_SAFETY_LIMIT = 10000;
    
    /// <summary>
    /// Conservative safety limit for more restrictive scenarios.
    /// </summary>
    public const int CONSERVATIVE_SAFETY_LIMIT = 5000;
    
    /// <summary>
    /// Minimum safety limit to prevent excessive truncation.
    /// </summary>
    public const int MINIMUM_SAFETY_LIMIT = 1000;
    
    /// <summary>
    /// Average characters per token (based on empirical data).
    /// </summary>
    private const double CHARS_PER_TOKEN = 4.0;
    
    /// <summary>
    /// Token overhead for JSON structure (brackets, quotes, etc.).
    /// </summary>
    private const int JSON_STRUCTURE_OVERHEAD = 50;
    
    /// <summary>
    /// Maximum sample size for collection estimation.
    /// </summary>
    private const int MAX_SAMPLE_SIZE = 10;
    
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    
    /// <summary>
    /// Estimates the number of tokens in a string.
    /// </summary>
    /// <param name="text">The text to estimate.</param>
    /// <returns>Estimated token count.</returns>
    public static int EstimateString(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
        
        // Normalize whitespace for more accurate estimation
        var normalized = WhitespaceRegex.Replace(text, " ");
        
        // Calculate based on character count and average token length
        var charCount = normalized.Length;
        var wordCount = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        
        // Use weighted average of character and word-based estimates
        var charBasedEstimate = (int)Math.Ceiling(charCount / CHARS_PER_TOKEN);
        var wordBasedEstimate = (int)(wordCount * 1.3); // Words are roughly 1.3 tokens
        
        return (charBasedEstimate + wordBasedEstimate) / 2;
    }
    
    /// <summary>
    /// Estimates the number of tokens for an object when serialized to JSON.
    /// </summary>
    /// <param name="obj">The object to estimate.</param>
    /// <param name="options">JSON serialization options.</param>
    /// <returns>Estimated token count.</returns>
    public static int EstimateObject(object? obj, JsonSerializerOptions? options = null)
    {
        if (obj == null)
            return 0;
        
        try
        {
            // For simple types, use direct estimation
            if (obj is string str)
                return EstimateString(str);
            
            if (obj.GetType().IsPrimitive || obj is decimal || obj is DateTime || obj is DateTimeOffset || obj is Guid)
                return EstimateString(obj.ToString());
            
            // For complex objects, serialize and estimate
            options ??= new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            
            var json = JsonSerializer.Serialize(obj, options);
            return EstimateString(json) + JSON_STRUCTURE_OVERHEAD;
        }
        catch
        {
            // Fallback for objects that can't be serialized
            return EstimateString(obj.ToString()) + JSON_STRUCTURE_OVERHEAD;
        }
    }
    
    /// <summary>
    /// Estimates the number of tokens for a collection using sampling.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <param name="items">The collection to estimate.</param>
    /// <param name="itemEstimator">Function to estimate tokens for a single item.</param>
    /// <param name="sampleSize">Number of items to sample for estimation.</param>
    /// <returns>Estimated token count for the entire collection.</returns>
    public static int EstimateCollection<T>(
        IEnumerable<T>? items,
        Func<T, int>? itemEstimator = null,
        int sampleSize = MAX_SAMPLE_SIZE)
    {
        if (items == null)
            return 0;
        
        var itemsList = items as IList<T> ?? items.ToList();
        if (itemsList.Count == 0)
            return JSON_STRUCTURE_OVERHEAD; // Empty array
        
        itemEstimator ??= item => EstimateObject(item);
        
        // For small collections, estimate all items
        if (itemsList.Count <= sampleSize)
        {
            var total = itemsList.Sum(itemEstimator);
            return total + JSON_STRUCTURE_OVERHEAD + (itemsList.Count * 5); // Commas and spacing
        }
        
        // For large collections, use sampling
        var sampleIndices = GetSampleIndices(itemsList.Count, sampleSize);
        var sampleSum = sampleIndices.Sum(i => itemEstimator(itemsList[i]));
        var averageTokensPerItem = sampleSum / sampleSize;
        
        // Extrapolate to full collection with overhead
        var estimatedTotal = averageTokensPerItem * itemsList.Count;
        var structureOverhead = JSON_STRUCTURE_OVERHEAD + (itemsList.Count * 5);
        
        return estimatedTotal + structureOverhead;
    }
    
    /// <summary>
    /// Applies progressive reduction to a collection based on token limits.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <param name="items">The collection to reduce.</param>
    /// <param name="itemEstimator">Function to estimate tokens for a single item.</param>
    /// <param name="tokenLimit">Maximum allowed tokens.</param>
    /// <param name="reductionSteps">Percentage steps for reduction.</param>
    /// <returns>Reduced collection that fits within token limit.</returns>
    public static List<T> ApplyProgressiveReduction<T>(
        IEnumerable<T> items,
        Func<T, int> itemEstimator,
        int tokenLimit,
        int[]? reductionSteps = null)
    {
        var itemsList = items as IList<T> ?? items.ToList();
        if (itemsList.Count == 0)
            return new List<T>();
        
        reductionSteps ??= new[] { 100, 75, 50, 30, 20, 10, 5 };
        
        foreach (var percentage in reductionSteps)
        {
            var count = Math.Max(1, (itemsList.Count * percentage) / 100);
            var subset = itemsList.Take(count).ToList();
            var estimatedTokens = EstimateCollection(subset, itemEstimator);
            
            if (estimatedTokens <= tokenLimit)
                return subset;
        }
        
        // If even the smallest reduction doesn't fit, return just one item
        return itemsList.Take(1).ToList();
    }
    
    /// <summary>
    /// Calculates token budget based on safety limits and current usage.
    /// </summary>
    /// <param name="totalLimit">Total token limit (e.g., model context window).</param>
    /// <param name="currentUsage">Current token usage.</param>
    /// <param name="safetyMode">Safety mode to apply.</param>
    /// <returns>Available token budget.</returns>
    public static int CalculateTokenBudget(
        int totalLimit,
        int currentUsage,
        TokenSafetyMode safetyMode = TokenSafetyMode.Default)
    {
        var safetyLimit = safetyMode switch
        {
            TokenSafetyMode.Conservative => CONSERVATIVE_SAFETY_LIMIT,
            TokenSafetyMode.Minimal => MINIMUM_SAFETY_LIMIT,
            _ => DEFAULT_SAFETY_LIMIT
        };
        
        var availableTokens = totalLimit - currentUsage - safetyLimit;
        return Math.Max(0, availableTokens);
    }
    
    /// <summary>
    /// Gets sample indices for collection estimation using stratified sampling.
    /// </summary>
    private static List<int> GetSampleIndices(int collectionSize, int sampleSize)
    {
        if (collectionSize <= sampleSize)
            return Enumerable.Range(0, collectionSize).ToList();
        
        var indices = new List<int>();
        var step = collectionSize / sampleSize;
        
        // Stratified sampling - take evenly distributed samples
        for (int i = 0; i < sampleSize; i++)
        {
            var index = i * step + (step / 2); // Middle of each stratum
            if (index < collectionSize)
                indices.Add(index);
        }
        
        return indices;
    }
}

/// <summary>
/// Token safety modes for budget calculation.
/// </summary>
public enum TokenSafetyMode
{
    /// <summary>
    /// Default safety mode (10,000 token buffer).
    /// </summary>
    Default,
    
    /// <summary>
    /// Conservative safety mode (5,000 token buffer).
    /// </summary>
    Conservative,
    
    /// <summary>
    /// Minimal safety mode (1,000 token buffer).
    /// </summary>
    Minimal
}