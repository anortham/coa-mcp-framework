using System.Text.Json;
using COA.Mcp.Framework.Interfaces;

namespace COA.Mcp.Framework.TokenOptimization
{
    /// <summary>
    /// Default implementation of ITokenEstimator using the static TokenEstimator class.
    /// </summary>
    public class DefaultTokenEstimator : ITokenEstimator
    {
        public int EstimateString(string? text) => TokenEstimator.EstimateString(text);

        public int EstimateObject(object? obj, JsonSerializerOptions? options = null) => TokenEstimator.EstimateObject(obj, options);

        public int EstimateCollection<T>(IEnumerable<T>? items, Func<T, int>? itemEstimator = null, int sampleSize = 10)
            => TokenEstimator.EstimateCollection(items, itemEstimator, sampleSize);

        public List<T> ApplyProgressiveReduction<T>(IEnumerable<T> items, Func<T, int> itemEstimator, int tokenLimit, int[]? reductionSteps = null)
            => TokenEstimator.ApplyProgressiveReduction(items, itemEstimator, tokenLimit, reductionSteps);

        public int CalculateTokenBudget(int totalLimit, int currentUsage, COA.Mcp.Framework.Interfaces.TokenSafetyMode safetyMode = COA.Mcp.Framework.Interfaces.TokenSafetyMode.Default)
        {
            var mapped = safetyMode switch
            {
                COA.Mcp.Framework.Interfaces.TokenSafetyMode.Conservative => COA.Mcp.Framework.TokenOptimization.TokenSafetyMode.Conservative,
                COA.Mcp.Framework.Interfaces.TokenSafetyMode.Minimal => COA.Mcp.Framework.TokenOptimization.TokenSafetyMode.Minimal,
                _ => COA.Mcp.Framework.TokenOptimization.TokenSafetyMode.Default
            };
            return TokenEstimator.CalculateTokenBudget(totalLimit, currentUsage, mapped);
        }

        public int CalculateTokenBudget(int totalLimit, int currentUsage, double? safetyPercent = 0.05, int? minAbsoluteBuffer = 1000, int? maxAbsoluteBuffer = 10000)
            => TokenEstimator.CalculateTokenBudget(totalLimit, currentUsage, safetyPercent, minAbsoluteBuffer, maxAbsoluteBuffer);
    }
}
