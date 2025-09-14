using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Configuration;
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
        #region UTF-8 Encoding Tests

        private static readonly JsonSerializerOptions Utf8JsonOptions = new()
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        [Test]
        public async Task ExecuteAsync_WithEmojiInJsonElementParameters_PreservesUtf8Encoding()
        {
            // Arrange
            var emojiTestTool = new EmojiTestTool();
            IMcpTool untypedTool = emojiTestTool; // Use untyped interface to trigger JSON deserialization
            
            // Create JSON with emojis exactly like MCP would send
            var jsonString = """
                {
                    "content": "📊 **Daily Standup Report**\n✅ Completed tasks\n📈 Progress metrics\n⏳ In progress work\n💡 Key insights"
                }
                """;
            
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonString, Utf8JsonOptions);

            // Act - Use untyped interface to trigger deserialization path
            var result = await untypedTool.ExecuteAsync(jsonElement);

            // Assert
            result.Should().NotBeNull();
            var typedResult = (EmojiTestResult)result;
            typedResult.Success.Should().BeTrue();
            typedResult.ProcessedContent.Should().NotBeNull();
            
            // Verify emojis were NOT corrupted during deserialization
            typedResult.ProcessedContent.Should().Contain("📊", "chart emoji should be preserved");
            typedResult.ProcessedContent.Should().Contain("✅", "checkmark emoji should be preserved");
            typedResult.ProcessedContent.Should().Contain("📈", "trending up emoji should be preserved");
            typedResult.ProcessedContent.Should().Contain("⏳", "hourglass emoji should be preserved");
            typedResult.ProcessedContent.Should().Contain("💡", "light bulb emoji should be preserved");
            
            // Verify NO corruption patterns appear
            typedResult.ProcessedContent.Should().NotContain("╬ô├½├¡Γò₧├åΓö£ΓöñΓö£┬┐", "should not contain UTF-8/Windows-1252 corruption");
            typedResult.ProcessedContent.Should().NotContain("Γò¼├┤Γö¼├║Γö£├í", "should not contain UTF-8/Windows-1252 corruption");
            typedResult.ProcessedContent.Should().NotContain("╬ô├½├¡Γò₧├åΓö£ΓöñΓö£┬¼", "should not contain UTF-8/Windows-1252 corruption");
            typedResult.ProcessedContent.Should().NotContain("╬ô├½├¡Γò₧├åΓö£ΓòóΓö£├▒", "should not contain UTF-8/Windows-1252 corruption");
            typedResult.ProcessedContent.Should().NotContain("╬ô├½├¡Γò₧├åΓö£├ÑΓö£┬í", "should not contain UTF-8/Windows-1252 corruption");
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

        private class EmojiTestParameters
        {
            public string Content { get; set; }
        }

        private class EmojiTestResult : ToolResultBase
        {
            public override string Operation => "emoji_test";
            public string ProcessedContent { get; set; }
        }
        private class EmojiTestTool : McpToolBase<EmojiTestParameters, EmojiTestResult>
        {
            public override string Name => "emoji_test_tool";
            public override string Description => "Test tool for UTF-8 emoji handling";

            protected override Task<EmojiTestResult> ExecuteInternalAsync(EmojiTestParameters parameters, CancellationToken cancellationToken)
            {
                return Task.FromResult(new EmojiTestResult
                {
                    Success = true,
                    ProcessedContent = parameters.Content // Simply echo the content to test deserialization
                });
            }
        }

        [Test]
        public async Task ExecuteAsync_WithComplexEmojiSequences_PreservesUtf8Encoding()
        {
            // Arrange
            var emojiTestTool = new EmojiTestTool();
            
            // Test with various emoji types and Unicode sequences
            var jsonString = """
                {
                    "content": "📊 Target: 🚀 Launch\n👨‍💻 Developer: 🔧 Tools\n📚 Documentation: ⭐ Quality"
                }
                """;
            
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonString, Utf8JsonOptions);

            // Act
            IMcpTool untypedTool = emojiTestTool;
            var result = await untypedTool.ExecuteAsync(jsonElement);
            var typedResult = (EmojiTestResult)result;

            // Assert
            typedResult.Should().NotBeNull();
            typedResult.Success.Should().BeTrue();
            typedResult.ProcessedContent.Should().NotBeNull();
            
            // Verify complex emoji sequences are preserved
            typedResult.ProcessedContent.Should().Contain("📊", "direct hit emoji should be preserved");
            typedResult.ProcessedContent.Should().Contain("🚀", "rocket emoji should be preserved");
            typedResult.ProcessedContent.Should().Contain("👨‍💻", "man technologist emoji sequence should be preserved");
            typedResult.ProcessedContent.Should().Contain("🔧", "wrench emoji should be preserved");
            typedResult.ProcessedContent.Should().Contain("📚", "books emoji should be preserved");
            typedResult.ProcessedContent.Should().Contain("⭐", "star emoji should be preserved");
        }

        [Test]
        public async Task ExecuteAsync_WithMixedUtf8Characters_PreservesAllEncoding()
        {
            // Arrange
            var emojiTestTool = new EmojiTestTool();
            
            // Mix emojis with other UTF-8 characters
            var jsonString = """
                {
                    "content": "Résumé: 📊 Données financières\nÄlärem: 🔔 Notification\nNaïve café: ☕ Français"
                }
                """;
            
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonString, Utf8JsonOptions);

            // Act
            IMcpTool untypedTool = emojiTestTool;
            var result = await untypedTool.ExecuteAsync(jsonElement);
            var typedResult = (EmojiTestResult)result;

            // Assert
            typedResult.Should().NotBeNull();
            typedResult.Success.Should().BeTrue();
            typedResult.ProcessedContent.Should().NotBeNull();
            
            // Verify both emojis and accented characters are preserved
            typedResult.ProcessedContent.Should().Contain("Résumé", "accented characters should be preserved");
            typedResult.ProcessedContent.Should().Contain("📊", "chart emoji should be preserved");
            typedResult.ProcessedContent.Should().Contain("Älärem", "German umlauts should be preserved");
            typedResult.ProcessedContent.Should().Contain("🔔", "bell emoji should be preserved");
            typedResult.ProcessedContent.Should().Contain("Naïve café", "French accents should be preserved");
            typedResult.ProcessedContent.Should().Contain("☕", "coffee emoji should be preserved");
            typedResult.ProcessedContent.Should().Contain("Français", "French characters should be preserved");
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
            var loggerMock = new Mock<ILogger>();
            loggerMock.Setup(x => x.IsEnabled(LogLevel.Warning)).Returns(true);
            var highTokenTool = new HighTokenTool(loggerMock.Object);

            // Act
            await highTokenTool.ExecuteAsync(new TestParameters());

            // Assert
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Token budget exceeded")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Test]
        public void EstimateTokenUsage_WithoutParameters_ReturnsBasedOnCategory()
        {
            // Arrange
            var queryTool = new QueryCategoryTool();
            var analysisTool = new AnalysisCategoryTool();
            var utilityTool = new UtilityCategoryTool();

            // Act
            var queryEstimate = queryTool.TestEstimateTokenUsage();
            var analysisEstimate = analysisTool.TestEstimateTokenUsage();
            var utilityEstimate = utilityTool.TestEstimateTokenUsage();

            // Assert
            queryEstimate.Should().BeGreaterThan(analysisEstimate, "Query tools should estimate more tokens");
            analysisEstimate.Should().BeGreaterThan(utilityEstimate, "Analysis tools should estimate more than utility");
            utilityEstimate.Should().BePositive("All estimates should be positive");
        }

        [Test]
        public void EstimateTokenUsage_WithParameters_IncludesParameterSize()
        {
            // Arrange
            var tool = new TestTool();
            var simpleParams = new TestParameters { Name = "Hi", Value = 1 };
            var complexParams = new TestParameters { Name = "This is a much longer parameter value that should result in more estimated tokens", Value = 12345 };

            // Act
            var simpleEstimate = tool.TestEstimateTokenUsageWithParams(simpleParams);
            var complexEstimate = tool.TestEstimateTokenUsageWithParams(complexParams);

            // Assert
            complexEstimate.Should().BeGreaterThan(simpleEstimate, "Complex parameters should result in higher token estimate");
            simpleEstimate.Should().BePositive("Simple parameters should still have positive estimate");
        }

        [Test]
        public void EstimateTokensFromText_WithVariousInputs_ReturnsReasonableEstimates()
        {
            // Arrange
            var tool = new TestTool();

            // Act & Assert
            tool.TestEstimateTokensFromText(null).Should().Be(0, "Null text should return 0");
            tool.TestEstimateTokensFromText("").Should().Be(0, "Empty text should return 0");
            tool.TestEstimateTokensFromText("Hello").Should().Be(2, "Short text should return small estimate");
            tool.TestEstimateTokensFromText("This is a longer sentence with more words").Should().BeGreaterThan(5, "Longer text should return larger estimate");
        }

        [Test]
        public void EstimateResultTokens_WithDifferentResultTypes_VariesCorrectly()
        {
            // Arrange
            var stringTool = new StringResultTool();
            var collectionTool = new CollectionResultTool();
            var complexTool = new ComplexResultTool();

            // Act
            var stringEstimate = stringTool.TestEstimateResultTokens();
            var collectionEstimate = collectionTool.TestEstimateResultTokens();
            var complexEstimate = complexTool.TestEstimateResultTokens();

            // Assert
            collectionEstimate.Should().BeGreaterThan(complexEstimate, "Collections should estimate more tokens");
            complexEstimate.Should().BeGreaterThan(stringEstimate, "Complex objects should estimate more than strings");
        }

        [Test]
        public void EstimateTokenUsage_AppliesEstimationMultiplier()
        {
            // Arrange
            var tool = new CustomMultiplierTool();

            // Act
            var estimate = tool.TestEstimateTokenUsage();

            // Assert - Tool has 2.0 multiplier, so result should be doubled from base
            estimate.Should().BeGreaterThan(1000, "Multiplier should increase the estimate");
        }

        [Test]
        public async Task ExecuteWithTokenManagement_WithThrowStrategy_ThrowsForHighEstimate()
        {
            // Arrange
            var tool = new ThrowStrategyTool();

            // Act & Assert
            Func<Task> act = async () => await tool.ExecuteAsync(new TestParameters());
            var exception = await act.Should().ThrowAsync<ToolExecutionException>()
                .WithMessage("*estimated tokens*exceeds budget*");
            exception.And.InnerException.Should().BeOfType<InvalidOperationException>();
        }

        [Test]
        public async Task ExecuteWithTokenManagement_LogsTelemetry()
        {
            // Arrange
            var loggerMock = new Mock<ILogger>();
            loggerMock.Setup(x => x.IsEnabled(LogLevel.Information)).Returns(true);
            var tool = new TestTool(loggerMock.Object);

            // Act
            await tool.ExecuteAsync(new TestParameters { Name = "Test", Value = 42 });

            // Assert
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Token Usage")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Test]
        public async Task ExecuteWithTokenManagement_WithParameterAwareEstimation_UsesParameters()
        {
            // Arrange
            var tool = new ParameterAwareTool(_loggerMock.Object);
            var parameters = new TestParameters { Name = "LongParameterValue", Value = 999 };

            // Act
            await tool.ExecuteAsync(parameters);

            // Assert - Should log debug message with token estimate
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("token estimate")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Test]
        public void EstimateTokenUsage_WithFailureInEstimation_ReturnsFallback()
        {
            // Arrange
            var tool = new FailingEstimationTool();
            var parameters = new TestParameters { Name = "Test", Value = 100 };

            // Act
            var estimate = tool.TestEstimateTokenUsageWithParameters(parameters);
            var maxBudget = tool.TestGetMaxTokens();

            // Assert
            estimate.Should().Be(maxBudget / 2, $"Should return half of max budget ({maxBudget}) as fallback");
        }

        #endregion

        #region Test Tool Implementations


        private class TestTool : McpToolBase<TestParameters, TestResult>
        {
            public TestTool(ILogger? logger = null) : base(null, logger) { }

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

            // Expose token estimation methods for testing
            public int TestEstimateTokenUsage() => EstimateTokenUsage();
            public int TestEstimateTokenUsageWithParams(TestParameters parameters) => EstimateTokenUsage(parameters);
            public int TestEstimateTokensFromText(string text) => EstimateTokensFromText(text);
            public int TestEstimateResultTokens() => EstimateResultTokens();
        }

        private class TestToolWithValidation : McpToolBase<TestValidationParameters, TestResult>
        {
            public TestToolWithValidation(ILogger? logger = null) : base(null, logger) { }

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
            public HighTokenTool(ILogger logger) : base(null, logger) { }

            public override string Name => "high_token_tool";
            public override string Description => "Tool with high token usage";

            // Override TokenBudget to have a low max tokens so we exceed the budget and trigger a warning
            protected override TokenBudgetConfiguration TokenBudget => new()
            {
                MaxTokens = 2000, // Set low so our ~3000 token estimate exceeds budget
                WarningThreshold = 1600,
                Strategy = TokenLimitStrategy.Warn
            };

            protected override Task<TestResult> ExecuteInternalAsync(TestParameters parameters, CancellationToken cancellationToken)
            {
                return Task.FromResult(new TestResult { Success = true });
            }
        }

        private class QueryCategoryTool : McpToolBase<TestParameters, TestResult>
        {
            public override string Name => "query_tool";
            public override string Description => "Query category tool";
            public override ToolCategory Category => ToolCategory.Query;

            protected override Task<TestResult> ExecuteInternalAsync(TestParameters parameters, CancellationToken cancellationToken)
            {
                return Task.FromResult(new TestResult { Success = true });
            }

            public int TestEstimateTokenUsage() => EstimateTokenUsage();
        }

        private class AnalysisCategoryTool : McpToolBase<TestParameters, TestResult>
        {
            public override string Name => "analysis_tool";
            public override string Description => "Analysis category tool";
            public override ToolCategory Category => ToolCategory.Analysis;

            protected override Task<TestResult> ExecuteInternalAsync(TestParameters parameters, CancellationToken cancellationToken)
            {
                return Task.FromResult(new TestResult { Success = true });
            }

            public int TestEstimateTokenUsage() => EstimateTokenUsage();
        }

        private class UtilityCategoryTool : McpToolBase<TestParameters, TestResult>
        {
            public override string Name => "utility_tool";
            public override string Description => "Utility category tool";
            public override ToolCategory Category => ToolCategory.Utility;

            protected override Task<TestResult> ExecuteInternalAsync(TestParameters parameters, CancellationToken cancellationToken)
            {
                return Task.FromResult(new TestResult { Success = true });
            }

            public int TestEstimateTokenUsage() => EstimateTokenUsage();
        }

        private class StringResultTool : McpToolBase<TestParameters, string>
        {
            public override string Name => "string_result_tool";
            public override string Description => "Tool returning string";

            protected override Task<string> ExecuteInternalAsync(TestParameters parameters, CancellationToken cancellationToken)
            {
                return Task.FromResult("Result string");
            }

            public int TestEstimateResultTokens() => EstimateResultTokens();
        }

        private class CollectionResultTool : McpToolBase<TestParameters, List<string>>
        {
            public override string Name => "collection_result_tool";
            public override string Description => "Tool returning collection";

            protected override Task<List<string>> ExecuteInternalAsync(TestParameters parameters, CancellationToken cancellationToken)
            {
                return Task.FromResult(new List<string> { "item1", "item2" });
            }

            public int TestEstimateResultTokens() => EstimateResultTokens();
        }

        private class ComplexResultTool : McpToolBase<TestParameters, TestResult>
        {
            public override string Name => "complex_result_tool";
            public override string Description => "Tool returning complex object";

            protected override Task<TestResult> ExecuteInternalAsync(TestParameters parameters, CancellationToken cancellationToken)
            {
                return Task.FromResult(new TestResult { Success = true });
            }

            public int TestEstimateResultTokens() => EstimateResultTokens();
        }

        private class CustomMultiplierTool : McpToolBase<TestParameters, TestResult>
        {
            public override string Name => "custom_multiplier_tool";
            public override string Description => "Tool with custom multiplier";

            protected override TokenBudgetConfiguration TokenBudget => new() { EstimationMultiplier = 2.0 };

            protected override Task<TestResult> ExecuteInternalAsync(TestParameters parameters, CancellationToken cancellationToken)
            {
                return Task.FromResult(new TestResult { Success = true });
            }

            public int TestEstimateTokenUsage() => EstimateTokenUsage();
        }

        private class ThrowStrategyTool : McpToolBase<TestParameters, TestResult>
        {
            public override string Name => "throw_strategy_tool";
            public override string Description => "Tool with throw strategy";

            protected override TokenBudgetConfiguration TokenBudget => new() { Strategy = TokenLimitStrategy.Throw, MaxTokens = 1000 };

            protected override int EstimateTokenUsage() => 2000; // Above limit

            protected override Task<TestResult> ExecuteInternalAsync(TestParameters parameters, CancellationToken cancellationToken)
            {
                return Task.FromResult(new TestResult { Success = true });
            }
        }

        private class ParameterAwareTool : McpToolBase<TestParameters, TestResult>
        {
            public ParameterAwareTool(ILogger logger) : base(null, logger) { }

            public override string Name => "parameter_aware_tool";
            public override string Description => "Tool that uses parameter-aware estimation";

            protected override Task<TestResult> ExecuteInternalAsync(TestParameters parameters, CancellationToken cancellationToken)
            {
                return Task.FromResult(new TestResult { Success = true });
            }
        }

        private class FailingEstimationTool : McpToolBase<TestParameters, TestResult>
        {
            public override string Name => "failing_estimation_tool";
            public override string Description => "Tool that fails during estimation";

            protected override int EstimateParameterTokens(TestParameters parameters)
            {
                throw new InvalidOperationException("Estimation failed");
            }

            protected override Task<TestResult> ExecuteInternalAsync(TestParameters parameters, CancellationToken cancellationToken)
            {
                return Task.FromResult(new TestResult { Success = true });
            }

            public int TestEstimateTokenUsage() => EstimateTokenUsage();
            public int TestEstimateTokenUsageWithParameters(TestParameters parameters) => EstimateTokenUsage(parameters);
            public int TestGetMaxTokens() => TokenBudget.MaxTokens;
        }

        #endregion
    }
}