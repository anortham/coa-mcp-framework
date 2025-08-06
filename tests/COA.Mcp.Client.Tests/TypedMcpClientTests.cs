using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Client;
using COA.Mcp.Client.Configuration;
using COA.Mcp.Client.Interfaces;
using COA.Mcp.Framework.Models;
using COA.Mcp.Protocol;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace COA.Mcp.Client.Tests
{
    [TestFixture]
    public class TypedMcpClientTests
    {
        private Mock<IMcpClient> _mockBaseClient = null!;
        private Mock<ILogger<TypedMcpClient<TestParams, TestResult>>> _mockLogger = null!;
        private TypedMcpClient<TestParams, TestResult> _typedClient = null!;

        [SetUp]
        public void Setup()
        {
            _mockBaseClient = new Mock<IMcpClient>();
            _mockLogger = new Mock<ILogger<TypedMcpClient<TestParams, TestResult>>>();
        }

        [TearDown]
        public void TearDown()
        {
            _typedClient?.Dispose();
        }

        [Test]
        public void Constructor_WithValidBaseClient_ShouldInitialize()
        {
            // Act
            _typedClient = new TypedMcpClient<TestParams, TestResult>(_mockBaseClient.Object, _mockLogger.Object);

            // Assert
            _typedClient.Should().NotBeNull();
        }

        [Test]
        public void Constructor_WithNullBaseClient_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var act = () => new TypedMcpClient<TestParams, TestResult>((McpHttpClient)null!, _mockLogger.Object);
            act.Should().Throw<ArgumentNullException>().WithParameterName("baseClient");
        }

        [Test]
        public async Task CallToolAsync_WithValidResponse_ShouldReturnTypedResult()
        {
            // Arrange
            var testParams = new TestParams { Value = 42, Name = "Test" };
            var toolResult = new CallToolResult
            {
                Content = new List<ToolContent>
                {
                    new ToolContent 
                    { 
                        Type = "text", 
                        Text = "{\"success\":true,\"result\":84,\"message\":\"Doubled\"}" 
                    }
                },
                IsError = false
            };

            _mockBaseClient.Setup(x => x.CallToolAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(toolResult);

            _typedClient = new TypedMcpClient<TestParams, TestResult>(_mockBaseClient.Object, _mockLogger.Object);

            // Act
            var result = await _typedClient.CallToolAsync("test_tool", testParams);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Result.Should().Be(84);
            result.Message.Should().Be("Doubled");
        }

        [Test]
        public async Task CallToolAsync_WithEmptyResponse_ShouldReturnErrorResult()
        {
            // Arrange
            var testParams = new TestParams { Value = 42 };
            var toolResult = new CallToolResult
            {
                Content = new List<ToolContent>(),
                IsError = false
            };

            _mockBaseClient.Setup(x => x.CallToolAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(toolResult);

            _typedClient = new TypedMcpClient<TestParams, TestResult>(_mockBaseClient.Object, _mockLogger.Object);

            // Act
            var result = await _typedClient.CallToolAsync("test_tool", testParams);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Error.Should().NotBeNull();
            result.Error!.Code.Should().Be("EMPTY_RESPONSE");
        }

        [Test]
        public async Task CallToolAsync_WithErrorResponse_ShouldReturnErrorResult()
        {
            // Arrange
            var testParams = new TestParams { Value = 42 };
            var toolResult = new CallToolResult
            {
                Content = new List<ToolContent>
                {
                    new ToolContent { Type = "text", Text = "{\"success\":false}" }
                },
                IsError = true
            };

            _mockBaseClient.Setup(x => x.CallToolAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(toolResult);

            _typedClient = new TypedMcpClient<TestParams, TestResult>(_mockBaseClient.Object, _mockLogger.Object);

            // Act
            var result = await _typedClient.CallToolAsync("test_tool", testParams);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
        }

        [Test]
        public async Task CallToolAsync_WithException_ShouldReturnErrorResult()
        {
            // Arrange
            var testParams = new TestParams { Value = 42 };
            
            _mockBaseClient.Setup(x => x.CallToolAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Network error"));

            _typedClient = new TypedMcpClient<TestParams, TestResult>(_mockBaseClient.Object, _mockLogger.Object);

            // Act
            var result = await _typedClient.CallToolAsync("test_tool", testParams);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Error.Should().NotBeNull();
            result.Error!.Code.Should().Be("CLIENT_ERROR");
            result.Error.Message.Should().Contain("Network error");
        }

        [Test]
        public async Task CallToolWithRetryAsync_OnFirstSuccess_ShouldNotRetry()
        {
            // Arrange
            var testParams = new TestParams { Value = 42 };
            var toolResult = new CallToolResult
            {
                Content = new List<ToolContent>
                {
                    new ToolContent { Type = "text", Text = "{\"success\":true,\"result\":84}" }
                },
                IsError = false
            };

            var callCount = 0;
            _mockBaseClient.Setup(x => x.CallToolAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    callCount++;
                    return toolResult;
                });

            _typedClient = new TypedMcpClient<TestParams, TestResult>(_mockBaseClient.Object, _mockLogger.Object);

            // Act
            var result = await _typedClient.CallToolWithRetryAsync("test_tool", testParams);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            callCount.Should().Be(1);
        }

        [Test]
        public async Task CallToolWithRetryAsync_OnFailure_ShouldRetryUpToMaxAttempts()
        {
            // Arrange
            var testParams = new TestParams { Value = 42 };
            var failureResult = new CallToolResult
            {
                Content = new List<ToolContent>
                {
                    new ToolContent { Type = "text", Text = "{\"success\":false}" }
                },
                IsError = true
            };

            var callCount = 0;
            _mockBaseClient.Setup(x => x.CallToolAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    callCount++;
                    return failureResult;
                });

            _typedClient = new TypedMcpClient<TestParams, TestResult>(_mockBaseClient.Object, _mockLogger.Object);

            // Act
            var result = await _typedClient.CallToolWithRetryAsync("test_tool", testParams);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            callCount.Should().Be(3); // Default max attempts
        }

        [Test]
        public async Task CallToolsBatchAsync_ShouldExecuteInParallel()
        {
            // Arrange
            var batchCalls = new Dictionary<string, TestParams>
            {
                ["call1"] = new TestParams { Value = 10, Name = "First" },
                ["call2"] = new TestParams { Value = 20, Name = "Second" },
                ["call3"] = new TestParams { Value = 30, Name = "Third" }
            };

            _mockBaseClient.Setup(x => x.CallToolAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string name, object parameters, CancellationToken ct) =>
                {
                    var testParams = parameters as TestParams;
                    return new CallToolResult
                    {
                        Content = new List<ToolContent>
                        {
                            new ToolContent 
                            { 
                                Type = "text", 
                                Text = $"{{\"success\":true,\"result\":{testParams?.Value * 2}}}" 
                            }
                        },
                        IsError = false
                    };
                });

            _typedClient = new TypedMcpClient<TestParams, TestResult>(_mockBaseClient.Object, _mockLogger.Object);

            // Act
            var results = await _typedClient.CallToolsBatchAsync(batchCalls);

            // Assert
            results.Should().NotBeNull();
            results.Should().HaveCount(3);
            results["call1"].Success.Should().BeTrue();
            results["call1"].Result.Should().Be(20);
            results["call2"].Result.Should().Be(40);
            results["call3"].Result.Should().Be(60);
        }

        [Test]
        public void CallToolsBatchAsync_WithEmptyDictionary_ShouldThrowArgumentException()
        {
            // Arrange
            var emptyBatch = new Dictionary<string, TestParams>();
            _typedClient = new TypedMcpClient<TestParams, TestResult>(_mockBaseClient.Object, _mockLogger.Object);

            // Act & Assert
            var act = async () => await _typedClient.CallToolsBatchAsync(emptyBatch);
            act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*cannot be null or empty*");
        }

        [Test]
        public async Task IsConnected_ShouldDelegateToBaseClient()
        {
            // Arrange
            _mockBaseClient.SetupGet(x => x.IsConnected).Returns(true);
            _typedClient = new TypedMcpClient<TestParams, TestResult>(_mockBaseClient.Object, _mockLogger.Object);

            // Act
            var isConnected = _typedClient.IsConnected;

            // Assert
            isConnected.Should().BeTrue();
            _mockBaseClient.VerifyGet(x => x.IsConnected, Times.Once);
        }

        [Test]
        public async Task ServerInfo_ShouldDelegateToBaseClient()
        {
            // Arrange
            var serverInfo = new InitializeResult
            {
                ServerInfo = new Implementation { Name = "Test Server" }
            };
            _mockBaseClient.SetupGet(x => x.ServerInfo).Returns(serverInfo);
            _typedClient = new TypedMcpClient<TestParams, TestResult>(_mockBaseClient.Object, _mockLogger.Object);

            // Act
            var info = _typedClient.ServerInfo;

            // Assert
            info.Should().Be(serverInfo);
            _mockBaseClient.VerifyGet(x => x.ServerInfo, Times.Once);
        }

        // Test helper classes
        public class TestParams
        {
            public int Value { get; set; }
            public string? Name { get; set; }
        }

        public class TestResult : ToolResultBase
        {
            public override string Operation => "test";
            public int Result { get; set; }
            public new string? Message { get; set; }
        }
    }
}