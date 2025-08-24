using System;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Exceptions;
using COA.Mcp.Framework.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace COA.Mcp.Framework.Tests.Base;

[TestFixture]
public class DisposableToolBaseTests
{
    private Mock<ILogger<TestDisposableTool>> _loggerMock;
    private TestDisposableTool _tool;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<TestDisposableTool>>();
        _tool = new TestDisposableTool(_loggerMock.Object);
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_tool != null && !_tool.IsDisposed)
        {
            await _tool.DisposeAsync();
        }
    }

    [Test]
    public async Task DisposeAsync_Should_SetIsDisposedToTrue()
    {
        // Arrange
        _tool.IsDisposed.Should().BeFalse();

        // Act
        await _tool.DisposeAsync();

        // Assert
        _tool.IsDisposed.Should().BeTrue();
    }

    [Test]
    public async Task DisposeAsync_Should_CallDisposeManagedResources()
    {
        // Act
        await _tool.DisposeAsync();

        // Assert
        _tool.ManagedResourcesDisposed.Should().BeTrue();
    }

    [Test]
    public async Task DisposeAsync_Should_BeIdempotent()
    {
        // Act
        await _tool.DisposeAsync();
        await _tool.DisposeAsync(); // Second call

        // Assert
        _tool.IsDisposed.Should().BeTrue();
        _tool.DisposeCallCount.Should().Be(1); // Should only dispose once
    }

    [Test]
    public async Task ExecuteAsync_Should_ThrowObjectDisposedException_WhenDisposed()
    {
        // Arrange
        await _tool.DisposeAsync();
        var parameters = new TestParams { Value = "test" };

        // Act & Assert
        Func<Task> act = async () => await _tool.ExecuteAsync(parameters);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Test]
    public async Task ExecuteAsync_Should_Work_BeforeDisposal()
    {
        // Arrange
        var parameters = new TestParams { Value = "test" };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().Be("Processed: test");
    }

    [Test]
    public async Task DisposeOnFailure_WhenTrue_Should_DisposeOnException()
    {
        // Arrange
        var failingTool = new FailingDisposableTool(_loggerMock.Object);
        var parameters = new TestParams { Value = "fail" };

        // Act & Assert
        Func<Task> act = async () => await failingTool.ExecuteAsync(parameters);
        var exception = await act.Should().ThrowAsync<ToolExecutionException>();
        exception.And.InnerException.Should().BeOfType<InvalidOperationException>();
        
        // Tool should be disposed after failure
        failingTool.IsDisposed.Should().BeTrue();
    }

    [Test]
    public async Task ThrowIfDisposed_Should_ThrowWhenDisposed()
    {
        // Arrange
        await _tool.DisposeAsync();

        // Act & Assert
        Action act = () => _tool.TestThrowIfDisposed();
        act.Should().Throw<ObjectDisposedException>();
    }

    [Test]
    public void ThrowIfDisposed_Should_NotThrowWhenNotDisposed()
    {
        // Act & Assert
        Action act = () => _tool.TestThrowIfDisposed();
        act.Should().NotThrow();
    }

    // Test implementations
    public class TestParams
    {
        public string Value { get; set; } = string.Empty;
    }

    public class TestResult : ToolResultBase
    {
        public string? Data { get; set; }
        public override string Operation => "test";
    }

    public class TestDisposableTool : DisposableToolBase<TestParams, TestResult>
    {
        public bool ManagedResourcesDisposed { get; private set; }
        public bool UnmanagedResourcesDisposed { get; private set; }
        public int DisposeCallCount { get; private set; }

        public TestDisposableTool(ILogger<TestDisposableTool> logger) : base(null, logger)
        {
        }

        public override string Name => "TestDisposableTool";
        public override string Description => "Test tool for disposal testing";

        protected override Task<TestResult> ExecuteInternalAsync(TestParams parameters, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            
            return Task.FromResult(new TestResult
            {
                Success = true,
                Data = $"Processed: {parameters.Value}"
            });
        }

        protected override async ValueTask DisposeManagedResourcesAsync()
        {
            await base.DisposeManagedResourcesAsync();
            ManagedResourcesDisposed = true;
            DisposeCallCount++;
        }

        protected override async ValueTask DisposeUnmanagedResourcesAsync()
        {
            await base.DisposeUnmanagedResourcesAsync();
            UnmanagedResourcesDisposed = true;
        }

        public void TestThrowIfDisposed()
        {
            ThrowIfDisposed();
        }
    }

    public class FailingDisposableTool : DisposableToolBase<TestParams, TestResult>
    {
        public FailingDisposableTool(ILogger logger) : base(null, logger)
        {
        }

        public override string Name => "FailingTool";
        public override string Description => "Tool that always fails";

        protected override bool DisposeOnFailure => true; // Enable disposal on failure

        protected override Task<TestResult> ExecuteInternalAsync(TestParams parameters, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("This tool always fails");
        }
    }
}