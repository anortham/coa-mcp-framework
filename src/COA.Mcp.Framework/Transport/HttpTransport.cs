using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using COA.Mcp.Framework.Transport.Configuration;
using COA.Mcp.Framework.Transport.Correlation;

namespace COA.Mcp.Framework.Transport
{
    /// <summary>
    /// HTTP transport implementation using HttpListener for simple HTTP communication.
    /// </summary>
    public class HttpTransport : IMcpTransport
    {
        private readonly HttpTransportOptions _options;
        private readonly ILogger<HttpTransport>? _logger;
        private readonly ConcurrentQueue<TransportMessage> _messageQueue = new();
        private readonly SemaphoreSlim _messageAvailable = new(0);
        private readonly ConcurrentDictionary<string, WebSocketConnection> _webSocketConnections = new();
        private readonly RequestResponseCorrelator _correlator;
        private readonly ConcurrentDictionary<string, HttpRequestContext> _pendingHttpRequests = new();
        private HttpListener? _listener;
        private CancellationTokenSource? _listenerCts;
        private Task? _listenerTask;
        private bool _isConnected;
        private bool _disposed;
        private bool _authModeWarningLogged;

        public TransportType Type => TransportType.Http;
        public bool IsConnected => _isConnected;

        public event EventHandler<TransportDisconnectedEventArgs>? Disconnected;

        public HttpTransport(HttpTransportOptions options, ILogger<HttpTransport>? logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;
            _correlator = new RequestResponseCorrelator(
                TimeSpan.FromSeconds(_options.RequestTimeoutSeconds));
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _logger?.LogInformation("Starting HTTP transport on {Host}:{Port} (HTTPS: {UseHttps})", 
                _options.Host, _options.Port, _options.UseHttps);

            // Configure HTTPS if enabled
            if (_options.UseHttps)
            {
                await ConfigureHttpsAsync();
            }

            _listener = new HttpListener();
            var prefix = $"{(_options.UseHttps ? "https" : "http")}://{_options.Host}:{_options.Port}/";
            _listener.Prefixes.Add(prefix);
            
            try
            {
                _listener.Start();
                _isConnected = true;
                
                _listenerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _listenerTask = Task.Run(() => ProcessHttpRequests(_listenerCts.Token), _listenerCts.Token);
                
                _logger?.LogInformation("HTTP transport started successfully on {Prefix}", prefix);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to start HTTP transport. " +
                    "For HTTPS, ensure the certificate is properly configured.");
                throw;
            }
            
            await Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            _logger?.LogInformation("Stopping HTTP transport");
            
            _isConnected = false;
            _listenerCts?.Cancel();
            
            // Close all WebSocket connections
            foreach (var connection in _webSocketConnections.Values)
            {
                try
                {
                    await connection.CloseAsync();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error closing WebSocket connection {ConnectionId}", connection.Id);
                }
            }
            _webSocketConnections.Clear();
            
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
                _logger?.LogError(ex, "Error stopping HTTP transport");
            }
            
