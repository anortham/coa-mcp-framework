using COA.Mcp.Framework;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Schema;
using COA.Mcp.Framework.Testing.Assertions;
using COA.Mcp.Framework.Testing.Builders;
using COA.Mcp.Framework.TokenOptimization;
using COA.Mcp.Framework.TokenOptimization.Models;
using COA.Mcp.Framework.Models;
using FluentAssertions;
using FluentAssertions.Execution;
using NUnit.Framework;
using System.Collections.Generic;
using TestingInsightBuilder = COA.Mcp.Framework.Testing.Builders.InsightBuilder;
using System.Linq;
using System.Threading.Tasks;

namespace COA.Mcp.Framework.Testing.Tests.Examples
{
    /// <summary>
    /// Examples demonstrating the custom fluent assertions.
    /// </summary>
    [TestFixture]
    public class FluentAssertionExamples
    {
        [Test]
        public void Tool_Assertions()
        {
            // Arrange
            var tool = new WeatherTool(null!, null!);

            // Act & Assert - Tool properties
            tool.Should()
                .HaveName("get_weather")
                .And.BeInCategory(ToolCategory.Query)
                .And.HaveDescription()
                .And.HaveDescriptionContaining("weather");
        }

        [Test]
        public void ToolMetadata_Assertions()
        {
            // Arrange
            var metadata = new ToolMetadata
            {
                Name = "test_tool",
                Description = "Test tool",
                Category = ToolCategory.Analysis,
                Enabled = true,
                Parameters = new ParameterSchema
                {
                    Properties = new Dictionary<string, ParameterProperty>
                    {
                        ["param1"] = new ParameterProperty { Name = "param1", Type = "string", Required = true },
                        ["param2"] = new ParameterProperty { Name = "param2", Type = "int", Required = false }
                    }
                }
            };

            // Act & Assert
            metadata.Should()
                .BeEnabled()
                .And.HaveParameterCount(2)
                .And.HaveParameter("param1")
                .And.HaveParameter("param2");
        }

        [Test]
        public void Token_Assertions()
        {
            // Token count assertions
            var shortText = "Hello, World!";
            var longText = new TestDataGenerator().GenerateLorem(1000);

            // String token assertions
            var shortTextTokens = shortText.HaveTokenCount(4); // "Hello, World!" is 4 tokens
            shortTextTokens.BeWithinSafetyLimit();
            shortTextTokens.BeEfficient(shortText.Length);

            // Object token assertions
            var complexObject = new
            {
                Name = "Test",
                Values = new[] { 1, 2, 3, 4, 5 },
                Nested = new { Deep = "Value" }
            };

            var complexObjectTokens = complexObject.HaveTokenCountLessThan(100);
            complexObjectTokens.BeCloseTo(23, tolerancePercentage: 15); // Adjusted based on actual estimation

            // Token estimation accuracy
            complexObject.TokenEstimation()
                .BeAccurate(actualTokens: 23, tolerancePercentage: 15);
        }

        [Test]
        public void Response_Assertions()
        {
            // Successful response
            var successResponse = ResponseBuilder.Success(new { data = "test" });
            
            successResponse.Should()
                .BeSuccessful()
                .And.NotBeTruncated()
                .And.BeTokenOptimized()
                .And.HaveInsightCount(1);

            // Truncated response
            var truncatedResponse = ResponseBuilder.LargeResultSet(
                new[] { 1, 2, 3, 4, 5 },
                totalCount: 100,
                resourceUri: "resource://data/full");

            truncatedResponse.Should()
                .BeSuccessful()
                .And.BeTruncated()
                .And.HaveResourceUri()
                .And.HaveTruncationMessage()
                .And.HaveInsightContaining("truncated")
                .And.HaveNextAction("get_more_results");

            // Error response
            var errorResponse = ResponseBuilder.Error("Something went wrong", "Retry operation");
            
            errorResponse.Should()
                .BeTokenOptimized()
                .And.HaveInsightContaining("Error occurred")
                .And.HaveNextAction("retry");
        }

