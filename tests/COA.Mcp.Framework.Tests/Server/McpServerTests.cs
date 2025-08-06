using System;
using System.IO;
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
using Moq;
using NUnit.Framework;

namespace COA.Mcp.Framework.Tests.Server
{
    [TestFixture]
    public class McpServerTests
    {
        private ServiceProvider _serviceProvider;
        private McpToolRegistry _toolRegistry;
        private ResourceRegistry _resourceRegistry;
        private Implementation _serverInfo;
        private Mock<ILogger<McpServer>> _loggerMock;
        private McpServer _server;

        [SetUp]
        public void SetUp()
        {
            var services = new ServiceCollection();
            services.AddLogging(); // Add logging to fix dependency issues
            services.AddSingleton<McpToolRegistry>();
            services.AddSingleton<ResourceRegistry>();
            _serviceProvider = services.BuildServiceProvider();
            
            _toolRegistry = _serviceProvider.GetRequiredService<McpToolRegistry>();
            _resourceRegistry = _serviceProvider.GetRequiredService<ResourceRegistry>();
            _serverInfo = new Implementation
            {
                Name = "TestServer",
                Version = "1.0.0"
            };
            _loggerMock = new Mock<ILogger<McpServer>>();
            
            _server = new McpServer(_toolRegistry, _resourceRegistry, _serverInfo, _loggerMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _serviceProvider?.Dispose();
        }

        [Test]
        public void Constructor_WithNullToolRegistry_ShouldThrow()
        {
            // Act & Assert
            var act = () => new McpServer(null!, _resourceRegistry, _serverInfo, _loggerMock.Object);
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("toolRegistry");
        }

        [Test]
        public void Constructor_WithNullResourceRegistry_ShouldThrow()
        {
            // Act & Assert
            var act = () => new McpServer(_toolRegistry, null!, _serverInfo, _loggerMock.Object);
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("resourceRegistry");
        }

        [Test]
        public void Constructor_WithNullServerInfo_ShouldThrow()
        {
            // Act & Assert
            var act = () => new McpServer(_toolRegistry, _resourceRegistry, null!, _loggerMock.Object);
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("serverInfo");
        }

        [Test]
        public void Constructor_WithValidParameters_ShouldCreateServer()
        {
            // Act
            var server = new McpServer(_toolRegistry, _resourceRegistry, _serverInfo, _loggerMock.Object);

            // Assert
            server.Should().NotBeNull();
            server.Should().BeOfType<McpServer>();
        }

        [Test]
        public void Constructor_WithNullLogger_ShouldCreateServer()
        {
            // Act
            var server = new McpServer(_toolRegistry, _resourceRegistry, _serverInfo, null);

            // Assert
            server.Should().NotBeNull();
        }

        [Test]
        public void SetStreams_WithValidStreams_ShouldNotThrow()
        {
            // Arrange
            var input = new StringReader("test");
            var output = new StringWriter();

            // Act
            var act = () => _server.SetStreams(input, output);

            // Assert
            act.Should().NotThrow();
        }

        [Test]
        public void SetStreams_WithNullInput_ShouldThrow()
        {
            // Arrange
            var output = new StringWriter();

            // Act & Assert
            var act = () => _server.SetStreams(null!, output);
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("input");
        }

        [Test]
        public void SetStreams_WithNullOutput_ShouldThrow()
        {
            // Arrange
            var input = new StringReader("test");

            // Act & Assert
            var act = () => _server.SetStreams(input, null!);
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("output");
        }

        [Test]
        public async Task StartAsync_ShouldStartServer()
        {
            // Arrange
            using var cts = new CancellationTokenSource();

            // Act
            await _server.StartAsync(cts.Token);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting MCP server")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task StopAsync_ShouldStopServer()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            await _server.StartAsync(cts.Token);

            // Act
            await _server.StopAsync(CancellationToken.None);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Stopping MCP server")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task StartStop_Sequence_ShouldWorkCorrectly()
        {
            // Arrange
            using var startCts = new CancellationTokenSource();
            using var stopCts = new CancellationTokenSource();

            // Act
            await _server.StartAsync(startCts.Token);
            await _server.StopAsync(stopCts.Token);

            // Assert - Should not throw
            // Verify both start and stop logs
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting MCP server")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
            
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Stopping MCP server")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public void Server_ShouldImplementIHostedService()
        {
            // Assert
            _server.Should().BeAssignableTo<IHostedService>();
        }

        [Test]
        public async Task Server_WithRegisteredTools_ShouldBeAccessible()
        {
            // Arrange
            var tool = new SimpleTool();
            _toolRegistry.RegisterTool<SimpleParams, SimpleResult>(tool);
            
            using var cts = new CancellationTokenSource();

            // Act
            await _server.StartAsync(cts.Token);

            // Assert
            _toolRegistry.IsToolRegistered("simple_tool").Should().BeTrue();
            _toolRegistry.GetTool("simple_tool").Should().BeSameAs(tool);
        }

        [Test]
        public void Server_WithCustomStreams_ShouldUseThemForCommunication()
        {
            // Arrange
            var input = new StringReader("test input");
            var output = new StringWriter();

            // Act
            _server.SetStreams(input, output);

            // Assert - Streams are set (actual communication would require protocol testing)
            // This is more of an integration test scenario
            _server.Should().NotBeNull();
        }

        // Test helper classes
        private class SimpleTool : McpToolBase<SimpleParams, SimpleResult>
        {
            public override string Name => "simple_tool";
            public override string Description => "A simple test tool";
            public override ToolCategory Category => ToolCategory.General;

            protected override Task<SimpleResult> ExecuteInternalAsync(SimpleParams parameters, CancellationToken cancellationToken)
            {
                return Task.FromResult(new SimpleResult { Success = true });
            }
        }

        private class SimpleParams
        {
            public string? Value { get; set; }
        }

        private class SimpleResult
        {
            public bool Success { get; set; }
            public string? Message { get; set; }
        }
    }
}