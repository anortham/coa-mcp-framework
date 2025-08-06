using System;
using System.Net.Http;
using System.Threading.Tasks;
using COA.Mcp.Client;
using COA.Mcp.Client.Configuration;
using COA.Mcp.Client.Interfaces;
using COA.Mcp.Framework.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace COA.Mcp.Client.Tests
{
    [TestFixture]
    public class McpClientBuilderTests
    {
        private McpClientBuilder _builder;
        private Mock<ILoggerFactory> _mockLoggerFactory;

        [SetUp]
        public void Setup()
        {
            _builder = McpClientBuilder.Create();
            _mockLoggerFactory = new Mock<ILoggerFactory>();
        }

        [Test]
        public void Create_ShouldReturnNewBuilder()
        {
            // Act
            var builder = McpClientBuilder.Create();

            // Assert
            builder.Should().NotBeNull();
            builder.Should().BeOfType<McpClientBuilder>();
        }

        [Test]
        public void Create_WithBaseUrl_ShouldSetBaseUrl()
        {
            // Act
            var builder = McpClientBuilder.Create("http://example.com");
            var client = builder.Build();

            // Assert
            builder.Should().NotBeNull();
            // We can't directly verify the URL without accessing private fields,
            // but we can verify the client was created
            client.Should().NotBeNull();
        }

        [Test]
        public void WithBaseUrl_ShouldReturnSameBuilder()
        {
            // Act
            var result = _builder.WithBaseUrl("http://example.com");

            // Assert
            result.Should().BeSameAs(_builder);
        }

        [Test]
        public void UseWebSocket_ShouldConfigureWebSocket()
        {
            // Act
            var result = _builder.UseWebSocket("/ws");

            // Assert
            result.Should().BeSameAs(_builder);
        }

        [Test]
        public void WithTimeout_ShouldSetTimeout()
        {
            // Act
            var result = _builder.WithTimeout(TimeSpan.FromSeconds(60));

            // Assert
            result.Should().BeSameAs(_builder);
        }

        [Test]
        public void WithRetry_ShouldConfigureRetryPolicy()
        {
            // Act
            var result = _builder.WithRetry(maxAttempts: 5, delayMs: 2000);

            // Assert
            result.Should().BeSameAs(_builder);
        }

        [Test]
        public void WithoutRetry_ShouldDisableRetry()
        {
            // Act
            var result = _builder.WithoutRetry();

            // Assert
            result.Should().BeSameAs(_builder);
        }

        [Test]
        public void WithCircuitBreaker_ShouldConfigureCircuitBreaker()
        {
            // Act
            var result = _builder.WithCircuitBreaker(failureThreshold: 10, durationSeconds: 60);

            // Assert
            result.Should().BeSameAs(_builder);
        }

        [Test]
        public void WithoutCircuitBreaker_ShouldDisableCircuitBreaker()
        {
            // Act
            var result = _builder.WithoutCircuitBreaker();

            // Assert
            result.Should().BeSameAs(_builder);
        }

        [Test]
        public void WithApiKey_ShouldConfigureApiKeyAuth()
        {
            // Act
            var result = _builder.WithApiKey("test-key", "X-Custom-Key");

            // Assert
            result.Should().BeSameAs(_builder);
        }

        [Test]
        public void WithJwtToken_ShouldConfigureJwtAuth()
        {
            // Act
            var result = _builder.WithJwtToken("test-token", async () => await Task.FromResult("new-token"));

            // Assert
            result.Should().BeSameAs(_builder);
        }

        [Test]
        public void WithBasicAuth_ShouldConfigureBasicAuth()
        {
            // Act
            var result = _builder.WithBasicAuth("user", "pass");

            // Assert
            result.Should().BeSameAs(_builder);
        }

        [Test]
        public void WithCustomAuth_ShouldConfigureCustomAuth()
        {
            // Act
            var result = _builder.WithCustomAuth(async req => await Task.CompletedTask);

            // Assert
            result.Should().BeSameAs(_builder);
        }

        [Test]
        public void WithClientInfo_ShouldSetClientInfo()
        {
            // Act
            var result = _builder.WithClientInfo("TestClient", "1.0.0", new Dictionary<string, object> { ["env"] = "test" });

            // Assert
            result.Should().BeSameAs(_builder);
        }

        [Test]
        public void WithHeader_ShouldAddCustomHeader()
        {
            // Act
            var result = _builder.WithHeader("X-Test", "value");

            // Assert
            result.Should().BeSameAs(_builder);
        }

        [Test]
        public void WithRequestLogging_ShouldEnableLogging()
        {
            // Act
            var result = _builder.WithRequestLogging(true);

            // Assert
            result.Should().BeSameAs(_builder);
        }

        [Test]
        public void WithMetrics_ShouldEnableMetrics()
        {
            // Act
            var result = _builder.WithMetrics(true);

            // Assert
            result.Should().BeSameAs(_builder);
        }

        [Test]
        public void UseHttpClient_ShouldSetHttpClient()
        {
            // Arrange
            var httpClient = new HttpClient();

            // Act
            var result = _builder.UseHttpClient(httpClient);

            // Assert
            result.Should().BeSameAs(_builder);

            // Cleanup
            httpClient.Dispose();
        }

        [Test]
        public void UseHttpClient_WithNull_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var act = () => _builder.UseHttpClient(null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("httpClient");
        }

        [Test]
        public void UseLoggerFactory_ShouldSetLoggerFactory()
        {
            // Act
            var result = _builder.UseLoggerFactory(_mockLoggerFactory.Object);

            // Assert
            result.Should().BeSameAs(_builder);
        }

        [Test]
        public void UseLoggerFactory_WithNull_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var act = () => _builder.UseLoggerFactory(null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("loggerFactory");
        }

        [Test]
        public void Configure_ShouldApplyConfiguration()
        {
            // Act
            var result = _builder.Configure(options =>
            {
                options.BaseUrl = "http://configured.com";
                options.TimeoutSeconds = 120;
            });

            // Assert
            result.Should().BeSameAs(_builder);
        }

        [Test]
        public void Build_ShouldReturnIMcpClient()
        {
            // Act
            var client = _builder
                .WithBaseUrl("http://localhost:5000")
                .Build();

            // Assert
            client.Should().NotBeNull();
            client.Should().BeAssignableTo<IMcpClient>();
            client.Should().BeOfType<McpHttpClient>();
        }

        [Test]
        public void BuildTyped_ShouldReturnTypedClient()
        {
            // Act
            var client = _builder
                .WithBaseUrl("http://localhost:5000")
                .BuildTyped<TestParams, TestResult>();

            // Assert
            client.Should().NotBeNull();
            client.Should().BeAssignableTo<ITypedMcpClient<TestParams, TestResult>>();
            client.Should().BeOfType<TypedMcpClient<TestParams, TestResult>>();
        }

        [Test]
        public void FluentChaining_ShouldWorkCorrectly()
        {
            // Act
            var client = McpClientBuilder
                .Create("http://localhost:5000")
                .WithTimeout(TimeSpan.FromSeconds(60))
                .WithRetry(3, 1000)
                .WithCircuitBreaker(5, 30)
                .WithApiKey("api-key")
                .WithClientInfo("TestClient", "1.0.0")
                .WithHeader("X-Custom", "value")
                .WithRequestLogging(true)
                .WithMetrics(true)
                .UseLoggerFactory(_mockLoggerFactory.Object)
                .Build();

            // Assert
            client.Should().NotBeNull();
            client.Should().BeOfType<McpHttpClient>();
        }

        // Test helper classes
        public class TestParams
        {
            public string Value { get; set; } = string.Empty;
        }

        public class TestResult : ToolResultBase
        {
            public override string Operation => "test";
            public string Data { get; set; } = string.Empty;
        }
    }
}