        [Test]
        public void Insight_Assertions()
        {
            // Single insight
            var criticalInsight = TestingInsightBuilder.Critical("System is experiencing high load");
            
            criticalInsight.Should()
                .HaveImportance(InsightImportance.Critical)
                .And.ContainText("high load");

            // Contextual insight
            var contextualInsight = new TestingInsightBuilder()
                .WithContent("Performance degradation detected")
                .WithImportance(InsightImportance.High)
                .WithContext("During peak hours")
                .WithMetadata("affectedServices", new[] { "API", "Database" })
                .Build();

            contextualInsight.Should()
                .BeContextual()
                .And.HaveMetadata("context")
                .And.HaveMetadata("affectedServices");

            // Collection of insights
            var insights = new List<Insight>
            {
                TestingInsightBuilder.Critical("Critical issue"),
                new Insight { Text = "High priority", Importance = InsightImportance.High },
                new Insight { Text = "Medium priority", Importance = InsightImportance.Medium },
                new Insight { Text = "Low priority", Importance = InsightImportance.Low }
            };

            insights.Should()
                .AllHaveMinimumImportance(InsightImportance.Low)
                .And.ContainInsightAbout("Critical issue")
                .And.HaveCount(4);

            // Prioritized insights
            var prioritizedInsights = insights.OrderByDescending(i => i.Importance).ToList();
            
            prioritizedInsights.Should()
                .BePrioritized();
        }

        [Test]
        public void Action_Assertions()
        {
            // Basic action
            var action = new AIAction
            {
                Tool = "analyze_code",
                Description = "Analyze code quality",
                Parameters = new Dictionary<string, object> { ["path"] = "/src", ["depth"] = 3 },
                Rationale = "Code quality check is overdue",
                Category = "analysis"
            };

            action.Should()
                .UseTool("analyze_code")
                .And.HaveRationale()
                .And.BeInCategory("analysis")
                .And.HaveValidParameters();

            // Action without rationale
            var simpleAction = new AIAction
            {
                Tool = "retry",
                Description = "Retry operation",
                Parameters = new Dictionary<string, object>()
            };

            simpleAction.Invoking(a => a.Should().HaveRationale())
                .Should().Throw<AssertionException>();
        }

        [Test]
        public void Combined_Assertions_Scenario()
        {
            // Build a complex response
            var response = new ResponseBuilder()
                .WithSummary("Analysis complete")
                .WithResults(new
                {
                    Files = 150,
                    Issues = 12,
                    Coverage = 85.5
                })
                .WithInsightObjects(
                    TestingInsightBuilder.Critical("12 critical issues found"),
                    new TestingInsightBuilder()
                        .WithContent("Code coverage at 85.5%")
                        .WithImportance(InsightImportance.High)
                        .WithMetadata("threshold", 80)
                        .Build(),
                    new Insight { Text = "Consider refactoring", Importance = InsightImportance.Medium }
                )
                .WithAction("fix_issues", "Fix critical issues first", new { priority = "critical" })
                .WithAction("improve_coverage", "Increase test coverage", new { target = 90 })
                .WithExecutionTime(250)
                .Build();

            // Multiple assertions
            response.Should()
                .BeSuccessful()
                .And.NotBeTruncated()
                .And.HaveInsightCount(3)
                .And.HaveNextAction("fix_issues")
                .And.HaveNextAction("improve_coverage");

            // Token assertions
            // Token assertions are separate chains
            response.Should().HaveTokenCountLessThan(1000);
            var responseTokens = TokenEstimator.EstimateObject(response);
            TokenAssertionExtensions.Should(responseTokens).BeWithinSafetyLimit(TokenSafetyMode.Conservative);

            // Insight collection assertions
            response.Data.Results.Should().NotBeNull();
            
            var insights = response.Insights
                .Select(content => new Insight { Text = content })
                .ToList();
                
            insights.Should()
                .ContainInsightAbout("critical issues")
                .And.ContainInsightAbout("coverage");
        }

        [Test]
        public void ToolCollection_Assertions()
        {
            // Arrange
            var tools = new List<IMcpTool>
            {
                new WeatherTool(null!, null!),
                new MockTool("analyze_code", ToolCategory.Analysis),
                new MockTool("format_code", ToolCategory.Refactoring)
            };

            // Act & Assert
            tools.Should().HaveCount(3);
            tools.Should().Contain(t => t.Name == "get_weather");
            tools.Should().Contain(t => t.Name == "analyze_code");

            // Find specific tool and assert
            var weatherTool = tools.Should().ContainToolNamed("get_weather");
            weatherTool.Should()
                .BeInCategory(ToolCategory.Query);
        }

        // Helper mock tool
        private class MockTool : IMcpTool
        {
            public string Name { get; }
            public string Description => $"Mock {Name} tool";
            public ToolCategory Category { get; }
            public Type ParameterType => typeof(object);
            public Type ResultType => typeof(object);

            public MockTool(string name, ToolCategory category)
            {
                Name = name;
                Category = category;
            }

            public IJsonSchema GetInputSchema()
            {
                var schema = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>()
                };
                return new RuntimeJsonSchema(schema);
            }

            public Task<object?> ExecuteAsync(object? parameters, CancellationToken cancellationToken = default) 
                => Task.FromResult<object?>(new { });
        }
    }
}