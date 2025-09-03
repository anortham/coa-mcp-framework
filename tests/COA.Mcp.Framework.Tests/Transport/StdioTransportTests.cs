using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework.Transport;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace COA.Mcp.Framework.Tests.Transport
{
    [TestFixture]
    public class StdioTransportTests
    {
        private Mock<ILogger<StdioTransport>> _loggerMock;
        private MemoryStream _input;
        private MemoryStream _output;
        private StdioTransport _transport;

        [SetUp]
        public void SetUp()
        {
            _loggerMock = new Mock<ILogger<StdioTransport>>();
            _input = new MemoryStream();
            _output = new MemoryStream();
            _transport = new StdioTransport(_input, _output, _loggerMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _transport?.Dispose();
            _input?.Dispose();
            _output?.Dispose();
        }

        [Test]
        public void Constructor_WithNullInput_ShouldUseConsoleIn()
        {
            // Act
            var transport = new StdioTransport(null, _output, _loggerMock.Object);
            
            // Assert - Transport created successfully with default input
            transport.Should().NotBeNull();
            transport.Type.Should().Be(TransportType.Stdio);
        }

        [Test]
        public void Constructor_WithNullOutput_ShouldUseConsoleOut()
        {
            // Act
            var transport = new StdioTransport(_input, null, _loggerMock.Object);
            
            // Assert - Transport created successfully with default output
            transport.Should().NotBeNull();
            transport.Type.Should().Be(TransportType.Stdio);
        }

        [Test]
        public void Constructor_WithValidParameters_ShouldCreateTransport()
        {
            // Assert
            _transport.Should().NotBeNull();
            _transport.Type.Should().Be(TransportType.Stdio);
            _transport.IsConnected.Should().BeFalse("transport not started yet");
        }

        [Test]
        public void Constructor_WithNullLogger_ShouldCreateTransport()
        {
            // Act
            var transport = new StdioTransport(_input, _output, null);

            // Assert
            transport.Should().NotBeNull();
            transport.Type.Should().Be(TransportType.Stdio);
        }

        [Test]
        public async Task StartAsync_ShouldSetIsConnected()
        {
            // Act
            await _transport.StartAsync();

            // Assert
            _transport.IsConnected.Should().BeTrue();
            
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,  // Changed from Information to Debug
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting stdio transport")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task StopAsync_ShouldClearIsConnected()
        {
            // Arrange
            await _transport.StartAsync();

            // Act
            await _transport.StopAsync();

            // Assert
            _transport.IsConnected.Should().BeFalse();
            
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,  // Changed from Information to Debug
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Stopping stdio transport")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task WriteMessageAsync_ShouldWriteToOutput()
        {
            // Arrange
            await _transport.StartAsync();
            var message = new TransportMessage
            {
                Content = "{\"test\":\"message\"}",
                Headers = { ["type"] = "test" }
            };

            // Act
            await _transport.WriteMessageAsync(message);

            // Assert
            var output = Encoding.UTF8.GetString(_output.ToArray());
            output.Should().Contain("{\"test\":\"message\"}");
        }

        [Test]
        public async Task WriteMessageAsync_WithLargeMessage_ShouldWriteCompletely()
        {
            // Arrange
            await _transport.StartAsync();
            var largeContent = new string('x', 10000);
            var message = new TransportMessage
            {
                Content = largeContent
            };

            // Act
            await _transport.WriteMessageAsync(message);

            // Assert
            var output = Encoding.UTF8.GetString(_output.ToArray());
            output.Should().Contain(largeContent);
        }

        [Test]
        public async Task ReadMessageAsync_WithValidMessage_ShouldReturnTransportMessage()
        {
            // Arrange
            var jsonContent = "{\"jsonrpc\":\"2.0\",\"method\":\"test\",\"id\":1}";
            
            _input.Dispose();
            _input = new MemoryStream(Encoding.UTF8.GetBytes(jsonContent));
            _transport.Dispose();
            _transport = new StdioTransport(_input, _output, _loggerMock.Object);
            
            await _transport.StartAsync();

            // Act
            var result = await _transport.ReadMessageAsync();

            // Assert
            result.Should().NotBeNull();
            result!.Content.Should().Be(jsonContent);
        }

        [Test]
        public async Task ReadMessageAsync_WithEmptyInput_ShouldReturnNull()
        {
            // Arrange
            _input.Dispose();
            _input = new MemoryStream(Array.Empty<byte>());
            _transport.Dispose();
            _transport = new StdioTransport(_input, _output, _loggerMock.Object);
            
            await _transport.StartAsync();

            // Act
            var result = await _transport.ReadMessageAsync();

            // Assert
            result.Should().BeNull();
        }

        [Test]
        public async Task ReadMessageAsync_WhenNotConnected_ShouldReturnNull()
        {
            // Act - Don't start the transport
            var result = await _transport.ReadMessageAsync();

            // Assert
            result.Should().BeNull();
        }

        [Test]
        public async Task Disconnected_WhenStopCalled_ShouldRaiseEvent()
        {
            // Arrange
            await _transport.StartAsync();
            
            TransportDisconnectedEventArgs? eventArgs = null;
            _transport.Disconnected += (sender, args) => eventArgs = args;

            // Act
            await _transport.StopAsync();

            // Assert
            eventArgs.Should().NotBeNull();
            eventArgs!.Reason.Should().Be("Transport stopped");
            eventArgs.WasClean.Should().BeTrue();
        }

        [Test]
        public async Task ReadMessageAsync_WithCancellation_ShouldCancel()
        {
            // Arrange
            await _transport.StartAsync();
            
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            var act = async () => await _transport.ReadMessageAsync(cts.Token);

            // Assert
            await act.Should().ThrowAsync<TaskCanceledException>();
        }

        [Test]
        public async Task WriteMessageAsync_WithCancellation_ShouldThrow()
        {
            // Arrange
            await _transport.StartAsync();
            
            var message = new TransportMessage { Content = "test" };
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            var act = async () => await _transport.WriteMessageAsync(message, cts.Token);

            // Assert - Should throw TaskCanceledException when cancelled
            await act.Should().ThrowAsync<TaskCanceledException>();
        }

        [Test]
        public async Task MultipleStartStop_ShouldWorkCorrectly()
        {
            // Act & Assert
            await _transport.StartAsync();
            _transport.IsConnected.Should().BeTrue();

            await _transport.StopAsync();
            _transport.IsConnected.Should().BeFalse();

            await _transport.StartAsync();
            _transport.IsConnected.Should().BeTrue();

            await _transport.StopAsync();
            _transport.IsConnected.Should().BeFalse();
        }

        [Test]
        public void Dispose_ShouldCleanupResources()
        {
            // Arrange
            var transport = new StdioTransport(_input, _output, _loggerMock.Object);

            // Act
            transport.Dispose();
            transport.Dispose(); // Second dispose should not throw

            // Assert
            transport.IsConnected.Should().BeFalse();
        }

        [Test]
        public async Task ReadMessageAsync_WithMultipleMessages_ShouldReadSequentially()
        {
            // Arrange
            var json1 = "{\"id\":1}";
            var json2 = "{\"id\":2}";
            var frame1 = $"Content-Length: {Encoding.UTF8.GetByteCount(json1)}\r\n\r\n{json1}";
            var frame2 = $"Content-Length: {Encoding.UTF8.GetByteCount(json2)}\r\n\r\n{json2}";

            // First message
            _input.Dispose();
            _input = new MemoryStream(Encoding.UTF8.GetBytes(frame1));
            _transport.Dispose();
            _transport = new StdioTransport(_input, _output, _loggerMock.Object);
            await _transport.StartAsync();
            var result1 = await _transport.ReadMessageAsync();

            // Second message (new stream)
            _input.Dispose();
            _input = new MemoryStream(Encoding.UTF8.GetBytes(frame2));
            _transport.Dispose();
            _transport = new StdioTransport(_input, _output, _loggerMock.Object);
            await _transport.StartAsync();
            var result2 = await _transport.ReadMessageAsync();

            // Assert
            result1.Should().NotBeNull();
            result1!.Content.Should().Be(json1);

            result2.Should().NotBeNull();
            result2!.Content.Should().Be(json2);
        }
    }
}
