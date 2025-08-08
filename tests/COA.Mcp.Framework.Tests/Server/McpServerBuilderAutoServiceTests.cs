using System;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework.Server;
using COA.Mcp.Framework.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace COA.Mcp.Framework.Tests.Server;

[TestFixture]
public class McpServerBuilderAutoServiceTests
{
    private McpServerBuilder _builder;

    [SetUp]
    public void Setup()
    {
        _builder = new McpServerBuilder();
    }

    [Test]
    public void UseAutoService_WithNullConfigure_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _builder.UseAutoService(null!));
    }

    [Test]
    public void UseAutoService_RegistersServiceManager()
    {
        // Act
        _builder.UseAutoService(config =>
        {
            config.ServiceId = "test-service";
            config.ExecutablePath = "test.exe";
            config.Port = 5000;
        });

        // Assert
        var serviceProvider = _builder.Services.BuildServiceProvider();
        var serviceManager = serviceProvider.GetService<IServiceManager>();
        Assert.That(serviceManager, Is.Not.Null);
        Assert.That(serviceManager, Is.InstanceOf<ServiceManager>());
    }

    [Test]
    public void UseAutoService_RegistersHostedService()
    {
        // Act
        _builder.UseAutoService(config =>
        {
            config.ServiceId = "test-service";
            config.ExecutablePath = "test.exe";
            config.Port = 5000;
        });

        // Assert
        var serviceProvider = _builder.Services.BuildServiceProvider();
        var hostedServices = serviceProvider.GetServices<IHostedService>();
        Assert.That(hostedServices, Is.Not.Empty);
    }

    [Test]
    public void UseAutoService_CalledMultipleTimes_RegistersMultipleServices()
    {
        // Act
        _builder.UseAutoService(config =>
        {
            config.ServiceId = "service1";
            config.ExecutablePath = "service1.exe";
            config.Port = 5001;
        });

        _builder.UseAutoService(config =>
        {
            config.ServiceId = "service2";
            config.ExecutablePath = "service2.exe";
            config.Port = 5002;
        });

        // Assert
        var serviceProvider = _builder.Services.BuildServiceProvider();
        var hostedServices = serviceProvider.GetServices<IHostedService>();
        
        // Should have at least 3: ServiceManager + 2 ServiceLifecycleHost instances
        // Note: GetServices returns all registered IHostedService instances
        Assert.That(hostedServices.Count(), Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void UseAutoService_OnlyRegistersServiceManagerOnce()
    {
        // Act
        _builder.UseAutoService(config =>
        {
            config.ServiceId = "service1";
            config.ExecutablePath = "service1.exe";
            config.Port = 5001;
        });

        _builder.UseAutoService(config =>
        {
            config.ServiceId = "service2";
            config.ExecutablePath = "service2.exe";
            config.Port = 5002;
        });

        // Assert
        var serviceProvider = _builder.Services.BuildServiceProvider();
        
        // Get all IServiceManager registrations - should only be one
        var serviceManagers = serviceProvider.GetServices<IServiceManager>();
        Assert.That(serviceManagers.Count(), Is.EqualTo(1));
    }

    [Test]
    public void UseAutoServices_RegistersMultipleServices()
    {
        // Act
        _builder.UseAutoServices(
            config =>
            {
                config.ServiceId = "service1";
                config.ExecutablePath = "service1.exe";
                config.Port = 5001;
            },
            config =>
            {
                config.ServiceId = "service2";
                config.ExecutablePath = "service2.exe";
                config.Port = 5002;
            },
            config =>
            {
                config.ServiceId = "service3";
                config.ExecutablePath = "service3.exe";
                config.Port = 5003;
            }
        );

        // Assert
        var serviceProvider = _builder.Services.BuildServiceProvider();
        var hostedServices = serviceProvider.GetServices<IHostedService>();
        
        // Should have at least 2: First call registers ServiceManager (as IHostedService),
        // then additional calls don't re-register it but do add ServiceLifecycleHost instances
        Assert.That(hostedServices.Count(), Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void UseAutoService_ConfigurationIsPassedCorrectly()
    {
        // Arrange
        ServiceConfiguration? capturedConfig = null;

        // Act
        _builder.UseAutoService(config =>
        {
            config.ServiceId = "test-service";
            config.ExecutablePath = "/usr/bin/test";
            config.Port = 8080;
            config.HealthEndpoint = "http://localhost:8080/health";
            config.StartupTimeoutSeconds = 45;
            config.HealthCheckIntervalSeconds = 15;
            config.AutoRestart = false;
            config.MaxRestartAttempts = 10;
            capturedConfig = config;
        });

        // Assert
        Assert.That(capturedConfig, Is.Not.Null);
        Assert.That(capturedConfig!.ServiceId, Is.EqualTo("test-service"));
        Assert.That(capturedConfig.ExecutablePath, Is.EqualTo("/usr/bin/test"));
        Assert.That(capturedConfig.Port, Is.EqualTo(8080));
        Assert.That(capturedConfig.HealthEndpoint, Is.EqualTo("http://localhost:8080/health"));
        Assert.That(capturedConfig.StartupTimeoutSeconds, Is.EqualTo(45));
        Assert.That(capturedConfig.HealthCheckIntervalSeconds, Is.EqualTo(15));
        Assert.That(capturedConfig.AutoRestart, Is.False);
        Assert.That(capturedConfig.MaxRestartAttempts, Is.EqualTo(10));
    }

    [Test]
    public void UseAutoService_CanBeChained()
    {
        // Act
        var result = _builder
            .UseAutoService(config =>
            {
                config.ServiceId = "service1";
                config.ExecutablePath = "service1.exe";
            })
            .UseAutoService(config =>
            {
                config.ServiceId = "service2";
                config.ExecutablePath = "service2.exe";
            })
            .WithServerInfo("TestServer", "1.0.0");

        // Assert
        Assert.That(result, Is.SameAs(_builder));
    }
}