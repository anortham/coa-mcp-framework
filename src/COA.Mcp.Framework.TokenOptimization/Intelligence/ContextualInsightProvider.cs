using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using COA.Mcp.Framework.TokenOptimization.Models;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.TokenOptimization.Intelligence
{
    /// <summary>
    /// Provides context-aware insights based on operation history and patterns
    /// </summary>
    public class ContextualInsightProvider : IInsightGenerator
    {
        private readonly IInsightGenerator _baseGenerator;
        private readonly ILogger<ContextualInsightProvider> _logger;
        private readonly Dictionary<string, List<OperationContext>> _operationHistory;
        private readonly object _historyLock = new object();
        private const int MaxHistoryPerOperation = 100;

        public ContextualInsightProvider(
            IInsightGenerator baseGenerator,
            ILogger<ContextualInsightProvider> logger)
        {
            _baseGenerator = baseGenerator ?? throw new ArgumentNullException(nameof(baseGenerator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _operationHistory = new Dictionary<string, List<OperationContext>>();
        }

        public async Task<List<Insight>> GenerateInsightsAsync<T>(T data, InsightContext context)
        {
            // Get base insights
            var insights = await _baseGenerator.GenerateInsightsAsync(data, context);

            // Enhance with contextual insights
            var contextualInsights = await GenerateContextualInsights(data, context);
            insights.AddRange(contextualInsights);

            // Record this operation for future context
            RecordOperation(context, data, insights);

            return insights;
        }

        public async Task<List<Insight>> GenerateInsightsAsync<T>(T data, InsightContext context, int tokenBudget)
        {
            var insights = await GenerateInsightsAsync(data, context);
            return ApplyTokenBudget(insights, tokenBudget);
        }

        public bool CanHandle(Type dataType)
        {
            return _baseGenerator.CanHandle(dataType);
        }

        private async Task<List<Insight>> GenerateContextualInsights<T>(T data, InsightContext context)
        {
            var contextualInsights = new List<Insight>();

            // Get operation history
            var history = GetOperationHistory(context.OperationName);
            if (!history.Any()) return contextualInsights;

            // Analyze patterns in history
            var patterns = await Task.Run(() => AnalyzePatterns(history, context));
            contextualInsights.AddRange(patterns);

            // Compare with previous results
            if (data is System.Collections.ICollection currentCollection)
            {
                var comparison = CompareWithHistory(currentCollection.Count, history);
                if (comparison != null)
                {
                    contextualInsights.Add(comparison);
                }
            }

            // Suggest based on success patterns
            var suggestions = GenerateSuggestionsFromHistory(history, context);
            contextualInsights.AddRange(suggestions);

            return contextualInsights;
        }

        private List<OperationContext> GetOperationHistory(string operationName)
        {
            lock (_historyLock)
            {
                if (_operationHistory.TryGetValue(operationName, out var history))
                {
                    return history.ToList(); // Return copy to avoid threading issues
                }
                return new List<OperationContext>();
            }
        }

        private void RecordOperation<T>(InsightContext context, T data, List<Insight> insights)
        {
            lock (_historyLock)
            {
                if (!_operationHistory.TryGetValue(context.OperationName, out var history))
                {
                    history = new List<OperationContext>();
                    _operationHistory[context.OperationName] = history;
                }

                var operationContext = new OperationContext
                {
                    Timestamp = DateTime.UtcNow,
                    Parameters = new Dictionary<string, object>(context.Parameters),
                    ResultCount = data is System.Collections.ICollection collection ? collection.Count : 1,
                    InsightCount = insights.Count,
                    Success = !insights.Any(i => i.Type == InsightType.Error)
                };

                history.Add(operationContext);

                // Maintain history size limit
                if (history.Count > MaxHistoryPerOperation)
                {
                    history.RemoveAt(0);
                }
            }
        }

        private List<Insight> AnalyzePatterns(List<OperationContext> history, InsightContext context)
        {
            var insights = new List<Insight>();

            // Check for repeated failures
            var recentFailures = history
                .Where(h => h.Timestamp > DateTime.UtcNow.AddHours(-1))
                .Count(h => !h.Success);

            if (recentFailures > 3)
            {
                insights.Add(new Insight
                {
                    Type = InsightType.Warning,
                    Text = "Multiple failures detected in recent operations. Review your parameters or check system status",
                    Importance = InsightImportance.High
                });
            }

            // Check for performance degradation
            if (history.Count > 10)
            {
                var recentOps = history.Skip(history.Count - 5).ToList();
                var olderOps = history.Skip(Math.Max(0, history.Count - 15)).Take(5).ToList();

                if (recentOps.Any() && olderOps.Any() && 
                    context.Parameters.TryGetValue("executionTime", out var currentTime) && 
                    currentTime is TimeSpan currentExecTime)
                {
                    // Simple trend detection (can be enhanced)
                    insights.Add(new Insight
                    {
                        Type = InsightType.Performance,
                        Text = "Consider caching results if you're making similar queries frequently",
                        Importance = InsightImportance.Medium
                    });
                }
            }

            return insights;
        }

        private Insight? CompareWithHistory(int currentCount, List<OperationContext> history)
        {
            if (history.Count < 3) return null;

            var avgCount = history.Average(h => h.ResultCount);
            var deviation = Math.Abs(currentCount - avgCount) / Math.Max(avgCount, 1);

            if (deviation > 0.5)
            {
                if (currentCount > avgCount)
                {
                    return new Insight
                    {
                        Type = InsightType.Analysis,
                        Text = $"Result count ({currentCount}) is significantly higher than average ({avgCount:F0}). This might indicate broader matching criteria",
                        Importance = InsightImportance.Medium
                    };
                }
                else
                {
                    return new Insight
                    {
                        Type = InsightType.Analysis,
                        Text = $"Result count ({currentCount}) is lower than average ({avgCount:F0}). Consider if your filters are too restrictive",
                        Importance = InsightImportance.Medium
                    };
                }
            }

            return null;
        }

        private List<Insight> GenerateSuggestionsFromHistory(List<OperationContext> history, InsightContext context)
        {
            var suggestions = new List<Insight>();

            // Find successful patterns
            var successfulOps = history.Where(h => h.Success && h.ResultCount > 0).ToList();
            if (successfulOps.Count > 5)
            {
                // Look for common successful parameters
                var commonParams = FindCommonParameters(successfulOps);
                if (commonParams.Any())
                {
                    suggestions.Add(new Insight
                    {
                        Type = InsightType.Suggestion,
                        Text = $"Previous successful queries often used: {string.Join(", ", commonParams.Select(p => $"{p.Key}={p.Value}"))}",
                        Importance = InsightImportance.Medium
                    });
                }
            }

            return suggestions;
        }

        private Dictionary<string, object> FindCommonParameters(List<OperationContext> operations)
        {
            var commonParams = new Dictionary<string, object>();
            
            if (!operations.Any()) return commonParams;

            // Find parameters that appear in most operations
            var allParams = operations.SelectMany(op => op.Parameters).ToList();
            var paramGroups = allParams.GroupBy(p => p.Key);

            foreach (var group in paramGroups)
            {
                var frequency = (double)group.Count() / operations.Count;
                if (frequency > 0.7) // Parameter appears in 70%+ of operations
                {
                    // Find most common value
                    var mostCommonValue = group
                        .GroupBy(g => g.Value?.ToString() ?? "null")
                        .OrderByDescending(g => g.Count())
                        .First()
                        .First()
                        .Value;

                    commonParams[group.Key] = mostCommonValue;
                }
            }

            return commonParams;
        }

        private List<Insight> ApplyTokenBudget(List<Insight> insights, int tokenBudget)
        {
            var result = new List<Insight>();
            var currentTokens = 0;

            foreach (var insight in insights.OrderByDescending(i => i.Importance))
            {
                var insightTokens = TokenEstimator.EstimateObject(insight);
                if (currentTokens + insightTokens <= tokenBudget)
                {
                    result.Add(insight);
                    currentTokens += insightTokens;
                }
                else
                {
                    break;
                }
            }

            return result;
        }

        private class OperationContext
        {
            public DateTime Timestamp { get; set; }
            public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
            public int ResultCount { get; set; }
            public int InsightCount { get; set; }
            public bool Success { get; set; }
        }
    }
}