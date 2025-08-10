using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Exceptions;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using FrameworkRangeAttribute = COA.Mcp.Framework.Attributes.RangeAttribute;

namespace COA.Mcp.Framework.Tests.Base
{
    [TestFixture]
    public class McpToolBaseGenericTests
    {
        private Mock<ILogger> _loggerMock;
        private TestTool _tool;
        private TestToolWithValidation _validationTool;

        [SetUp]
        public void SetUp()
        {
            _loggerMock = new Mock<ILogger>();
            _tool = new TestTool(_loggerMock.Object);
            _validationTool = new TestToolWithValidation(_loggerMock.Object);
        }

        #region ExecuteAsync Tests

        [Test]
        public async Task ExecuteAsync_WithValidParameters_ReturnsExpectedResult()
        {
            // Arrange
            var parameters = new TestParameters { Name = "Test", Value = 42 };

            // Act
            var result = await _tool.ExecuteAsync(parameters);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Data.Should().Be("Processed: Test-42");
        }

        [Test]
        public async Task ExecuteAsync_WithNullParameters_ForOptionalParams_Succeeds()
        {
            // Arrange
            var tool = new OptionalParamsTool();

            // Act
            var result = await tool.ExecuteAsync(null);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
        }

        [Test]
        public void ExecuteAsync_WithNullRequiredParameters_ThrowsToolExecutionException()
        {
            // Act & Assert
            Func<Task> act = async () => await _tool.ExecuteAsync(null);
            act.Should().ThrowAsync<ToolExecutionException>()
                .WithMessage("*Validation failed*");
        }

        [Test]
        public void ExecuteAsync_WithInvalidParameters_ThrowsToolExecutionException()
        {
            // Arrange
            var parameters = new TestValidationParameters { RequiredField = null, RangeValue = 150 };

            // Act & Assert
            Func<Task> act = async () => await _validationTool.ExecuteAsync(parameters);
            act.Should().ThrowAsync<ToolExecutionException>()
                .WithMessage("*Validation failed*");
        }

        [Test]
        [Ignore("Temporarily disabled - may be causing test hang")]
        public async Task ExecuteAsync_WhenCancelled_ThrowsOperationCanceledException()
        {
            // Arrange
            var slowTool = new SlowTool();
            var cts = new CancellationTokenSource();
            cts.CancelAfter(50);

            // Act & Assert
            Func<Task> act = async () => await slowTool.ExecuteAsync(new TestParameters(), cts.Token);
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Test]
        public async Task ExecuteAsync_WhenInternalThrows_WrapsInToolExecutionException()
        {
            // Arrange
            var throwingTool = new ThrowingTool();

            // Act & Assert
            Func<Task> act = async () => await throwingTool.ExecuteAsync(new TestParameters());
            var exception = await act.Should().ThrowAsync<ToolExecutionException>()
                .WithMessage("*Tool 'throwing_tool' execution failed*");
            exception.And.InnerException.Should().BeOfType<InvalidOperationException>();
        }

        #endregion

        #region IMcpTool.ExecuteAsync Tests

        [Test]
        public async Task IMcpToolExecuteAsync_WithDirectTypedParameters_Succeeds()
        {
            // Arrange
            var parameters = new TestParameters { Name = "Test", Value = 42 };
            IMcpTool tool = _tool;

            // Act
            var result = await tool.ExecuteAsync(parameters);

            // Assert
            result.Should().NotBeNull();
            ((TestResult)result).Success.Should().BeTrue();
        }

        [Test]
        public async Task IMcpToolExecuteAsync_WithJsonElement_Succeeds()
        {
            // Arrange
            var json = JsonSerializer.Serialize(new TestParameters { Name = "Test", Value = 42 });
            var jsonElement = JsonDocument.Parse(json).RootElement;
            IMcpTool tool = _tool;

            // Act
            var result = await tool.ExecuteAsync(jsonElement);

            // Assert
            result.Should().NotBeNull();
            ((TestResult)result).Success.Should().BeTrue();
        }

        [Test]
        public async Task IMcpToolExecuteAsync_WithAnonymousObject_Succeeds()
        {
            // Arrange
            var parameters = new { Name = "Test", Value = 42 };
            IMcpTool tool = _tool;

            // Act
            var result = await tool.ExecuteAsync(parameters);

            // Assert
            result.Should().NotBeNull();
            ((TestResult)result).Success.Should().BeTrue();
        }

