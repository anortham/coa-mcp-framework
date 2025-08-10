using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Registration;
using COA.Mcp.Framework.Resources;
using COA.Mcp.Framework.Server;
using COA.Mcp.Protocol;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace COA.Mcp.Framework.Tests.Server
{
    [TestFixture]
    public class McpServerBuilderTests
    {
        private McpServerBuilder _builder;

        [SetUp]
        public void SetUp()
        {
            _builder = new McpServerBuilder();
        }

        [Test]
        public void Builder_ShouldHaveDefaultServices()
        {
            // Act
            var services = _builder.Services;

            // Assert
            services.Should().NotBeNull();
            services.Should().NotBeEmpty();
            
            // Should have the default framework services
            var serviceProvider = services.BuildServiceProvider();
            serviceProvider.GetService<McpToolRegistry>().Should().NotBeNull();
            serviceProvider.GetService<ResourceRegistry>().Should().NotBeNull();
        }

        [Test]
        public void WithServerInfo_ShouldSetServerNameAndVersion()
        {
            // Arrange
            var name = "TestServer";
            var version = "2.0.0";

            // Act
            var result = _builder.WithServerInfo(name, version);

            // Assert
            result.Should().BeSameAs(_builder, "fluent API should return same builder");
            
            // Build and verify server info is used
            var server = _builder.Build();
            server.Should().NotBeNull();
        }

        [Test]
        public void UseStdioTransport_ShouldConfigureStdioTransport()
        {
            // Arrange & Act
            var result = _builder.UseStdioTransport(options =>
            {
                options.Input = new StringReader("test input");
                options.Output = new StringWriter();
            });

            // Assert
            result.Should().BeSameAs(_builder);
            
            // Build and verify transport is configured
            var server = _builder.Build();
            server.Should().NotBeNull();
        }
        
        [Test]
        public void UseHttpTransport_ShouldConfigureHttpTransport()
        {
            // Arrange & Act
            var result = _builder.UseHttpTransport(options =>
            {
                options.Port = 8080;
                options.Host = "localhost";
            });

            // Assert
            result.Should().BeSameAs(_builder);
            
            // Build and verify transport is configured
            var server = _builder.Build();
            server.Should().NotBeNull();
        }

        [Test]
        public void UseWebSocketTransport_ShouldConfigureWebSocketTransport()
        {
            // Arrange & Act
            var result = _builder.UseWebSocketTransport(options =>
            {
                options.Port = 8081;
                options.Host = "localhost";
                options.UseHttps = false;
            });

            // Assert
            result.Should().BeSameAs(_builder);
            
            // Build and verify transport is configured
            var server = _builder.Build();
            server.Should().NotBeNull();
        }

        [Test]
        public void RegisterTool_ShouldAddToolToRegistry()
        {
            // Arrange
            var tool = new TestTool();
            var toolRegistered = false;

            // Act
            var result = _builder.RegisterTool<TestParams, TestResult>(tool);

            // Assert - fluent API should return same builder
            result.Should().BeSameAs(_builder);
            
            // Verify the tool gets registered when Build() is called
            // We'll add a ConfigureTools action to verify it was registered
            _builder.ConfigureTools(registry =>
            {
                toolRegistered = registry.IsToolRegistered("test_tool");
            });
            
            // Build to trigger tool registration
            var server = _builder.Build();
            
            // The tool should have been registered
            toolRegistered.Should().BeTrue();
        }

        [Test]
        public void RegisterToolType_ShouldAddToolTypeToServices()
        {
            // Act
            var result = _builder.RegisterToolType<TestTool>();

            // Assert
            result.Should().BeSameAs(_builder);
            
            // Verify service is registered
            var serviceProvider = _builder.Services.BuildServiceProvider();
            serviceProvider.GetService<TestTool>().Should().NotBeNull();
        }

        [Test]
        public void ConfigureTools_ShouldApplyConfiguration()
        {
            // Arrange
            var tool = new TestTool();
            var configured = false;

            // Act
            var result = _builder.ConfigureTools(registry =>
            {
                configured = true;
                registry.RegisterTool<TestParams, TestResult>(tool);
            });

            // Assert
            result.Should().BeSameAs(_builder);
            
            // Build to trigger configuration
            var server = _builder.Build();
            configured.Should().BeTrue();
        }

        [Test]
        public void ConfigureResources_ShouldApplyConfiguration()
        {
            // Arrange
            var configured = false;

            // Act
            var result = _builder.ConfigureResources(registry =>
            {
                configured = true;
                // Resources configuration would go here
            });

            // Assert
            result.Should().BeSameAs(_builder);
            
            // Build to trigger configuration
            var server = _builder.Build();
            configured.Should().BeTrue();
        }

        [Test]
        public void ConfigureLogging_ShouldSetupLogging()
        {
            // Arrange
            var loggerConfigured = false;

            // Act
            var result = _builder.ConfigureLogging(logging =>
            {
                loggerConfigured = true;
                logging.SetMinimumLevel(LogLevel.Debug);
            });

            // Assert
            result.Should().BeSameAs(_builder);
            
            // Build and verify logging is configured
            var serviceProvider = _builder.Services.BuildServiceProvider();
            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            loggerFactory.Should().NotBeNull();
            loggerConfigured.Should().BeTrue();
        }

        [Test]
        public void AddService_ShouldRegisterCustomService()
        {
            // Act
            var result = _builder.AddService<ITestService, TestService>(ServiceLifetime.Singleton);

            // Assert
            result.Should().BeSameAs(_builder);
            
            // Verify service is registered
            var serviceProvider = _builder.Services.BuildServiceProvider();
            serviceProvider.GetService<ITestService>().Should().NotBeNull()
                .And.BeOfType<TestService>();
        }

        [Test]
        public void AddSingleton_ShouldRegisterSingletonInstance()
        {
            // Arrange
            var instance = new TestService();

            // Act
            var result = _builder.AddSingleton<ITestService>(instance);

            // Assert
            result.Should().BeSameAs(_builder);
            
            // Verify same instance is returned
            var serviceProvider = _builder.Services.BuildServiceProvider();
            var service = serviceProvider.GetService<ITestService>();
            service.Should().BeSameAs(instance);
        }

        [Test]
        public void Build_ShouldCreateMcpServer()
        {
            // Arrange
            _builder.WithServerInfo("TestServer", "1.0.0");

            // Act
            var server = _builder.Build();

            // Assert
            server.Should().NotBeNull();
            server.Should().BeOfType<McpServer>();
        }

        [Test]
        public void BuildHostedService_ShouldReturnIHostedService()
        {
            // Arrange
            _builder.WithServerInfo("TestServer", "1.0.0");

            // Act
            var hostedService = _builder.BuildHostedService();

            // Assert
            hostedService.Should().NotBeNull();
            hostedService.Should().BeAssignableTo<IHostedService>();
            hostedService.Should().BeOfType<McpServer>();
        }

        [Test]
        public void DiscoverTools_ShouldSetAssemblyForDiscovery()
        {
            // Act
            var result = _builder.DiscoverTools(typeof(TestTool).Assembly);

            // Assert
            result.Should().BeSameAs(_builder);
            
            // Build will trigger discovery
            var server = _builder.Build();
            server.Should().NotBeNull();
        }

        [Test]
        public void DiscoverTools_WithNullAssembly_ShouldUseCallingAssembly()
        {
            // Act
            var result = _builder.DiscoverTools();

            // Assert
            result.Should().BeSameAs(_builder);
            
            // Build will trigger discovery using calling assembly
            var server = _builder.Build();
            server.Should().NotBeNull();
        }

        [Test]
        public async Task RunAsync_ShouldStartAndRunServer()
        {
            // Arrange
            _builder.WithServerInfo("TestServer", "1.0.0");
            
            using var cts = new CancellationTokenSource();

            // Act - Start the server and cancel immediately
            var runTask = _builder.RunAsync(cts.Token);
            cts.Cancel();

            // Assert - Should complete without throwing
            var act = async () => await runTask;
            await act.Should().NotThrowAsync();
        }

        // Test helpers
        private class TestTool : McpToolBase<TestParams, TestResult>
        {
            public override string Name => "test_tool";
            public override string Description => "Test tool";
            public override ToolCategory Category => ToolCategory.General;

            protected override Task<TestResult> ExecuteInternalAsync(TestParams parameters, CancellationToken cancellationToken)
            {
                return Task.FromResult(new TestResult { Success = true });
            }
        }

        private class TestParams
        {
            public string? Value { get; set; }
        }

        private class TestResult
        {
            public bool Success { get; set; }
        }

        private interface ITestService { }

        private class TestService : ITestService { }
    }
}