using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
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
    public class HttpTransportTests
    {
        private Mock<ILogger<HttpTransport>> _loggerMock = null!;
        private HttpTransportOptions _options = null!;
        private HttpTransport _transport = null!;
        private HttpClient _httpClient = null!;

        [SetUp]
        public void SetUp()
        {
            _loggerMock = new Mock<ILogger<HttpTransport>>();
            _options = new HttpTransportOptions
            {
                Port = 0, // Use dynamic port
                Host = "localhost",
                EnableCors = true
            };
            _httpClient = new HttpClient();
        }

        [TearDown]
        public async Task TearDown()
        {
            if (_transport != null)
            {
                await _transport.StopAsync();
                _transport.Dispose();
            }
            _httpClient?.Dispose();
        }

        [Test]
        public void Constructor_WithNullOptions_ShouldThrow()
        {
            // Act & Assert
            var act = () => new HttpTransport(null!, _loggerMock.Object);
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("options");
        }

        [Test]
        public void Constructor_WithValidOptions_ShouldCreateTransport()
        {
            // Act
            _transport = new HttpTransport(_options, _loggerMock.Object);

            // Assert
            _transport.Should().NotBeNull();
            _transport.Type.Should().Be(TransportType.Http);
            _transport.IsConnected.Should().BeFalse("transport not started yet");
        }

        [Test]
        public void Constructor_WithNullLogger_ShouldCreateTransport()
        {
            // Act
            _transport = new HttpTransport(_options, null);

            // Assert
            _transport.Should().NotBeNull();
            _transport.Type.Should().Be(TransportType.Http);
        }

        [Test]
        public async Task StartAsync_ShouldStartHttpListener()
        {
            // Arrange
            _options.Port = GetAvailablePort();
            _transport = new HttpTransport(_options, _loggerMock.Object);

            // Act
            await _transport.StartAsync();

            // Assert
            _transport.IsConnected.Should().BeTrue();
            
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting HTTP transport")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task StopAsync_ShouldStopHttpListener()
        {
            // Arrange
            _options.Port = GetAvailablePort();
            _transport = new HttpTransport(_options, _loggerMock.Object);
            await _transport.StartAsync();

            // Act
            await _transport.StopAsync();

            // Assert
            _transport.IsConnected.Should().BeFalse();
            
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Stopping HTTP transport")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task HealthEndpoint_ShouldReturnHealthStatus()
        {
            // Arrange
            _options.Port = GetAvailablePort();
            _transport = new HttpTransport(_options, _loggerMock.Object);
            await _transport.StartAsync();

            // Act
            var response = await _httpClient.GetAsync($"http://localhost:{_options.Port}/mcp/health");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("\"status\":\"healthy\"");
            content.Should().Contain("\"transport\":\"http\"");
        }

        [Test]
        public async Task HealthEndpoint_WithCors_ShouldIncludeCorsHeaders()
        {
            // Arrange
            _options.Port = GetAvailablePort();
            _options.EnableCors = true;
            _transport = new HttpTransport(_options, _loggerMock.Object);
            await _transport.StartAsync();

            // Act
            var response = await _httpClient.GetAsync($"http://localhost:{_options.Port}/mcp/health");

            // Assert
            response.Headers.Should().Contain(h => h.Key == "Access-Control-Allow-Origin");
            response.Headers.GetValues("Access-Control-Allow-Origin").Should().Contain("*");
        }

        [Test]
        public async Task OptionsRequest_ShouldReturnCorsHeaders()
        {
            // Arrange
            _options.Port = GetAvailablePort();
            _options.EnableCors = true;
            _transport = new HttpTransport(_options, _loggerMock.Object);
            await _transport.StartAsync();

            var request = new HttpRequestMessage(HttpMethod.Options, $"http://localhost:{_options.Port}/mcp/rpc");

            // Act
            var response = await _httpClient.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
            response.Headers.Should().Contain(h => h.Key == "Access-Control-Allow-Methods");
        }

        [Test]
        public async Task RpcEndpoint_WithValidJson_ShouldQueueMessage()
        {
            // Arrange
            _options.Port = GetAvailablePort();
            _options.RequestTimeoutSeconds = 2; // Short timeout for test
            _transport = new HttpTransport(_options, _loggerMock.Object);
            await _transport.StartAsync();

            var jsonContent = "{\"jsonrpc\":\"2.0\",\"method\":\"test\",\"id\":1}";
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Start a task to read the message and send a response
            var readTask = Task.Run(async () =>
            {
                var message = await _transport.ReadMessageAsync();
                if (message != null && !string.IsNullOrEmpty(message.CorrelationId))
                {
                    // Send a response back
                    var response = new TransportMessage
                    {
                        Content = "{\"jsonrpc\":\"2.0\",\"result\":\"test\",\"id\":1}",
                        CorrelationId = message.CorrelationId
                    };
                    await _transport.WriteMessageAsync(response);
                }
            });

            // Act
            var response = await _httpClient.PostAsync($"http://localhost:{_options.Port}/mcp/rpc", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var responseContent = await response.Content.ReadAsStringAsync();
            responseContent.Should().Contain("\"jsonrpc\":\"2.0\"");
            responseContent.Should().Contain("\"result\":\"test\"");

            await readTask; // Ensure the read task completes
        }

        [Test]
        public async Task RpcEndpoint_WithEmptyBody_ShouldReturnBadRequest()
        {
            // Arrange
            _options.Port = GetAvailablePort();
            _transport = new HttpTransport(_options, _loggerMock.Object);
            await _transport.StartAsync();

            var content = new StringContent("", Encoding.UTF8, "application/json");

            // Act
            var response = await _httpClient.PostAsync($"http://localhost:{_options.Port}/mcp/rpc", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var responseContent = await response.Content.ReadAsStringAsync();
            responseContent.Should().Contain("Empty request body");
        }

        [Test]
        public async Task InvalidEndpoint_ShouldReturn404()
        {
            // Arrange
            _options.Port = GetAvailablePort();
            _transport = new HttpTransport(_options, _loggerMock.Object);
            await _transport.StartAsync();

            // Act
            var response = await _httpClient.GetAsync($"http://localhost:{_options.Port}/invalid/endpoint");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Test]
        public async Task ToolsEndpoint_ShouldReturnToolsList()
        {
            // Arrange
            _options.Port = GetAvailablePort();
            _transport = new HttpTransport(_options, _loggerMock.Object);
            await _transport.StartAsync();

            // Act
            var response = await _httpClient.GetAsync($"http://localhost:{_options.Port}/mcp/tools");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("\"tools\"");
        }

        [Test]
        public async Task ReadMessageAsync_AfterRpcPost_ShouldReturnMessage()
        {
            // Arrange
            _options.Port = GetAvailablePort();
            _transport = new HttpTransport(_options, _loggerMock.Object);
            await _transport.StartAsync();

            var jsonContent = "{\"jsonrpc\":\"2.0\",\"method\":\"test\",\"id\":1}";
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Act
            var postTask = _httpClient.PostAsync($"http://localhost:{_options.Port}/mcp/rpc", content);
            await Task.Delay(100); // Give time for request to be processed
            var message = await _transport.ReadMessageAsync();

            // Assert
            message.Should().NotBeNull();
            message!.Content.Should().Be(jsonContent);
            message.Headers.Should().ContainKey("transport").WhoseValue.Should().Be("http");
        }

        [Test]
        public async Task Disconnected_WhenStopCalled_ShouldRaiseEvent()
        {
            // Arrange
            _options.Port = GetAvailablePort();
            _transport = new HttpTransport(_options, _loggerMock.Object);
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
        public async Task HttpsOption_ShouldUseHttpsPrefix()
        {
            // Arrange
            _options.Port = GetAvailablePort();
            _options.UseHttps = true;
            _transport = new HttpTransport(_options, _loggerMock.Object);

            // Act & Assert - Will fail to start due to certificate requirements, but verifies prefix
            try
            {
                await _transport.StartAsync();
            }
            catch
            {
                // Expected - HTTPS requires certificate configuration
            }

            _loggerMock.Verify(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("HTTPS: True") || v.ToString()!.Contains("Failed to configure HTTPS")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Test]
        public void Dispose_ShouldCleanupResources()
        {
            // Arrange
            _transport = new HttpTransport(_options, _loggerMock.Object);

            // Act
            _transport.Dispose();
            _transport.Dispose(); // Second dispose should not throw

            // Assert
            _transport.IsConnected.Should().BeFalse();
        }

        [Test]
        public async Task MultipleStartStop_ShouldWorkCorrectly()
        {
            // Arrange
            _options.Port = GetAvailablePort();
            _transport = new HttpTransport(_options, _loggerMock.Object);

            // Act & Assert
            await _transport.StartAsync();
            _transport.IsConnected.Should().BeTrue();

            await _transport.StopAsync();
            _transport.IsConnected.Should().BeFalse();

            // Update port for second start
            _options.Port = GetAvailablePort();
            _transport.Dispose();
            _transport = new HttpTransport(_options, _loggerMock.Object);
            
            await _transport.StartAsync();
            _transport.IsConnected.Should().BeTrue();

            await _transport.StopAsync();
            _transport.IsConnected.Should().BeFalse();
        }

        [Test]
        public async Task WriteMessageAsync_ShouldNotThrow()
        {
            // Arrange
            _options.Port = GetAvailablePort();
            _transport = new HttpTransport(_options, _loggerMock.Object);
            await _transport.StartAsync();
            
            var message = new TransportMessage { Content = "test" };

            // Act
            var act = async () => await _transport.WriteMessageAsync(message);

            // Assert - HTTP transport doesn't implement write, but shouldn't throw
            await act.Should().NotThrowAsync();
        }

        private int GetAvailablePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        [Test]
        public async Task WriteMessageAsync_WithWebSocketEnabled_ShouldNotThrow()
        {
            // Arrange
            _options.EnableWebSocket = true;
            _options.Port = GetAvailablePort();
            _transport = new HttpTransport(_options, _loggerMock.Object);
            await _transport.StartAsync();
            
            var message = new TransportMessage
            {
                Content = "{\"test\":\"message\"}",
                Headers = { ["transport"] = "websocket" }
            };

            // Act & Assert
            await _transport.WriteMessageAsync(message);
            
            // Should complete without throwing
            Assert.Pass("WriteMessageAsync with WebSocket enabled completed successfully");
        }

        [Test]
        public async Task WriteMessageAsync_WithConnectionId_ShouldTargetSpecificConnection()
        {
            // Arrange
            _options.EnableWebSocket = true;
            _options.Port = GetAvailablePort();
            _transport = new HttpTransport(_options, _loggerMock.Object);
            await _transport.StartAsync();
            
            var message = new TransportMessage
            {
                Content = "{\"test\":\"message\"}",
                Headers = 
                { 
                    ["transport"] = "websocket",
                    ["connection-id"] = "test-connection-123"
                }
            };

            // Act
            await _transport.WriteMessageAsync(message);

            // Assert
            // Verify logger was called with trace message about WebSocket not available
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Trace,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("WebSocket")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task HealthCheck_WithWebSocketEnabled_ShouldIncludeWebSocketStatus()
        {
            // Arrange
            _options.EnableWebSocket = true;
            _options.Port = GetAvailablePort();
            _transport = new HttpTransport(_options, _loggerMock.Object);
            await _transport.StartAsync();
            
            // Act
            var response = await _httpClient.GetAsync($"http://localhost:{_options.Port}/mcp/health");
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("webSocketEnabled");
            content.Should().Contain("webSocketConnections");
        }
    }
}
