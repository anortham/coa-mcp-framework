using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using COA.Mcp.Framework.TokenOptimization.Models;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.TokenOptimization.Actions
{
    /// <summary>
    /// Default implementation for analyzing action context
    /// </summary>
    public class ActionContextAnalyzer : IActionContextAnalyzer
    {
        private readonly ILogger<ActionContextAnalyzer> _logger;
        private readonly Dictionary<string, UserIntent> _intentKeywords;

        public ActionContextAnalyzer(ILogger<ActionContextAnalyzer> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _intentKeywords = InitializeIntentKeywords();
        }

        public Task<ContextAnalysis> AnalyzeAsync<T>(T data, ActionContext context)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (context == null) throw new ArgumentNullException(nameof(context));

            var analysis = new ContextAnalysis();

            // Analyze user intent
            analysis.UserIntent = DetermineUserIntent(context);

            // Analyze data characteristics
            AnalyzeDataCharacteristics(data, context, analysis);

            // Check for errors and performance issues
            AnalyzeHealthIndicators(context, analysis);

            // Determine complexity
            analysis.Complexity = DetermineComplexity(data);

            // Suggest workflow
            analysis.SuggestedWorkflow = SuggestWorkflow(analysis, context);

            // Calculate confidence
            analysis.Confidence = CalculateConfidence(analysis, context);

            _logger.LogDebug("Context analysis complete: Intent={Intent}, Complexity={Complexity}, Confidence={Confidence:F2}",
                analysis.UserIntent, analysis.Complexity, analysis.Confidence);

            return Task.FromResult(analysis);
        }

        private UserIntent DetermineUserIntent(ActionContext context)
        {
            // Check user's explicit intent
            if (!string.IsNullOrEmpty(context.UserIntent))
            {
                var lowerIntent = context.UserIntent.ToLower();
                foreach (var kvp in _intentKeywords)
                {
                    if (lowerIntent.Contains(kvp.Key))
                    {
                        return kvp.Value;
                    }
                }
            }

            // Infer from operation name
            var operation = context.OperationName.ToLower();
            foreach (var kvp in _intentKeywords)
            {
                if (operation.Contains(kvp.Key))
                {
                    return kvp.Value;
                }
            }

            // Infer from previous actions
            if (context.PreviousActions?.Any() == true)
            {
                var lastAction = context.PreviousActions.Last().ToLower();
                if (lastAction.Contains("analyze")) return UserIntent.Analyze;
                if (lastAction.Contains("filter")) return UserIntent.Filter;
                if (lastAction.Contains("export")) return UserIntent.Export;
            }

            return UserIntent.General;
        }

        private void AnalyzeDataCharacteristics<T>(T data, ActionContext context, ContextAnalysis analysis)
        {
            // Check for truncation
            if (context.Parameters.TryGetValue("truncated", out var truncated) && 
                truncated is bool isTruncated)
            {
                analysis.HasTruncatedResults = isTruncated;
            }

            // Check collection size
            if (data is ICollection collection)
            {
                if (collection.Count == 0)
                {
                    // Empty results might indicate too restrictive filters
                    analysis.UserIntent = UserIntent.Filter;
                }
                else if (collection.Count > 1000)
                {
                    // Large results might need analysis or filtering
                    if (analysis.UserIntent == UserIntent.General)
                    {
                        analysis.UserIntent = UserIntent.Analyze;
                    }
                }
            }
        }

        private void AnalyzeHealthIndicators(ActionContext context, ContextAnalysis analysis)
        {
            // Check insights for errors
            if (context.RelatedInsights?.Any(i => i.Type == InsightType.Error) == true)
            {
                analysis.HasErrors = true;
                if (analysis.UserIntent == UserIntent.General)
                {
                    analysis.UserIntent = UserIntent.Debug;
                }
            }

            // Check for performance issues
            if (context.RelatedInsights?.Any(i => i.Type == InsightType.Performance) == true)
            {
                analysis.HasPerformanceIssues = true;
                if (analysis.UserIntent == UserIntent.General)
                {
                    analysis.UserIntent = UserIntent.Optimize;
                }
            }

            // Check execution time
            if (context.Parameters.TryGetValue("executionTime", out var execTime) && 
                execTime is TimeSpan timeSpan && timeSpan.TotalSeconds > 5)
            {
                analysis.HasPerformanceIssues = true;
            }
        }

        private DataComplexity DetermineComplexity<T>(T data)
        {
            if (data == null) return DataComplexity.Simple;

            var type = data.GetType();

            // Check if it's a collection
            if (data is ICollection collection)
            {
                if (collection.Count == 0) return DataComplexity.Simple;
                if (collection.Count > 100) return DataComplexity.Complex;

                // Check first item for nested structures
                var firstItem = collection.Cast<object>().FirstOrDefault();
                if (firstItem != null)
                {
                    var itemType = firstItem.GetType();
                    var properties = itemType.GetProperties();
                    
                    // Check for nested collections
                    if (properties.Any(p => typeof(IEnumerable).IsAssignableFrom(p.PropertyType) && 
                                          p.PropertyType != typeof(string)))
                    {
                        return DataComplexity.Hierarchical;
                    }

                    // Check for graph-like structures (self-references)
                    if (properties.Any(p => p.PropertyType == itemType || 
                                          p.PropertyType == typeof(IEnumerable<>).MakeGenericType(itemType)))
                    {
                        return DataComplexity.Graph;
                    }
                }

                return DataComplexity.Moderate;
            }

            // Single complex object
            var propCount = type.GetProperties().Length;
            if (propCount > 20) return DataComplexity.Complex;
            if (propCount > 10) return DataComplexity.Moderate;

            return DataComplexity.Simple;
        }

        private string? SuggestWorkflow(ContextAnalysis analysis, ActionContext context)
        {
            // Suggest workflow based on analysis
            if (analysis.HasErrors)
            {
                return "ErrorResolution";
            }

            if (analysis.HasPerformanceIssues)
            {
                return "PerformanceOptimization";
            }

            return analysis.UserIntent switch
            {
                UserIntent.Explore => "DataExploration",
                UserIntent.Analyze => "AnalysisAndReporting",
                UserIntent.Filter => "SearchAndFilter",
                UserIntent.Export => "ExportAndShare",
                UserIntent.Debug => "Troubleshooting",
                UserIntent.Compare => "Comparison",
                _ => null
            };
        }

        private double CalculateConfidence(ContextAnalysis analysis, ActionContext context)
        {
            double confidence = 0.5; // Base confidence

            // Increase confidence based on explicit signals
            if (!string.IsNullOrEmpty(context.UserIntent))
            {
                confidence += 0.2;
            }

            if (context.RelatedInsights?.Any() == true)
            {
                confidence += 0.1;
            }

            if (context.PreviousActions?.Any() == true)
            {
                confidence += 0.1;
            }

            // Adjust based on data clarity
            if (analysis.Complexity == DataComplexity.Simple)
            {
                confidence += 0.1;
            }
            else if (analysis.Complexity == DataComplexity.Graph)
            {
                confidence -= 0.1;
            }

            return Math.Min(1.0, Math.Max(0.0, confidence));
        }

        private Dictionary<string, UserIntent> InitializeIntentKeywords()
        {
            return new Dictionary<string, UserIntent>
            {
                { "explore", UserIntent.Explore },
                { "browse", UserIntent.Explore },
                { "discover", UserIntent.Explore },
                { "navigate", UserIntent.Explore },
                { "analyze", UserIntent.Analyze },
                { "report", UserIntent.Analyze },
                { "statistics", UserIntent.Analyze },
                { "metrics", UserIntent.Analyze },
                { "filter", UserIntent.Filter },
                { "search", UserIntent.Filter },
                { "query", UserIntent.Filter },
                { "find", UserIntent.Filter },
                { "export", UserIntent.Export },
                { "download", UserIntent.Export },
                { "save", UserIntent.Export },
                { "share", UserIntent.Export },
                { "debug", UserIntent.Debug },
                { "troubleshoot", UserIntent.Debug },
                { "fix", UserIntent.Debug },
                { "error", UserIntent.Debug },
                { "optimize", UserIntent.Optimize },
                { "performance", UserIntent.Optimize },
                { "speed", UserIntent.Optimize },
                { "improve", UserIntent.Optimize },
                { "compare", UserIntent.Compare },
                { "diff", UserIntent.Compare },
                { "versus", UserIntent.Compare },
                { "monitor", UserIntent.Monitor },
                { "watch", UserIntent.Monitor },
                { "track", UserIntent.Monitor }
            };
        }
    }
}