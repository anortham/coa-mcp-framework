using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Client;
using COA.Mcp.Client.Configuration;
using COA.Mcp.Protocol;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using NUnit.Framework;

namespace COA.Mcp.Client.Tests
{
    [TestFixture]
    public class McpHttpClientTests
    {
        private Mock<HttpMessageHandler> _mockHttpHandler = null!;
        private HttpClient _httpClient = null!;
        private McpClientOptions _options = null!;
        private Mock<ILogger<McpHttpClient>> _mockLogger = null!;
        private McpHttpClient _client = null!;

        [SetUp]
        public void Setup()
        {
            _mockHttpHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpHandler.Object);
            _options = new McpClientOptions
            {
                BaseUrl = "http://localhost:5000",
                TimeoutSeconds = 30
            };
            _mockLogger = new Mock<ILogger<McpHttpClient>>();
        }

        [TearDown]
        public void TearDown()
        {
            _client?.Dispose();
            _httpClient?.Dispose();
        }

        [Test]
        public void Constructor_WithValidOptions_ShouldInitialize()
        {
            // Act
            _client = new McpHttpClient(_options, _httpClient, _mockLogger.Object);

            // Assert
            _client.Should().NotBeNull();
            _client.IsConnected.Should().BeFalse();
            _client.ServerInfo.Should().BeNull();
        }

