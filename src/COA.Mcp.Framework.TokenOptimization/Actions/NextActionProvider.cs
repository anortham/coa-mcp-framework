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
    /// Default implementation for providing next action suggestions
    /// </summary>
    public class NextActionProvider : INextActionProvider
    {
        private readonly List<IWorkflowPattern> _workflowPatterns;
        private readonly IActionContextAnalyzer _contextAnalyzer;
        private readonly ILogger<NextActionProvider> _logger;
        private readonly object _lock = new object();

        public NextActionProvider(
            IActionContextAnalyzer contextAnalyzer,
            ILogger<NextActionProvider> logger)
        {
            _contextAnalyzer = contextAnalyzer ?? throw new ArgumentNullException(nameof(contextAnalyzer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _workflowPatterns = new List<IWorkflowPattern>();

            RegisterDefaultPatterns();
        }

        public async Task<List<AIAction>> GetNextActionsAsync<T>(T data, ActionContext context)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (context == null) throw new ArgumentNullException(nameof(context));

            var actions = new List<AIAction>();

            // Analyze context to understand user intent
            var analysis = await _contextAnalyzer.AnalyzeAsync(data, context);

            // Get workflow-based actions
            var workflowActions = await GetWorkflowActionsAsync(context, analysis);
            actions.AddRange(workflowActions);

            // Get intent-based actions
            var intentActions = GetIntentBasedActions(analysis, context);
            actions.AddRange(intentActions);

            // Get data-driven actions
            var dataActions = GetDataDrivenActions(data, analysis, context);
            actions.AddRange(dataActions);

            // Remove duplicates and return
            return actions.GroupBy(a => a.Tool).Select(g => g.First()).ToList();
        }

        public bool CanProvideActionsFor(Type dataType)
        {
            // We can provide actions for any type through context analysis
            return true;
        }

        public void RegisterWorkflowPattern(IWorkflowPattern pattern)
        {
            if (pattern == null) throw new ArgumentNullException(nameof(pattern));

            lock (_lock)
            {
                if (!_workflowPatterns.Any(p => p.Name == pattern.Name))
                {
                    _workflowPatterns.Add(pattern);
                    _logger.LogDebug("Registered workflow pattern: {Name}", pattern.Name);
                }
            }
        }

        private Task<List<AIAction>> GetWorkflowActionsAsync(ActionContext context, ContextAnalysis analysis)
        {
            var actions = new List<AIAction>();

            lock (_lock)
            {
                var applicablePatterns = _workflowPatterns
                    .Where(p => p.IsApplicable(context.OperationName, context))
                    .ToList();

                foreach (var pattern in applicablePatterns)
                {
                    try
                    {
                        var patternActions = pattern.GetNextActionsAsync(context.OperationName, context).Result;
                        actions.AddRange(patternActions);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error getting actions from pattern {Pattern}", pattern.Name);
                    }
                }
            }

            return Task.FromResult(actions);
        }

        private List<AIAction> GetIntentBasedActions(ContextAnalysis analysis, ActionContext context)
        {
            var actions = new List<AIAction>();

            switch (analysis.UserIntent)
            {
                case UserIntent.Explore:
                    actions.Add(new AIAction
                    {
                        Tool = "browse_related",
                        Description = "Browse related items",
                        Rationale = "Explore connections and relationships",
                        Category = "navigate"
                    });
                    break;

                case UserIntent.Analyze:
                    actions.Add(new AIAction
                    {
                        Tool = "deep_analysis",
                        Description = "Perform detailed analysis",
                        Rationale = "Get deeper insights into the data",
                        Category = "analyze"
                    });
                    break;

                case UserIntent.Filter:
                    actions.Add(new AIAction
                    {
                        Tool = "advanced_filter",
                        Description = "Apply advanced filters",
                        Rationale = "Narrow down to specific items of interest",
                        Category = "filter"
                    });
                    break;

                case UserIntent.Export:
                    actions.Add(new AIAction
                    {
                        Tool = "export_with_format",
                        Description = "Export data in preferred format",
                        Rationale = "Save results for external use",
                        Category = "export"
                    });
                    break;
            }

            return actions;
        }

        private List<AIAction> GetDataDrivenActions<T>(T data, ContextAnalysis analysis, ActionContext context)
        {
            var actions = new List<AIAction>();

            // If insights suggest errors, provide fix actions
            if (context.RelatedInsights?.Any(i => i.Type == InsightType.Error) == true)
            {
                actions.Add(new AIAction
                {
                    Tool = "troubleshoot_errors",
                    Description = "Troubleshoot and fix errors",
                    Rationale = "Errors were detected in the results",
                    Category = "fix"
                });
            }

            // If performance issues detected, suggest optimization
            if (context.RelatedInsights?.Any(i => i.Type == InsightType.Performance) == true)
            {
                actions.Add(new AIAction
                {
                    Tool = "optimize_query",
                    Description = "Optimize the query for better performance",
                    Rationale = "Performance can be improved",
                    Category = "optimize"
                });
            }

            // If truncated results, suggest full retrieval
            if (analysis.HasTruncatedResults)
            {
                actions.Add(new AIAction
                {
                    Tool = "get_full_results",
                    Description = "Retrieve complete results without truncation",
                    Rationale = "Results were truncated due to size limits",
                    Category = "navigate",
                    Parameters = new Dictionary<string, object>
                    {
                        { "use_pagination", true },
                        { "use_streaming", true }
                    }
                });
            }

            return actions;
        }

        private void RegisterDefaultPatterns()
        {
            // Register built-in workflow patterns
            RegisterWorkflowPattern(new SearchRefineWorkflow());
            RegisterWorkflowPattern(new AnalyzeExportWorkflow());
            RegisterWorkflowPattern(new ErrorResolutionWorkflow());
            RegisterWorkflowPattern(new DataExplorationWorkflow());
        }
    }

    /// <summary>
    /// Search and refine workflow pattern
    /// </summary>
    public class SearchRefineWorkflow : IWorkflowPattern
    {
        public string Name => "SearchRefine";
        public string[] ApplicableOperations => new[] { "search", "query", "find", "list" };

        public Task<List<AIAction>> GetNextActionsAsync(string currentOperation, ActionContext context)
        {
            var actions = new List<AIAction>
            {
                new AIAction
                {
                    Tool = "refine_search",
                    Description = "Refine search with additional criteria",
                    Rationale = "Narrow down results to find exactly what you need",
                    Category = "filter"
                },
                new AIAction
                {
                    Tool = "save_search",
                    Description = "Save this search for future use",
                    Rationale = "Reuse successful search criteria",
                    Category = "utility"
                }
            };

            return Task.FromResult(actions);
        }

        public bool IsApplicable(string operation, ActionContext context)
        {
            return ApplicableOperations.Any(op => operation.Contains(op, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Analyze and export workflow pattern
    /// </summary>
    public class AnalyzeExportWorkflow : IWorkflowPattern
    {
        public string Name => "AnalyzeExport";
        public string[] ApplicableOperations => new[] { "analyze", "report", "statistics" };

        public Task<List<AIAction>> GetNextActionsAsync(string currentOperation, ActionContext context)
        {
            var actions = new List<AIAction>
            {
                new AIAction
                {
                    Tool = "generate_report",
                    Description = "Generate detailed report",
                    Rationale = "Create comprehensive documentation of findings",
                    Category = "export"
                },
                new AIAction
                {
                    Tool = "visualize_data",
                    Description = "Create visualizations",
                    Rationale = "Visual representations can reveal patterns",
                    Category = "analyze"
                }
            };

            return Task.FromResult(actions);
        }

        public bool IsApplicable(string operation, ActionContext context)
        {
            return ApplicableOperations.Any(op => operation.Contains(op, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Error resolution workflow pattern
    /// </summary>
    public class ErrorResolutionWorkflow : IWorkflowPattern
    {
        public string Name => "ErrorResolution";
        public string[] ApplicableOperations => new[] { "error", "fix", "troubleshoot", "debug" };

        public Task<List<AIAction>> GetNextActionsAsync(string currentOperation, ActionContext context)
        {
            var actions = new List<AIAction>
            {
                new AIAction
                {
                    Tool = "check_logs",
                    Description = "Check detailed logs",
                    Rationale = "Logs may contain additional error information",
                    Category = "debug"
                },
                new AIAction
                {
                    Tool = "validate_input",
                    Description = "Validate input parameters",
                    Rationale = "Ensure all inputs meet requirements",
                    Category = "fix"
                }
            };

            return Task.FromResult(actions);
        }

        public bool IsApplicable(string operation, ActionContext context)
        {
            return ApplicableOperations.Any(op => operation.Contains(op, StringComparison.OrdinalIgnoreCase)) ||
                   context.RelatedInsights?.Any(i => i.Type == InsightType.Error) == true;
        }
    }

    /// <summary>
    /// Data exploration workflow pattern
    /// </summary>
    public class DataExplorationWorkflow : IWorkflowPattern
    {
        public string Name => "DataExploration";
        public string[] ApplicableOperations => new[] { "explore", "browse", "discover", "navigate" };

        public Task<List<AIAction>> GetNextActionsAsync(string currentOperation, ActionContext context)
        {
            var actions = new List<AIAction>
            {
                new AIAction
                {
                    Tool = "show_relationships",
                    Description = "Show relationships between items",
                    Rationale = "Understanding connections provides context",
                    Category = "navigate"
                },
                new AIAction
                {
                    Tool = "find_similar",
                    Description = "Find similar items",
                    Rationale = "Discover related content",
                    Category = "navigate"
                }
            };

            return Task.FromResult(actions);
        }

        public bool IsApplicable(string operation, ActionContext context)
        {
            return ApplicableOperations.Any(op => operation.Contains(op, StringComparison.OrdinalIgnoreCase));
        }
    }
}