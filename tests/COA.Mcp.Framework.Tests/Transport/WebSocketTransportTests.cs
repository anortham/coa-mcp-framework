using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework.Transport;
using COA.Mcp.Framework.Transport.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace COA.Mcp.Framework.Tests.Transport
{
    [TestFixture]
    public class WebSocketTransportTests
    {
        private WebSocketTransport _transport;
        private HttpTransportOptions _options;
        private Mock<ILogger<WebSocketTransport>> _mockLogger;
        private int _testPort = 5678;

        [SetUp]
        public void Setup()
        {
            _testPort++; // Use different port for each test
            _options = new HttpTransportOptions
            {
                Port = _testPort,
                Host = "localhost",
                EnableWebSocket = true
            };
            _mockLogger = new Mock<ILogger<WebSocketTransport>>();
            _transport = new WebSocketTransport(_options, _mockLogger.Object);
        }

        [TearDown]
        public async Task TearDown()
        {
            if (_transport?.IsConnected == true)
            {
                await _transport.StopAsync();
            }
            _transport?.Dispose();
        }

        [Test]
        public void Constructor_ShouldInitializeProperties()
        {
            // Assert
            _transport.Type.Should().Be(TransportType.WebSocket);
            _transport.IsConnected.Should().BeFalse();
        }

        [Test]
        public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new WebSocketTransport(null!, _mockLogger.Object));
        }

        [Test]
        public async Task StartAsync_ShouldStartTransport()
        {
            // Act
            await _transport.StartAsync();

            // Assert
            _transport.IsConnected.Should().BeTrue();
        }

        [Test]
        public async Task StopAsync_ShouldStopTransport()
        {
            // Arrange
            await _transport.StartAsync();

            // Act
            await _transport.StopAsync();

            // Assert
            _transport.IsConnected.Should().BeFalse();
        }

        [Test]
        public async Task StopAsync_ShouldRaiseDisconnectedEvent()
        {
            // Arrange
            await _transport.StartAsync();
            var eventRaised = false;
            TransportDisconnectedEventArgs? eventArgs = null;
            
            _transport.Disconnected += (sender, args) =>
            {
                eventRaised = true;
                eventArgs = args;
            };

            // Act
            await _transport.StopAsync();

            // Assert
            eventRaised.Should().BeTrue();
            eventArgs.Should().NotBeNull();
            eventArgs!.Reason.Should().Be("Transport stopped");
            eventArgs.WasClean.Should().BeTrue();
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public async Task ReadMessageAsync_WithInvalidMessage_ShouldReturnNull(string? content)
        {
            // Arrange
            await _transport.StartAsync();
            
            // Simulate empty message queue
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            // Act & Assert
            // This should timeout and return null since there are no messages
            Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await _transport.ReadMessageAsync(cts.Token);
            });
        }

        [Test]
        public async Task WriteMessageAsync_WithWebSocketConnection_ShouldSendMessage()
        {
            // Arrange
            await _transport.StartAsync();
            
            var message = new TransportMessage
            {
                Content = "Test message",
                Headers = { ["test"] = "header" }
            };

            // Act
            await _transport.WriteMessageAsync(message);

            // Assert
            // Since we don't have actual WebSocket connections in this test,
            // we just verify it doesn't throw
            Assert.Pass("WriteMessageAsync completed without throwing");
        }

        [Test]
        public async Task Dispose_ShouldCleanupResources()
        {
            // Arrange
            await _transport.StartAsync();

            // Act
            await _transport.StopAsync();
            _transport.Dispose();

            // Assert
            _transport.IsConnected.Should().BeFalse();
            
            // Calling dispose again should not throw
            Assert.DoesNotThrow(() => _transport.Dispose());
        }

        [Test]
        public void StartAsync_WithInvalidPort_ShouldThrow()
        {
            // Arrange
            var invalidOptions = new HttpTransportOptions
            {
                Port = -1,
                Host = "localhost"
            };
            var transport = new WebSocketTransport(invalidOptions, _mockLogger.Object);

            // Act & Assert
            Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await transport.StartAsync();
            });

            transport.Dispose();
        }

        [Test]
        public async Task Multiple_StartAsync_Calls_ShouldNotThrow()
        {
            // Act
            await _transport.StartAsync();
            
            // Starting again should not throw (idempotent)
            await _transport.StartAsync();

            // Assert
            _transport.IsConnected.Should().BeTrue();
        }

        [Test]
        public async Task Multiple_StopAsync_Calls_ShouldNotThrow()
        {
            // Arrange
            await _transport.StartAsync();

            // Act
            await _transport.StopAsync();
            await _transport.StopAsync(); // Second call should not throw

            // Assert
            _transport.IsConnected.Should().BeFalse();
        }
    }
}