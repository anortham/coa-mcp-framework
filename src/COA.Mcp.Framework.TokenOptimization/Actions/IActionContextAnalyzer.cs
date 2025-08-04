using System.Threading.Tasks;
using COA.Mcp.Framework.TokenOptimization.Models;

namespace COA.Mcp.Framework.TokenOptimization.Actions
{
    /// <summary>
    /// Analyzes action context to understand user intent and data characteristics
    /// </summary>
    public interface IActionContextAnalyzer
    {
        /// <summary>
        /// Analyzes the context to determine user intent and data characteristics
        /// </summary>
        /// <typeparam name="T">The type of data being analyzed</typeparam>
        /// <param name="data">The data to analyze</param>
        /// <param name="context">The action context</param>
        /// <returns>Analysis results</returns>
        Task<ContextAnalysis> AnalyzeAsync<T>(T data, ActionContext context);
    }

    /// <summary>
    /// Results of context analysis
    /// </summary>
    public class ContextAnalysis
    {
        /// <summary>
        /// Detected user intent
        /// </summary>
        public UserIntent UserIntent { get; set; } = UserIntent.General;

        /// <summary>
        /// Whether the results appear to be truncated
        /// </summary>
        public bool HasTruncatedResults { get; set; }

        /// <summary>
        /// Whether errors were detected
        /// </summary>
        public bool HasErrors { get; set; }

        /// <summary>
        /// Whether performance issues were detected
        /// </summary>
        public bool HasPerformanceIssues { get; set; }

        /// <summary>
        /// Data complexity level
        /// </summary>
        public DataComplexity Complexity { get; set; } = DataComplexity.Simple;

        /// <summary>
        /// Suggested workflow type
        /// </summary>
        public string? SuggestedWorkflow { get; set; }

        /// <summary>
        /// Confidence score for the analysis (0.0 - 1.0)
        /// </summary>
        public double Confidence { get; set; } = 0.5;
    }

    /// <summary>
    /// Types of user intent
    /// </summary>
    public enum UserIntent
    {
        General,
        Explore,
        Analyze,
        Filter,
        Export,
        Debug,
        Optimize,
        Compare,
        Monitor
    }

    /// <summary>
    /// Data complexity levels
    /// </summary>
    public enum DataComplexity
    {
        Simple,
        Moderate,
        Complex,
        Hierarchical,
        Graph
    }
}