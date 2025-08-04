using System.Collections.Generic;
using System.Threading.Tasks;
using COA.Mcp.Framework.TokenOptimization.Models;

namespace COA.Mcp.Framework.TokenOptimization.Intelligence
{
    /// <summary>
    /// Prioritizes and filters insights based on relevance and importance
    /// </summary>
    public interface IInsightPrioritizer
    {
        /// <summary>
        /// Prioritizes insights based on context and importance
        /// </summary>
        /// <param name="insights">The insights to prioritize</param>
        /// <param name="context">The context for prioritization</param>
        /// <returns>Prioritized list of insights</returns>
        Task<List<Insight>> PrioritizeAsync(List<Insight> insights, InsightContext context);

        /// <summary>
        /// Scores an insight based on various factors
        /// </summary>
        /// <param name="insight">The insight to score</param>
        /// <param name="context">The context for scoring</param>
        /// <returns>Score between 0.0 and 1.0</returns>
        double ScoreInsight(Insight insight, InsightContext context);
    }
}