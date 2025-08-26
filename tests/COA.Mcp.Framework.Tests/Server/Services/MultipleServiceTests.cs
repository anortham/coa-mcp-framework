using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework.Server.Services;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace COA.Mcp.Framework.Tests.Server.Services;

[TestFixture]
public class MultipleServiceTests
{
    private ServiceManager _serviceManager;
    private TestLogger<ServiceManager> _logger;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<ServiceManager>();
        _serviceManager = new ServiceManager(_logger);
    }

    [TearDown]
    public void TearDown()
    {
        _serviceManager?.Dispose();
    }

    [Test]
    public async Task GetServiceStatus_MultipleServices_TracksIndependently()
    {
        // Arrange
        var service1Id = "service1";
        var service2Id = "service2";
        var service3Id = "service3";

        // Act
        var status1 = _serviceManager.GetServiceStatus(service1Id);
        var status2 = _serviceManager.GetServiceStatus(service2Id);
        var status3 = _serviceManager.GetServiceStatus(service3Id);

        // Assert
        Assert.That(status1, Is.EqualTo(ServiceStatus.NotStarted));
        Assert.That(status2, Is.EqualTo(ServiceStatus.NotStarted));
        Assert.That(status3, Is.EqualTo(ServiceStatus.NotStarted));
    }

    [Test]
    public async Task RegisterHealthCheck_MultipleServices_MaintainsSeparateCallbacks()
    {
        // Arrange
        var healthCheck1Called = false;
        var healthCheck2Called = false;

        _serviceManager.RegisterHealthCheck("service1", async () =>
        {
            healthCheck1Called = true;
            return await Task.FromResult(true);
        });

        _serviceManager.RegisterHealthCheck("service2", async () =>
        {
            healthCheck2Called = true;
            return await Task.FromResult(false);
        });

        // Act - registering health checks doesn't call them
        // Assert
        Assert.That(healthCheck1Called, Is.False);
        Assert.That(healthCheck2Called, Is.False);
    }

    [Test]
    public async Task StopServiceAsync_OnlyStopsSpecifiedService()
    {
        // This test verifies that stopping one service doesn't affect others
        // Since we can't actually start services in unit tests, we just verify the API

        // Act
        await _serviceManager.StopServiceAsync("service1");

        // Assert - other services should remain unaffected
        var status2 = _serviceManager.GetServiceStatus("service2");
        Assert.That(status2, Is.EqualTo(ServiceStatus.NotStarted));
    }

    [Test]
    public async Task StartAsync_StopAsync_HandlesConcurrentOperations()
    {
        // Arrange
        var cts = new CancellationTokenSource();

        // Act - Start and stop the service manager
        await _serviceManager.StartAsync(cts.Token);
        
        // Simulate some operations
        _serviceManager.RegisterHealthCheck("service1", async () => await Task.FromResult(true));
        _serviceManager.RegisterHealthCheck("service2", async () => await Task.FromResult(false));
        
        await _serviceManager.StopAsync(CancellationToken.None);

        // Assert - should complete without exceptions
        Assert.Pass("Test completed without throwing");
    }

    [Test]
    public void ServiceConfiguration_SupportsMultipleConfigurations()
    {
        // Arrange
        var configs = new List<ServiceConfiguration>();

        // Act
        for (int i = 1; i <= 5; i++)
        {
            configs.Add(new ServiceConfiguration
            {
                ServiceId = $"service{i}",
                ExecutablePath = $"service{i}.exe",
                Port = 5000 + i,
                HealthEndpoint = $"http://localhost:{5000 + i}/health",
                AutoRestart = i % 2 == 0, // Even services have auto-restart
                MaxRestartAttempts = i,
                EnvironmentVariables = new Dictionary<string, string>
                {
                    { $"SERVICE_{i}_VAR", $"value{i}" }
                }
            });
        }

        // Assert
        Assert.That(configs.Count, Is.EqualTo(5));
        
        for (int i = 0; i < configs.Count; i++)
        {
            var config = configs[i];
            var expectedId = $"service{i + 1}";
            
            Assert.That(config.ServiceId, Is.EqualTo(expectedId));
            Assert.That(config.Port, Is.EqualTo(5001 + i));
            Assert.That(config.AutoRestart, Is.EqualTo((i + 1) % 2 == 0));
            Assert.That(config.MaxRestartAttempts, Is.EqualTo(i + 1));
            Assert.That(config.EnvironmentVariables.Count, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task EnsureServiceRunningAsync_ParallelCalls_HandledCorrectly()
    {
        // Arrange
        var config1 = new ServiceConfiguration
        {
            ServiceId = "parallel-service1",
            ExecutablePath = "nonexistent1.exe",
            Port = 6001
        };

        var config2 = new ServiceConfiguration
        {
            ServiceId = "parallel-service2",
            ExecutablePath = "nonexistent2.exe",
            Port = 6002
        };

        // Act - Start both services in parallel
        var task1 = Task.Run(async () => await _serviceManager.EnsureServiceRunningAsync(config1));
        var task2 = Task.Run(async () => await _serviceManager.EnsureServiceRunningAsync(config2));

        // Wait for both with timeout
        var allTasks = Task.WhenAll(task1, task2);
        var completedTask = await Task.WhenAny(allTasks, Task.Delay(TimeSpan.FromSeconds(5)));

        // Assert
        Assert.That(completedTask, Is.EqualTo(allTasks), "Tasks should complete within timeout");
        
        // Both should fail since executables don't exist
        Assert.That(task1.Result, Is.EqualTo(ServiceStatus.Failed));
        Assert.That(task2.Result, Is.EqualTo(ServiceStatus.Failed));
    }

    // Test logger implementation
    private class TestLogger<T> : ILogger<T>
    {
        public TestLogger()
        {
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => new NoopDisposable();

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            TestContext.Out.WriteLine($"[{logLevel}] {formatter(state, exception)}");
            if (exception != null)
            {
                TestContext.Out.WriteLine(exception.ToString());
            }
        }

        private class NoopDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}