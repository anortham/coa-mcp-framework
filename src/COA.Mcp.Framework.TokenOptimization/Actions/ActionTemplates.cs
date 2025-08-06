using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using COA.Mcp.Framework.TokenOptimization.Models;
using COA.Mcp.Framework.Models;

namespace COA.Mcp.Framework.TokenOptimization.Actions
{
    /// <summary>
    /// Base class for action templates
    /// </summary>
    public abstract class ActionTemplateBase : IActionTemplate
    {
        public abstract string Name { get; }
        public abstract Type[] SupportedTypes { get; }
        public virtual int Priority => 50;

        public abstract Task<AIAction?> GenerateAsync(object data, ActionContext context);

        public virtual bool IsApplicable(ActionContext context)
        {
            return true;
        }

        protected AIAction CreateAction(string tool, string description, string rationale, 
            string category = "general", Dictionary<string, object>? parameters = null)
        {
            return new AIAction
            {
                Tool = tool,
                Description = description,
                Rationale = rationale,
                Category = category,
                Parameters = parameters
            };
        }
    }

    /// <summary>
    /// Template for empty collection actions
    /// </summary>
    public class EmptyCollectionActionTemplate : ActionTemplateBase
    {
        public override string Name => "EmptyCollectionActions";
        public override Type[] SupportedTypes => new[] { typeof(ICollection), typeof(IEnumerable) };
        public override int Priority => 100;

        public override Task<AIAction?> GenerateAsync(object data, ActionContext context)
        {
            if (data is ICollection collection && collection.Count == 0)
            {
                var action = CreateAction(
                    tool: $"{context.OperationName}_with_broader_criteria",
                    description: "Search again with less restrictive filters",
                    rationale: "No results were found with current criteria",
                    category: "retry",
                    parameters: new Dictionary<string, object> 
                    { 
                        { "reduce_filters", true },
                        { "expand_scope", true }
                    });
                
                return Task.FromResult<AIAction?>(action);
            }

            return Task.FromResult<AIAction?>(null);
        }
    }

    /// <summary>
    /// Template for large collection actions
    /// </summary>
    public class LargeCollectionActionTemplate : ActionTemplateBase
    {
        private const int LargeCollectionThreshold = 100;

        public override string Name => "LargeCollectionActions";
        public override Type[] SupportedTypes => new[] { typeof(ICollection), typeof(IEnumerable) };
        public override int Priority => 90;

        public override Task<AIAction?> GenerateAsync(object data, ActionContext context)
        {
            if (data is ICollection collection && collection.Count > LargeCollectionThreshold)
            {
                var action = CreateAction(
                    tool: $"{context.OperationName}_with_filters",
                    description: "Refine search with additional filters",
                    rationale: $"Found {collection.Count} results - filtering can improve relevance",
                    category: "filter",
                    parameters: new Dictionary<string, object>
                    {
                        { "suggested_limit", 50 },
                        { "sort_by", "relevance" }
                    });

                return Task.FromResult<AIAction?>(action);
            }

            return Task.FromResult<AIAction?>(null);
        }
    }

    /// <summary>
    /// Template for pagination actions
    /// </summary>
    public class PaginationActionTemplate : ActionTemplateBase
    {
        public override string Name => "PaginationActions";
        public override Type[] SupportedTypes => new[] { typeof(ICollection), typeof(IEnumerable) };
        public override int Priority => 80;

        public override Task<AIAction?> GenerateAsync(object data, ActionContext context)
        {
            if (context.Parameters.TryGetValue("hasMore", out var hasMore) && 
                hasMore is bool hasMoreResults && hasMoreResults)
            {
                var currentPage = 1;
                if (context.Parameters.TryGetValue("page", out var page) && page is int pageNum)
                {
                    currentPage = pageNum;
                }

                var action = CreateAction(
                    tool: $"{context.OperationName}_next_page",
                    description: "Get the next page of results",
                    rationale: "More results are available",
                    category: "navigate",
                    parameters: new Dictionary<string, object>
                    {
                        { "page", currentPage + 1 }
                    });

                return Task.FromResult<AIAction?>(action);
            }

            return Task.FromResult<AIAction?>(null);
        }
    }

    /// <summary>
    /// Template for export actions
    /// </summary>
    public class ExportActionTemplate : ActionTemplateBase
    {
        private const int ExportThreshold = 10;

        public override string Name => "ExportActions";
        public override Type[] SupportedTypes => new[] { typeof(ICollection), typeof(IEnumerable) };
        public override int Priority => 70;

