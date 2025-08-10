using System;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework.Server.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace COA.Mcp.Framework.Tests.Server.Services;

[TestFixture]
public class ServiceLifecycleHostTests
{
    private Mock<IServiceManager> _serviceManagerMock;
    private ServiceConfiguration _configuration;
    private ServiceLifecycleHost _host;

    [SetUp]
    public void Setup()
    {
        _serviceManagerMock = new Mock<IServiceManager>();
        _configuration = new ServiceConfiguration
        {
            ServiceId = "test-service",
            ExecutablePath = "test.exe",
            Port = 5000,
            HealthEndpoint = "http://localhost:5000/health"
        };
        
        var logger = new TestLogger<ServiceLifecycleHost>();
        _host = new ServiceLifecycleHost(_serviceManagerMock.Object, _configuration, logger);
    }

    [Test]
    public void Constructor_WithNullServiceManager_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new ServiceLifecycleHost(null!, _configuration));
    }

    [Test]
    public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new ServiceLifecycleHost(_serviceManagerMock.Object, null!));
    }

    [Test]
    public async Task StartAsync_CallsEnsureServiceRunning()
    {
        // Arrange
        _serviceManagerMock
            .Setup(m => m.EnsureServiceRunningAsync(It.IsAny<ServiceConfiguration>()))
            .ReturnsAsync(ServiceStatus.Healthy);

        // Act
        await _host.StartAsync(CancellationToken.None);

        // Assert
        _serviceManagerMock.Verify(m => m.EnsureServiceRunningAsync(_configuration), Times.Once);
    }

    [Test]
    public async Task StartAsync_WithHealthyService_Succeeds()
    {
        // Arrange
        _serviceManagerMock
            .Setup(m => m.EnsureServiceRunningAsync(It.IsAny<ServiceConfiguration>()))
            .ReturnsAsync(ServiceStatus.Healthy);

        // Act
        await _host.StartAsync(CancellationToken.None);

        // Assert - should complete without throwing
        Assert.Pass("Test completed without throwing");
    }

    [Test]
    public async Task StartAsync_WithFailedService_DoesNotThrow()
    {
        // Arrange
        _serviceManagerMock
            .Setup(m => m.EnsureServiceRunningAsync(It.IsAny<ServiceConfiguration>()))
            .ReturnsAsync(ServiceStatus.Failed);

        // Act & Assert - should not throw
        await _host.StartAsync(CancellationToken.None);
    }

    [Test]
    public async Task StartAsync_WithException_DoesNotThrow()
    {
        // Arrange
        _serviceManagerMock
            .Setup(m => m.EnsureServiceRunningAsync(It.IsAny<ServiceConfiguration>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act & Assert - should not throw
        await _host.StartAsync(CancellationToken.None);
    }

    [Test]
    public async Task StopAsync_CallsStopService()
    {
        // Arrange
        _serviceManagerMock
            .Setup(m => m.StopServiceAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _host.StopAsync(CancellationToken.None);

        // Assert
        _serviceManagerMock.Verify(m => m.StopServiceAsync(_configuration.ServiceId), Times.Once);
    }

    [Test]
    public async Task StopAsync_WithException_DoesNotThrow()
    {
        // Arrange
        _serviceManagerMock
            .Setup(m => m.StopServiceAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act & Assert - should not throw
        await _host.StopAsync(CancellationToken.None);
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
            Console.WriteLine($"[{logLevel}] {formatter(state, exception)}");
            if (exception != null)
            {
                Console.WriteLine(exception.ToString());
            }
        }

        private class NoopDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}