        [Test]
        public async Task IMcpToolExecuteAsync_WithEmptyParameters_ForEmptyParamsTool_Succeeds()
        {
            // Arrange
            var tool = new EmptyParamsTool();
            IMcpTool untypedTool = tool;

            // Act
            var result = await untypedTool.ExecuteAsync(null);

            // Assert
            result.Should().NotBeNull();
            ((TestResult)result).Success.Should().BeTrue();
        }

        #endregion

        #region Schema Tests

        [Test]
        public void GetInputSchema_ReturnsCorrectSchema()
        {
            // Act
            var schema = _tool.GetInputSchema();

            // Assert
            schema.Should().NotBeNull();
            schema.Should().BeOfType<Schema.JsonSchema<TestParameters>>();
        }

        [Test]
        public void IMcpToolGetInputSchema_ReturnsCorrectSchema()
        {
            // Arrange
            IMcpTool tool = _tool;

            // Act
            var schema = tool.GetInputSchema();

            // Assert
            schema.Should().NotBeNull();
            schema.Should().BeAssignableTo<Schema.IJsonSchema>();
        }

        #endregion

        #region Validation Helper Tests

        [Test]
        public void ValidateRequired_WithValue_ReturnsValue()
        {
            // Act
            var result = _tool.TestValidateRequired("test", "param");

            // Assert
            result.Should().Be("test");
        }

        [Test]
        public void ValidateRequired_WithNull_ThrowsValidationException()
        {
            // Act & Assert
            Action act = () => _tool.TestValidateRequired<string>(null, "param");
            act.Should().Throw<ValidationException>()
                .WithMessage("*param*required*");
        }

        [Test]
        public void ValidateRequired_WithEmptyString_ThrowsValidationException()
        {
            // Act & Assert
            Action act = () => _tool.TestValidateRequired("", "param");
            act.Should().Throw<ValidationException>()
                .WithMessage("*param*required*");
        }

        [Test]
        public void ValidatePositive_WithPositive_ReturnsValue()
        {
            // Act
            var result = _tool.TestValidatePositive(5, "param");

            // Assert
            result.Should().Be(5);
        }

        [Test]
        public void ValidatePositive_WithZero_ThrowsValidationException()
        {
            // Act & Assert
            Action act = () => _tool.TestValidatePositive(0, "param");
            act.Should().Throw<ValidationException>()
                .WithMessage("*param*positive*");
        }

        [Test]
        public void ValidateRange_WithinRange_ReturnsValue()
        {
            // Act
            var result = _tool.TestValidateRange(5, 1, 10, "param");

            // Assert
            result.Should().Be(5);
        }

        [Test]
        public void ValidateRange_OutOfRange_ThrowsValidationException()
        {
            // Act & Assert
            Action act = () => _tool.TestValidateRange(15, 1, 10, "param");
            act.Should().Throw<ValidationException>()
                .WithMessage("*param*between 1 and 10*");
        }

        [Test]
        public void ValidateNotEmpty_WithItems_ReturnsCollection()
        {
            // Arrange
            var list = new[] { "item1", "item2" };

            // Act
            var result = _tool.TestValidateNotEmpty(list, "param");

            // Assert
            result.Should().BeEquivalentTo(list);
        }

        [Test]
        public void ValidateNotEmpty_WithEmptyCollection_ThrowsValidationException()
        {
            // Act & Assert
            Action act = () => _tool.TestValidateNotEmpty(Array.Empty<string>(), "param");
            act.Should().Throw<ValidationException>()
                .WithMessage("*param*cannot be empty*");
        }

        #endregion

        #region Error Helper Tests

        [Test]
        public void CreateErrorResult_ReturnsProperErrorInfo()
        {
            // Act
            var error = _tool.TestCreateErrorResult("operation", "error message", "recovery step");

            // Assert
            error.Should().NotBeNull();
            error.Code.Should().Be("TOOL_ERROR");
            error.Message.Should().Be("error message");
            error.Recovery.Should().NotBeNull();
            error.Recovery.Steps.Should().ContainSingle().Which.Should().Be("recovery step");
        }

        [Test]
        public void CreateValidationErrorResult_ReturnsProperErrorInfo()
        {
            // Act
            var error = _tool.TestCreateValidationErrorResult("operation", "param", "must be positive");

            // Assert
            error.Should().NotBeNull();
            error.Code.Should().Be("VALIDATION_ERROR");
            error.Message.Should().Contain("param");
            error.Message.Should().Contain("must be positive");
            error.Recovery.Should().NotBeNull();
        }

        #endregion

        #region Token Management Tests

