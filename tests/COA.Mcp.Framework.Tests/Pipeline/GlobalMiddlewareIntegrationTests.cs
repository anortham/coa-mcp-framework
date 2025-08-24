using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework.Pipeline;
using COA.Mcp.Framework.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace COA.Mcp.Framework.Tests.Pipeline;

[TestFixture]
public class GlobalMiddlewareIntegrationTests
{
    private ServiceCollection _services;
    private IServiceProvider? _serviceProvider;

    [SetUp]
    public void Setup()
    {
        _services = new ServiceCollection();
        _services.AddSingleton<ILogger<TestTool>>(NullLogger<TestTool>.Instance);
        _services.AddSingleton<ILogger<TestMiddleware>>(NullLogger<TestMiddleware>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    [Test]
    public void WithGlobalMiddleware_SingleInstance_RegistersMiddleware()
    {
        // Arrange
        var middleware = new TestMiddleware(10);
        var builder = new McpServerBuilder();

        // Act
        builder.WithGlobalMiddleware(middleware);
        
        // Get the service collection from the builder and build service provider
        _serviceProvider = builder.Services.BuildServiceProvider();

        // Assert
        var registeredMiddleware = _serviceProvider.GetServices<ISimpleMiddleware>().ToList();
        Assert.That(registeredMiddleware, Has.Count.EqualTo(1));
        Assert.That(registeredMiddleware.First(), Is.SameAs(middleware));
    }

    [Test]
    public void WithGlobalMiddleware_MultipleInstances_RegistersAllMiddleware()
    {
        // Arrange
        var middleware1 = new TestMiddleware(10);
        var middleware2 = new TestMiddleware(20);
        var builder = new McpServerBuilder();

        // Act
        builder.WithGlobalMiddleware(middleware1, middleware2);
        _serviceProvider = builder.Services.BuildServiceProvider();

        // Assert
        var registeredMiddleware = _serviceProvider.GetServices<ISimpleMiddleware>().ToList();
        Assert.That(registeredMiddleware, Has.Count.EqualTo(2));
        Assert.That(registeredMiddleware, Contains.Item(middleware1));
        Assert.That(registeredMiddleware, Contains.Item(middleware2));
    }

    [Test]
    public void WithGlobalMiddleware_IEnumerableOverload_RegistersAllMiddleware()
    {
        // Arrange
        var middleware1 = new TestMiddleware(10);
        var middleware2 = new TestMiddleware(20);
        var middlewareList = new List<ISimpleMiddleware> { middleware1, middleware2 };
        var builder = new McpServerBuilder();

        // Act
        builder.WithGlobalMiddleware(middlewareList);
        _serviceProvider = builder.Services.BuildServiceProvider();

        // Assert
        var registeredMiddleware = _serviceProvider.GetServices<ISimpleMiddleware>().ToList();
        Assert.That(registeredMiddleware, Has.Count.EqualTo(2));
        Assert.That(registeredMiddleware, Contains.Item(middleware1));
        Assert.That(registeredMiddleware, Contains.Item(middleware2));
    }

    [Test]
    public void AddGlobalMiddleware_TypeBased_RegistersMiddleware()
    {
        // Arrange
        var builder = new McpServerBuilder();

        // Act - Use a factory to provide the required constructor parameter
        builder.AddGlobalMiddleware<TestMiddleware>(provider =>
        {
            var logger = provider.GetService<ILogger<TestMiddleware>>();
            return new TestMiddleware(25, logger);
        });
        _serviceProvider = builder.Services.BuildServiceProvider();

        // Assert
        var registeredMiddleware = _serviceProvider.GetServices<ISimpleMiddleware>().ToList();
        Assert.That(registeredMiddleware, Has.Count.EqualTo(1));
        Assert.That(registeredMiddleware.First(), Is.TypeOf<TestMiddleware>());
    }

    [Test]
    public void AddGlobalMiddleware_WithFactory_RegistersMiddleware()
    {
        // Arrange
        var builder = new McpServerBuilder();
        var factoryCalled = false;

        // Act
        builder.AddGlobalMiddleware<TestMiddleware>(provider =>
        {
            factoryCalled = true;
            var logger = provider.GetService<ILogger<TestMiddleware>>();
            return new TestMiddleware(50, logger);
        });
        _serviceProvider = builder.Services.BuildServiceProvider();

        // Assert
        var registeredMiddleware = _serviceProvider.GetServices<ISimpleMiddleware>().First();
        Assert.That(factoryCalled, Is.True);
        Assert.That(registeredMiddleware, Is.TypeOf<TestMiddleware>());
        Assert.That(((TestMiddleware)registeredMiddleware).Order, Is.EqualTo(50));
    }

    [Test]
    public void Tool_WithGlobalMiddleware_CombinesWithToolSpecificMiddleware()
    {
        // Arrange
        var globalMiddleware = new TestMiddleware(10, null, "global");
        var builder = new McpServerBuilder();
        builder.WithGlobalMiddleware(globalMiddleware);
        _serviceProvider = builder.Services.BuildServiceProvider();

        var tool = new TestTool(_serviceProvider);

        // Act
        var combinedMiddleware = tool.GetCombinedMiddleware();

        // Assert
        Assert.That(combinedMiddleware, Has.Count.EqualTo(2));
        // Should be ordered by Order property: global (10), tool-specific (30)
        Assert.That(combinedMiddleware.ElementAt(0).Order, Is.EqualTo(10));
        Assert.That(combinedMiddleware.ElementAt(1).Order, Is.EqualTo(30));
    }

    [Test]
    public void Tool_WithoutServiceProvider_OnlyUsesToolSpecificMiddleware()
    {
        // Arrange
        var tool = new TestTool(null);

        // Act
        var middleware = tool.GetCombinedMiddleware();

        // Assert
        Assert.That(middleware, Has.Count.EqualTo(1));
        Assert.That(middleware.First().Order, Is.EqualTo(30));
    }

    [Test]
    public void Tool_WithGlobalMiddleware_OrdersCorrectly()
    {
        // Arrange
        var highOrderMiddleware = new TestMiddleware(100, null, "high");
        var lowOrderMiddleware = new TestMiddleware(5, null, "low");
        
        var builder = new McpServerBuilder();
        builder.WithGlobalMiddleware(highOrderMiddleware, lowOrderMiddleware);
        _serviceProvider = builder.Services.BuildServiceProvider();

        var tool = new TestTool(_serviceProvider);

        // Act
        var combinedMiddleware = tool.GetCombinedMiddleware();

        // Assert
        Assert.That(combinedMiddleware, Has.Count.EqualTo(3));
        // Should be ordered: low (5), tool-specific (30), high (100)
        Assert.That(combinedMiddleware.ElementAt(0).Order, Is.EqualTo(5));
        Assert.That(combinedMiddleware.ElementAt(1).Order, Is.EqualTo(30));
        Assert.That(combinedMiddleware.ElementAt(2).Order, Is.EqualTo(100));
    }

    [Test]
    public async Task Tool_ExecutionWithGlobalMiddleware_InvokesAllMiddlewareInOrder()
    {
        // Arrange
        var executionLog = new List<string>();
        var globalMiddleware1 = new TestMiddleware(10, null, "global1", executionLog);
        var globalMiddleware2 = new TestMiddleware(5, null, "global2", executionLog);
        
        var builder = new McpServerBuilder();
        builder.WithGlobalMiddleware(globalMiddleware1, globalMiddleware2);
        _serviceProvider = builder.Services.BuildServiceProvider();

        var tool = new TestTool(_serviceProvider, executionLog);

        // Act
        await tool.ExecuteAsync(new TestParameters { Value = "test" }, CancellationToken.None);

        // Assert
        // Expected order: global2 (order 5), global1 (order 10), tool (order 30)
        var expectedLog = new[]
        {
            "Before: global2",
            "Before: global1", 
            "Before: tool",
            "Execute: test",
            "After: tool",
            "After: global1",
            "After: global2"
        };
        
        Assert.That(executionLog, Is.EqualTo(expectedLog));
    }

    #region Test Classes

    public class TestParameters
    {
        public string Value { get; set; } = "";
    }

    public class TestResult : ToolResultBase
    {
        public override string Operation => "test";
        public string ProcessedValue { get; set; } = "";
    }

    public class TestTool : McpToolBase<TestParameters, TestResult>
    {
        private readonly List<string>? _executionLog;
        private readonly TestMiddleware _toolMiddleware;

        public TestTool(IServiceProvider? serviceProvider, List<string>? executionLog = null) 
            : base(serviceProvider, serviceProvider?.GetService<ILogger<TestTool>>())
        {
            _executionLog = executionLog;
            _toolMiddleware = new TestMiddleware(30, null, "tool", executionLog);
        }

        public override string Name => "test_tool";
        public override string Description => "Test tool for middleware integration testing";

        protected override IReadOnlyList<ISimpleMiddleware>? ToolSpecificMiddleware =>
            new List<ISimpleMiddleware> { _toolMiddleware };

        public IReadOnlyList<ISimpleMiddleware>? GetCombinedMiddleware() => Middleware;

        protected override Task<TestResult> ExecuteInternalAsync(TestParameters parameters, CancellationToken cancellationToken)
        {
            _executionLog?.Add($"Execute: {parameters.Value}");
            return Task.FromResult(new TestResult { ProcessedValue = parameters.Value });
        }
    }

    public class TestMiddleware : SimpleMiddlewareBase
    {
        private readonly ILogger? _logger;
        private readonly string _name;
        private readonly List<string>? _executionLog;

        public TestMiddleware(int order, ILogger? logger = null, string name = "test", List<string>? executionLog = null)
        {
            Order = order;
            _logger = logger;
            _name = name;
            _executionLog = executionLog;
        }

        public override Task OnBeforeExecutionAsync(string toolName, object? parameters)
        {
            _executionLog?.Add($"Before: {_name}");
            _logger?.LogInformation("TestMiddleware {Name} (Order: {Order}) - Before execution of {ToolName}", _name, Order, toolName);
            return Task.CompletedTask;
        }

        public override Task OnAfterExecutionAsync(string toolName, object? parameters, object? result, long elapsedMs)
        {
            _executionLog?.Add($"After: {_name}");
            _logger?.LogInformation("TestMiddleware {Name} (Order: {Order}) - After execution of {ToolName} in {ElapsedMs}ms", _name, Order, toolName, elapsedMs);
            return Task.CompletedTask;
        }

        public override Task OnErrorAsync(string toolName, object? parameters, Exception exception, long elapsedMs)
        {
            _executionLog?.Add($"Error: {_name}");
            _logger?.LogError(exception, "TestMiddleware {Name} (Order: {Order}) - Error in {ToolName} after {ElapsedMs}ms", _name, Order, toolName, elapsedMs);
            return Task.CompletedTask;
        }
    }

    #endregion
}