        public override Task<AIAction?> GenerateAsync(object data, ActionContext context)
        {
            if (data is ICollection collection && collection.Count >= ExportThreshold)
            {
                var action = CreateAction(
                    tool: "export_results",
                    description: "Export results to CSV or JSON format",
                    rationale: $"You have {collection.Count} results that can be exported for further analysis",
                    category: "export",
                    parameters: new Dictionary<string, object>
                    {
                        { "formats", new[] { "csv", "json", "excel" } },
                        { "include_metadata", true }
                    });

                return Task.FromResult<AIAction?>(action);
            }

            return Task.FromResult<AIAction?>(null);
        }
    }

    /// <summary>
    /// Template for analysis actions
    /// </summary>
    public class AnalysisActionTemplate : ActionTemplateBase
    {
        public override string Name => "AnalysisActions";
        public override Type[] SupportedTypes => new[] { typeof(ICollection), typeof(IEnumerable) };
        public override int Priority => 75;

        public override Task<AIAction?> GenerateAsync(object data, ActionContext context)
        {
            if (data is ICollection collection && collection.Count > 5)
            {
                // Determine analysis type based on data
                var analysisType = DetermineAnalysisType(collection, context);
                if (analysisType != null)
                {
                    var action = CreateAction(
                        tool: $"analyze_{analysisType}",
                        description: $"Perform {analysisType} analysis on the results",
                        rationale: "Statistical analysis can reveal patterns and insights",
                        category: "analyze",
                        parameters: new Dictionary<string, object>
                        {
                            { "analysis_type", analysisType },
                            { "include_visualization", true }
                        });

                    return Task.FromResult<AIAction?>(action);
                }
            }

            return Task.FromResult<AIAction?>(null);
        }

        private string? DetermineAnalysisType(ICollection collection, ActionContext context)
        {
            // Simple heuristic - can be enhanced
            var firstItem = collection.Cast<object>().FirstOrDefault();
            if (firstItem == null) return null;

            var type = firstItem.GetType();
            if (type.GetProperties().Any(p => p.PropertyType == typeof(DateTime)))
            {
                return "temporal";
            }
            else if (type.GetProperties().Any(p => p.PropertyType == typeof(int) || 
                                                  p.PropertyType == typeof(double) ||
                                                  p.PropertyType == typeof(decimal)))
            {
                return "statistical";
            }
            else
            {
                return "categorical";
            }
        }
    }

    /// <summary>
    /// Template for drill-down actions
    /// </summary>
    public class DrillDownActionTemplate : ActionTemplateBase
    {
        public override string Name => "DrillDownActions";
        public override Type[] SupportedTypes => new[] { typeof(object) };
        public override int Priority => 85;

        public override Task<AIAction?> GenerateAsync(object data, ActionContext context)
        {
            // Check if data has identifiable items
            if (data is IEnumerable enumerable)
            {
                var items = enumerable.Cast<object>().ToList();
                if (items.Any())
                {
                    var firstItem = items.First();
                    var idProperty = firstItem.GetType().GetProperties()
                        .FirstOrDefault(p => p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
                                           p.Name.Equals("Name", StringComparison.OrdinalIgnoreCase));

                    if (idProperty != null)
                    {
                        var action = CreateAction(
                            tool: "get_details",
                            description: "Get detailed information about specific items",
                            rationale: "Drill down into individual items for more information",
                            category: "navigate",
                            parameters: new Dictionary<string, object>
                            {
                                { "item_property", idProperty.Name },
                                { "supports_batch", true }
                            });

                        return Task.FromResult<AIAction?>(action);
                    }
                }
            }

            return Task.FromResult<AIAction?>(null);
        }
    }

    /// <summary>
    /// Template for comparison actions
    /// </summary>
    public class ComparisonActionTemplate : ActionTemplateBase
    {
        public override string Name => "ComparisonActions";
        public override Type[] SupportedTypes => new[] { typeof(ICollection), typeof(IEnumerable) };
        public override int Priority => 65;

        public override Task<AIAction?> GenerateAsync(object data, ActionContext context)
        {
            if (data is ICollection collection && collection.Count >= 2 && collection.Count <= 50)
            {
                var action = CreateAction(
                    tool: "compare_items",
                    description: "Compare selected items side by side",
                    rationale: "Comparison can help identify differences and make decisions",
                    category: "analyze",
                    parameters: new Dictionary<string, object>
                    {
                        { "max_items", 10 },
                        { "highlight_differences", true }
                    });

                return Task.FromResult<AIAction?>(action);
            }

            return Task.FromResult<AIAction?>(null);
        }
    }
}