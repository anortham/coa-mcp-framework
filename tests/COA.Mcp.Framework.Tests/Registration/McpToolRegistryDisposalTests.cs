using System;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework.Registration;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace COA.Mcp.Framework.Tests.Registration;

[TestFixture]
public class McpToolRegistryDisposalTests
{
    private McpToolRegistry _registry;
    private IServiceProvider _serviceProvider;
    private Mock<ILogger<McpToolRegistry>> _loggerMock;

    [SetUp]
    public void Setup()
    {
        var services = new ServiceCollection();
        _loggerMock = new Mock<ILogger<McpToolRegistry>>();
        services.AddSingleton(_loggerMock.Object);
        services.AddLogging();
        
        _serviceProvider = services.BuildServiceProvider();
        _registry = new McpToolRegistry(_serviceProvider, _loggerMock.Object);
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_registry != null)
        {
            await _registry.DisposeAsync();
        }
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    [Test]
    public async Task DisposeAsync_Should_DisposeAllDisposableTools()
    {
        // Arrange
        var tool1 = new TestDisposableTool("Tool1");
        var tool2 = new TestDisposableTool("Tool2");
        var regularTool = new RegularTool();
        
        _registry.RegisterTool<TestParams, TestResult>(tool1);
        _registry.RegisterTool<TestParams, TestResult>(tool2);
        _registry.RegisterTool<TestParams, TestResult>(regularTool);

        // Act
        await _registry.DisposeAsync();

        // Assert
        tool1.IsDisposed.Should().BeTrue();
        tool2.IsDisposed.Should().BeTrue();
        // Regular tool doesn't implement IDisposable, so no disposal check
    }

    [Test]
    public async Task DisposeAsync_Should_ClearAllTools()
    {
        // Arrange
        var tool = new TestDisposableTool("Tool1");
        _registry.RegisterTool<TestParams, TestResult>(tool);
        _registry.IsToolRegistered("Tool1").Should().BeTrue();

        // Act
        await _registry.DisposeAsync();

        // Assert
        _registry.IsToolRegistered("Tool1").Should().BeFalse();
        _registry.GetAllTools().Should().BeEmpty();
    }

    [Test]
    public async Task DisposeAsync_Should_BeIdempotent()
    {
        // Arrange
        var tool = new TestDisposableTool("Tool1");
        _registry.RegisterTool<TestParams, TestResult>(tool);

        // Act
        await _registry.DisposeAsync();
        await _registry.DisposeAsync(); // Second call

        // Assert
        tool.DisposeCount.Should().Be(1); // Should only be disposed once
    }

    [Test]
    public async Task UnregisterTool_Should_DisposeDisposableTool()
    {
        // Arrange
        var tool = new TestDisposableTool("Tool1");
        _registry.RegisterTool<TestParams, TestResult>(tool);

        // Act
        var removed = _registry.UnregisterTool("Tool1");

        // Assert
        removed.Should().BeTrue();
        tool.IsDisposed.Should().BeTrue();
    }

    [Test]
    public async Task Clear_Should_DisposeAllDisposableTools()
    {
        // Arrange
        var tool1 = new TestDisposableTool("Tool1");
        var tool2 = new TestDisposableTool("Tool2");
        
        _registry.RegisterTool<TestParams, TestResult>(tool1);
        _registry.RegisterTool<TestParams, TestResult>(tool2);

        // Act
        _registry.Clear();

        // Assert
        tool1.IsDisposed.Should().BeTrue();
        tool2.IsDisposed.Should().BeTrue();
        _registry.GetAllTools().Should().BeEmpty();
    }

    [Test]
    public async Task DisposeAsync_Should_HandleExceptionsGracefully()
    {
        // Arrange
        var failingTool = new FailingDisposableTool();
        var normalTool = new TestDisposableTool("NormalTool");
        
        _registry.RegisterTool<TestParams, TestResult>(failingTool);
        _registry.RegisterTool<TestParams, TestResult>(normalTool);

        // Act & Assert
        Func<Task> act = async () => await _registry.DisposeAsync();
        await act.Should().NotThrowAsync(); // Should not throw even if a tool disposal fails
        
        normalTool.IsDisposed.Should().BeTrue(); // Other tools should still be disposed
    }

    // Test tool implementations
    private class TestParams
    {
        public string Value { get; set; } = string.Empty;
    }

    private class TestResult : ToolResultBase
    {
        public string? Data { get; set; }
        public override string Operation => "test";
    }

    private class TestDisposableTool : DisposableToolBase<TestParams, TestResult>
    {
        public int DisposeCount { get; private set; }
        private readonly string _name;

        public TestDisposableTool(string name) : base(null)
        {
            _name = name;
        }

        public override string Name => _name;
        public override string Description => "Test disposable tool";

        protected override Task<TestResult> ExecuteInternalAsync(TestParams parameters, CancellationToken cancellationToken)
        {
            return Task.FromResult(new TestResult
            {
                Success = true,
                Data = parameters.Value
            });
        }

        protected override async ValueTask DisposeManagedResourcesAsync()
        {
            await base.DisposeManagedResourcesAsync();
            DisposeCount++;
        }
    }

    private class FailingDisposableTool : DisposableToolBase<TestParams, TestResult>
    {
        public FailingDisposableTool() : base(null) { }
        
        public override string Name => "FailingDisposableTool";
        public override string Description => "Tool that fails on disposal";

        protected override Task<TestResult> ExecuteInternalAsync(TestParams parameters, CancellationToken cancellationToken)
        {
            return Task.FromResult(new TestResult { Success = true });
        }

        protected override async ValueTask DisposeManagedResourcesAsync()
        {
            await base.DisposeManagedResourcesAsync();
            await Task.Delay(10); // Simulate some async work
            throw new InvalidOperationException("Disposal failed!");
        }
    }

    private class RegularTool : McpToolBase<TestParams, TestResult>
    {
        public override string Name => "RegularTool";
        public override string Description => "Non-disposable tool";

        protected override Task<TestResult> ExecuteInternalAsync(TestParams parameters, CancellationToken cancellationToken)
        {
            return Task.FromResult(new TestResult
            {
                Success = true,
                Data = parameters.Value
            });
        }
    }
}