using System.Text.Json;

namespace COA.Mcp.Framework.Interfaces
{
    /// <summary>
    /// Interface for token estimation services. Implemented by optional packages.
    /// </summary>
    public interface ITokenEstimator
    {
        int EstimateString(string? text);
        int EstimateObject(object? obj, JsonSerializerOptions? options = null);
        int EstimateCollection<T>(IEnumerable<T>? items, Func<T, int>? itemEstimator = null, int sampleSize = 10);
        List<T> ApplyProgressiveReduction<T>(IEnumerable<T> items, Func<T, int> itemEstimator, int tokenLimit, int[]? reductionSteps = null);
        int CalculateTokenBudget(int totalLimit, int currentUsage, TokenSafetyMode safetyMode = TokenSafetyMode.Default);
        int CalculateTokenBudget(int totalLimit, int currentUsage, double? safetyPercent = 0.05, int? minAbsoluteBuffer = 1000, int? maxAbsoluteBuffer = 10000);
    }

    /// <summary>
    /// Token safety modes for budget calculation.
    /// </summary>
    public enum TokenSafetyMode
    {
        Default,
        Conservative,
        Minimal
    }
}

