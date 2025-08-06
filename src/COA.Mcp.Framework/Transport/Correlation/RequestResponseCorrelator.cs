using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.Transport.Correlation
{
    /// <summary>
    /// Manages request-response correlation for async messaging in transports.
    /// </summary>
    public class RequestResponseCorrelator : IDisposable
    {
        private readonly ConcurrentDictionary<string, PendingRequest> _pendingRequests = new();
        private readonly ILogger<RequestResponseCorrelator>? _logger;
        private readonly Timer _cleanupTimer;
        private readonly TimeSpan _defaultTimeout;
        private bool _disposed;

        public RequestResponseCorrelator(
            TimeSpan? defaultTimeout = null,
            ILogger<RequestResponseCorrelator>? logger = null)
        {
            _defaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(30);
            _logger = logger;
            
            // Cleanup expired requests every 10 seconds
            _cleanupTimer = new Timer(
                CleanupExpiredRequests,
                null,
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(10));
        }

        /// <summary>
        /// Registers a request and returns a task that completes when the response arrives.
        /// </summary>
        public Task<TResponse> RegisterRequestAsync<TResponse>(
            string correlationId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var effectiveTimeout = timeout ?? _defaultTimeout;
            var pendingRequest = new PendingRequest(effectiveTimeout, cancellationToken);
            
            if (!_pendingRequests.TryAdd(correlationId, pendingRequest))
            {
                throw new InvalidOperationException($"Request with correlation ID '{correlationId}' already exists");
            }

            _logger?.LogTrace("Registered request with correlation ID: {CorrelationId}", correlationId);
            
            // Setup timeout
            pendingRequest.TimeoutCancellation = new CancellationTokenSource(effectiveTimeout);
            pendingRequest.TimeoutCancellation.Token.Register(() =>
            {
                if (_pendingRequests.TryRemove(correlationId, out var request))
                {
                    request.TaskCompletionSource.TrySetException(
                        new TimeoutException($"Request '{correlationId}' timed out after {effectiveTimeout}"));
                    request.Dispose();
                }
            });

            // Setup user cancellation
            if (cancellationToken != CancellationToken.None)
            {
                cancellationToken.Register(() =>
                {
                    if (_pendingRequests.TryRemove(correlationId, out var request))
                    {
                        request.TaskCompletionSource.TrySetCanceled(cancellationToken);
                        request.Dispose();
                    }
                });
            }

            return pendingRequest.GetResponseAsync<TResponse>();
        }

        /// <summary>
        /// Completes a pending request with a response.
        /// </summary>
        public bool TryCompleteRequest(string correlationId, object response)
        {
            if (_pendingRequests.TryRemove(correlationId, out var pendingRequest))
            {
                _logger?.LogTrace("Completing request with correlation ID: {CorrelationId}", correlationId);
                
                try
                {
                    pendingRequest.TaskCompletionSource.TrySetResult(response);
                    return true;
                }
                finally
                {
                    pendingRequest.Dispose();
                }
            }

            _logger?.LogDebug("No pending request found for correlation ID: {CorrelationId}", correlationId);
            return false;
        }

        /// <summary>
        /// Fails a pending request with an error.
        /// </summary>
        public bool TryFailRequest(string correlationId, Exception exception)
        {
            if (_pendingRequests.TryRemove(correlationId, out var pendingRequest))
            {
                _logger?.LogTrace("Failing request with correlation ID: {CorrelationId}", correlationId);
                
                try
                {
                    pendingRequest.TaskCompletionSource.TrySetException(exception);
                    return true;
                }
                finally
                {
                    pendingRequest.Dispose();
                }
            }

            return false;
        }

        /// <summary>
        /// Cancels a pending request.
        /// </summary>
        public bool CancelRequest(string correlationId)
        {
            if (_pendingRequests.TryRemove(correlationId, out var pendingRequest))
            {
                _logger?.LogTrace("Cancelling request with correlation ID: {CorrelationId}", correlationId);
                
                try
                {
                    pendingRequest.TaskCompletionSource.TrySetCanceled();
                    return true;
                }
                finally
                {
                    pendingRequest.Dispose();
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the count of pending requests.
        /// </summary>
        public int PendingRequestCount => _pendingRequests.Count;

        /// <summary>
        /// Checks if a request is pending.
        /// </summary>
        public bool IsRequestPending(string correlationId)
        {
            return _pendingRequests.ContainsKey(correlationId);
        }

        private void CleanupExpiredRequests(object? state)
        {
            var expiredRequests = new List<string>();
            var now = DateTime.UtcNow;

            foreach (var kvp in _pendingRequests)
            {
                if (now > kvp.Value.ExpiryTime)
                {
                    expiredRequests.Add(kvp.Key);
                }
            }

            foreach (var correlationId in expiredRequests)
            {
                if (_pendingRequests.TryRemove(correlationId, out var request))
                {
                    _logger?.LogWarning("Request expired: {CorrelationId}", correlationId);
                    request.TaskCompletionSource.TrySetException(
                        new TimeoutException($"Request '{correlationId}' expired"));
                    request.Dispose();
                }
            }

            if (expiredRequests.Count > 0)
            {
                _logger?.LogDebug("Cleaned up {Count} expired requests", expiredRequests.Count);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _cleanupTimer?.Dispose();

            // Cancel all pending requests
            foreach (var kvp in _pendingRequests)
            {
                kvp.Value.TaskCompletionSource.TrySetCanceled();
                kvp.Value.Dispose();
            }
            
            _pendingRequests.Clear();
            _disposed = true;
        }

        private class PendingRequest : IDisposable
        {
            public TaskCompletionSource<object> TaskCompletionSource { get; }
            public DateTime ExpiryTime { get; }
            public CancellationTokenSource? TimeoutCancellation { get; set; }

            public PendingRequest(TimeSpan timeout, CancellationToken cancellationToken)
            {
                TaskCompletionSource = new TaskCompletionSource<object>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                ExpiryTime = DateTime.UtcNow.Add(timeout);
            }

            public async Task<TResponse> GetResponseAsync<TResponse>()
            {
                var result = await TaskCompletionSource.Task;
                
                if (result is TResponse typed)
                {
                    return typed;
                }

                // Try to deserialize if it's a string
                if (result is string json)
                {
                    try
                    {
                        return System.Text.Json.JsonSerializer.Deserialize<TResponse>(json)!;
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Failed to deserialize response to {typeof(TResponse).Name}", ex);
                    }
                }

                throw new InvalidCastException(
                    $"Cannot convert response of type {result?.GetType().Name} to {typeof(TResponse).Name}");
            }

            public void Dispose()
            {
                TimeoutCancellation?.Dispose();
            }
        }
    }
}