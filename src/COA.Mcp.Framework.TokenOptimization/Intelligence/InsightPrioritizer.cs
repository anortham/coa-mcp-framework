using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using COA.Mcp.Framework.TokenOptimization.Models;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.TokenOptimization.Intelligence
{
    /// <summary>
    /// Default implementation for prioritizing insights
    /// </summary>
    public class InsightPrioritizer : IInsightPrioritizer
    {
        private readonly ILogger<InsightPrioritizer> _logger;

        public InsightPrioritizer(ILogger<InsightPrioritizer> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<List<Insight>> PrioritizeAsync(List<Insight> insights, InsightContext context)
        {
            if (insights == null) throw new ArgumentNullException(nameof(insights));
            if (context == null) throw new ArgumentNullException(nameof(context));

            _logger.LogDebug("Prioritizing {Count} insights for {Operation}",
                insights.Count, context.OperationName);

            // Score each insight
            var scoredInsights = insights
                .Select(insight => new
                {
                    Insight = insight,
                    Score = ScoreInsight(insight, context)
                })
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Insight.Importance)
                .Select(x => x.Insight)
                .ToList();

            // Apply deduplication
            var deduplicatedInsights = DeduplicateInsights(scoredInsights);

            // Apply diversity rules
            var diverseInsights = ApplyDiversityRules(deduplicatedInsights, context);

            _logger.LogDebug("Prioritization complete. Reduced from {Original} to {Final} insights",
                insights.Count, diverseInsights.Count);

            return Task.FromResult(diverseInsights);
        }

        public double ScoreInsight(Insight insight, InsightContext context)
        {
            if (insight == null) return 0.0;

            double score = 0.0;

            // Base score from importance
            score += GetImportanceScore(insight.Importance);

            // Type-based scoring
            score += GetTypeScore(insight.Type, context);

            // Relevance to user query
            if (!string.IsNullOrEmpty(context.UserQuery))
            {
                score += CalculateRelevanceScore(insight.Text, context.UserQuery);
            }

            // Context priority bonus
            score += GetPriorityBonus(context.Priority);

            // Freshness bonus (if insight has timestamp)
            if (insight.Metadata?.ContainsKey("timestamp") == true)
            {
                score += GetFreshnessScore(insight.Metadata["timestamp"]);
            }

            // Normalize score to 0.0-1.0 range
            return Math.Min(1.0, Math.Max(0.0, score / 4.0));
        }

        private double GetImportanceScore(InsightImportance importance)
        {
            return importance switch
            {
                InsightImportance.Critical => 1.0,
                InsightImportance.High => 0.8,
                InsightImportance.Medium => 0.5,
                InsightImportance.Low => 0.2,
                _ => 0.3
            };
        }

        private double GetTypeScore(InsightType type, InsightContext context)
        {
            // Adjust scoring based on context priority
            if (context.Priority == InsightPriority.Critical)
            {
                return type switch
                {
                    InsightType.Error => 1.0,
                    InsightType.Warning => 0.9,
                    InsightType.Security => 0.95,
                    _ => 0.5
                };
            }

            return type switch
            {
                InsightType.Error => 0.9,
                InsightType.Warning => 0.7,
                InsightType.Security => 0.85,
                InsightType.Performance => 0.6,
                InsightType.Suggestion => 0.5,
                InsightType.Analysis => 0.6,
                InsightType.Information => 0.3,
                _ => 0.4
            };
        }

        private double CalculateRelevanceScore(string insightText, string userQuery)
        {
            if (string.IsNullOrEmpty(insightText) || string.IsNullOrEmpty(userQuery))
                return 0.0;

            // Simple keyword matching (can be enhanced with more sophisticated NLP)
            var queryWords = userQuery.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var insightWords = insightText.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var matchCount = queryWords.Count(qw => insightWords.Contains(qw));
            var relevance = (double)matchCount / queryWords.Length;

            return relevance * 0.5; // Max 0.5 contribution to score
        }

        private double GetPriorityBonus(InsightPriority priority)
        {
            return priority switch
            {
                InsightPriority.Critical => 0.3,
                InsightPriority.High => 0.2,
                InsightPriority.Normal => 0.1,
                InsightPriority.Low => 0.0,
                _ => 0.05
            };
        }

        private double GetFreshnessScore(object timestamp)
        {
            if (timestamp is DateTime dateTime)
            {
                var age = DateTime.UtcNow - dateTime;
                if (age.TotalMinutes < 5) return 0.2;
                if (age.TotalHours < 1) return 0.1;
                if (age.TotalDays < 1) return 0.05;
            }
            return 0.0;
        }

        private List<Insight> DeduplicateInsights(List<Insight> insights)
        {
            var deduped = new List<Insight>();
            var seenTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var insight in insights)
            {
                // Check for exact duplicates
                if (!seenTexts.Contains(insight.Text))
                {
                    seenTexts.Add(insight.Text);
                    deduped.Add(insight);
                }
                else
                {
                    _logger.LogDebug("Removed duplicate insight: {Text}", insight.Text);
                }
            }

            return deduped;
        }

        private List<Insight> ApplyDiversityRules(List<Insight> insights, InsightContext context)
        {
            var diverse = new List<Insight>();
            var typeCounts = new Dictionary<InsightType, int>();

            // Maximum insights per type (based on total allowed)
            var maxPerType = Math.Max(2, context.MaxInsights / 3);

            foreach (var insight in insights)
            {
                if (!typeCounts.TryGetValue(insight.Type, out var count))
                {
                    count = 0;
                }

                // Allow more critical insights
                var typeLimit = insight.Importance == InsightImportance.Critical ? maxPerType + 1 : maxPerType;

                if (count < typeLimit)
                {
                    diverse.Add(insight);
                    typeCounts[insight.Type] = count + 1;
                }
            }

            return diverse;
        }
    }
}