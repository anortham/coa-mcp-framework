using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using COA.Mcp.Framework.TokenOptimization;
using COA.Mcp.Framework.TokenOptimization.Actions;
using COA.Mcp.Framework.TokenOptimization.Models;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace COA.Mcp.Framework.TokenOptimization.Tests.Actions
{
    [TestFixture]
    public class ActionGeneratorTests
    {
        private Mock<IActionTemplateProvider> _templateProviderMock = null!;
        private Mock<INextActionProvider> _nextActionProviderMock = null!;
        private Mock<ITokenEstimator> _tokenEstimatorMock = null!;
        private Mock<ILogger<ActionGenerator>> _loggerMock = null!;
        private ActionGenerator _actionGenerator = null!;

        [SetUp]
        public void SetUp()
        {
            _templateProviderMock = new Mock<IActionTemplateProvider>();
            _nextActionProviderMock = new Mock<INextActionProvider>();
            _tokenEstimatorMock = new Mock<ITokenEstimator>();
            _loggerMock = new Mock<ILogger<ActionGenerator>>();

            _actionGenerator = new ActionGenerator(
                _templateProviderMock.Object,
                _nextActionProviderMock.Object,
                _tokenEstimatorMock.Object,
                _loggerMock.Object);
        }

        [Test]
        public async Task GenerateActionsAsync_WithTemplates_ReturnsActions()
        {
            // Arrange
            var data = new List<string> { "item1", "item2" };
            var context = new ActionContext
            {
                OperationName = "test_operation",
                MaxActions = 5
            };

            var template = new Mock<IActionTemplate>();
            template.Setup(t => t.GenerateAsync(It.IsAny<object>(), It.IsAny<ActionContext>()))
                .ReturnsAsync(new AIAction 
                { 
                    Action = "test_action", 
                    Description = "Test action",
                    Category = "test"
                });

            _templateProviderMock.Setup(p => p.GetTemplatesAsync(It.IsAny<Type>(), It.IsAny<ActionContext>()))
                .ReturnsAsync(new[] { template.Object });

            _nextActionProviderMock.Setup(p => p.GetNextActionsAsync(It.IsAny<List<string>>(), It.IsAny<ActionContext>()))
                .ReturnsAsync(new List<AIAction>());

            // Act
            var result = await _actionGenerator.GenerateActionsAsync(data, context);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(result[0].Action, Is.EqualTo("test_action"));
        }

        [Test]
        public async Task GenerateActionsAsync_WithNextActions_IncludesNextActions()
        {
            // Arrange
            var data = new List<string> { "item1", "item2" };
            var context = new ActionContext
            {
                OperationName = "test_operation",
                MaxActions = 5
            };

            var nextActions = new List<AIAction>
            {
                new AIAction 
                { 
                    Action = "next_action", 
                    Description = "Next suggested action",
                    Category = "navigate"
                }
            };

            _templateProviderMock.Setup(p => p.GetTemplatesAsync(It.IsAny<Type>(), It.IsAny<ActionContext>()))
                .ReturnsAsync(Enumerable.Empty<IActionTemplate>());

            _nextActionProviderMock.Setup(p => p.GetNextActionsAsync(It.IsAny<List<string>>(), It.IsAny<ActionContext>()))
                .ReturnsAsync(nextActions);

            // Act
            var result = await _actionGenerator.GenerateActionsAsync(data, context);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Any(a => a.Action == "next_action"), Is.True);
        }

        [Test]
        public async Task GenerateActionsAsync_WithDuplicates_RemovesDuplicates()
        {
            // Arrange
            var data = new List<string> { "item1", "item2" };
            var context = new ActionContext
            {
                OperationName = "test_operation",
                MaxActions = 5
            };

            var duplicateAction = new AIAction 
            { 
                Action = "duplicate_action", 
                Description = "Duplicate action",
                Category = "test"
            };

            var template1 = new Mock<IActionTemplate>();
            template1.Setup(t => t.GenerateAsync(It.IsAny<object>(), It.IsAny<ActionContext>()))
                .ReturnsAsync(duplicateAction);

            var template2 = new Mock<IActionTemplate>();
            template2.Setup(t => t.GenerateAsync(It.IsAny<object>(), It.IsAny<ActionContext>()))
                .ReturnsAsync(duplicateAction);

            _templateProviderMock.Setup(p => p.GetTemplatesAsync(It.IsAny<Type>(), It.IsAny<ActionContext>()))
                .ReturnsAsync(new[] { template1.Object, template2.Object });

            _nextActionProviderMock.Setup(p => p.GetNextActionsAsync(It.IsAny<List<string>>(), It.IsAny<ActionContext>()))
                .ReturnsAsync(new List<AIAction>());

            // Act
            var result = await _actionGenerator.GenerateActionsAsync(data, context);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count(a => a.Action == "duplicate_action"), Is.EqualTo(1));
        }

        [Test]
        public async Task GenerateActionsAsync_WithMaxActions_RespectsLimit()
        {
            // Arrange
            var data = new List<string> { "item1", "item2" };
            var context = new ActionContext
            {
                OperationName = "test_operation",
                MaxActions = 2
            };

            var actions = new List<AIAction>();
            for (int i = 0; i < 5; i++)
            {
                actions.Add(new AIAction 
                { 
                    Action = $"action_{i}", 
                    Description = $"Action {i}",
                    Category = "test"
                });
            }

            _templateProviderMock.Setup(p => p.GetTemplatesAsync(It.IsAny<Type>(), It.IsAny<ActionContext>()))
                .ReturnsAsync(Enumerable.Empty<IActionTemplate>());

            _nextActionProviderMock.Setup(p => p.GetNextActionsAsync(It.IsAny<List<string>>(), It.IsAny<ActionContext>()))
                .ReturnsAsync(actions);

            // Act
            var result = await _actionGenerator.GenerateActionsAsync(data, context);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(context.MaxActions));
        }

        [Test]
        public async Task GenerateActionsAsync_WithTokenBudget_RespectsLimit()
        {
            // Arrange
            var data = new List<string> { "item1", "item2" };
            var context = new ActionContext
            {
                OperationName = "test_operation",
                MaxActions = 10
            };
            var tokenBudget = 100;

            var actions = new List<AIAction>
            {
                new AIAction { Action = "action1", Description = "Action 1" },
                new AIAction { Action = "action2", Description = "Action 2" },
                new AIAction { Action = "action3", Description = "Action 3" }
            };

            _templateProviderMock.Setup(p => p.GetTemplatesAsync(It.IsAny<Type>(), It.IsAny<ActionContext>()))
                .ReturnsAsync(Enumerable.Empty<IActionTemplate>());

            _nextActionProviderMock.Setup(p => p.GetNextActionsAsync(It.IsAny<List<string>>(), It.IsAny<ActionContext>()))
                .ReturnsAsync(actions);

            _tokenEstimatorMock.Setup(e => e.EstimateObject(It.IsAny<AIAction>(), null))
                .Returns(50); // Each action is 50 tokens

            // Act
            var result = await _actionGenerator.GenerateActionsAsync(data, context, tokenBudget);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(2)); // Only 2 actions fit within 100 token budget
        }
    }
}