using System.Collections.Generic;
using System.Threading.Tasks;
using COA.Mcp.Framework.TokenOptimization.Models;
using COA.Mcp.Framework.Models;

namespace COA.Mcp.Framework.TokenOptimization.Actions
{
    /// <summary>
    /// Defines the contract for generating suggested actions based on results
    /// </summary>
    public interface IActionGenerator
    {
        /// <summary>
        /// Generates suggested actions based on data and context
        /// </summary>
        /// <typeparam name="T">The type of data to analyze</typeparam>
        /// <param name="data">The data to generate actions from</param>
        /// <param name="context">The context for action generation</param>
        /// <returns>A collection of suggested actions</returns>
        Task<List<AIAction>> GenerateActionsAsync<T>(T data, ActionContext context);

        /// <summary>
        /// Generates actions with token budget constraints
        /// </summary>
        /// <typeparam name="T">The type of data to analyze</typeparam>
        /// <param name="data">The data to generate actions from</param>
        /// <param name="context">The context for action generation</param>
        /// <param name="tokenBudget">Maximum tokens to use for actions</param>
        /// <returns>A collection of suggested actions within token budget</returns>
        Task<List<AIAction>> GenerateActionsAsync<T>(T data, ActionContext context, int tokenBudget);

        /// <summary>
        /// Checks if the generator can handle a specific data type
        /// </summary>
        /// <param name="dataType">The type to check</param>
        /// <returns>True if the generator can handle the type</returns>
        bool CanHandle(System.Type dataType);
    }

    /// <summary>
    /// Context information for action generation
    /// </summary>
    public class ActionContext
    {
        /// <summary>
        /// The operation that generated the data
        /// </summary>
        public string OperationName { get; set; } = string.Empty;

        /// <summary>
        /// Insights generated for the data
        /// </summary>
        public List<Insight>? RelatedInsights { get; set; }

        /// <summary>
        /// User's original intent or query
        /// </summary>
        public string? UserIntent { get; set; }

        /// <summary>
        /// Previous actions taken in the session
        /// </summary>
        public List<string>? PreviousActions { get; set; }

        /// <summary>
        /// Additional context parameters
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Maximum number of actions to suggest
        /// </summary>
        public int MaxActions { get; set; } = 5;

        /// <summary>
        /// Priority level for action generation
        /// </summary>
        public ActionPriority Priority { get; set; } = ActionPriority.Normal;

        /// <summary>
        /// Whether to include actions that require additional parameters
        /// </summary>
        public bool IncludeParameterizedActions { get; set; } = true;
    }

    /// <summary>
    /// Priority levels for action generation
    /// </summary>
    public enum ActionPriority
    {
        Low,
        Normal,
        High,
        Critical
    }
}