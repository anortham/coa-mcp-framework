using System.Collections.Generic;
using System.Threading.Tasks;
using COA.Mcp.Framework.TokenOptimization.Models;

namespace COA.Mcp.Framework.TokenOptimization.Intelligence
{
    /// <summary>
    /// Defines the contract for generating contextual insights from data
    /// </summary>
    public interface IInsightGenerator
    {
        /// <summary>
        /// Generates insights based on the provided data and context
        /// </summary>
        /// <typeparam name="T">The type of data to analyze</typeparam>
        /// <param name="data">The data to generate insights from</param>
        /// <param name="context">The context for insight generation</param>
        /// <returns>A collection of generated insights</returns>
        Task<List<Insight>> GenerateInsightsAsync<T>(T data, InsightContext context);

        /// <summary>
        /// Generates insights with token budget constraints
        /// </summary>
        /// <typeparam name="T">The type of data to analyze</typeparam>
        /// <param name="data">The data to generate insights from</param>
        /// <param name="context">The context for insight generation</param>
        /// <param name="tokenBudget">Maximum tokens to use for insights</param>
        /// <returns>A collection of generated insights within token budget</returns>
        Task<List<Insight>> GenerateInsightsAsync<T>(T data, InsightContext context, int tokenBudget);

        /// <summary>
        /// Checks if the generator can handle a specific data type
        /// </summary>
        /// <param name="dataType">The type to check</param>
        /// <returns>True if the generator can handle the type</returns>
        bool CanHandle(System.Type dataType);
    }

    /// <summary>
    /// Context information for insight generation
    /// </summary>
    public class InsightContext
    {
        /// <summary>
        /// The operation or tool that generated the data
        /// </summary>
        public string OperationName { get; set; } = string.Empty;

        /// <summary>
        /// Additional context parameters
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// User's original query or intent
        /// </summary>
        public string? UserQuery { get; set; }

        /// <summary>
        /// Previous insights from related operations
        /// </summary>
        public List<Insight>? PreviousInsights { get; set; }

        /// <summary>
        /// Minimum number of insights to generate
        /// </summary>
        public int MinInsights { get; set; } = 3;

        /// <summary>
        /// Maximum number of insights to generate
        /// </summary>
        public int MaxInsights { get; set; } = 5;

        /// <summary>
        /// Priority level for insight generation
        /// </summary>
        public InsightPriority Priority { get; set; } = InsightPriority.Normal;
    }

    /// <summary>
    /// Priority levels for insight generation
    /// </summary>
    public enum InsightPriority
    {
        Low,
        Normal,
        High,
        Critical
    }
}