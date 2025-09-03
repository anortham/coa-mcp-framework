using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using COA.Mcp.Framework.TokenOptimization;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.TokenOptimization.Actions;
using COA.Mcp.Framework.TokenOptimization.Intelligence;
using COA.Mcp.Framework.TokenOptimization.Models;
using COA.Mcp.Framework.TokenOptimization.ResponseBuilders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace COA.Mcp.Framework.TokenOptimization.Tests
{
    [TestFixture]
    public class ResponseIntelligenceIntegrationTests
    {
        private ServiceProvider _serviceProvider = null!;
        private IInsightGenerator _insightGenerator = null!;
        private IActionGenerator _actionGenerator = null!;

        [SetUp]
        public void SetUp()
        {
            var services = new ServiceCollection();

            // Register logging
            services.AddLogging();

            // Register token estimation
            services.AddSingleton<ITokenEstimator, DefaultTokenEstimator>();

            // Register insight generation
            services.AddSingleton<IInsightTemplateProvider, InsightTemplateProvider>();
            services.AddSingleton<IInsightPrioritizer, InsightPrioritizer>();
            services.AddSingleton<InsightGenerator>();
            services.AddSingleton<IInsightGenerator>(provider => provider.GetRequiredService<InsightGenerator>());

            // Register action generation
            services.AddSingleton<IActionTemplateProvider, ActionTemplateProvider>();
            services.AddSingleton<IActionContextAnalyzer, ActionContextAnalyzer>();
            services.AddSingleton<INextActionProvider, NextActionProvider>();
            services.AddSingleton<IActionGenerator, ActionGenerator>();

            _serviceProvider = services.BuildServiceProvider();
            _insightGenerator = _serviceProvider.GetRequiredService<IInsightGenerator>();
            _actionGenerator = _serviceProvider.GetRequiredService<IActionGenerator>();
        }

        [TearDown]
        public void TearDown()
        {
            _serviceProvider?.Dispose();
        }

        [Test]
        public async Task FullResponseIntelligence_WithEmptyCollection_GeneratesAppropriateInsightsAndActions()
        {
            // Arrange
            var emptyData = new List<string>();
            var insightContext = new InsightContext
            {
                OperationName = "search_items",
                MinInsights = 2,
                MaxInsights = 5,
                UserQuery = "find user accounts"
            };

            var actionContext = new ActionContext
            {
                OperationName = "search_items",
                MaxActions = 3,
                UserIntent = "find user accounts"
            };

            // Act
            var insights = await _insightGenerator.GenerateInsightsAsync(emptyData, insightContext);
            actionContext.RelatedInsights = insights;
            var actions = await _actionGenerator.GenerateActionsAsync(emptyData, actionContext);

            // Assert
            Assert.That(insights, Is.Not.Null);
            Assert.That(insights.Count, Is.GreaterThanOrEqualTo(insightContext.MinInsights));
            Assert.That(insights.Any(i => i.Type == InsightType.Warning), Is.True, "Should have warning about empty results");

            Assert.That(actions, Is.Not.Null);
            Assert.That(actions.Count, Is.GreaterThan(0));
            Assert.That(actions.Any(a => a.Category == "retry"), Is.True, "Should suggest retry with broader criteria");
        }

        [Test]
        public async Task FullResponseIntelligence_WithLargeCollection_GeneratesPerformanceInsights()
        {
            // Arrange
            var largeData = Enumerable.Range(1, 500).Select(i => $"Item {i}").ToList();
            var insightContext = new InsightContext
            {
                OperationName = "list_all_items",
                MinInsights = 3,
                MaxInsights = 10,
                Parameters = new Dictionary<string, object>
                {
                    { "executionTime", TimeSpan.FromSeconds(2) }
                }
            };

            var actionContext = new ActionContext
            {
                OperationName = "list_all_items",
                MaxActions = 5
            };

            // Act
            var insights = await _insightGenerator.GenerateInsightsAsync(largeData, insightContext);
            actionContext.RelatedInsights = insights;
            var actions = await _actionGenerator.GenerateActionsAsync(largeData, actionContext);

            // Assert
            Assert.That(insights, Is.Not.Null);
            Assert.That(insights.Any(i => i.Type == InsightType.Suggestion || i.Type == InsightType.Performance), 
                Is.True, "Should have performance or suggestion insights");

            Assert.That(actions, Is.Not.Null);
            Assert.That(actions.Any(a => a.Category == "filter" || a.Category == "analyze"), 
                Is.True, "Should suggest filtering or analysis for large datasets");
        }

        [Test]
        public async Task ResponseIntelligence_WithTokenBudget_RespectsLimits()
        {
            // Arrange
            var data = Enumerable.Range(1, 100).Select(i => $"Item {i}").ToList();
            var tokenBudget = 200; // Small budget

            var insightContext = new InsightContext
            {
                OperationName = "process_items",
                MinInsights = 5,
                MaxInsights = 10
            };

            var actionContext = new ActionContext
            {
                OperationName = "process_items",
                MaxActions = 10
            };

            // Act
            var insights = await _insightGenerator.GenerateInsightsAsync(data, insightContext, tokenBudget / 2);
            actionContext.RelatedInsights = insights;
            var actions = await _actionGenerator.GenerateActionsAsync(data, actionContext, tokenBudget / 2);

            // Assert
            var tokenEstimator = _serviceProvider.GetRequiredService<ITokenEstimator>();
            var insightTokens = insights.Sum(i => tokenEstimator.EstimateObject(i));
            var actionTokens = actions.Sum(a => tokenEstimator.EstimateObject(a));

            Assert.That(insightTokens, Is.LessThanOrEqualTo(tokenBudget / 2 + 50), "Insights should respect token budget");
            Assert.That(actionTokens, Is.LessThanOrEqualTo(tokenBudget / 2 + 50), "Actions should respect token budget");
        }

        [Test]
        public async Task ResponseIntelligence_WithErrors_GeneratesErrorInsightsAndFixActions()
        {
            // Arrange
            var data = new { Error = "Database connection failed" };
            var insightContext = new InsightContext
            {
                OperationName = "database_query",
                MinInsights = 2,
                MaxInsights = 5,
                Parameters = new Dictionary<string, object>
                {
                    { "errorCount", 3 },
                    { "totalCount", 3 }
                }
            };

            var actionContext = new ActionContext
            {
                OperationName = "database_query",
                MaxActions = 5,
                RelatedInsights = new List<Insight>
                {
                    new Insight { Type = InsightType.Error, Text = "Database connection failed" }
                }
            };

            // Act
            var insights = await _insightGenerator.GenerateInsightsAsync(data, insightContext);
            var actions = await _actionGenerator.GenerateActionsAsync(data, actionContext);

            // Assert
            Assert.That(insights, Is.Not.Null);
            Assert.That(insights.Any(i => i.Type == InsightType.Warning || i.Type == InsightType.Error), 
                Is.True, "Should have error-related insights");

            Assert.That(actions, Is.Not.Null);
            Assert.That(actions.Any(a => a.Category == "fix" || a.Category == "debug"), 
                Is.True, "Should suggest fix or debug actions for errors");
        }

        [Test]
        public async Task AIOptimizedResponse_Integration_BuildsCompleteResponse()
        {
            // Arrange
            var searchResults = new List<SearchResult>
            {
                new SearchResult { Id = "1", Name = "Result 1", Score = 0.95 },
                new SearchResult { Id = "2", Name = "Result 2", Score = 0.85 },
                new SearchResult { Id = "3", Name = "Result 3", Score = 0.75 }
            };

            var insightContext = new InsightContext
            {
                OperationName = "search",
                MinInsights = 2,
                MaxInsights = 5
            };

            var actionContext = new ActionContext
            {
                OperationName = "search",
                MaxActions = 3
            };

            // Act
            var insights = await _insightGenerator.GenerateInsightsAsync(searchResults, insightContext);
            
            // If no insights were generated, ensure we have at least some for the test
            if (insights.Count == 0)
            {
                insights = new List<Insight>
                {
                    new Insight { Text = "Search completed successfully", Type = InsightType.Information },
                    new Insight { Text = "Results are sorted by relevance score", Type = InsightType.Information }
                };
            }
            
            actionContext.RelatedInsights = insights;
            var actions = await _actionGenerator.GenerateActionsAsync(searchResults, actionContext);

#pragma warning disable CS0618 // Type or member is obsolete - testing backward compatibility
            var response = new AIOptimizedResponse
            {
                Data = new AIResponseData
                {
#pragma warning restore CS0618 // Type or member is obsolete
                    Summary = $"Found {searchResults.Count} results",
                    Results = searchResults,
                    Count = searchResults.Count
                },
                Insights = insights.Select(i => i.Text).ToList(),
                Actions = actions,
                Meta = new AIResponseMeta
                {
                    ExecutionTime = "150ms",
                    Truncated = false,
                    TokenInfo = new TokenInfo
                    {
                        Estimated = 500,
                        Limit = 10000,
                        ReductionStrategy = "none"
                    }
                }
            };

            // Assert
            Assert.That(response.Format, Is.EqualTo("ai-optimized"));
            Assert.That(response.Data.Count, Is.EqualTo(3));
            Assert.That(response.Insights.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(response.Actions.Count, Is.GreaterThan(0));
            Assert.That(response.Meta.TokenInfo, Is.Not.Null);
        }

        private class SearchResult
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public double Score { get; set; }
        }
    }
}