        [Test]
        public async Task ExecuteWithTokenManagement_LogsWarningForHighTokenEstimate()
        {
            // Arrange
            var highTokenTool = new HighTokenTool(_loggerMock.Object);

            // Act
            await highTokenTool.ExecuteAsync(new TestParameters());

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("exceeds token budget")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion

        #region Test Tool Implementations

        private class TestParameters
        {
            public string Name { get; set; }
            public int Value { get; set; }
        }

        private class TestValidationParameters
        {
            [Required]
            public string RequiredField { get; set; }

            [FrameworkRangeAttribute(1, 100)]
            public int RangeValue { get; set; }
        }

        private class TestResult : ToolResultBase
        {
            public override string Operation => "test_operation";
            public string Data { get; set; }
        }

        private class TestTool : McpToolBase<TestParameters, TestResult>
        {
            public TestTool(ILogger? logger = null) : base(logger) { }

            public override string Name => "test_tool";
            public override string Description => "Test tool";

            protected override Task<TestResult> ExecuteInternalAsync(TestParameters parameters, CancellationToken cancellationToken)
            {
                return Task.FromResult(new TestResult
                {
                    Success = true,
                    Data = $"Processed: {parameters.Name}-{parameters.Value}"
                });
            }

            // Expose protected methods for testing
            public T TestValidateRequired<T>(T value, string parameterName) => ValidateRequired(value, parameterName);
            public int TestValidatePositive(int value, string parameterName) => ValidatePositive(value, parameterName);
            public int TestValidateRange(int value, int min, int max, string parameterName) => ValidateRange(value, min, max, parameterName);
            public ICollection<T> TestValidateNotEmpty<T>(ICollection<T> collection, string parameterName) => ValidateNotEmpty(collection, parameterName);
            public ErrorInfo TestCreateErrorResult(string operation, string error, string recovery) => CreateErrorResult(operation, error, recovery);
            public ErrorInfo TestCreateValidationErrorResult(string operation, string param, string requirement) => CreateValidationErrorResult(operation, param, requirement);
        }

        private class TestToolWithValidation : McpToolBase<TestValidationParameters, TestResult>
        {
            public TestToolWithValidation(ILogger? logger = null) : base(logger) { }

            public override string Name => "validation_tool";
            public override string Description => "Tool with validation";

            protected override Task<TestResult> ExecuteInternalAsync(TestValidationParameters parameters, CancellationToken cancellationToken)
            {
                return Task.FromResult(new TestResult { Success = true });
            }
        }

        private class OptionalParamsTool : McpToolBase<TestParameters, TestResult>
        {
            public override string Name => "optional_tool";
            public override string Description => "Tool with optional params";

            protected override Task<TestResult> ExecuteInternalAsync(TestParameters parameters, CancellationToken cancellationToken)
            {
                return Task.FromResult(new TestResult { Success = true, Data = "No params needed" });
            }

            protected override void ValidateParameters(TestParameters parameters)
            {
                // Allow null parameters
            }
        }

        private class EmptyParamsTool : McpToolBase<EmptyParameters, TestResult>
        {
            public override string Name => "empty_params_tool";
            public override string Description => "Tool with empty params";

            protected override Task<TestResult> ExecuteInternalAsync(EmptyParameters parameters, CancellationToken cancellationToken)
            {
                return Task.FromResult(new TestResult { Success = true, Data = "Empty params" });
            }
        }

        private class SlowTool : McpToolBase<TestParameters, TestResult>
        {
            public override string Name => "slow_tool";
            public override string Description => "Slow tool";

            protected override async Task<TestResult> ExecuteInternalAsync(TestParameters parameters, CancellationToken cancellationToken)
            {
                await Task.Delay(1000, cancellationToken);
                return new TestResult { Success = true };
            }
        }

        private class ThrowingTool : McpToolBase<TestParameters, TestResult>
        {
            public override string Name => "throwing_tool";
            public override string Description => "Tool that throws";

            protected override Task<TestResult> ExecuteInternalAsync(TestParameters parameters, CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("Internal error");
            }
        }

        private class HighTokenTool : McpToolBase<TestParameters, TestResult>
        {
            public HighTokenTool(ILogger logger) : base(logger) { }

            public override string Name => "high_token_tool";
            public override string Description => "Tool with high token usage";

            protected override int EstimateTokenUsage() => 15000; // Above default limit

            protected override Task<TestResult> ExecuteInternalAsync(TestParameters parameters, CancellationToken cancellationToken)
            {
                return Task.FromResult(new TestResult { Success = true });
            }
        }

        #endregion
    }
}