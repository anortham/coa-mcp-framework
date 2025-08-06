using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using COA.Mcp.Framework.TokenOptimization.Models;
using COA.Mcp.Framework.Models;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.TokenOptimization.Actions
{
    /// <summary>
    /// Base implementation for generating suggested actions
    /// </summary>
    public class ActionGenerator : IActionGenerator
    {
        private readonly IActionTemplateProvider _templateProvider;
        private readonly INextActionProvider _nextActionProvider;
        private readonly ITokenEstimator _tokenEstimator;
        private readonly ILogger<ActionGenerator> _logger;

        public ActionGenerator(
            IActionTemplateProvider templateProvider,
            INextActionProvider nextActionProvider,
            ITokenEstimator tokenEstimator,
            ILogger<ActionGenerator> logger)
        {
            _templateProvider = templateProvider ?? throw new ArgumentNullException(nameof(templateProvider));
            _nextActionProvider = nextActionProvider ?? throw new ArgumentNullException(nameof(nextActionProvider));
            _tokenEstimator = tokenEstimator ?? throw new ArgumentNullException(nameof(tokenEstimator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<AIAction>> GenerateActionsAsync<T>(T data, ActionContext context)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (context == null) throw new ArgumentNullException(nameof(context));

            _logger.LogDebug("Generating actions for {OperationName} with data type {DataType}",
                context.OperationName, typeof(T).Name);

            try
            {
                var actions = new List<AIAction>();

                // Get template-based actions
                var templateActions = await GenerateTemplateActions(data, context);
                actions.AddRange(templateActions);

                // Get next action suggestions
                var nextActions = await _nextActionProvider.GetNextActionsAsync(data, context);
                actions.AddRange(nextActions);

                // Remove duplicates and prioritize
                actions = PrioritizeAndDeduplicate(actions, context);

                // Limit to max actions
                if (actions.Count > context.MaxActions)
                {
                    actions = actions.Take(context.MaxActions).ToList();
                }

                _logger.LogDebug("Generated {Count} actions for {OperationName}",
                    actions.Count, context.OperationName);

                return actions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating actions for {OperationName}", context.OperationName);
                return GenerateErrorActions(context);
            }
        }

        public async Task<List<AIAction>> GenerateActionsAsync<T>(T data, ActionContext context, int tokenBudget)
        {
            if (tokenBudget <= 0) throw new ArgumentException("Token budget must be positive", nameof(tokenBudget));

            var actions = await GenerateActionsAsync(data, context);
            return ApplyTokenBudget(actions, tokenBudget);
        }

        public bool CanHandle(Type dataType)
        {
            return _templateProvider.HasTemplatesFor(dataType) || 
                   _nextActionProvider.CanProvideActionsFor(dataType);
        }

        private async Task<List<AIAction>> GenerateTemplateActions<T>(T data, ActionContext context)
        {
            var templates = await _templateProvider.GetTemplatesAsync(typeof(T), context);
            var actions = new List<AIAction>();

            foreach (var template in templates)
            {
                var action = await template.GenerateAsync(data!, context);
                if (action != null)
                {
                    actions.Add(action);
                }
            }

            return actions;
        }

        private List<AIAction> PrioritizeAndDeduplicate(List<AIAction> actions, ActionContext context)
        {
            // Remove exact duplicates by tool name
            var deduped = actions
                .GroupBy(a => a.Tool)
                .Select(g => g.First())
                .ToList();

            // Score and sort actions
            var scored = deduped
                .Select(action => new
                {
                    Action = action,
                    Score = ScoreAction(action, context)
                })
                .OrderByDescending(x => x.Score)
                .Select(x => x.Action)
                .ToList();

            return scored;
        }

        private double ScoreAction(AIAction action, ActionContext context)
        {
            double score = 0.0;

            // Priority based on context
            if (context.Priority == ActionPriority.Critical && action.Category == "fix")
            {
                score += 1.0;
            }

            // Relevance to insights
            if (context.RelatedInsights?.Any() == true)
            {
                var insightTypes = context.RelatedInsights.Select(i => i.Type).Distinct();
                if (insightTypes.Contains(InsightType.Error) && action.Category == "fix")
                {
                    score += 0.8;
                }
                else if (insightTypes.Contains(InsightType.Performance) && action.Category == "optimize")
                {
                    score += 0.7;
                }
            }

            // Avoid repeating recent actions
            if (context.PreviousActions?.Contains(action.Tool) == true)
            {
                score -= 0.3;
            }

            // Parameterized actions score
            if (!context.IncludeParameterizedActions && action.Parameters?.Any() == true)
            {
                score -= 0.5;
            }

            // Default category scores
            score += action.Category switch
            {
                "analyze" => 0.5,
                "filter" => 0.4,
                "export" => 0.3,
                "navigate" => 0.6,
                _ => 0.2
            };

            return Math.Max(0.0, score);
        }

        private List<AIAction> GenerateErrorActions(ActionContext context)
        {
            return new List<AIAction>
            {
                new AIAction
                {
                    Tool = "retry_operation",
                    Description = $"Retry the {context.OperationName} operation",
                    Rationale = "The previous operation encountered an error",
                    Category = "fix"
                }
            };
        }

        private List<AIAction> ApplyTokenBudget(List<AIAction> actions, int tokenBudget)
        {
            var result = new List<AIAction>();
            var currentTokens = 0;

            foreach (var action in actions)
            {
                var actionTokens = _tokenEstimator.EstimateObject(action);
                if (currentTokens + actionTokens <= tokenBudget)
                {
                    result.Add(action);
                    currentTokens += actionTokens;
                }
                else
                {
                    _logger.LogDebug("Stopped adding actions due to token budget. Used {Used}/{Budget} tokens",
                        currentTokens, tokenBudget);
                    break;
                }
            }

            return result;
        }
    }
}