using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using COA.Mcp.Framework.TokenOptimization;
using COA.Mcp.Framework.TokenOptimization.Intelligence;
using COA.Mcp.Framework.TokenOptimization.Models;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace COA.Mcp.Framework.TokenOptimization.Tests.Intelligence
{
    [TestFixture]
    public class InsightGeneratorTests
    {
        private Mock<IInsightTemplateProvider> _templateProviderMock = null!;
        private Mock<IInsightPrioritizer> _prioritizerMock = null!;
        private Mock<ITokenEstimator> _tokenEstimatorMock = null!;
        private Mock<ILogger<InsightGenerator>> _loggerMock = null!;
        private InsightGenerator _insightGenerator = null!;

        [SetUp]
        public void SetUp()
        {
            _templateProviderMock = new Mock<IInsightTemplateProvider>();
            _prioritizerMock = new Mock<IInsightPrioritizer>();
            _tokenEstimatorMock = new Mock<ITokenEstimator>();
            _loggerMock = new Mock<ILogger<InsightGenerator>>();

            _insightGenerator = new InsightGenerator(
                _templateProviderMock.Object,
                _prioritizerMock.Object,
                _tokenEstimatorMock.Object,
                _loggerMock.Object);
        }

        [Test]
        public async Task GenerateInsightsAsync_WithTemplates_ReturnsInsights()
        {
            // Arrange
            var data = new List<string> { "item1", "item2" };
            var context = new InsightContext
            {
                OperationName = "test_operation",
                MinInsights = 2,
                MaxInsights = 5
            };

            var template = new Mock<IInsightTemplate>();
            template.Setup(t => t.GenerateAsync(It.IsAny<object>(), It.IsAny<InsightContext>()))
                .ReturnsAsync(new Insight { Text = "Test insight", Type = InsightType.Information });

            _templateProviderMock.Setup(p => p.GetTemplatesAsync(It.IsAny<Type>(), It.IsAny<InsightContext>()))
                .ReturnsAsync(new[] { template.Object });

            _prioritizerMock.Setup(p => p.PrioritizeAsync(It.IsAny<List<Insight>>(), It.IsAny<InsightContext>()))
                .ReturnsAsync((List<Insight> insights, InsightContext _) => insights);

            // Act
            var result = await _insightGenerator.GenerateInsightsAsync(data, context);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(result[0].Text, Is.EqualTo("Test insight"));
        }

        [Test]
        public async Task GenerateInsightsAsync_WithNoTemplates_ReturnsDefaultInsights()
        {
            // Arrange
            var data = new List<string> { "item1", "item2" };
            var context = new InsightContext
            {
                OperationName = "test_operation",
                MinInsights = 2,
                MaxInsights = 5
            };

            _templateProviderMock.Setup(p => p.GetTemplatesAsync(It.IsAny<Type>(), It.IsAny<InsightContext>()))
                .ReturnsAsync(Enumerable.Empty<IInsightTemplate>());

            _prioritizerMock.Setup(p => p.PrioritizeAsync(It.IsAny<List<Insight>>(), It.IsAny<InsightContext>()))
                .ReturnsAsync((List<Insight> insights, InsightContext _) => insights);

            // Act
            var result = await _insightGenerator.GenerateInsightsAsync(data, context);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.GreaterThanOrEqualTo(context.MinInsights));
            Assert.That(result.Any(i => i.Type == InsightType.Information), Is.True);
        }

        [Test]
        public async Task GenerateInsightsAsync_WithTokenBudget_RespectsLimit()
        {
            // Arrange
            var data = new List<string> { "item1", "item2" };
            var context = new InsightContext
            {
                OperationName = "test_operation",
                MinInsights = 1,
                MaxInsights = 10
            };
            var tokenBudget = 100;

            var insights = new List<Insight>
            {
                new Insight { Text = "Insight 1", Importance = InsightImportance.High },
                new Insight { Text = "Insight 2", Importance = InsightImportance.Medium },
                new Insight { Text = "Insight 3", Importance = InsightImportance.Low }
            };

            _templateProviderMock.Setup(p => p.GetTemplatesAsync(It.IsAny<Type>(), It.IsAny<InsightContext>()))
                .ReturnsAsync(Enumerable.Empty<IInsightTemplate>());

            _prioritizerMock.Setup(p => p.PrioritizeAsync(It.IsAny<List<Insight>>(), It.IsAny<InsightContext>()))
                .ReturnsAsync(insights);

            _tokenEstimatorMock.Setup(e => e.EstimateObject(It.IsAny<Insight>(), null))
                .Returns(50); // Each insight is 50 tokens

            // Act
            var result = await _insightGenerator.GenerateInsightsAsync(data, context, tokenBudget);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(2)); // Only 2 insights fit within 100 token budget
        }

        [Test]
        public async Task CanHandle_WithSupportedType_ReturnsTrue()
        {
            // Arrange
            _templateProviderMock.Setup(p => p.HasTemplatesFor(typeof(string)))
                .Returns(true);

            // Act
            var result = _insightGenerator.CanHandle(typeof(string));

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task CanHandle_WithUnsupportedType_ReturnsFalse()
        {
            // Arrange
            _templateProviderMock.Setup(p => p.HasTemplatesFor(typeof(int)))
                .Returns(false);

            // Act
            var result = _insightGenerator.CanHandle(typeof(int));

            // Assert
            Assert.That(result, Is.False);
        }
    }
}