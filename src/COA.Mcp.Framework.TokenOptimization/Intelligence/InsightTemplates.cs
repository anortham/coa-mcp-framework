using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using COA.Mcp.Framework.TokenOptimization.Models;

namespace COA.Mcp.Framework.TokenOptimization.Intelligence
{
    /// <summary>
    /// Base class for insight templates
    /// </summary>
    public abstract class InsightTemplateBase : IInsightTemplate
    {
        public abstract string Name { get; }
        public abstract Type[] SupportedTypes { get; }
        public virtual int Priority => 50;

        public abstract Task<Insight?> GenerateAsync(object data, InsightContext context);

        public virtual bool IsApplicable(InsightContext context)
        {
            return true;
        }

        protected Insight CreateInsight(string text, InsightType type = InsightType.Information, 
            InsightImportance importance = InsightImportance.Medium)
        {
            return new Insight
            {
                Text = text,
                Type = type,
                Importance = importance
            };
        }
    }

    /// <summary>
    /// Template for empty collection insights
    /// </summary>
    public class EmptyCollectionInsightTemplate : InsightTemplateBase
    {
        public override string Name => "EmptyCollection";
        public override Type[] SupportedTypes => new[] { typeof(ICollection), typeof(IEnumerable) };
        public override int Priority => 100;

        public override Task<Insight?> GenerateAsync(object data, InsightContext context)
        {
            if (data is ICollection collection && collection.Count == 0)
            {
                var insight = CreateInsight(
                    $"No results found for {context.OperationName}. Consider broadening your search criteria or checking your filters",
                    InsightType.Warning,
                    InsightImportance.High);
                return Task.FromResult<Insight?>(insight);
            }

            return Task.FromResult<Insight?>(null);
        }
    }

    /// <summary>
    /// Template for large collection insights
    /// </summary>
    public class LargeCollectionInsightTemplate : InsightTemplateBase
    {
        private const int LargeCollectionThreshold = 100;

        public override string Name => "LargeCollection";
        public override Type[] SupportedTypes => new[] { typeof(ICollection), typeof(IEnumerable) };
        public override int Priority => 90;

        public override Task<Insight?> GenerateAsync(object data, InsightContext context)
        {
            if (data is ICollection collection && collection.Count > LargeCollectionThreshold)
            {
                var insight = CreateInsight(
                    $"Found {collection.Count} results. Consider adding filters to narrow down the results for better performance",
                    InsightType.Suggestion,
                    InsightImportance.Medium);
                return Task.FromResult<Insight?>(insight);
            }

            return Task.FromResult<Insight?>(null);
        }
    }

    /// <summary>
    /// Template for truncated results insights
    /// </summary>
    public class TruncatedResultsInsightTemplate : InsightTemplateBase
    {
        public override string Name => "TruncatedResults";
        public override Type[] SupportedTypes => new[] { typeof(object) };
        public override int Priority => 95;

        public override Task<Insight?> GenerateAsync(object data, InsightContext context)
        {
            if (context.Parameters.TryGetValue("truncated", out var truncated) && 
                truncated is bool isTruncated && isTruncated)
            {
                var insight = CreateInsight(
                    "Results were truncated to fit within token limits. Full results are available via the resource URI",
                    InsightType.Information,
                    InsightImportance.High);
                return Task.FromResult<Insight?>(insight);
            }

            return Task.FromResult<Insight?>(null);
        }
    }

    /// <summary>
    /// Template for performance insights
    /// </summary>
    public class PerformanceInsightTemplate : InsightTemplateBase
    {
        private const int SlowOperationThresholdMs = 1000;

        public override string Name => "Performance";
        public override Type[] SupportedTypes => new[] { typeof(object) };
        public override int Priority => 80;

        public override Task<Insight?> GenerateAsync(object data, InsightContext context)
        {
            if (context.Parameters.TryGetValue("executionTime", out var execTime) && 
                execTime is TimeSpan timeSpan && timeSpan.TotalMilliseconds > SlowOperationThresholdMs)
            {
                var insight = CreateInsight(
                    $"Operation took {timeSpan.TotalSeconds:F1} seconds. Consider using more specific parameters to improve performance",
                    InsightType.Performance,
                    InsightImportance.Medium);
                return Task.FromResult<Insight?>(insight);
            }

            return Task.FromResult<Insight?>(null);
        }
    }

    /// <summary>
    /// Template for pattern detection insights
    /// </summary>
    public class PatternDetectionInsightTemplate : InsightTemplateBase
    {
        public override string Name => "PatternDetection";
        public override Type[] SupportedTypes => new[] { typeof(IEnumerable) };
        public override int Priority => 70;

        public override async Task<Insight?> GenerateAsync(object data, InsightContext context)
        {
            if (data is IEnumerable enumerable)
            {
                var items = enumerable.Cast<object>().ToList();
                if (items.Count < 10) return null;

                // Analyze for patterns (simplified example)
                var patterns = await Task.Run(() => AnalyzePatterns(items, context));
                if (patterns.Any())
                {
                    var insight = CreateInsight(
                        $"Detected patterns in results: {string.Join(", ", patterns)}",
                        InsightType.Analysis,
                        InsightImportance.High);
                    return insight;
                }
            }

            return null;
        }

        private List<string> AnalyzePatterns(List<object> items, InsightContext context)
        {
            var patterns = new List<string>();

            // Example: Check for common prefixes in string properties
            if (items.All(i => i is string))
            {
                var strings = items.Cast<string>().ToList();
                var commonPrefix = GetCommonPrefix(strings);
                if (!string.IsNullOrEmpty(commonPrefix) && commonPrefix.Length > 3)
                {
                    patterns.Add($"Common prefix '{commonPrefix}'");
                }
            }

            return patterns;
        }

        private string GetCommonPrefix(List<string> strings)
        {
            if (!strings.Any()) return string.Empty;
            
            var prefix = strings[0];
            foreach (var s in strings.Skip(1))
            {
                while (!s.StartsWith(prefix) && prefix.Length > 0)
                {
                    prefix = prefix.Substring(0, prefix.Length - 1);
                }
            }
            return prefix;
        }
    }

    /// <summary>
    /// Template for error rate insights
    /// </summary>
    public class ErrorRateInsightTemplate : InsightTemplateBase
    {
        public override string Name => "ErrorRate";
        public override Type[] SupportedTypes => new[] { typeof(object) };
        public override int Priority => 85;

        public override Task<Insight?> GenerateAsync(object data, InsightContext context)
        {
            if (context.Parameters.TryGetValue("errorCount", out var errorCount) &&
                context.Parameters.TryGetValue("totalCount", out var totalCount) &&
                errorCount is int errors && totalCount is int total && total > 0)
            {
                var errorRate = (double)errors / total;
                if (errorRate > 0.1) // More than 10% errors
                {
                    var insight = CreateInsight(
                        $"High error rate detected ({errorRate:P0}). Review error logs for common issues",
                        InsightType.Warning,
                        InsightImportance.High);
                    return Task.FromResult<Insight?>(insight);
                }
            }

            return Task.FromResult<Insight?>(null);
        }
    }
}