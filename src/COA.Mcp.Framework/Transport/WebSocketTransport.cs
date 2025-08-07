using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using COA.Mcp.Framework.Transport.Configuration;

namespace COA.Mcp.Framework.Transport
{
    /// <summary>
    /// WebSocket transport implementation for bidirectional real-time communication.
    /// </summary>
    public class WebSocketTransport : IMcpTransport
    {
        private readonly HttpTransportOptions _options;
        private readonly ILogger<WebSocketTransport>? _logger;
        private readonly ConcurrentQueue<TransportMessage> _messageQueue = new();
        private readonly SemaphoreSlim _messageAvailable = new(0);
        private readonly ConcurrentDictionary<string, WebSocketClient> _connections = new();
        private HttpListener? _listener;
        private CancellationTokenSource? _listenerCts;
        private Task? _listenerTask;
        private bool _isConnected;
        private bool _disposed;

        public TransportType Type => TransportType.WebSocket;
        public bool IsConnected => _isConnected;

        public event EventHandler<TransportDisconnectedEventArgs>? Disconnected;

        public WebSocketTransport(HttpTransportOptions options, ILogger<WebSocketTransport>? logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            // Check if already started (idempotent)
            if (_isConnected)
            {
                _logger?.LogDebug("WebSocket transport already started");
                return;
            }

            // Validate port
            if (_options.Port < 1 || _options.Port > 65535)
            {
                throw new ArgumentException($"Invalid port number: {_options.Port}. Port must be between 1 and 65535.");
            }

            _logger?.LogInformation("Starting WebSocket transport on {Host}:{Port}", _options.Host, _options.Port);

            _listener = new HttpListener();
            var prefix = $"{(_options.UseHttps ? "https" : "http")}://{_options.Host}:{_options.Port}/";
            _listener.Prefixes.Add(prefix);
            
            try
            {
                _listener.Start();
                _isConnected = true;
                
                _listenerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _listenerTask = Task.Run(() => ProcessWebSocketRequests(_listenerCts.Token), _listenerCts.Token);
                
                _logger?.LogInformation("WebSocket transport started successfully on {Prefix}", prefix);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to start WebSocket transport");
                
                // Clean up on failure
                _listener?.Close();
                _listener = null;
                _isConnected = false;
                
                throw;
            }
            
            await Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            // Check if already stopped (idempotent)
            if (!_isConnected)
            {
                _logger?.LogDebug("WebSocket transport already stopped");
                return;
            }

            _logger?.LogInformation("Stopping WebSocket transport");
            
            _isConnected = false;
            
            // Only cancel if not already disposed
            try
            {
                _listenerCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
            }
            
            // Close all active connections
            foreach (var connection in _connections.Values)
            {
                await connection.CloseAsync();
            }
            _connections.Clear();
            
            try
            {
                _listener?.Stop();
                _listener?.Close();
                
                if (_listenerTask != null)
                {
                    await _listenerTask.ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error stopping WebSocket transport");
            }
            finally
            {
                _listener = null;
                _listenerCts = null;
                _listenerTask = null;
            }
            
            Disconnected?.Invoke(this, new TransportDisconnectedEventArgs
            {
                Reason = "Transport stopped",
                WasClean = true
            });
        }

        private async Task ProcessWebSocketRequests(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _listener?.IsListening == true)
            {
                try
                {
                    var contextTask = _listener.GetContextAsync();
                    
                    using (cancellationToken.Register(() => _listener?.Stop()))
                    {
                        var context = await contextTask.ConfigureAwait(false);
                        
                        // Check if this is a WebSocket request
                        if (context.Request.IsWebSocketRequest)
                        {
                            // Process WebSocket upgrade asynchronously with error handling
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await HandleWebSocketConnection(context, cancellationToken);
                                }
                                catch (Exception ex)
                                {
                                    _logger?.LogError(ex, "Failed to handle WebSocket connection");
                                }
                            }, cancellationToken);
                        }
                        else
                        {
                            // Return 400 for non-WebSocket requests
                            context.Response.StatusCode = 400;
                            var errorBytes = Encoding.UTF8.GetBytes("WebSocket upgrade required");
                            await context.Response.OutputStream.WriteAsync(errorBytes, 0, errorBytes.Length);
                            context.Response.Close();
                        }
                    }
                }
                catch (HttpListenerException ex) when (ex.ErrorCode == 995) // Listener stopped
                {
                    _logger?.LogDebug("WebSocket listener stopped");
                    break;
                }
                catch (ObjectDisposedException)
                {
                    _logger?.LogDebug("WebSocket listener disposed");
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error processing WebSocket request");
                }
            }
        }

        private async Task HandleWebSocketConnection(HttpListenerContext context, CancellationToken cancellationToken)
        {
            WebSocketContext? webSocketContext = null;
            WebSocketClient? connection = null;
            
            try
            {
                // Accept the WebSocket connection
                webSocketContext = await context.AcceptWebSocketAsync(null);
                var webSocket = webSocketContext.WebSocket;
                
                var connectionId = Guid.NewGuid().ToString();
                connection = new WebSocketClient(connectionId, webSocket, _logger);
                _connections[connectionId] = connection;
                
                _logger?.LogInformation("WebSocket connection established: {ConnectionId}", connectionId);
                
                // Process messages from this connection
                await ProcessWebSocketMessages(connection, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling WebSocket connection");
            }
            finally
            {
                if (connection != null)
                {
                    _connections.TryRemove(connection.Id, out _);
                    await connection.CloseAsync();
                    _logger?.LogInformation("WebSocket connection closed: {ConnectionId}", connection.Id);
                }
            }
        }

        private async Task ProcessWebSocketMessages(WebSocketClient connection, CancellationToken cancellationToken)
        {
            var buffer = new ArraySegment<byte>(new byte[4096]);
            
            while (!cancellationToken.IsCancellationRequested && connection.IsOpen)
            {
                try
                {
                    var result = await connection.ReceiveAsync(buffer, cancellationToken);
                    
                    if (result == null)
                    {
                        break;
                    }
                    
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer.Array!, 0, result.Count);
                        
                        var transportMessage = new TransportMessage
                        {
                            Content = message,
                            Headers = 
                            {
                                ["transport"] = "websocket",
                                ["connection-id"] = connection.Id
                            }
                        };
                        
                        _messageQueue.Enqueue(transportMessage);
                        _messageAvailable.Release();
                        
                        _logger?.LogTrace("Received WebSocket message from {ConnectionId}: {MessageLength} bytes", 
                            connection.Id, result.Count);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await connection.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client requested close");
                        break;
                    }
                }
                catch (WebSocketException ex)
                {
                    _logger?.LogError(ex, "WebSocket error for connection {ConnectionId}", connection.Id);
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error processing WebSocket message for connection {ConnectionId}", connection.Id);
                    break;
                }
            }
        }

        public async Task<TransportMessage?> ReadMessageAsync(CancellationToken cancellationToken = default)
        {
            await _messageAvailable.WaitAsync(cancellationToken);
            
            if (_messageQueue.TryDequeue(out var message))
            {
                return message;
            }
            
            return null;
        }

        public async Task WriteMessageAsync(TransportMessage message, CancellationToken cancellationToken = default)
        {
            // If message has a connection ID, send to specific connection
            if (message.Headers.TryGetValue("connection-id", out var connectionId) && 
                _connections.TryGetValue(connectionId, out var connection))
            {
                await connection.SendAsync(message.Content, cancellationToken);
                _logger?.LogTrace("Sent message to WebSocket connection {ConnectionId}", connectionId);
            }
            else
            {
                // Broadcast to all connections
                var tasks = new List<Task>();
                foreach (var conn in _connections.Values)
                {
                    tasks.Add(conn.SendAsync(message.Content, cancellationToken));
                }
                
                await Task.WhenAll(tasks);
                _logger?.LogTrace("Broadcast message to {Count} WebSocket connections", _connections.Count);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            // Ensure we're marked as disconnected
            _isConnected = false;
            
            // Only cancel if not already disposed
            try
            {
                _listenerCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
            }
            
            _listenerCts?.Dispose();
            _listener?.Close();
            _messageAvailable?.Dispose();
            
            foreach (var connection in _connections.Values)
            {
                connection.Dispose();
            }
            
            _disposed = true;
        }
    }

    /// <summary>
    /// Represents a WebSocket client connection in standalone WebSocketTransport.
    /// </summary>
    internal class WebSocketClient : IDisposable
    {
        private readonly WebSocket _webSocket;
        private readonly ILogger? _logger;
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private bool _disposed;

        public string Id { get; }
        public bool IsOpen => _webSocket.State == WebSocketState.Open;

        public WebSocketClient(string id, WebSocket webSocket, ILogger? logger)
        {
            Id = id;
            _webSocket = webSocket;
            _logger = logger;
        }

        public async Task<WebSocketReceiveResult?> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            try
            {
                if (_webSocket.State != WebSocketState.Open)
                {
                    return null;
                }
                
                return await _webSocket.ReceiveAsync(buffer, cancellationToken);
            }
            catch (WebSocketException)
            {
                return null;
            }
        }

        public async Task SendAsync(string message, CancellationToken cancellationToken)
        {
            if (_webSocket.State != WebSocketState.Open)
            {
                _logger?.LogWarning("Cannot send message to closed WebSocket connection {ConnectionId}", Id);
                return;
            }
            
            await _sendLock.WaitAsync(cancellationToken);
            try
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                var buffer = new ArraySegment<byte>(bytes);
                
                await _webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, cancellationToken);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public async Task CloseAsync(WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure, string? description = null)
        {
            try
            {
                if (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.CloseReceived)
                {
                    await _webSocket.CloseAsync(status, description ?? "Closing connection", CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error closing WebSocket connection {ConnectionId}", Id);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            _sendLock?.Dispose();
            _webSocket?.Dispose();
            _disposed = true;
        }
    }
}