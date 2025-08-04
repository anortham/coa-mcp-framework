using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using COA.Mcp.Framework.TokenOptimization.Models;

namespace COA.Mcp.Framework.TokenOptimization.Actions
{
    /// <summary>
    /// Provides action templates for different data types and contexts
    /// </summary>
    public interface IActionTemplateProvider
    {
        /// <summary>
        /// Gets applicable templates for a data type and context
        /// </summary>
        /// <param name="dataType">The type of data to generate actions for</param>
        /// <param name="context">The context for action generation</param>
        /// <returns>A collection of applicable action templates</returns>
        Task<IEnumerable<IActionTemplate>> GetTemplatesAsync(Type dataType, ActionContext context);

        /// <summary>
        /// Checks if templates exist for a data type
        /// </summary>
        /// <param name="dataType">The type to check</param>
        /// <returns>True if templates exist</returns>
        bool HasTemplatesFor(Type dataType);

        /// <summary>
        /// Registers a new template
        /// </summary>
        /// <param name="template">The template to register</param>
        void RegisterTemplate(IActionTemplate template);

        /// <summary>
        /// Registers templates for a specific data type
        /// </summary>
        /// <param name="dataType">The data type</param>
        /// <param name="templates">Templates to register</param>
        void RegisterTemplates(Type dataType, params IActionTemplate[] templates);
    }

    /// <summary>
    /// Defines a template for generating actions
    /// </summary>
    public interface IActionTemplate
    {
        /// <summary>
        /// Name of the template
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Data types this template can handle
        /// </summary>
        Type[] SupportedTypes { get; }

        /// <summary>
        /// Priority of this template
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Generates an action from data
        /// </summary>
        /// <param name="data">The data to analyze</param>
        /// <param name="context">The generation context</param>
        /// <returns>Generated action or null if not applicable</returns>
        Task<AIAction?> GenerateAsync(object data, ActionContext context);

        /// <summary>
        /// Checks if the template is applicable to the context
        /// </summary>
        /// <param name="context">The context to check</param>
        /// <returns>True if applicable</returns>
        bool IsApplicable(ActionContext context);
    }
}