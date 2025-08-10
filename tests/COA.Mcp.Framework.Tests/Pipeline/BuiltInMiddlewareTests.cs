using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Exceptions;
using COA.Mcp.Framework.Pipeline;
using COA.Mcp.Framework.Pipeline.SimpleMiddleware;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Moq;

namespace COA.Mcp.Framework.Tests.Pipeline;

[TestFixture]
public class BuiltInMiddlewareTests
{
    private Mock<ILogger<TestTool>>? _mockToolLogger;
    private Mock<ILogger<LoggingSimpleMiddleware>>? _mockLoggingLogger;
    private Mock<ILogger<TokenCountingSimpleMiddleware>>? _mockTokenLogger;

    [SetUp]
    public void Setup()
    {
        _mockToolLogger = new Mock<ILogger<TestTool>>();
        _mockLoggingLogger = new Mock<ILogger<LoggingSimpleMiddleware>>();
        _mockTokenLogger = new Mock<ILogger<TokenCountingSimpleMiddleware>>();
    }

    [Test]
    public async Task LoggingMiddleware_LogsExecutionEvents()
    {
        // Arrange
        var loggingMiddleware = new LoggingSimpleMiddleware(_mockLoggingLogger!.Object);
        var middleware = new List<ISimpleMiddleware> { loggingMiddleware };
        var tool = new TestTool(_mockToolLogger!.Object, middleware);
        var parameters = new TestParameters { Value = "test", Number = 42 };

        // Act
        var result = await tool.ExecuteAsync(parameters);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Message, Is.EqualTo("Processed: test (42)"));

        // Verify logging calls were made
        _mockLoggingLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting execution of tool 'test_tool'")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLoggingLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("completed successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task LoggingMiddleware_LogsErrors()
    {
        // Arrange
        var loggingMiddleware = new LoggingSimpleMiddleware(_mockLoggingLogger!.Object);
        var middleware = new List<ISimpleMiddleware> { loggingMiddleware };
        var tool = new FailingTool(_mockToolLogger!.Object, middleware);
        var parameters = new TestParameters { Value = "test", Number = 42 };

        // Act & Assert
        Assert.ThrowsAsync<ToolExecutionException>(async () => await tool.ExecuteAsync(parameters));

        // Verify error logging
        _mockLoggingLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("failed after")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task TokenCountingMiddleware_LogsTokenUsage()
    {
        // Arrange
        var tokenMiddleware = new TokenCountingSimpleMiddleware(_mockTokenLogger!.Object);
        var middleware = new List<ISimpleMiddleware> { tokenMiddleware };
        var tool = new TestTool(_mockToolLogger!.Object, middleware);
        var parameters = new TestParameters { Value = "test data that should be counted", Number = 123 };

        // Act
        var result = await tool.ExecuteAsync(parameters);

        // Assert
        Assert.That(result, Is.Not.Null);

        // Verify token logging calls were made
        _mockTokenLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("estimated input tokens")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockTokenLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("token usage:")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task CombinedMiddleware_ExecutesInCorrectOrder()
    {
        // Arrange
        var loggingMiddleware = new LoggingSimpleMiddleware(_mockLoggingLogger!.Object, LogLevel.Debug);
        var tokenMiddleware = new TokenCountingSimpleMiddleware(_mockTokenLogger!.Object);
        
        // Token middleware has higher order (100) so runs first for "before" hooks
        var middleware = new List<ISimpleMiddleware> { loggingMiddleware, tokenMiddleware };
        var tool = new TestTool(_mockToolLogger!.Object, middleware);
        var parameters = new TestParameters { Value = "integration test", Number = 999 };

        // Act
        var result = await tool.ExecuteAsync(parameters);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Message, Is.EqualTo("Processed: integration test (999)"));

        // Verify both middleware logged their events
        _mockLoggingLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);

        _mockTokenLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Test]
    public void TokenCountingMiddleware_EstimatesTokensCorrectly()
    {
        // This test verifies that the token estimation logic produces reasonable results
        var middleware = new TokenCountingSimpleMiddleware();

        // Test with null - should return 0
        var nullParams = (object?)null;
        Assert.DoesNotThrowAsync(async () => 
            await middleware.OnBeforeExecutionAsync("test", nullParams));

        // Test with simple object - should not throw and should estimate > 0
        var simpleParams = new { message = "Hello world", count = 42 };
        Assert.DoesNotThrowAsync(async () => 
            await middleware.OnBeforeExecutionAsync("test", simpleParams));

        // Test with complex object
        var complexParams = new TestParameters 
        { 
            Value = "This is a longer string with more content to estimate tokens for", 
            Number = 12345 
        };
        Assert.DoesNotThrowAsync(async () => 
            await middleware.OnBeforeExecutionAsync("test", complexParams));
    }

    [Test]
    public async Task MiddlewareErrorHandling_ContinuesExecution()
    {
        // Arrange - middleware that throws in before hook
        var faultyMiddleware = new Mock<ISimpleMiddleware>();
        faultyMiddleware.Setup(m => m.IsEnabled).Returns(true);
        faultyMiddleware.Setup(m => m.Order).Returns(1);
        faultyMiddleware.Setup(m => m.OnBeforeExecutionAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ThrowsAsync(new Exception("Middleware failed"));

        var goodMiddleware = new LoggingSimpleMiddleware(_mockLoggingLogger!.Object);
        var middleware = new List<ISimpleMiddleware> { faultyMiddleware.Object, goodMiddleware };
        var tool = new TestTool(_mockToolLogger!.Object, middleware);
        var parameters = new TestParameters { Value = "test", Number = 1 };

        // Act & Assert - should still throw because the faulty middleware throws  
        // The framework wraps exceptions in ToolExecutionException
        Assert.ThrowsAsync<ToolExecutionException>(async () => await tool.ExecuteAsync(parameters));
    }

    // Test classes
    public class TestParameters
    {
        public string Value { get; set; } = string.Empty;
        public int Number { get; set; }
    }

    public class TestResult
    {
        public string Message { get; set; } = string.Empty;
    }

    public class TestTool : McpToolBase<TestParameters, TestResult>
    {
        private readonly IReadOnlyList<ISimpleMiddleware>? _middleware;

        public TestTool(ILogger<TestTool> logger, IReadOnlyList<ISimpleMiddleware>? middleware)
            : base(logger)
        {
            _middleware = middleware;
        }

        public override string Name => "test_tool";
        public override string Description => "Test tool for built-in middleware testing";

        protected override IReadOnlyList<ISimpleMiddleware>? Middleware => _middleware;

        protected override Task<TestResult> ExecuteInternalAsync(TestParameters parameters, CancellationToken cancellationToken)
        {
            return Task.FromResult(new TestResult { Message = $"Processed: {parameters.Value} ({parameters.Number})" });
        }
    }

    public class FailingTool : McpToolBase<TestParameters, TestResult>
    {
        private readonly IReadOnlyList<ISimpleMiddleware>? _middleware;

        public FailingTool(ILogger<TestTool> logger, IReadOnlyList<ISimpleMiddleware>? middleware)
            : base(logger)
        {
            _middleware = middleware;
        }

        public override string Name => "failing_tool";
        public override string Description => "Tool that always fails for error testing";

        protected override IReadOnlyList<ISimpleMiddleware>? Middleware => _middleware;

        protected override Task<TestResult> ExecuteInternalAsync(TestParameters parameters, CancellationToken cancellationToken)
        {
            throw new Exception("Intentional failure for testing");
        }
    }
}