using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using COA.Mcp.Framework.TokenOptimization.Models;
using COA.Mcp.Framework.Models;

namespace COA.Mcp.Framework.TokenOptimization.Actions
{
    /// <summary>
    /// Provides intelligent next action suggestions based on current context
    /// </summary>
    public interface INextActionProvider
    {
        /// <summary>
        /// Gets next action suggestions based on data and context
        /// </summary>
        /// <typeparam name="T">The type of data to analyze</typeparam>
        /// <param name="data">The current data</param>
        /// <param name="context">The action context</param>
        /// <returns>A collection of suggested next actions</returns>
        Task<List<AIAction>> GetNextActionsAsync<T>(T data, ActionContext context);

        /// <summary>
        /// Checks if the provider can generate actions for a data type
        /// </summary>
        /// <param name="dataType">The type to check</param>
        /// <returns>True if actions can be provided</returns>
        bool CanProvideActionsFor(Type dataType);

        /// <summary>
        /// Registers a workflow pattern
        /// </summary>
        /// <param name="pattern">The workflow pattern to register</param>
        void RegisterWorkflowPattern(IWorkflowPattern pattern);
    }

    /// <summary>
    /// Defines a workflow pattern for generating next actions
    /// </summary>
    public interface IWorkflowPattern
    {
        /// <summary>
        /// Name of the workflow pattern
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Operations this pattern applies to
        /// </summary>
        string[] ApplicableOperations { get; }

        /// <summary>
        /// Gets next actions based on current state
        /// </summary>
        /// <param name="currentOperation">The current operation</param>
        /// <param name="context">The action context</param>
        /// <returns>Suggested next actions</returns>
        Task<List<AIAction>> GetNextActionsAsync(string currentOperation, ActionContext context);

        /// <summary>
        /// Checks if the pattern is applicable
        /// </summary>
        /// <param name="operation">The current operation</param>
        /// <param name="context">The action context</param>
        /// <returns>True if applicable</returns>
        bool IsApplicable(string operation, ActionContext context);
    }
}