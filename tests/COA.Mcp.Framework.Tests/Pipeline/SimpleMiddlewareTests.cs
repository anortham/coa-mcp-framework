using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Exceptions;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework.Pipeline;
using COA.Mcp.Framework.Pipeline.SimpleMiddleware;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Moq;

namespace COA.Mcp.Framework.Tests.Pipeline;

[TestFixture]
public class SimpleMiddlewareTests
{
    private Mock<ILogger<TestTool>>? _mockLogger;
    private Mock<ILogger<LoggingSimpleMiddleware>>? _mockLoggingLogger;
    private Mock<ILogger<TokenCountingSimpleMiddleware>>? _mockTokenLogger;

    [SetUp]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<TestTool>>();
        _mockLoggingLogger = new Mock<ILogger<LoggingSimpleMiddleware>>();
        _mockTokenLogger = new Mock<ILogger<TokenCountingSimpleMiddleware>>();
    }

    [Test]
    public async Task ExecuteAsync_WithMiddleware_CallsLifecycleHooks()
    {
        // Arrange
        var middleware = new Mock<ISimpleMiddleware>();
        middleware.Setup(m => m.IsEnabled).Returns(true);
        middleware.Setup(m => m.Order).Returns(1);

        var tool = new TestTool(_mockLogger!.Object, new List<ISimpleMiddleware> { middleware.Object });
        var parameters = new TestParameters { Value = "test" };

        // Act
        var result = await tool.ExecuteAsync(parameters);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Message, Is.EqualTo("Processed: test"));

        middleware.Verify(m => m.OnBeforeExecutionAsync("test_tool", parameters), Times.Once);
        middleware.Verify(m => m.OnAfterExecutionAsync("test_tool", parameters, result, It.IsAny<long>()), Times.Once);
        middleware.Verify(m => m.OnErrorAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<Exception>(), It.IsAny<long>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_WithMiddleware_OnError_CallsErrorHook()
    {
        // Arrange
        var middleware = new Mock<ISimpleMiddleware>();
        middleware.Setup(m => m.IsEnabled).Returns(true);
        middleware.Setup(m => m.Order).Returns(1);

        var tool = new FailingTool(_mockLogger!.Object, new List<ISimpleMiddleware> { middleware.Object });
        var parameters = new TestParameters { Value = "test" };

        // Act & Assert
        Assert.ThrowsAsync<ToolExecutionException>(async () => await tool.ExecuteAsync(parameters));

        middleware.Verify(m => m.OnBeforeExecutionAsync("failing_tool", parameters), Times.Once);
        middleware.Verify(m => m.OnAfterExecutionAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<object>(), It.IsAny<long>()), Times.Never);
        middleware.Verify(m => m.OnErrorAsync("failing_tool", parameters, It.IsAny<Exception>(), It.IsAny<long>()), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WithDisabledMiddleware_SkipsMiddleware()
    {
        // Arrange
        var middleware = new Mock<ISimpleMiddleware>();
        middleware.Setup(m => m.IsEnabled).Returns(false);
        middleware.Setup(m => m.Order).Returns(1);

        var tool = new TestTool(_mockLogger!.Object, new List<ISimpleMiddleware> { middleware.Object });
        var parameters = new TestParameters { Value = "test" };

        // Act
        var result = await tool.ExecuteAsync(parameters);

        // Assert
        Assert.That(result, Is.Not.Null);

        middleware.Verify(m => m.OnBeforeExecutionAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
        middleware.Verify(m => m.OnAfterExecutionAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<object>(), It.IsAny<long>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_WithMultipleMiddleware_ExecutesInOrder()
    {
        // Arrange
        var executionOrder = new List<string>();

        var middleware1 = new Mock<ISimpleMiddleware>();
        middleware1.Setup(m => m.IsEnabled).Returns(true);
        middleware1.Setup(m => m.Order).Returns(1);
        middleware1.Setup(m => m.OnBeforeExecutionAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Callback(() => executionOrder.Add("middleware1-before"))
            .Returns(Task.CompletedTask);

        var middleware2 = new Mock<ISimpleMiddleware>();
        middleware2.Setup(m => m.IsEnabled).Returns(true);
        middleware2.Setup(m => m.Order).Returns(2);
        middleware2.Setup(m => m.OnBeforeExecutionAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Callback(() => executionOrder.Add("middleware2-before"))
            .Returns(Task.CompletedTask);

        var tool = new TestTool(_mockLogger!.Object, new List<ISimpleMiddleware> { middleware2.Object, middleware1.Object });
        var parameters = new TestParameters { Value = "test" };

        // Act
        await tool.ExecuteAsync(parameters);

        // Assert
        Assert.That(executionOrder, Has.Count.EqualTo(2));
        Assert.That(executionOrder[0], Is.EqualTo("middleware1-before"));
        Assert.That(executionOrder[1], Is.EqualTo("middleware2-before"));
    }

    [Test]
    public void LoggingSimpleMiddleware_LogsExecution()
    {
        // Arrange
        var middleware = new LoggingSimpleMiddleware(_mockLoggingLogger!.Object);

        // Act & Assert
        Assert.That(middleware.Order, Is.EqualTo(10));
        Assert.That(middleware.IsEnabled, Is.True);

        // Test that methods complete without throwing
        Assert.DoesNotThrowAsync(async () => 
            await middleware.OnBeforeExecutionAsync("test_tool", new { value = "test" }));
        
        Assert.DoesNotThrowAsync(async () => 
            await middleware.OnAfterExecutionAsync("test_tool", new { value = "test" }, "result", 100));
        
        Assert.DoesNotThrowAsync(async () => 
            await middleware.OnErrorAsync("test_tool", new { value = "test" }, new Exception("test"), 100));
    }

    [Test]
    public void TokenCountingSimpleMiddleware_CountsTokens()
    {
        // Arrange
        var middleware = new TokenCountingSimpleMiddleware(_mockTokenLogger!.Object);

        // Act & Assert
        Assert.That(middleware.Order, Is.EqualTo(100));
        Assert.That(middleware.IsEnabled, Is.True);

        // Test that methods complete without throwing
        Assert.DoesNotThrowAsync(async () => 
            await middleware.OnBeforeExecutionAsync("test_tool", new { value = "test" }));
        
        Assert.DoesNotThrowAsync(async () => 
            await middleware.OnAfterExecutionAsync("test_tool", new { value = "test" }, "result", 100));
    }

    [Test]
    public void SimpleMiddlewareBase_DefaultImplementation()
    {
        // Arrange
        var middleware = new TestMiddleware();

        // Act & Assert
        Assert.That(middleware.Order, Is.EqualTo(0));
        Assert.That(middleware.IsEnabled, Is.True);

        // Test that default methods complete without throwing
        Assert.DoesNotThrowAsync(async () => 
            await middleware.OnBeforeExecutionAsync("test_tool", new { value = "test" }));
        
        Assert.DoesNotThrowAsync(async () => 
            await middleware.OnAfterExecutionAsync("test_tool", new { value = "test" }, "result", 100));
        
        Assert.DoesNotThrowAsync(async () => 
            await middleware.OnErrorAsync("test_tool", new { value = "test" }, new Exception("test"), 100));
    }

    [Test]
    public async Task ExecuteAsync_WithNoMiddleware_ExecutesNormally()
    {
        // Arrange
        var tool = new TestTool(_mockLogger!.Object, null);
        var parameters = new TestParameters { Value = "test" };

        // Act
        var result = await tool.ExecuteAsync(parameters);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Message, Is.EqualTo("Processed: test"));
    }

    // Test classes
    public class TestParameters
    {
        public string Value { get; set; } = string.Empty;
    }

    public class TestResult
    {
        public string Message { get; set; } = string.Empty;
    }

    public class TestTool : McpToolBase<TestParameters, TestResult>
    {
        private readonly IReadOnlyList<ISimpleMiddleware>? _middleware;

        public TestTool(ILogger<TestTool> logger, IReadOnlyList<ISimpleMiddleware>? middleware)
            : base(null, logger)
        {
            _middleware = middleware;
        }

        public override string Name => "test_tool";
        public override string Description => "Test tool for middleware testing";

        protected override IReadOnlyList<ISimpleMiddleware>? Middleware => _middleware;

        protected override Task<TestResult> ExecuteInternalAsync(TestParameters parameters, CancellationToken cancellationToken)
        {
            return Task.FromResult(new TestResult { Message = $"Processed: {parameters.Value}" });
        }
    }

    public class FailingTool : McpToolBase<TestParameters, TestResult>
    {
        private readonly IReadOnlyList<ISimpleMiddleware>? _middleware;

        public FailingTool(ILogger<TestTool> logger, IReadOnlyList<ISimpleMiddleware>? middleware)
            : base(null, logger)
        {
            _middleware = middleware;
        }

        public override string Name => "failing_tool";
        public override string Description => "Tool that always fails";

        protected override IReadOnlyList<ISimpleMiddleware>? Middleware => _middleware;

        protected override Task<TestResult> ExecuteInternalAsync(TestParameters parameters, CancellationToken cancellationToken)
        {
            throw new Exception("Tool execution failed");
        }
    }

    public class TestMiddleware : SimpleMiddlewareBase
    {
        // Uses default implementation
    }
}