        [Test]
        public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var act = () => new McpHttpClient(null!, _httpClient, _mockLogger.Object);
            act.Should().Throw<ArgumentNullException>().WithParameterName("options");
        }

        [Test]
        public async Task ConnectAsync_WithHealthyServer_ShouldConnect()
        {
            // Arrange
            SetupHttpResponse("/mcp/health", HttpStatusCode.OK, "{\"status\":\"healthy\"}");
            _client = new McpHttpClient(_options, _httpClient, _mockLogger.Object);

            var connectedEventRaised = false;
            _client.Connected += (sender, e) => connectedEventRaised = true;

            // Act
            await _client.ConnectAsync();

            // Assert
            _client.IsConnected.Should().BeTrue();
            connectedEventRaised.Should().BeTrue();
        }

        [Test]
        public async Task ConnectAsync_WithUnhealthyServer_ShouldThrowException()
        {
            // Arrange
            SetupHttpResponse("/mcp/health", HttpStatusCode.ServiceUnavailable, "");
            _client = new McpHttpClient(_options, _httpClient, _mockLogger.Object);

            // Act & Assert
            var act = async () => await _client.ConnectAsync();
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Failed to connect to MCP server*");
        }

        [Test]
        public async Task DisconnectAsync_WhenConnected_ShouldDisconnect()
        {
            // Arrange
            SetupHttpResponse("/mcp/health", HttpStatusCode.OK, "{\"status\":\"healthy\"}");
            _client = new McpHttpClient(_options, _httpClient, _mockLogger.Object);
            await _client.ConnectAsync();

            var disconnectedEventRaised = false;
            _client.Disconnected += (sender, e) => disconnectedEventRaised = true;

            // Act
            await _client.DisconnectAsync();

            // Assert
            _client.IsConnected.Should().BeFalse();
            _client.ServerInfo.Should().BeNull();
            disconnectedEventRaised.Should().BeTrue();
        }

        [Test]
        public async Task InitializeAsync_WithValidResponse_ShouldInitialize()
        {
            // Arrange
            var initResult = new InitializeResult
            {
                ProtocolVersion = "2024-11-05",
                ServerInfo = new Implementation
                {
                    Name = "Test Server",
                    Version = "1.0.0"
                },
                Capabilities = new ServerCapabilities()
            };

            SetupHttpResponse("/mcp/health", HttpStatusCode.OK, "{\"status\":\"healthy\"}");
            SetupJsonRpcResponse("initialize", initResult);
            
            _client = new McpHttpClient(_options, _httpClient, _mockLogger.Object);
            await _client.ConnectAsync();

            // Act
            var result = await _client.InitializeAsync();

            // Assert
            result.Should().NotBeNull();
            result.ProtocolVersion.Should().Be("2024-11-05");
            result.ServerInfo.Name.Should().Be("Test Server");
            _client.ServerInfo.Should().Be(result);
        }

        [Test]
        public async Task ListToolsAsync_ShouldReturnTools()
        {
            // Arrange
            var toolsResult = new ListToolsResult
            {
                Tools = new List<Tool>
                {
                    new Tool { Name = "tool1", Description = "Tool 1" },
                    new Tool { Name = "tool2", Description = "Tool 2" }
                }
            };

            SetupHttpResponse("/mcp/health", HttpStatusCode.OK, "{\"status\":\"healthy\"}");
            SetupJsonRpcResponse("tools/list", toolsResult);
            
            _client = new McpHttpClient(_options, _httpClient, _mockLogger.Object);
            await _client.ConnectAsync();

            // Act
            var result = await _client.ListToolsAsync();

            // Assert
            result.Should().NotBeNull();
            result.Tools.Should().HaveCount(2);
            result.Tools[0].Name.Should().Be("tool1");
            result.Tools[1].Name.Should().Be("tool2");
        }

        [Test]
        public async Task CallToolAsync_WithValidTool_ShouldReturnResult()
        {
            // Arrange
            var callResult = new CallToolResult
            {
                Content = new List<ToolContent>
                {
                    new ToolContent { Type = "text", Text = "{\"value\":42}" }
                },
                IsError = false
            };

            SetupHttpResponse("/mcp/health", HttpStatusCode.OK, "{\"status\":\"healthy\"}");
            SetupJsonRpcResponse("tools/call", callResult);
            
            _client = new McpHttpClient(_options, _httpClient, _mockLogger.Object);
            await _client.ConnectAsync();

            // Act
            var result = await _client.CallToolAsync("test_tool", new { param = "value" });

            // Assert
            result.Should().NotBeNull();
            result.IsError.Should().BeFalse();
            result.Content.Should().HaveCount(1);
            result.Content[0].Text.Should().Contain("42");
        }

        [Test]
        public async Task CallToolAsync_WhenNotConnected_ShouldThrowException()
        {
            // Arrange
            _client = new McpHttpClient(_options, _httpClient, _mockLogger.Object);

            // Act & Assert
            var act = async () => await _client.CallToolAsync("test_tool");
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Client is not connected*");
        }

        [Test]
        public async Task SendRequestAsync_WithJsonRpcError_ShouldThrowJsonRpcException()
        {
            // Arrange
            var errorResponse = new
            {
                jsonrpc = "2.0",
                error = new
                {
                    code = -32601,
                    message = "Method not found",
                    data = "Additional error info"
                },
                id = 1
            };

            SetupHttpResponse("/mcp/health", HttpStatusCode.OK, "{\"status\":\"healthy\"}");
            SetupHttpResponse("/mcp/rpc", HttpStatusCode.OK, JsonSerializer.Serialize(errorResponse));
            
            _client = new McpHttpClient(_options, _httpClient, _mockLogger.Object);
            await _client.ConnectAsync();

            // Act & Assert - InitializeAsync will fail with JSON-RPC error
            var act = async () => await _client.InitializeAsync();
            await act.Should().ThrowAsync<JsonRpcException>()
                .Where(e => e.Code == -32601 && e.Message.Contains("Method not found"));
        }

        [Test]
        public void Dispose_ShouldDisconnectAndCleanup()
        {
            // Arrange
            SetupHttpResponse("/mcp/health", HttpStatusCode.OK, "{\"status\":\"healthy\"}");
            _client = new McpHttpClient(_options, _httpClient, _mockLogger.Object);
            _client.ConnectAsync().Wait();

            // Act
            _client.Dispose();

            // Assert
            _client.IsConnected.Should().BeFalse();
        }

        [Test]
        public async Task Authentication_WithApiKey_ShouldAddHeader()
        {
            // Arrange
            _options.Authentication = new AuthenticationOptions
            {
                Type = AuthenticationType.ApiKey,
                ApiKey = "test-api-key",
                ApiKeyHeader = "X-API-Key"
            };

            string? capturedApiKey = null;
            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, ct) =>
                {
                    if (req.Headers.TryGetValues("X-API-Key", out var values))
                    {
                        capturedApiKey = values.FirstOrDefault();
                    }
                })
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"status\":\"healthy\"}")
                });

            _client = new McpHttpClient(_options, _httpClient, _mockLogger.Object);

            // Act
            await _client.ConnectAsync();

            // Assert
            capturedApiKey.Should().Be("test-api-key");
        }

        [Test]
        public async Task RetryPolicy_OnTransientError_ShouldRetry()
        {
            // Arrange
            _options.EnableRetries = true;
            _options.MaxRetryAttempts = 3;
            _options.RetryDelayMs = 10;

            var callCount = 0;
            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    callCount++;
                    if (callCount < 3)
                    {
                        return new HttpResponseMessage { StatusCode = HttpStatusCode.ServiceUnavailable };
                    }
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent("{\"status\":\"healthy\"}")
                    };
                });

            _client = new McpHttpClient(_options, _httpClient, _mockLogger.Object);

            // Act
            await _client.ConnectAsync();

            // Assert
            callCount.Should().Be(3);
            _client.IsConnected.Should().BeTrue();
        }

        private void SetupHttpResponse(string path, HttpStatusCode statusCode, string content)
        {
            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.PathAndQuery.Contains(path)),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(content, Encoding.UTF8, "application/json")
                });
        }

        private void SetupJsonRpcResponse<T>(string method, T result)
        {
            var response = new
            {
                jsonrpc = "2.0",
                result = result,
                id = 1
            };

            SetupHttpResponse("/mcp/rpc", HttpStatusCode.OK, JsonSerializer.Serialize(response));
        }
    }
}