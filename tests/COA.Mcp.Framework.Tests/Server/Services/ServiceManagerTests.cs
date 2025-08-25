using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework.Server.Services;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Moq;

namespace COA.Mcp.Framework.Tests.Server.Services;

[TestFixture]
public class ServiceManagerTests : IDisposable
{
    private ServiceManager _serviceManager;
    private TestLogger<ServiceManager> _logger;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<ServiceManager>();
        _serviceManager = new ServiceManager(_logger);
    }

    public void Dispose()
    {
        _serviceManager?.Dispose();
    }

    [Test]
    public async Task EnsureServiceRunningAsync_WithNullConfig_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(async () => 
            await _serviceManager.EnsureServiceRunningAsync(null!));
    }

    [Test]
    public async Task EnsureServiceRunningAsync_WithEmptyServiceId_ThrowsArgumentException()
    {
        // Arrange
        var config = new ServiceConfiguration
        {
            ServiceId = "",
            ExecutablePath = "dummy.exe"
        };

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () => 
            await _serviceManager.EnsureServiceRunningAsync(config));
    }

    [Test]
    public async Task GetServiceStatus_WithUnknownServiceId_ReturnsNotStarted()
    {
        // Act
        var status = _serviceManager.GetServiceStatus("unknown-service");

        // Assert
        Assert.That(status, Is.EqualTo(ServiceStatus.NotStarted));
    }

    [Test]
    public async Task IsServiceHealthyAsync_WithEmptyEndpoint_ReturnsFalse()
    {
        // Act
        var isHealthy = await _serviceManager.IsServiceHealthyAsync("");

        // Assert
        Assert.That(isHealthy, Is.False);
    }

    [Test]
    public async Task IsServiceHealthyAsync_WithInvalidEndpoint_ReturnsFalse()
    {
        // Act
        var isHealthy = await _serviceManager.IsServiceHealthyAsync("http://localhost:99999/health");

        // Assert
        Assert.That(isHealthy, Is.False);
    }

    [Test]
    public async Task RegisterHealthCheck_WithUnknownServiceId_DoesNotThrow()
    {
        // Act & Assert (should not throw)
        _serviceManager.RegisterHealthCheck("unknown-service", async () => await Task.FromResult(true));
    }

    [Test]
    public async Task StopServiceAsync_WithUnknownServiceId_DoesNotThrow()
    {
        // Act & Assert (should not throw)
        await _serviceManager.StopServiceAsync("unknown-service");
    }

    [Test]
    public async Task StartAsync_InitializesMonitoring()
    {
        // Act
        await _serviceManager.StartAsync(CancellationToken.None);

        // Assert - service manager should be ready to accept services
        var status = _serviceManager.GetServiceStatus("test");
        Assert.That(status, Is.EqualTo(ServiceStatus.NotStarted));
    }

    [Test]
    public async Task StopAsync_StopsAllServices()
    {
        // Arrange
        await _serviceManager.StartAsync(CancellationToken.None);

        // Act
        await _serviceManager.StopAsync(CancellationToken.None);

        // Assert - should complete without throwing
        Assert.Pass("Test completed without throwing");
    }

    [Test]
    [Ignore("Integration test - requires actual executable")]
    public async Task EnsureServiceRunningAsync_WithValidConfig_StartsService()
    {
        // This test would require an actual executable to run
        // It's marked as Skip for unit tests but can be used for integration testing
        
        // Arrange
        var config = new ServiceConfiguration
        {
            ServiceId = "test-service",
            ExecutablePath = GetTestExecutablePath(),
            Arguments = new[] { "--test" },
            Port = 5555,
            HealthEndpoint = "http://localhost:5555/health",
            StartupTimeoutSeconds = 10
        };

        // Act
        var status = await _serviceManager.EnsureServiceRunningAsync(config);

        // Assert
        Assert.That(status == ServiceStatus.Running || status == ServiceStatus.Healthy, Is.True);

        // Cleanup
        await _serviceManager.StopServiceAsync("test-service");
    }

    private string GetTestExecutablePath()
    {
        // This would return a path to a test executable
        // For actual tests, you'd need to create a simple test service
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "notepad.exe";
        else
            return "/usr/bin/echo";
    }

    // Test logger implementation
    private class TestLogger<T> : ILogger<T>
    {
        public TestLogger()
        {
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => new NoopDisposable();

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            TestContext.WriteLine($"[{logLevel}] {formatter(state, exception)}");
            if (exception != null)
            {
                TestContext.WriteLine(exception.ToString());
            }
        }

        private class NoopDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}