            Disconnected?.Invoke(this, new TransportDisconnectedEventArgs
            {
                Reason = "Transport stopped",
                WasClean = true
            });
        }

        private async Task ConfigureHttpsAsync()
        {
            try
            {
                // Check if certificate is already configured
                var certHash = GetCertificateHash();
                if (certHash != null)
                {
                    // Certificate is configured, verify it's bound to the port
                    await EnsureCertificateBinding(certHash);
                    _logger?.LogInformation("HTTPS configured with existing certificate");
                    return;
                }

                // If certificate path is provided, install it
                if (!string.IsNullOrEmpty(_options.CertificatePath))
                {
                    if (!File.Exists(_options.CertificatePath))
                    {
                        throw new FileNotFoundException($"Certificate file not found: {_options.CertificatePath}");
                    }

                    await InstallCertificate(_options.CertificatePath, _options.CertificatePassword);
                    _logger?.LogInformation("HTTPS configured with certificate from {Path}", _options.CertificatePath);
                }
                else
                {
                    // Try to use development certificate
                    await ConfigureDevelopmentCertificate();
                    _logger?.LogWarning("HTTPS configured with development certificate. " +
                        "This should only be used for development/testing.");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to configure HTTPS");
                throw new InvalidOperationException("Failed to configure HTTPS. " +
                    "Ensure you have the necessary permissions and a valid certificate.", ex);
            }
        }

        private async Task InstallCertificate(string certPath, string? password)
        {
            try
            {
                var cert = X509CertificateLoader.LoadPkcs12FromFile(certPath, password, 
                    X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.MachineKeySet);

                // Install certificate to local machine store
                using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadWrite);
                store.Add(cert);
                store.Close();

                // Bind certificate to port
                await BindCertificateToPort(cert.GetCertHash());
                
                _logger?.LogInformation("Certificate installed and bound to port {Port}", _options.Port);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to install certificate");
                throw;
            }
        }

        private async Task ConfigureDevelopmentCertificate()
        {
            // Check if development certificate exists
            using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            
            var certs = store.Certificates.Find(X509FindType.FindBySubjectName, _options.Host, false);
            X509Certificate2? devCert = null;
            
            foreach (var cert in certs)
            {
                if (cert.Subject.Contains($"CN={_options.Host}"))
                {
                    devCert = cert;
                    break;
                }
            }
            
            store.Close();

            if (devCert == null)
            {
                // Create a self-signed certificate for development
                devCert = CreateSelfSignedCertificate(_options.Host);
                
                // Install it
                using var installStore = new X509Store(StoreName.My, StoreLocation.LocalMachine);
                installStore.Open(OpenFlags.ReadWrite);
                installStore.Add(devCert);
                installStore.Close();
                
                _logger?.LogInformation("Created and installed development certificate for {Host}", _options.Host);
            }

            // Bind certificate to port
            await BindCertificateToPort(devCert.GetCertHash());
        }

        private X509Certificate2 CreateSelfSignedCertificate(string subjectName)
        {
            var distinguishedName = new X500DistinguishedName($"CN={subjectName}");

            using var rsa = System.Security.Cryptography.RSA.Create(2048);
            var request = new CertificateRequest(distinguishedName, rsa, 
                System.Security.Cryptography.HashAlgorithmName.SHA256, 
                System.Security.Cryptography.RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(false, false, 0, false));
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, 
                    false));
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, // Server Authentication
                    false));

            // Add Subject Alternative Names
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName(subjectName);
            if (subjectName == "localhost")
            {
                sanBuilder.AddIpAddress(IPAddress.Loopback);
                sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);
            }
            request.CertificateExtensions.Add(sanBuilder.Build());

            var certificate = request.CreateSelfSigned(
                DateTimeOffset.Now.AddMinutes(-5),
                DateTimeOffset.Now.AddYears(1));

            var pfxData = certificate.Export(X509ContentType.Pfx, "");
            return X509CertificateLoader.LoadPkcs12(pfxData, "", 
                X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.MachineKeySet);
        }

        private async Task BindCertificateToPort(byte[] certHash)
        {
            var certHashString = BitConverter.ToString(certHash).Replace("-", "");
            var appId = Guid.NewGuid().ToString();
            
            // Use netsh to bind certificate to port
            var netshArgs = $"http add sslcert ipport=0.0.0.0:{_options.Port} " +
                $"certhash={certHashString} appid={{{appId}}}";

            try
            {
                // First, try to delete existing binding
                await RunNetshCommand($"http delete sslcert ipport=0.0.0.0:{_options.Port}");
            }
            catch
            {
                // Ignore errors from delete - binding might not exist
            }

            // Add new binding
            await RunNetshCommand(netshArgs);
            _logger?.LogInformation("Certificate bound to port {Port}", _options.Port);
        }

        private async Task EnsureCertificateBinding(byte[] certHash)
        {
            // Check if binding exists
            var output = await RunNetshCommand($"http show sslcert ipport=0.0.0.0:{_options.Port}");
            
            if (!output.Contains("Certificate Hash"))
            {
                // Binding doesn't exist, create it
                await BindCertificateToPort(certHash);
            }
        }

        private byte[]? GetCertificateHash()
        {
            using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            
            X509Certificate2? cert = null;
            
            // If certificate path is provided, look for that specific cert
            if (!string.IsNullOrEmpty(_options.CertificatePath))
            {
                var targetCert = X509CertificateLoader.LoadPkcs12FromFile(_options.CertificatePath, _options.CertificatePassword);
                cert = store.Certificates.Find(X509FindType.FindByThumbprint, targetCert.Thumbprint, false)
                    .Cast<X509Certificate2>()
                    .FirstOrDefault();
            }
            else
            {
                // Look for a certificate for the host
                var certs = store.Certificates.Find(X509FindType.FindBySubjectName, _options.Host, false);
                cert = certs.Cast<X509Certificate2>().FirstOrDefault();
            }
            
            store.Close();
            return cert?.GetCertHash();
        }

        private async Task<string> RunNetshCommand(string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start netsh process");
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync();
            
            if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
            {
                throw new InvalidOperationException($"netsh command failed: {error}");
            }
            
            return output;
        }

        private async Task ProcessHttpRequests(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _listener?.IsListening == true)
            {
                try
                {
                    var contextTask = _listener.GetContextAsync();
                    
                    // Use cancellation token to stop waiting
                    using (cancellationToken.Register(() => _listener?.Stop()))
                    {
                        var context = await contextTask.ConfigureAwait(false);
                        
                        // Process request asynchronously
                        _ = Task.Run(() => HandleHttpRequest(context), cancellationToken);
                    }
                }
                catch (HttpListenerException ex) when (ex.ErrorCode == 995) // Listener stopped
                {
                    _logger?.LogDebug("HTTP listener stopped");
                    break;
                }
                catch (ObjectDisposedException)
                {
                    _logger?.LogDebug("HTTP listener disposed");
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error processing HTTP request");
                }
            }
        }

        private async Task HandleHttpRequest(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;
                
                // Check if this is a WebSocket upgrade request
                if (_options.EnableWebSocket && request.IsWebSocketRequest)
                {
                    await HandleWebSocketUpgrade(context);
                    return;
                }
                
                // Add CORS headers if enabled
                if (_options.EnableCors)
                {
                    var origin = request.Headers["Origin"];
                    if (IsOriginAllowed(origin))
                    {
                        response.Headers.Add("Access-Control-Allow-Origin", string.IsNullOrEmpty(origin) ? "*" : origin);
                        response.Headers.Add("Vary", "Origin");
                        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, X-API-Key");
                    }
                    else if (!string.IsNullOrEmpty(origin))
                    {
                        response.StatusCode = 403;
                        response.Close();
                        return;
                    }
                }
                
                // Handle preflight requests
                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 204;
                    response.Close();
                    return;
                }
                
                // Route based on path
                var path = request.Url?.AbsolutePath ?? "/";
                
                switch (path)
                {
                    case "/mcp/rpc" when request.HttpMethod == "POST":
                        await HandleJsonRpcRequest(context);
                        break;
                        
                    case "/mcp/health" when request.HttpMethod == "GET":
                        await HandleHealthCheck(context);
                        break;
                        
                    case "/mcp/tools" when request.HttpMethod == "GET":
                        await HandleListTools(context);
                        break;
                        
                    case "/mcp/ws" when request.HttpMethod == "GET" && _options.EnableWebSocket:
                        // WebSocket endpoint - should be handled above
                        response.StatusCode = 400;
                        var wsErrorBytes = Encoding.UTF8.GetBytes("WebSocket upgrade required");
                        await response.OutputStream.WriteAsync(wsErrorBytes, 0, wsErrorBytes.Length);
                        response.Close();
                        break;
                        
                    default:
                        response.StatusCode = 404;
                        var notFoundBytes = Encoding.UTF8.GetBytes("Not Found");
                        await response.OutputStream.WriteAsync(notFoundBytes, 0, notFoundBytes.Length);
                        response.Close();
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling HTTP request");
                
                try
                {
                    context.Response.StatusCode = 500;
                    var errorBytes = Encoding.UTF8.GetBytes("Internal Server Error");
                    await context.Response.OutputStream.WriteAsync(errorBytes, 0, errorBytes.Length);
                    context.Response.Close();
                }
                catch
                {
                    // Best effort error response
                }
            }
        }

        private async Task HandleJsonRpcRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var correlationId = Guid.NewGuid().ToString();
            
            try
            {
                // Enforce authentication if configured
                if (!Authenticate(request))
                {
                    context.Response.StatusCode = 401;
                    context.Response.Close();
                    return;
                }

                // Enforce max request size
                if (request.ContentLength64 > 0 && request.ContentLength64 > _options.MaxRequestSize)
                {
                    context.Response.StatusCode = 413; // Payload Too Large
                    var msg = Encoding.UTF8.GetBytes("Request entity too large");
                    await context.Response.OutputStream.WriteAsync(msg, 0, msg.Length);
                    context.Response.Close();
                    return;
                }

                // Read request body
                string requestBody;
                using (var limited = new LimitedStream(request.InputStream, _options.MaxRequestSize))
                using (var reader = new StreamReader(limited, request.ContentEncoding))
                {
                    requestBody = await reader.ReadToEndAsync();
                }
                
                if (string.IsNullOrWhiteSpace(requestBody))
                {
                    context.Response.StatusCode = 400;
                    var errorBytes = Encoding.UTF8.GetBytes("Empty request body");
                    await context.Response.OutputStream.WriteAsync(errorBytes, 0, errorBytes.Length);
                    context.Response.Close();
                    return;
                }

                // Parse JSON to extract the request ID
                object? jsonRpcId = null;
                try
                {
                    using var doc = JsonDocument.Parse(requestBody);
                    if (doc.RootElement.TryGetProperty("id", out var idElement))
                    {
                        jsonRpcId = idElement.ValueKind switch
                        {
                            JsonValueKind.Number => idElement.GetInt32(),
                            JsonValueKind.String => idElement.GetString(),
                            _ => idElement.GetRawText()
                        };
                    }
                }
                catch (JsonException ex)
                {
                    _logger?.LogWarning(ex, "Failed to parse JSON-RPC request");
                }

                // Create request context
                var requestContext = new HttpRequestContext(context, correlationId, requestBody)
                {
                    JsonRpcId = jsonRpcId
                };
                
                // Store the context for later response
                _pendingHttpRequests[correlationId] = requestContext;
                
                // Create transport message with correlation ID
                var transportMessage = new TransportMessage
                {
                    Content = requestBody,
                    CorrelationId = correlationId,
                    Headers = 
                    {
                        ["transport"] = "http",
                        ["method"] = request.HttpMethod,
                        ["path"] = request.Url?.AbsolutePath ?? "/",
                        ["remote-ip"] = request.RemoteEndPoint?.Address?.ToString() ?? "unknown",
                        ["has-response-context"] = "true"
                    }
                };
                
                // Add to queue for processing
                _messageQueue.Enqueue(transportMessage);
                _messageAvailable.Release();
                
                // Wait for response with timeout
                var timeout = TimeSpan.FromSeconds(_options.RequestTimeoutSeconds);
                var responseTask = _correlator.RegisterRequestAsync<string>(correlationId, timeout);
                
                try
                {
                    var responseContent = await responseTask;
                    await requestContext.SendResponseAsync(responseContent);
                }
                catch (TimeoutException)
                {
                    _logger?.LogWarning("Request timeout for correlation ID: {CorrelationId}", correlationId);
                    await requestContext.SendErrorResponseAsync(
                        "Request timeout", 
                        -32000, // Custom error code for timeout
                        504); // Gateway Timeout
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error processing request with correlation ID: {CorrelationId}", correlationId);
                    await requestContext.SendErrorResponseAsync(
                        "Internal server error",
                        -32603, // Internal error
                        500);
                }
                finally
                {
                    _pendingHttpRequests.TryRemove(correlationId, out _);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Fatal error handling JSON-RPC request");
                
                try
                {
                    context.Response.StatusCode = 500;
                    var errorBytes = Encoding.UTF8.GetBytes("Internal Server Error");
                    await context.Response.OutputStream.WriteAsync(errorBytes, 0, errorBytes.Length);
                    context.Response.Close();
                }
                catch
                {
                    // Best effort error response
                }
            }
        }

        private async Task HandleHealthCheck(HttpListenerContext context)
        {
            var response = context.Response;
            
            var health = new
            {
                status = _isConnected ? "healthy" : "unhealthy",
                transport = "http",
                timestamp = DateTime.UtcNow,
                options = new
                {
                    port = _options.Port,
                    host = _options.Host,
                    corsEnabled = _options.EnableCors,
                    webSocketEnabled = _options.EnableWebSocket,
                    authentication = _options.Authentication.ToString()
                },
                webSocketConnections = _webSocketConnections.Count
            };
            
            response.ContentType = "application/json";
            var json = JsonSerializer.Serialize(health);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            response.Close();
        }

        private async Task HandleListTools(HttpListenerContext context)
        {
            var response = context.Response;
            
            // Create a tools/list request message
            var request = new
            {
                jsonrpc = "2.0",
                method = "tools/list",
                id = Guid.NewGuid().ToString()
            };
            
            var transportMessage = new TransportMessage
            {
                Content = JsonSerializer.Serialize(request),
                Headers = { ["transport"] = "http", ["endpoint"] = "tools" }
            };
            
            _messageQueue.Enqueue(transportMessage);
            _messageAvailable.Release();
            
            // For now, return empty tools list
            // In real implementation, would wait for response
            response.ContentType = "application/json";
            var json = JsonSerializer.Serialize(new { tools = Array.Empty<object>() });
            var bytes = Encoding.UTF8.GetBytes(json);
            
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            response.Close();
        }

        private async Task HandleWebSocketUpgrade(HttpListenerContext context)
        {
            WebSocketContext? webSocketContext = null;
            WebSocketConnection? connection = null;
            
            try
            {
                // Accept the WebSocket connection
                webSocketContext = await context.AcceptWebSocketAsync(null);
                var webSocket = webSocketContext.WebSocket;
                
                var connectionId = Guid.NewGuid().ToString();
                connection = new WebSocketConnection(connectionId, webSocket, _logger);
                _webSocketConnections[connectionId] = connection;
                
                _logger?.LogInformation("WebSocket connection established: {ConnectionId} from {RemoteEndPoint}", 
                    connectionId, context.Request.RemoteEndPoint);
                
                // Process messages from this connection
                await ProcessWebSocketMessages(connection);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling WebSocket upgrade");
            }
            finally
            {
                if (connection != null)
                {
                    _webSocketConnections.TryRemove(connection.Id, out _);
                    await connection.CloseAsync();
                    _logger?.LogInformation("WebSocket connection closed: {ConnectionId}", connection.Id);
                }
            }
        }

        private async Task ProcessWebSocketMessages(WebSocketConnection connection)
        {
            var buffer = new ArraySegment<byte>(new byte[4096]);
            
            while (connection.IsOpen)
            {
                try
                {
                    var result = await connection.ReceiveAsync(buffer, CancellationToken.None);
                    
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
            // Wait for a message to be available
            await _messageAvailable.WaitAsync(cancellationToken);
            
            if (_messageQueue.TryDequeue(out var message))
            {
                return message;
            }
            
            return null;
        }

        public async Task WriteMessageAsync(TransportMessage message, CancellationToken cancellationToken = default)
        {
            // Check if this is a response to an HTTP request
            if (!string.IsNullOrEmpty(message.CorrelationId))
            {
                // Try to complete the pending HTTP request
                if (_correlator.TryCompleteRequest(message.CorrelationId, message.Content))
                {
                    _logger?.LogTrace("Completed HTTP request with correlation ID: {CorrelationId}", message.CorrelationId);
                    return; // Response has been sent via the correlator
                }
                
                // Also check if it's a pending HTTP request that needs direct response
                if (_pendingHttpRequests.TryRemove(message.CorrelationId, out var requestContext))
                {
                    try
                    {
                        await requestContext.SendResponseAsync(message.Content);
                        _logger?.LogTrace("Sent HTTP response for correlation ID: {CorrelationId}", message.CorrelationId);
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to send HTTP response for correlation ID: {CorrelationId}", message.CorrelationId);
                    }
                }
            }
            
            // If WebSocket is enabled and we have connections, send to them
            if (_options.EnableWebSocket && _webSocketConnections.Count > 0)
            {
                // If message has a connection ID, send to specific connection
                if (message.Headers.TryGetValue("connection-id", out var connectionId) && 
                    _webSocketConnections.TryGetValue(connectionId, out var connection))
                {
                    await connection.SendAsync(message.Content, cancellationToken);
                    _logger?.LogTrace("Sent message to WebSocket connection {ConnectionId}", connectionId);
                }
                else
                {
                    // Broadcast to all WebSocket connections
                    var tasks = new List<Task>();
                    foreach (var conn in _webSocketConnections.Values)
                    {
                        tasks.Add(conn.SendAsync(message.Content, cancellationToken));
                    }
                    
                    await Task.WhenAll(tasks);
                    _logger?.LogTrace("Broadcast message to {Count} WebSocket connections", _webSocketConnections.Count);
                }
            }
            else if (string.IsNullOrEmpty(message.CorrelationId))
            {
                // For basic HTTP without correlation, log that we can't send async messages
                _logger?.LogTrace("HTTP transport WriteMessageAsync called without correlation ID - cannot send async messages without WebSocket");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            _listenerCts?.Cancel();
            _listener?.Close();
            _messageAvailable?.Dispose();
            _listenerCts?.Dispose();
            _correlator?.Dispose();
            
            // Cancel any pending HTTP requests
            foreach (var context in _pendingHttpRequests.Values)
            {
                try
                {
                    context.SendErrorResponseAsync("Server shutting down", -32000, 503).Wait(TimeSpan.FromSeconds(1));
                }
                catch
                {
                    // Best effort
                }
            }
            _pendingHttpRequests.Clear();
            
            foreach (var connection in _webSocketConnections.Values)
            {
                connection.Dispose();
            }
            
            _disposed = true;
        }

        private bool IsOriginAllowed(string? origin)
        {
            if (!_options.EnableCors) return true;
            if (string.IsNullOrEmpty(origin)) return true;
            if (_options.AllowedOrigins.Any(o => o == "*")) return true;
            return _options.AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);
        }

        private bool Authenticate(HttpListenerRequest request)
        {
            switch (_options.Authentication)
            {
                case AuthenticationType.None:
                    return true;
                case AuthenticationType.ApiKey:
                    var header = request.Headers[_options.ApiKeyHeader];
                    return !string.IsNullOrEmpty(_options.ApiKey) && string.Equals(header, _options.ApiKey, StringComparison.Ordinal);
                case AuthenticationType.Basic:
                    return ValidateBasicAuth(request);
                case AuthenticationType.Jwt:
                    return ValidateJwtHs256(request);
                case AuthenticationType.Custom:
                default:
                    // Not implemented; log a warning once per process and treat as disabled to avoid false confidence
                    if (!_authModeWarningLogged)
                    {
                        _logger?.LogWarning("HTTP authentication mode {Mode} is configured but not enforced. Supported modes: None, ApiKey, Basic, Jwt (HS256)", _options.Authentication);
                        _authModeWarningLogged = true;
                    }
                    return true;
            }
        }

        private bool ValidateBasicAuth(HttpListenerRequest request)
        {
            try
            {
                var auth = request.Headers["Authorization"];
                if (string.IsNullOrEmpty(auth) || !auth.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                var b64 = auth.Substring("Basic ".Length).Trim();
                byte[] bytes;
                try { bytes = Convert.FromBase64String(b64); }
                catch { return false; }
                var decoded = Encoding.UTF8.GetString(bytes);
                var idx = decoded.IndexOf(':');
                if (idx <= 0) return false;
                var user = decoded.Substring(0, idx);
                var pass = decoded.Substring(idx + 1);
                if (string.IsNullOrEmpty(_options.BasicUsername) || string.IsNullOrEmpty(_options.BasicPassword))
                {
                    _logger?.LogWarning("Basic auth configured without credentials");
                    return false;
                }
                return user == _options.BasicUsername && pass == _options.BasicPassword;
            }
            catch
            {
                return false;
            }
        }

        private bool ValidateJwtHs256(HttpListenerRequest request)
        {
            try
            {
                if (_options.JwtSettings == null || string.IsNullOrEmpty(_options.JwtSettings.SecretKey))
                {
                    _logger?.LogWarning("JWT auth configured without secret key");
                    return false;
                }
                var auth = request.Headers["Authorization"];
                if (string.IsNullOrEmpty(auth) || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                var token = auth.Substring("Bearer ".Length).Trim();
                var parts = token.Split('.');
                if (parts.Length != 3) return false;

                var headerJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[0]));
                var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));

                // Verify alg
                if (!headerJson.Contains("\"HS256\"", StringComparison.Ordinal))
                {
                    _logger?.LogWarning("Unsupported JWT alg (expected HS256)");
                    return false;
                }

                // Verify signature
                var signingInput = Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]);
                using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(_options.JwtSettings.SecretKey));
                var sigBytes = hmac.ComputeHash(signingInput);
                var expectedSig = Base64UrlEncode(sigBytes);
                if (!string.Equals(parts[2], expectedSig, StringComparison.Ordinal))
                {
                    return false;
                }

                // Optional exp check
                try
                {
                    using var doc = JsonDocument.Parse(payloadJson);
                    if (doc.RootElement.TryGetProperty("exp", out var expEl) && expEl.ValueKind == JsonValueKind.Number)
                    {
                        var exp = expEl.GetInt64();
                        if (DateTimeOffset.UtcNow > DateTimeOffset.FromUnixTimeSeconds(exp))
                            return false;
                    }
                    if (!string.IsNullOrEmpty(_options.JwtSettings.Issuer) &&
                        doc.RootElement.TryGetProperty("iss", out var issEl) && issEl.ValueKind == JsonValueKind.String)
                    {
                        if (!string.Equals(issEl.GetString(), _options.JwtSettings.Issuer, StringComparison.Ordinal))
                            return false;
                    }
                    if (!string.IsNullOrEmpty(_options.JwtSettings.Audience) &&
                        doc.RootElement.TryGetProperty("aud", out var audEl) && audEl.ValueKind == JsonValueKind.String)
                    {
                        if (!string.Equals(audEl.GetString(), _options.JwtSettings.Audience, StringComparison.Ordinal))
                            return false;
                    }
                }
                catch
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static byte[] Base64UrlDecode(string s)
        {
            s = s.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
            }
            return Convert.FromBase64String(s);
        }

        private static string Base64UrlEncode(byte[] data)
        {
            var s = Convert.ToBase64String(data).TrimEnd('=');
            s = s.Replace('+', '-').Replace('/', '_');
            return s;
        }

        /// <summary>
        /// Stream wrapper that enforces a maximum number of readable bytes.
        /// </summary>
        private sealed class LimitedStream : Stream
        {
            private readonly Stream _inner;
            private long _remaining;

            public LimitedStream(Stream inner, long limit)
            {
                _inner = inner;
                _remaining = limit <= 0 ? long.MaxValue : limit;
            }

            public override bool CanRead => _inner.CanRead;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _inner.Length;
            public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }
            public override void Flush() => _inner.Flush();
            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_remaining <= 0) return 0;
                var toRead = (int)Math.Min(count, _remaining);
                var n = _inner.Read(buffer, offset, toRead);
                _remaining -= n;
                return n;
            }
            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                if (_remaining <= 0) return 0;
                var toRead = (int)Math.Min(count, _remaining);
                var n = await _inner.ReadAsync(buffer.AsMemory(offset, toRead), cancellationToken);
                _remaining -= n;
                return n;
            }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Represents a single WebSocket connection in HTTP transport.
    /// </summary>
    internal class WebSocketConnection : IDisposable
    {
        private readonly WebSocket _webSocket;
        private readonly ILogger? _logger;
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private bool _disposed;

        public string Id { get; }
        public bool IsOpen => _webSocket.State == WebSocketState.Open;

        public WebSocketConnection(string id, WebSocket webSocket, ILogger? logger)
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
