using System.Text.Json;

namespace COA.Mcp.Framework.TokenOptimization
{
    /// <summary>
    /// Interface for token estimation services.
    /// </summary>
    public interface ITokenEstimator
    {
        /// <summary>
        /// Estimates the number of tokens in a string.
        /// </summary>
        /// <param name="text">The text to estimate.</param>
        /// <returns>Estimated token count.</returns>
        int EstimateString(string? text);

        /// <summary>
        /// Estimates the number of tokens for an object when serialized to JSON.
        /// </summary>
        /// <param name="obj">The object to estimate.</param>
        /// <param name="options">JSON serialization options.</param>
        /// <returns>Estimated token count.</returns>
        int EstimateObject(object? obj, JsonSerializerOptions? options = null);

        /// <summary>
        /// Estimates the number of tokens for a collection using sampling.
        /// </summary>
        /// <typeparam name="T">The type of items in the collection.</typeparam>
        /// <param name="items">The collection to estimate.</param>
        /// <param name="itemEstimator">Function to estimate tokens for a single item.</param>
        /// <param name="sampleSize">Number of items to sample for estimation.</param>
        /// <returns>Estimated token count for the entire collection.</returns>
        int EstimateCollection<T>(
            IEnumerable<T>? items,
            Func<T, int>? itemEstimator = null,
            int sampleSize = 10);

        /// <summary>
        /// Applies progressive reduction to a collection based on token limits.
        /// </summary>
        /// <typeparam name="T">The type of items in the collection.</typeparam>
        /// <param name="items">The collection to reduce.</param>
        /// <param name="itemEstimator">Function to estimate tokens for a single item.</param>
        /// <param name="tokenLimit">Maximum allowed tokens.</param>
        /// <param name="reductionSteps">Percentage steps for reduction.</param>
        /// <returns>Reduced collection that fits within token limit.</returns>
        List<T> ApplyProgressiveReduction<T>(
            IEnumerable<T> items,
            Func<T, int> itemEstimator,
            int tokenLimit,
            int[]? reductionSteps = null);

        /// <summary>
        /// Calculates token budget based on safety limits and current usage.
        /// </summary>
        /// <param name="totalLimit">Total token limit (e.g., model context window).</param>
        /// <param name="currentUsage">Current token usage.</param>
        /// <param name="safetyMode">Safety mode to apply.</param>
        /// <returns>Available token budget.</returns>
        int CalculateTokenBudget(
            int totalLimit,
            int currentUsage,
            TokenSafetyMode safetyMode = TokenSafetyMode.Default);
    }

    /// <summary>
    /// Default implementation of ITokenEstimator using the static TokenEstimator class.
    /// </summary>
    public class DefaultTokenEstimator : ITokenEstimator
    {
        public int EstimateString(string? text)
        {
            return TokenEstimator.EstimateString(text);
        }

        public int EstimateObject(object? obj, JsonSerializerOptions? options = null)
        {
            return TokenEstimator.EstimateObject(obj, options);
        }

        public int EstimateCollection<T>(IEnumerable<T>? items, Func<T, int>? itemEstimator = null, int sampleSize = 10)
        {
            return TokenEstimator.EstimateCollection(items, itemEstimator, sampleSize);
        }

        public List<T> ApplyProgressiveReduction<T>(IEnumerable<T> items, Func<T, int> itemEstimator, int tokenLimit, int[]? reductionSteps = null)
        {
            return TokenEstimator.ApplyProgressiveReduction(items, itemEstimator, tokenLimit, reductionSteps);
        }

        public int CalculateTokenBudget(int totalLimit, int currentUsage, TokenSafetyMode safetyMode = TokenSafetyMode.Default)
        {
            return TokenEstimator.CalculateTokenBudget(totalLimit, currentUsage, safetyMode);
        }
    }
}