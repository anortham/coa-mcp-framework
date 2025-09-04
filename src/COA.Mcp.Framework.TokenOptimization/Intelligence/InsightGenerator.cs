using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using COA.Mcp.Framework.TokenOptimization.Models;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.TokenOptimization.Intelligence
{
    /// <summary>
    /// Base implementation for generating contextual insights from data
    /// </summary>
    public class InsightGenerator : IInsightGenerator
    {
        private readonly IInsightTemplateProvider _templateProvider;
        private readonly IInsightPrioritizer _prioritizer;
        private readonly ITokenEstimator _tokenEstimator;
        private readonly ILogger<InsightGenerator> _logger;

        public InsightGenerator(
            IInsightTemplateProvider templateProvider,
            IInsightPrioritizer prioritizer,
            ITokenEstimator tokenEstimator,
            ILogger<InsightGenerator> logger)
        {
            _templateProvider = templateProvider ?? throw new ArgumentNullException(nameof(templateProvider));
            _prioritizer = prioritizer ?? throw new ArgumentNullException(nameof(prioritizer));
            _tokenEstimator = tokenEstimator ?? throw new ArgumentNullException(nameof(tokenEstimator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<Insight>> GenerateInsightsAsync<T>(T data, InsightContext context)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (context == null) throw new ArgumentNullException(nameof(context));

            _logger.LogDebug("Generating insights for {OperationName} with data type {DataType}",
                context.OperationName, typeof(T).Name);

            try
            {
                // Get applicable templates
                var templates = await _templateProvider.GetTemplatesAsync(typeof(T), context);
                if (!templates.Any())
                {
                    _logger.LogWarning("No insight templates found for type {DataType}", typeof(T).Name);
                    return GenerateDefaultInsights(data, context);
                }

                // Generate insights from templates
                var insights = new List<Insight>();
                foreach (var template in templates)
                {
                    var insight = await template.GenerateAsync(data, context);
                    if (insight != null)
                    {
                        insights.Add(insight);
                    }
                }

                // Prioritize and filter insights
                var prioritizedInsights = await _prioritizer.PrioritizeAsync(insights, context);

                // Ensure we meet minimum insights requirement
                if (prioritizedInsights.Count < context.MinInsights)
                {
                    var additionalInsights = GenerateAdditionalInsights(data, context, 
                        context.MinInsights - prioritizedInsights.Count);
                    prioritizedInsights.AddRange(additionalInsights);
                }

                // Limit to maximum insights
                if (prioritizedInsights.Count > context.MaxInsights)
                {
                    prioritizedInsights = prioritizedInsights.Take(context.MaxInsights).ToList();
                }

                _logger.LogDebug("Generated {Count} insights for {OperationName}",
                    prioritizedInsights.Count, context.OperationName);

                return prioritizedInsights;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating insights for {OperationName}", context.OperationName);
                return GenerateErrorInsights(context);
            }
        }

        public async Task<List<Insight>> GenerateInsightsAsync<T>(T data, InsightContext context, int tokenBudget)
        {
            if (tokenBudget <= 0) throw new ArgumentException("Token budget must be positive", nameof(tokenBudget));

            var insights = await GenerateInsightsAsync(data, context);
            return ApplyTokenBudget(insights, tokenBudget);
        }

        public bool CanHandle(Type dataType)
        {
            return _templateProvider.HasTemplatesFor(dataType);
        }

        private List<Insight> GenerateDefaultInsights<T>(T data, InsightContext context)
        {
            var insights = new List<Insight>();

            // Generic insight about data size
            if (data is System.Collections.ICollection collection)
            {
                insights.Add(new Insight
                {
                    Type = InsightType.Information,
                    Text = $"Found {collection.Count} items in the result set",
                    Importance = InsightImportance.Low
                });
            }

            // Generic insight about operation
            insights.Add(new Insight
            {
                Type = InsightType.Suggestion,
                Text = $"Consider refining your {context.OperationName} parameters for more specific results",
                Importance = InsightImportance.Medium
            });

            // Ensure we meet minimum insights requirement
            while (insights.Count < context.MinInsights)
            {
                insights.Add(new Insight
                {
                    Type = InsightType.Information,
                    Text = $"Operation '{context.OperationName}' completed successfully",
                    Importance = InsightImportance.Low
                });
            }

            return insights;
        }

        private List<Insight> GenerateAdditionalInsights<T>(T data, InsightContext context, int count)
        {
            var insights = new List<Insight>();

            // Generate generic insights based on data characteristics
            if (data is System.Collections.ICollection collection && collection.Count == 0)
            {
                insights.Add(new Insight
                {
                    Type = InsightType.Warning,
                    Text = "No results found. Try adjusting your search criteria",
                    Importance = InsightImportance.High
                });
            }

            // Add context-based insights
            if (context.PreviousInsights?.Any() == true)
            {
                insights.Add(new Insight
                {
                    Type = InsightType.Information,
                    Text = "This operation builds on previous insights. Review earlier results for complete context",
                    Importance = InsightImportance.Medium
                });
            }

            return insights.Take(count).ToList();
        }

        private List<Insight> GenerateErrorInsights(InsightContext context)
        {
            return new List<Insight>
            {
                new Insight
                {
                    Type = InsightType.Warning,
                    Text = "Unable to generate detailed insights for this operation",
                    Importance = InsightImportance.Low
                }
            };
        }

        private List<Insight> ApplyTokenBudget(List<Insight> insights, int tokenBudget)
        {
            var result = new List<Insight>();
            var currentTokens = 0;

            foreach (var insight in insights.OrderByDescending(i => i.Importance))
            {
                var insightTokens = _tokenEstimator.EstimateObject(insight);
                if (currentTokens + insightTokens <= tokenBudget)
                {
                    result.Add(insight);
                    currentTokens += insightTokens;
                }
                else
                {
                    _logger.LogDebug("Stopped adding insights due to token budget. Used {Used}/{Budget} tokens",
                        currentTokens, tokenBudget);
                    break;
                }
            }

            return result;
        }
    }
}