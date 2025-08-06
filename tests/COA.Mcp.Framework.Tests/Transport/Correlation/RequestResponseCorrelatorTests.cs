using System;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework.Transport.Correlation;
using FluentAssertions;
using NUnit.Framework;

namespace COA.Mcp.Framework.Tests.Transport.Correlation
{
    [TestFixture]
    public class RequestResponseCorrelatorTests
    {
        private RequestResponseCorrelator _correlator;

        [SetUp]
        public void Setup()
        {
            _correlator = new RequestResponseCorrelator(TimeSpan.FromSeconds(5));
        }

        [TearDown]
        public void TearDown()
        {
            _correlator?.Dispose();
        }

        [Test]
        public async Task RegisterRequestAsync_ShouldCompleteWhenResponseArrives()
        {
            // Arrange
            var correlationId = "test-123";
            var expectedResponse = "response data";

            // Act
            var responseTask = _correlator.RegisterRequestAsync<string>(correlationId);
            
            // Simulate response arriving
            await Task.Delay(100);
            var completed = _correlator.TryCompleteRequest(correlationId, expectedResponse);

            var response = await responseTask;

            // Assert
            completed.Should().BeTrue();
            response.Should().Be(expectedResponse);
            _correlator.PendingRequestCount.Should().Be(0);
        }

        [Test]
        public void RegisterRequestAsync_WithDuplicateId_ShouldThrow()
        {
            // Arrange
            var correlationId = "duplicate-123";

            // Act
            _correlator.RegisterRequestAsync<string>(correlationId);

            // Assert
            Assert.Throws<InvalidOperationException>(() =>
                _correlator.RegisterRequestAsync<string>(correlationId));
        }

        [Test]
        public async Task RegisterRequestAsync_ShouldTimeoutWhenNoResponse()
        {
            // Arrange
            var correlationId = "timeout-123";
            var timeout = TimeSpan.FromMilliseconds(100);

            // Act & Assert
            var responseTask = _correlator.RegisterRequestAsync<string>(correlationId, timeout);
            
            Assert.ThrowsAsync<TimeoutException>(async () => await responseTask);

            await Task.Delay(200); // Wait for cleanup
            _correlator.PendingRequestCount.Should().Be(0);
        }

        [Test]
        public async Task RegisterRequestAsync_ShouldCancelWhenTokenCancelled()
        {
            // Arrange
            var correlationId = "cancel-123";
            using var cts = new CancellationTokenSource();

            // Act
            var responseTask = _correlator.RegisterRequestAsync<string>(
                correlationId, 
                TimeSpan.FromSeconds(10), 
                cts.Token);

            cts.Cancel();

            // Assert
            Assert.ThrowsAsync<TaskCanceledException>(async () => await responseTask);

            await Task.Delay(100); // Wait for cleanup
            _correlator.PendingRequestCount.Should().Be(0);
        }

        [Test]
        public void TryCompleteRequest_WithNonExistentId_ShouldReturnFalse()
        {
            // Arrange
            var correlationId = "nonexistent-123";

            // Act
            var result = _correlator.TryCompleteRequest(correlationId, "response");

            // Assert
            result.Should().BeFalse();
        }

        [Test]
        public async Task TryFailRequest_ShouldCompleteWithException()
        {
            // Arrange
            var correlationId = "fail-123";
            var exception = new InvalidOperationException("Test error");

            // Act
            var responseTask = _correlator.RegisterRequestAsync<string>(correlationId);
            await Task.Delay(50);
            
            var failed = _correlator.TryFailRequest(correlationId, exception);

            // Assert
            failed.Should().BeTrue();
            Assert.ThrowsAsync<InvalidOperationException>(async () => await responseTask);
        }

        [Test]
        public async Task CancelRequest_ShouldCancelPendingRequest()
        {
            // Arrange
            var correlationId = "cancel-request-123";

            // Act
            var responseTask = _correlator.RegisterRequestAsync<string>(correlationId);
            await Task.Delay(50);
            
            var cancelled = _correlator.CancelRequest(correlationId);

            // Assert
            cancelled.Should().BeTrue();
            Assert.ThrowsAsync<TaskCanceledException>(async () => await responseTask);
        }

        [Test]
        public void IsRequestPending_ShouldReturnCorrectStatus()
        {
            // Arrange
            var correlationId = "pending-123";

            // Act & Assert
            _correlator.IsRequestPending(correlationId).Should().BeFalse();

            _correlator.RegisterRequestAsync<string>(correlationId);
            _correlator.IsRequestPending(correlationId).Should().BeTrue();

            _correlator.TryCompleteRequest(correlationId, "response");
            _correlator.IsRequestPending(correlationId).Should().BeFalse();
        }

        [Test]
        public async Task MultipleRequests_ShouldHandleConcurrently()
        {
            // Arrange
            var tasks = new Task<string>[10];
            var correlationIds = new string[10];

            // Act
            for (int i = 0; i < 10; i++)
            {
                correlationIds[i] = $"concurrent-{i}";
                tasks[i] = _correlator.RegisterRequestAsync<string>(correlationIds[i]);
            }

            // Complete requests in reverse order
            for (int i = 9; i >= 0; i--)
            {
                _correlator.TryCompleteRequest(correlationIds[i], $"response-{i}");
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            for (int i = 0; i < 10; i++)
            {
                results[i].Should().Be($"response-{i}");
            }
            _correlator.PendingRequestCount.Should().Be(0);
        }

        [Test]
        public async Task RegisterRequestAsync_WithJsonDeserialization_ShouldWork()
        {
            // Arrange
            var correlationId = "json-123";
            var responseObject = new { Name = "Test", Value = 42 };
            var jsonResponse = System.Text.Json.JsonSerializer.Serialize(responseObject);

            // Act
            var responseTask = _correlator.RegisterRequestAsync<string>(correlationId);
            _correlator.TryCompleteRequest(correlationId, jsonResponse);

            var response = await responseTask;

            // Assert
            response.Should().NotBeNull();
            response.Should().Contain("Test");
            response.Should().Contain("42");
        }

        [Test]
        public async Task Dispose_ShouldCancelAllPendingRequests()
        {
            // Arrange
            var tasks = new Task<string>[5];
            for (int i = 0; i < 5; i++)
            {
                tasks[i] = _correlator.RegisterRequestAsync<string>($"dispose-{i}");
            }

            // Act
            _correlator.Dispose();

            // Assert
            foreach (var task in tasks)
            {
                // Tasks should either be canceled or throw TaskCanceledException
                try
                {
                    await task;
                    Assert.Fail("Task should have been cancelled");
                }
                catch (TaskCanceledException)
                {
                    // Expected
                }
            }
        }
    }
}