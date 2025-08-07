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
    /// Average characters per token for CJK languages.
    /// </summary>
    private const double CJK_CHARS_PER_TOKEN = 2.0;
    
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
    /// Converts JSON structure character overhead to estimated tokens.
    /// </summary>
    /// <param name="structureChars">Number of structure characters (brackets, commas, etc.)</param>
    /// <returns>Estimated token count for the structure.</returns>
    private static int ApproxStructureTokensForJson(int structureChars)
    {
        // Convert punctuation/structure char overhead into tokens
        return (int)Math.Ceiling(structureChars / CHARS_PER_TOKEN);
    }
    
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
        
        var charCount = normalized.Length;
        var wordCount = ApproxWordCount(normalized);
        
        // Detect if text contains CJK characters or is code/URL-like (low space density)
        var useCjkRate = ContainsCjk(normalized) || IsLowSpaceDensity(normalized);
        var charsPerToken = useCjkRate ? CJK_CHARS_PER_TOKEN : CHARS_PER_TOKEN;
        
        var charBasedEstimate = (int)Math.Ceiling(charCount / charsPerToken);
        var wordBasedEstimate = (int)Math.Ceiling(wordCount * 1.3); // Words are roughly 1.3 tokens
        
        // Slightly favor char-based estimate as it generalizes better
        return (int)Math.Round(charBasedEstimate * 0.6 + wordBasedEstimate * 0.4);
    }
    
    /// <summary>
    /// Approximates word count without allocating a string array.
    /// </summary>
    private static int ApproxWordCount(string text)
    {
        int words = 0;
        bool inWord = false;
        
        foreach (var ch in text)
        {
            var isSpace = ch == ' ';
            if (!isSpace && !inWord)
            {
                inWord = true;
                words++;
            }
            else if (isSpace && inWord)
            {
                inWord = false;
            }
        }
        
        return words;
    }
    
    /// <summary>
    /// Checks if text has low space density (likely code or URLs).
    /// </summary>
    private static bool IsLowSpaceDensity(string text)
    {
        if (text.Length < 24) return false;
        
        int spaces = 0;
        foreach (var ch in text)
        {
            if (ch == ' ') spaces++;
        }
        
        return ((double)spaces / text.Length) < 0.05; // Less than 5% spaces
    }
    
    /// <summary>
    /// Detects if text contains CJK (Chinese, Japanese, Korean) characters.
    /// </summary>
    private static bool ContainsCjk(string text)
    {
        foreach (var ch in text)
        {
            var u = (int)ch;
            if ((u >= 0x4E00 && u <= 0x9FFF) || // CJK Unified Ideographs
                (u >= 0x3400 && u <= 0x4DBF) || // CJK Extension A
                (u >= 0x3040 && u <= 0x30FF) || // Hiragana and Katakana
                (u >= 0xAC00 && u <= 0xD7AF))   // Hangul Syllables
            {
                return true;
            }
        }
        
        return false;
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
            
            var type = obj.GetType();
            if (type.IsPrimitive || obj is decimal || obj is DateTime || obj is DateTimeOffset || obj is Guid)
                return EstimateString(obj.ToString());
            
            // Handle dictionaries efficiently
            if (obj is IDictionary dict)
            {
                var list = new List<KeyValuePair<object?, object?>>(dict.Count);
                foreach (DictionaryEntry entry in dict)
                {
                    list.Add(new KeyValuePair<object?, object?>(entry.Key, entry.Value));
                }
                
                return EstimateCollection(list, kv => 
                    EstimateObject(kv.Key) + EstimateObject(kv.Value));
            }
            
            // Handle collections efficiently without full serialization
            if (obj is IEnumerable enumerable && !(obj is string))
            {
                var list = enumerable.Cast<object?>().ToList();
                return EstimateCollection(list, item => EstimateObject(item));
            }
            
            // For complex objects, serialize and estimate
            options ??= DefaultJsonOptions;
            var json = JsonSerializer.Serialize(obj, options);
            return EstimateString(json) + ApproxStructureTokensForJson(16);
        }
        catch
        {
            // Fallback for objects that can't be serialized
            return EstimateString(obj.ToString()) + ApproxStructureTokensForJson(16);
        }
    }
    
    private static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
    
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
            return ApproxStructureTokensForJson(2); // "[]"
        
        itemEstimator ??= item => EstimateObject(item);
        
        // JSON array punctuation overhead: "[" + "]" + commas between items
        var structureChars = 2 + Math.Max(0, itemsList.Count - 1);
        var structureTokens = ApproxStructureTokensForJson(structureChars);
        
        // For small collections, estimate all items
        if (itemsList.Count <= sampleSize)
        {
            var total = 0;
            foreach (var item in itemsList)
            {
                total += itemEstimator(item);
            }
            return total + structureTokens;
        }
        
        // For large collections, use deterministic sampling
        var indices = GetSampleIndicesDeterministic(itemsList.Count, sampleSize);
        var sampleSum = 0;
        foreach (var i in indices)
        {
            sampleSum += itemEstimator(itemsList[i]);
        }
        
        var averageTokensPerItem = (double)sampleSum / indices.Count;
        var estimatedItemsTokens = (int)Math.Round(averageTokensPerItem * itemsList.Count);
        
        return estimatedItemsTokens + structureTokens;
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
    /// Calculates token budget using a percentage-based safety buffer.
    /// This overload adapts better to different model sizes.
    /// </summary>
    /// <param name="totalLimit">Total token limit (e.g., model context window).</param>
    /// <param name="currentUsage">Current token usage.</param>
    /// <param name="safetyPercent">Safety buffer as percentage of total limit (default: 5%).</param>
    /// <param name="minAbsoluteBuffer">Minimum absolute buffer in tokens (default: 1000).</param>
    /// <param name="maxAbsoluteBuffer">Maximum absolute buffer in tokens (default: 10000).</param>
    /// <returns>Available token budget.</returns>
    public static int CalculateTokenBudget(
        int totalLimit,
        int currentUsage,
        double? safetyPercent = 0.05,
        int? minAbsoluteBuffer = 1000,
        int? maxAbsoluteBuffer = 10000)
    {
        // Clamp percentage to reasonable range (0-50%)
        var pct = Math.Clamp(safetyPercent ?? 0.05, 0.0, 0.5);
        
        // Calculate buffer based on percentage of total limit
        var bufferFromPercent = (int)Math.Ceiling(totalLimit * pct);
        
        // Apply min/max constraints
        var buffer = bufferFromPercent;
        if (minAbsoluteBuffer.HasValue)
            buffer = Math.Max(buffer, minAbsoluteBuffer.Value);
        if (maxAbsoluteBuffer.HasValue)
            buffer = Math.Min(buffer, maxAbsoluteBuffer.Value);
        
        var availableTokens = totalLimit - currentUsage - buffer;
        return Math.Max(0, availableTokens);
    }
    
    /// <summary>
    /// Gets sample indices for collection estimation using deterministic, even-coverage sampling.
    /// Ensures first and last elements are included and provides uniform distribution.
    /// </summary>
    private static List<int> GetSampleIndicesDeterministic(int collectionSize, int sampleSize)
    {
        if (collectionSize <= sampleSize)
            return Enumerable.Range(0, collectionSize).ToList();
        
        var result = new HashSet<int>();
        var step = (double)collectionSize / sampleSize;
        
        // Take evenly spaced samples from the middle of each bucket
        for (int i = 0; i < sampleSize; i++)
        {
            var idx = (int)Math.Floor(i * step + step / 2.0);
            if (idx >= collectionSize)
                idx = collectionSize - 1;
            result.Add(idx);
        }
        
        // Ensure we always include first and last elements for better coverage
        result.Add(0);
        result.Add(collectionSize - 1);
        
        // Return sorted indices, limited to requested sample size
        return result.OrderBy(x => x).Take(sampleSize).ToList();
    }
    
    /// <summary>
    /// Gets sample indices for collection estimation using stratified sampling.
    /// Kept for backward compatibility if needed.
    /// </summary>
    private static List<int> GetSampleIndices(int collectionSize, int sampleSize)
    {
        return GetSampleIndicesDeterministic(collectionSize, sampleSize);
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