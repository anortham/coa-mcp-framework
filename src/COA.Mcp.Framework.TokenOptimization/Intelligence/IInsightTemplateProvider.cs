using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using COA.Mcp.Framework.TokenOptimization.Models;

namespace COA.Mcp.Framework.TokenOptimization.Intelligence
{
    /// <summary>
    /// Provides insight templates for different data types and contexts
    /// </summary>
    public interface IInsightTemplateProvider
    {
        /// <summary>
        /// Gets applicable templates for a data type and context
        /// </summary>
        /// <param name="dataType">The type of data to generate insights for</param>
        /// <param name="context">The context for insight generation</param>
        /// <returns>A collection of applicable insight templates</returns>
        Task<IEnumerable<IInsightTemplate>> GetTemplatesAsync(Type dataType, InsightContext context);

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
        void RegisterTemplate(IInsightTemplate template);

        /// <summary>
        /// Registers templates for a specific data type
        /// </summary>
        /// <param name="dataType">The data type</param>
        /// <param name="templates">Templates to register</param>
        void RegisterTemplates(Type dataType, params IInsightTemplate[] templates);
    }

    /// <summary>
    /// Defines a template for generating insights
    /// </summary>
    public interface IInsightTemplate
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
        /// Generates an insight from data
        /// </summary>
        /// <param name="data">The data to analyze</param>
        /// <param name="context">The generation context</param>
        /// <returns>Generated insight or null if not applicable</returns>
        Task<Insight?> GenerateAsync(object data, InsightContext context);

        /// <summary>
        /// Checks if the template is applicable to the context
        /// </summary>
        /// <param name="context">The context to check</param>
        /// <returns>True if applicable</returns>
        bool IsApplicable(InsightContext context);
    }
}