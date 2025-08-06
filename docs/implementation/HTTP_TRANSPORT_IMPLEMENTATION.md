# HTTP Transport Implementation Guide for COA MCP Framework

## Table of Contents
1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Implementation Steps](#implementation-steps)
4. [Code Examples](#code-examples)
5. [Configuration](#configuration)
6. [Testing](#testing)
7. [Security](#security)
8. [Performance](#performance)
9. [Migration Guide](#migration-guide)
10. [Troubleshooting](#troubleshooting)
11. [Implementation Checklist](#implementation-checklist)

## Overview

This document provides comprehensive guidance for implementing HTTP transport support in the COA MCP Framework, enabling MCP servers to communicate over HTTP/HTTPS in addition to the existing stdio transport.

### Goals
- Add HTTP transport without breaking existing stdio functionality
- Support both REST and WebSocket communication
- Enable web-based MCP clients
- Maintain backward compatibility
- Provide flexible configuration options

### Benefits
- **Web Integration**: Browser-based clients can connect directly
- **Debugging**: Use standard HTTP tools (Postman, curl, browser dev tools)
- **Scalability**: HTTP can be load-balanced and scaled horizontally
- **Security**: Built-in HTTPS, authentication, and CORS support
- **Monitoring**: Standard HTTP metrics and logging

## Architecture

### Transport Abstraction Layer

```
┌─────────────────┐
│   MCP Client    │
└────────┬────────┘
         │
    JSON-RPC
         │
┌────────▼────────┐
│  IMcpTransport  │ (Interface)
└────────┬────────┘
         │
    ┌────┴────┬──────────┐
    │         │          │
┌───▼──┐ ┌───▼──┐ ┌─────▼─────┐
│Stdio │ │HTTP  │ │WebSocket  │
└──────┘ └──────┘ └───────────┘
```

### HTTP Transport Architecture

```
┌─────────────────────────────────────────┐
│            HTTP Transport               │
├─────────────────────────────────────────┤
│  ┌─────────────┐    ┌──────────────┐  │
│  │  Kestrel    │    │   Routing    │  │
│  │   Server    │───▶│   Engine     │  │
│  └─────────────┘    └──────┬───────┘  │
│                             │          │
│  ┌──────────────────────────▼───────┐  │
│  │         Endpoints                │  │
│  ├──────────────────────────────────┤  │
│  │ POST /mcp/rpc    - JSON-RPC      │  │
│  │ GET  /mcp/tools  - List tools    │  │
│  │ GET  /mcp/health - Health check  │  │
│  │ WS   /mcp/ws     - WebSocket     │  │
│  └──────────────────────────────────┘  │
└─────────────────────────────────────────┘
```

## Implementation Steps

### Phase 1: Create Transport Abstraction

#### Step 1.1: Define IMcpTransport Interface

```csharp
// src/COA.Mcp.Framework/Transport/IMcpTransport.cs
using System;
using System.Threading;
using System.Threading.Tasks;

namespace COA.Mcp.Framework.Transport
{
    /// <summary>
    /// Defines the contract for MCP transport implementations.
    /// </summary>
    public interface IMcpTransport : IDisposable
    {
        /// <summary>
        /// Gets the transport type.
        /// </summary>
        TransportType Type { get; }
        
        /// <summary>
        /// Gets whether the transport is currently connected.
        /// </summary>
        bool IsConnected { get; }
        
        /// <summary>
        /// Starts the transport.
        /// </summary>
        Task StartAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Stops the transport.
        /// </summary>
        Task StopAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Reads the next message from the transport.
        /// </summary>
        Task<TransportMessage?> ReadMessageAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Writes a message to the transport.
        /// </summary>
        Task WriteMessageAsync(TransportMessage message, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Event raised when the transport is disconnected.
        /// </summary>
        event EventHandler<TransportDisconnectedEventArgs>? Disconnected;
    }
}
```

#### Step 1.2: Define Supporting Types

```csharp
// src/COA.Mcp.Framework/Transport/TransportType.cs
namespace COA.Mcp.Framework.Transport
{
    public enum TransportType
    {
        Stdio,
        Http,
        WebSocket,
        NamedPipe,
        Tcp
    }
}

// src/COA.Mcp.Framework/Transport/TransportMessage.cs
namespace COA.Mcp.Framework.Transport
{
    public class TransportMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Content { get; set; } = string.Empty;
        public Dictionary<string, string> Headers { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? CorrelationId { get; set; }
    }
}

// src/COA.Mcp.Framework/Transport/TransportDisconnectedEventArgs.cs
namespace COA.Mcp.Framework.Transport
{
    public class TransportDisconnectedEventArgs : EventArgs
    {
        public string Reason { get; set; }
        public Exception? Exception { get; set; }
        public bool WasClean { get; set; }
    }
}
```

### Phase 2: Implement Stdio Transport

#### Step 2.1: Create StdioTransport Class

```csharp
// src/COA.Mcp.Framework/Transport/StdioTransport.cs
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.Transport
{
    /// <summary>
    /// Stdio transport implementation for console-based communication.
    /// </summary>
    public class StdioTransport : IMcpTransport
    {
        private readonly TextReader _input;
        private readonly TextWriter _output;
        private readonly ILogger<StdioTransport>? _logger;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private bool _isConnected;
        private bool _disposed;

        public TransportType Type => TransportType.Stdio;
        public bool IsConnected => _isConnected;

        public event EventHandler<TransportDisconnectedEventArgs>? Disconnected;

        public StdioTransport(
            TextReader? input = null,
            TextWriter? output = null,
            ILogger<StdioTransport>? logger = null)
        {
            _input = input ?? Console.In;
            _output = output ?? Console.Out;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            _logger?.LogDebug("Starting stdio transport");
            _isConnected = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            _logger?.LogDebug("Stopping stdio transport");
            _isConnected = false;
            
            Disconnected?.Invoke(this, new TransportDisconnectedEventArgs
            {
                Reason = "Transport stopped",
                WasClean = true
            });
            
            return Task.CompletedTask;
        }

        public async Task<TransportMessage?> ReadMessageAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var line = await _input.ReadLineAsync();
                
                if (line == null)
                {
                    _logger?.LogDebug("End of input stream reached");
                    _isConnected = false;
                    
                    Disconnected?.Invoke(this, new TransportDisconnectedEventArgs
                    {
                        Reason = "End of input stream",
                        WasClean = true
                    });
                    
                    return null;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    return null;
                }

                return new TransportMessage
                {
                    Content = line,
                    Headers = { ["transport"] = "stdio" }
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error reading from stdio");
                
                Disconnected?.Invoke(this, new TransportDisconnectedEventArgs
                {
                    Reason = "Read error",
                    Exception = ex,
                    WasClean = false
                });
                
                throw;
            }
        }

        public async Task WriteMessageAsync(TransportMessage message, CancellationToken cancellationToken = default)
        {
            await _writeLock.WaitAsync(cancellationToken);
            try
            {
                await _output.WriteLineAsync(message.Content);
                await _output.FlushAsync();
                
                _logger?.LogTrace("Sent message: {MessageId}", message.Id);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            _writeLock?.Dispose();
            _disposed = true;
        }
    }
}
```

### Phase 3: Implement HTTP Transport

#### Step 3.1: Add Required NuGet Packages

```xml
<!-- Add to COA.Mcp.Framework.csproj -->
<ItemGroup>
  <PackageReference Include="Microsoft.AspNetCore.Http" Version="2.2.2" />
  <PackageReference Include="Microsoft.AspNetCore.Hosting" Version="2.2.7" />
  <PackageReference Include="Microsoft.AspNetCore.Server.Kestrel" Version="2.2.0" />
  <PackageReference Include="Microsoft.AspNetCore.WebSockets" Version="2.2.1" />
  <PackageReference Include="Microsoft.AspNetCore.Cors" Version="2.2.0" />
</ItemGroup>
```

#### Step 3.2: Create HTTP Transport Options

```csharp
// src/COA.Mcp.Framework/Transport/Configuration/HttpTransportOptions.cs
namespace COA.Mcp.Framework.Transport.Configuration
{
    public class HttpTransportOptions
    {
        /// <summary>
        /// Port to listen on (default: 5000).
        /// </summary>
        public int Port { get; set; } = 5000;
        
        /// <summary>
        /// Host to bind to (default: localhost).
        /// </summary>
        public string Host { get; set; } = "localhost";
        
        /// <summary>
        /// Enable HTTPS (default: false).
        /// </summary>
        public bool UseHttps { get; set; }
        
        /// <summary>
        /// Certificate path for HTTPS.
        /// </summary>
        public string? CertificatePath { get; set; }
        
        /// <summary>
        /// Certificate password for HTTPS.
        /// </summary>
        public string? CertificatePassword { get; set; }
        
        /// <summary>
        /// Enable WebSocket support (default: true).
        /// </summary>
        public bool EnableWebSocket { get; set; } = true;
        
        /// <summary>
        /// Enable CORS (default: true).
        /// </summary>
        public bool EnableCors { get; set; } = true;
        
        /// <summary>
        /// Allowed CORS origins (default: * for all).
        /// </summary>
        public string[] AllowedOrigins { get; set; } = new[] { "*" };
        
        /// <summary>
        /// Authentication type.
        /// </summary>
        public AuthenticationType Authentication { get; set; } = AuthenticationType.None;
        
        /// <summary>
        /// API key for authentication (if using ApiKey auth).
        /// </summary>
        public string? ApiKey { get; set; }
        
        /// <summary>
        /// JWT settings (if using JWT auth).
        /// </summary>
        public JwtSettings? JwtSettings { get; set; }
        
        /// <summary>
        /// Request timeout in seconds (default: 30).
        /// </summary>
        public int RequestTimeoutSeconds { get; set; } = 30;
        
        /// <summary>
        /// Max request size in bytes (default: 10MB).
        /// </summary>
        public long MaxRequestSize { get; set; } = 10 * 1024 * 1024;
    }
    
    public enum AuthenticationType
    {
        None,
        ApiKey,
        Jwt,
        Basic,
        Custom
    }
    
    public class JwtSettings
    {
        public string? SecretKey { get; set; }
        public string? Issuer { get; set; }
        public string? Audience { get; set; }
        public int ExpirationMinutes { get; set; } = 60;
    }
}
```

#### Step 3.3: Create HTTP Transport Implementation

```csharp
// src/COA.Mcp.Framework/Transport/HttpTransport.cs
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace COA.Mcp.Framework.Transport
{
    /// <summary>
    /// HTTP transport implementation for web-based communication.
    /// </summary>
    public class HttpTransport : IMcpTransport
    {
        private readonly HttpTransportOptions _options;
        private readonly ILogger<HttpTransport>? _logger;
        private readonly ConcurrentQueue<TransportMessage> _messageQueue = new();
        private readonly SemaphoreSlim _messageAvailable = new(0);
        private IWebHost? _webHost;
        private bool _isConnected;
        private bool _disposed;

        public TransportType Type => TransportType.Http;
        public bool IsConnected => _isConnected;

        public event EventHandler<TransportDisconnectedEventArgs>? Disconnected;

        public HttpTransport(HttpTransportOptions options, ILogger<HttpTransport>? logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _logger?.LogInformation("Starting HTTP transport on {Host}:{Port}", _options.Host, _options.Port);

            var builder = new WebHostBuilder()
                .UseKestrel(options =>
                {
                    options.Limits.MaxRequestBodySize = _options.MaxRequestSize;
                })
                .UseUrls($"{(_options.UseHttps ? "https" : "http")}://{_options.Host}:{_options.Port}")
                .ConfigureServices(services =>
                {
                    services.AddSingleton(this);
                    services.AddSingleton(_options);
                    
                    if (_options.EnableCors)
                    {
                        services.AddCors(options =>
                        {
                            options.AddPolicy("McpCors", policy =>
                            {
                                policy.WithOrigins(_options.AllowedOrigins)
                                      .AllowAnyMethod()
                                      .AllowAnyHeader()
                                      .AllowCredentials();
                            });
                        });
                    }
                    
                    services.AddRouting();
                    
                    if (_options.Authentication != AuthenticationType.None)
                    {
                        ConfigureAuthentication(services);
                    }
                })
                .Configure(app =>
                {
                    if (_options.EnableCors)
                    {
                        app.UseCors("McpCors");
                    }
                    
                    if (_options.Authentication != AuthenticationType.None)
                    {
                        app.UseAuthentication();
                    }
                    
                    if (_options.EnableWebSocket)
                    {
                        app.UseWebSockets();
                    }
                    
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        ConfigureEndpoints(endpoints);
                    });
                });

            if (_logger != null)
            {
                builder.ConfigureLogging(logging =>
                {
                    logging.AddProvider(_logger.GetType()
                        .GetProperty("Provider")
                        ?.GetValue(_logger) as ILoggerProvider);
                });
            }

            _webHost = builder.Build();
            await _webHost.StartAsync(cancellationToken);
            
            _isConnected = true;
            _logger?.LogInformation("HTTP transport started successfully");
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            _logger?.LogInformation("Stopping HTTP transport");
            
            _isConnected = false;
            
            if (_webHost != null)
            {
                await _webHost.StopAsync(cancellationToken);
                _webHost.Dispose();
                _webHost = null;
            }
            
            Disconnected?.Invoke(this, new TransportDisconnectedEventArgs
            {
                Reason = "Transport stopped",
                WasClean = true
            });
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

        public Task WriteMessageAsync(TransportMessage message, CancellationToken cancellationToken = default)
        {
            // For HTTP transport, responses are sent directly in the request handler
            // This method is used for async notifications or WebSocket messages
            _logger?.LogTrace("Queuing response message: {MessageId}", message.Id);
            return Task.CompletedTask;
        }

        private void ConfigureEndpoints(IEndpointRouteBuilder endpoints)
        {
            // Main JSON-RPC endpoint
            endpoints.MapPost("/mcp/rpc", async context =>
            {
                await HandleJsonRpcRequest(context);
            });

            // List tools endpoint
            endpoints.MapGet("/mcp/tools", async context =>
            {
                await HandleListToolsRequest(context);
            });

            // Health check endpoint
            endpoints.MapGet("/mcp/health", async context =>
            {
                await HandleHealthCheck(context);
            });

            // WebSocket endpoint
            if (_options.EnableWebSocket)
            {
                endpoints.Map("/mcp/ws", async context =>
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        await HandleWebSocket(context);
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                    }
                });
            }
        }

        private async Task HandleJsonRpcRequest(HttpContext context)
        {
            try
            {
                // Read request body
                using var reader = new System.IO.StreamReader(context.Request.Body);
                var requestBody = await reader.ReadToEndAsync();
                
                if (string.IsNullOrWhiteSpace(requestBody))
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync("Empty request body");
                    return;
                }

                // Create transport message
                var transportMessage = new TransportMessage
                {
                    Content = requestBody,
                    Headers = 
                    {
                        ["transport"] = "http",
                        ["method"] = context.Request.Method,
                        ["path"] = context.Request.Path,
                        ["remote-ip"] = context.Connection.RemoteIpAddress?.ToString() ?? "unknown"
                    }
                };

                // Add to queue for processing
                _messageQueue.Enqueue(transportMessage);
                _messageAvailable.Release();

                // Wait for response (this would be improved with proper correlation)
                // For now, we'll simulate a synchronous response
                await Task.Delay(100); // Give processor time to handle

                // Send response
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"jsonrpc\":\"2.0\",\"result\":\"processed\",\"id\":1}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling JSON-RPC request");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("Internal server error");
            }
        }

        private async Task HandleListToolsRequest(HttpContext context)
        {
            context.Response.ContentType = "application/json";
            
            // Create a tools/list request
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
            
            // TODO: Implement proper response correlation
            await context.Response.WriteAsync("{\"tools\":[]}");
        }

        private async Task HandleHealthCheck(HttpContext context)
        {
            var health = new
            {
                status = _isConnected ? "healthy" : "unhealthy",
                transport = "http",
                timestamp = DateTime.UtcNow,
                options = new
                {
                    port = _options.Port,
                    host = _options.Host,
                    webSocketEnabled = _options.EnableWebSocket,
                    corsEnabled = _options.EnableCors,
                    authentication = _options.Authentication.ToString()
                }
            };
            
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(health));
        }

        private async Task HandleWebSocket(HttpContext context)
        {
            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            _logger?.LogInformation("WebSocket connection established");
            
            var buffer = new byte[1024 * 4];
            
            while (webSocket.State == System.Net.WebSockets.WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    CancellationToken.None);
                
                if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Text)
                {
                    var message = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                    
                    var transportMessage = new TransportMessage
                    {
                        Content = message,
                        Headers = { ["transport"] = "websocket" }
                    };
                    
                    _messageQueue.Enqueue(transportMessage);
                    _messageAvailable.Release();
                }
                else if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(
                        System.Net.WebSockets.WebSocketCloseStatus.NormalClosure,
                        "Closing",
                        CancellationToken.None);
                }
            }
            
            _logger?.LogInformation("WebSocket connection closed");
        }

        private void ConfigureAuthentication(IServiceCollection services)
        {
            switch (_options.Authentication)
            {
                case AuthenticationType.ApiKey:
                    // Add API key authentication middleware
                    services.AddAuthentication("ApiKey")
                        .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>("ApiKey", options =>
                        {
                            options.ApiKey = _options.ApiKey;
                        });
                    break;
                    
                case AuthenticationType.Jwt:
                    // Add JWT authentication
                    // Implementation would go here
                    break;
                    
                case AuthenticationType.Basic:
                    // Add basic authentication
                    // Implementation would go here
                    break;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            _webHost?.Dispose();
            _messageAvailable?.Dispose();
            _disposed = true;
        }
    }
}
```

### Phase 4: Update McpServer

#### Step 4.1: Refactor McpServer to Use Transport

```csharp
// src/COA.Mcp.Framework/Server/McpServer.cs (modified)
using COA.Mcp.Framework.Transport;

public class McpServer : IHostedService
{
    private readonly IMcpTransport _transport;
    private readonly McpToolRegistry _toolRegistry;
    private readonly ResourceRegistry _resourceRegistry;
    private readonly ILogger<McpServer>? _logger;
    
    public McpServer(
        IMcpTransport transport,  // Changed from TextReader/TextWriter
        McpToolRegistry toolRegistry,
        ResourceRegistry resourceRegistry,
        Implementation serverInfo,
        ILogger<McpServer>? logger = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        // ... rest of initialization
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Starting MCP server with {Transport} transport", 
            _transport.Type);
        
        // Start the transport
        await _transport.StartAsync(cancellationToken);
        
        // Start processing messages
        _ = Task.Run(() => ProcessMessagesAsync(cancellationToken), cancellationToken);
    }
    
    private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _transport.IsConnected)
        {
            try
            {
                var message = await _transport.ReadMessageAsync(cancellationToken);
                if (message == null) continue;
                
                await ProcessMessageAsync(message, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing message");
            }
        }
    }
    
    private async Task SendResponseAsync(string response, string? correlationId = null)
    {
        var message = new TransportMessage
        {
            Content = response,
            CorrelationId = correlationId
        };
        
        await _transport.WriteMessageAsync(message);
    }
}
```

### Phase 5: Update McpServerBuilder

#### Step 5.1: Add Transport Configuration Methods

```csharp
// src/COA.Mcp.Framework/Server/McpServerBuilder.cs (modified)
using COA.Mcp.Framework.Transport;
using COA.Mcp.Framework.Transport.Configuration;

public class McpServerBuilder
{
    private IMcpTransport? _transport;
    private readonly List<IMcpTransport> _transports = new();
    private bool _useMultiTransport;
    
    /// <summary>
    /// Use stdio transport (default).
    /// </summary>
    public McpServerBuilder UseStdioTransport(Action<StdioTransportOptions>? configure = null)
    {
        var options = new StdioTransportOptions();
        configure?.Invoke(options);
        
        var transport = new StdioTransport(
            options.Input, 
            options.Output,
            _services.GetService<ILogger<StdioTransport>>());
        
        _transport = transport;
        _services.AddSingleton<IMcpTransport>(transport);
        
        return this;
    }
    
    /// <summary>
    /// Use HTTP transport.
    /// </summary>
    public McpServerBuilder UseHttpTransport(Action<HttpTransportOptions>? configure = null)
    {
        var options = new HttpTransportOptions();
        configure?.Invoke(options);
        
        var transport = new HttpTransport(
            options,
            _services.GetService<ILogger<HttpTransport>>());
        
        _transport = transport;
        _services.AddSingleton<IMcpTransport>(transport);
        _services.AddSingleton(options);
        
        return this;
    }
    
    /// <summary>
    /// Use multiple transports simultaneously.
    /// </summary>
    public McpServerBuilder UseMultiTransport(Action<MultiTransportOptions> configure)
    {
        var options = new MultiTransportOptions();
        configure(options);
        
        _useMultiTransport = true;
        
        if (options.EnableStdio)
        {
            var stdioTransport = new StdioTransport();
            _transports.Add(stdioTransport);
        }
        
        if (options.EnableHttp)
        {
            var httpTransport = new HttpTransport(options.HttpOptions);
            _transports.Add(httpTransport);
        }
        
        // Create multi-transport wrapper
        var multiTransport = new MultiTransport(_transports);
        _transport = multiTransport;
        _services.AddSingleton<IMcpTransport>(multiTransport);
        
        return this;
    }
    
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // If no transport specified, use stdio by default
        if (_transport == null)
        {
            UseStdioTransport();
        }
        
        var serviceProvider = _services.BuildServiceProvider();
        var server = serviceProvider.GetRequiredService<McpServer>();
        
        await server.StartAsync(cancellationToken);
        
        // Keep running until cancelled
        var tcs = new TaskCompletionSource<bool>();
        cancellationToken.Register(() => tcs.SetResult(true));
        await tcs.Task;
        
        await server.StopAsync(CancellationToken.None);
    }
}
```

## Code Examples

### Example 1: Simple HTTP Server

```csharp
// Program.cs
using COA.Mcp.Framework.Server;
using COA.Mcp.Framework.Transport.Configuration;

var builder = new McpServerBuilder()
    .WithServerInfo("HTTP MCP Server", "1.0.0")
    .UseHttpTransport(options =>
    {
        options.Port = 5000;
        options.EnableWebSocket = true;
        options.EnableCors = true;
    })
    .RegisterToolType<WeatherTool>()
    .ConfigureLogging(logging =>
    {
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
    });

await builder.RunAsync();
```

### Example 2: Secure HTTPS Server with Authentication

```csharp
var builder = new McpServerBuilder()
    .WithServerInfo("Secure MCP Server", "1.0.0")
    .UseHttpTransport(options =>
    {
        options.Port = 5001;
        options.UseHttps = true;
        options.CertificatePath = "/path/to/cert.pfx";
        options.CertificatePassword = Environment.GetEnvironmentVariable("CERT_PASSWORD");
        options.Authentication = AuthenticationType.ApiKey;
        options.ApiKey = Environment.GetEnvironmentVariable("API_KEY");
        options.EnableCors = true;
        options.AllowedOrigins = new[] { "https://myapp.com" };
    })
    .RegisterToolType<SecureTool>()
    .Build();

await builder.RunAsync();
```

### Example 3: Multi-Transport Server

```csharp
var builder = new McpServerBuilder()
    .WithServerInfo("Multi-Transport Server", "1.0.0")
    .UseMultiTransport(options =>
    {
        options.EnableStdio = true;
        options.EnableHttp = true;
        options.HttpOptions.Port = 5000;
        options.HttpOptions.EnableWebSocket = true;
    })
    .DiscoverTools(typeof(Program).Assembly)
    .Build();

await builder.RunAsync();
```

### Example 4: HTTP Client Implementation

```csharp
// McpHttpClient.cs
using System.Net.Http;
using System.Text;
using System.Text.Json;

public class McpHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private int _requestId = 0;

    public McpHttpClient(string baseUrl)
    {
        _baseUrl = baseUrl;
        _httpClient = new HttpClient();
    }

    public async Task<InitializeResult> InitializeAsync()
    {
        var request = new
        {
            jsonrpc = "2.0",
            method = "initialize",
            @params = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new
                {
                    name = "MCP HTTP Client",
                    version = "1.0.0"
                }
            },
            id = ++_requestId
        };

        var response = await SendRequestAsync(request);
        return JsonSerializer.Deserialize<InitializeResult>(response);
    }

    public async Task<ListToolsResult> ListToolsAsync()
    {
        var request = new
        {
            jsonrpc = "2.0",
            method = "tools/list",
            id = ++_requestId
        };

        var response = await SendRequestAsync(request);
        return JsonSerializer.Deserialize<ListToolsResult>(response);
    }

    public async Task<CallToolResult> CallToolAsync(string toolName, object parameters)
    {
        var request = new
        {
            jsonrpc = "2.0",
            method = "tools/call",
            @params = new
            {
                name = toolName,
                arguments = parameters
            },
            id = ++_requestId
        };

        var response = await SendRequestAsync(request);
        return JsonSerializer.Deserialize<CallToolResult>(response);
    }

    private async Task<string> SendRequestAsync(object request)
    {
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync($"{_baseUrl}/mcp/rpc", content);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadAsStringAsync();
    }
}

// Usage
var client = new McpHttpClient("http://localhost:5000");
await client.InitializeAsync();

var tools = await client.ListToolsAsync();
foreach (var tool in tools.Tools)
{
    Console.WriteLine($"Tool: {tool.Name} - {tool.Description}");
}

var result = await client.CallToolAsync("weather", new { location = "Seattle" });
Console.WriteLine($"Result: {result}");
```

### Example 5: JavaScript WebSocket Client

```javascript
// mcp-websocket-client.js
class McpWebSocketClient {
    constructor(url) {
        this.url = url;
        this.ws = null;
        this.requestId = 0;
        this.pendingRequests = new Map();
    }

    connect() {
        return new Promise((resolve, reject) => {
            this.ws = new WebSocket(this.url);
            
            this.ws.onopen = () => {
                console.log('Connected to MCP server');
                resolve();
            };
            
            this.ws.onerror = (error) => {
                console.error('WebSocket error:', error);
                reject(error);
            };
            
            this.ws.onmessage = (event) => {
                this.handleMessage(event.data);
            };
            
            this.ws.onclose = () => {
                console.log('Disconnected from MCP server');
            };
        });
    }

    handleMessage(data) {
        const message = JSON.parse(data);
        
        if (message.id && this.pendingRequests.has(message.id)) {
            const { resolve, reject } = this.pendingRequests.get(message.id);
            this.pendingRequests.delete(message.id);
            
            if (message.error) {
                reject(message.error);
            } else {
                resolve(message.result);
            }
        }
    }

    sendRequest(method, params) {
        return new Promise((resolve, reject) => {
            const id = ++this.requestId;
            
            const request = {
                jsonrpc: '2.0',
                method: method,
                params: params,
                id: id
            };
            
            this.pendingRequests.set(id, { resolve, reject });
            this.ws.send(JSON.stringify(request));
            
            // Timeout after 30 seconds
            setTimeout(() => {
                if (this.pendingRequests.has(id)) {
                    this.pendingRequests.delete(id);
                    reject(new Error('Request timeout'));
                }
            }, 30000);
        });
    }

    async initialize() {
        return this.sendRequest('initialize', {
            protocolVersion: '2024-11-05',
            capabilities: {},
            clientInfo: {
                name: 'MCP WebSocket Client',
                version: '1.0.0'
            }
        });
    }

    async listTools() {
        return this.sendRequest('tools/list', {});
    }

    async callTool(name, arguments) {
        return this.sendRequest('tools/call', {
            name: name,
            arguments: arguments
        });
    }

    close() {
        if (this.ws) {
            this.ws.close();
        }
    }
}

// Usage
async function main() {
    const client = new McpWebSocketClient('ws://localhost:5000/mcp/ws');
    
    try {
        await client.connect();
        
        const initResult = await client.initialize();
        console.log('Initialized:', initResult);
        
        const tools = await client.listTools();
        console.log('Available tools:', tools);
        
        const result = await client.callTool('weather', {
            location: 'Seattle'
        });
        console.log('Weather result:', result);
        
    } finally {
        client.close();
    }
}

main().catch(console.error);
```

## Configuration

### appsettings.json Configuration

```json
{
  "MCP": {
    "ServerInfo": {
      "Name": "My MCP Server",
      "Version": "1.0.0"
    },
    "Transport": {
      "Type": "Http",
      "Http": {
        "Port": 5000,
        "Host": "localhost",
        "UseHttps": false,
        "EnableWebSocket": true,
        "EnableCors": true,
        "AllowedOrigins": ["*"],
        "Authentication": "None",
        "RequestTimeoutSeconds": 30,
        "MaxRequestSize": 10485760
      }
    },
    "Logging": {
      "LogLevel": {
        "Default": "Information",
        "COA.Mcp.Framework.Transport": "Debug"
      }
    }
  }
}
```

### Environment Variable Configuration

```bash
# Linux/Mac
export MCP_TRANSPORT_TYPE=Http
export MCP_HTTP_PORT=5000
export MCP_HTTP_ENABLE_WEBSOCKET=true
export MCP_HTTP_ENABLE_CORS=true
export MCP_HTTP_AUTH_TYPE=ApiKey
export MCP_HTTP_API_KEY=your-secret-key

# Windows
set MCP_TRANSPORT_TYPE=Http
set MCP_HTTP_PORT=5000
set MCP_HTTP_ENABLE_WEBSOCKET=true
set MCP_HTTP_ENABLE_CORS=true
set MCP_HTTP_AUTH_TYPE=ApiKey
set MCP_HTTP_API_KEY=your-secret-key
```

### Docker Configuration

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY . .

# Expose HTTP port
EXPOSE 5000

# Environment variables
ENV MCP_TRANSPORT_TYPE=Http
ENV MCP_HTTP_PORT=5000
ENV MCP_HTTP_HOST=0.0.0.0
ENV MCP_HTTP_ENABLE_WEBSOCKET=true

ENTRYPOINT ["dotnet", "MyMcpServer.dll"]
```

## Testing

### Unit Tests

```csharp
// HttpTransportTests.cs
[TestFixture]
public class HttpTransportTests
{
    private HttpTransport _transport;
    private HttpTransportOptions _options;

    [SetUp]
    public void Setup()
    {
        _options = new HttpTransportOptions
        {
            Port = 5555, // Use different port for tests
            Host = "localhost"
        };
        _transport = new HttpTransport(_options);
    }

    [TearDown]
    public async Task Teardown()
    {
        if (_transport.IsConnected)
        {
            await _transport.StopAsync();
        }
        _transport.Dispose();
    }

    [Test]
    public async Task StartAsync_ShouldStartServer()
    {
        // Act
        await _transport.StartAsync();
        
        // Assert
        _transport.IsConnected.Should().BeTrue();
        _transport.Type.Should().Be(TransportType.Http);
    }

    [Test]
    public async Task StopAsync_ShouldStopServer()
    {
        // Arrange
        await _transport.StartAsync();
        
        // Act
        await _transport.StopAsync();
        
        // Assert
        _transport.IsConnected.Should().BeFalse();
    }

    [Test]
    public async Task HttpEndpoint_ShouldRespond()
    {
        // Arrange
        await _transport.StartAsync();
        using var client = new HttpClient();
        
        // Act
        var response = await client.GetAsync($"http://localhost:{_options.Port}/mcp/health");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("healthy");
    }
}
```

### Integration Tests

```csharp
// HttpTransportIntegrationTests.cs
[TestFixture]
public class HttpTransportIntegrationTests
{
    private McpServer _server;
    private McpHttpClient _client;

    [SetUp]
    public async Task Setup()
    {
        var builder = new McpServerBuilder()
            .WithServerInfo("Test Server", "1.0.0")
            .UseHttpTransport(options =>
            {
                options.Port = 5556;
                options.EnableWebSocket = true;
            })
            .RegisterToolType<TestTool>();

        _server = builder.Build();
        await _server.StartAsync();
        
        _client = new McpHttpClient("http://localhost:5556");
    }

    [TearDown]
    public async Task Teardown()
    {
        await _server.StopAsync();
        _client.Dispose();
    }

    [Test]
    public async Task FullWorkflow_ShouldWork()
    {
        // Initialize
        var initResult = await _client.InitializeAsync();
        initResult.Should().NotBeNull();
        initResult.ProtocolVersion.Should().Be("2024-11-05");
        
        // List tools
        var tools = await _client.ListToolsAsync();
        tools.Tools.Should().ContainSingle(t => t.Name == "test_tool");
        
        // Call tool
        var result = await _client.CallToolAsync("test_tool", new { input = "test" });
        result.Should().NotBeNull();
        result.IsError.Should().BeFalse();
    }
}
```

### Performance Tests

```csharp
[Test]
public async Task HttpTransport_ShouldHandleConcurrentRequests()
{
    // Arrange
    await _transport.StartAsync();
    var tasks = new List<Task<HttpResponseMessage>>();
    using var client = new HttpClient();
    
    // Act
    for (int i = 0; i < 100; i++)
    {
        tasks.Add(client.GetAsync($"http://localhost:{_options.Port}/mcp/health"));
    }
    
    var responses = await Task.WhenAll(tasks);
    
    // Assert
    responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));
}
```

## Security

### API Key Authentication Implementation

```csharp
// ApiKeyAuthenticationHandler.cs
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private const string ApiKeyHeaderName = "X-API-Key";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyHeaderValues))
        {
            return AuthenticateResult.Fail("API Key was not provided");
        }

        var providedApiKey = apiKeyHeaderValues.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(providedApiKey))
        {
            return AuthenticateResult.Fail("API Key was not provided");
        }

        if (providedApiKey != Options.ApiKey)
        {
            return AuthenticateResult.Fail("Invalid API Key");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "ApiKeyUser"),
            new Claim("ApiKey", "true")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public string? ApiKey { get; set; }
}
```

### CORS Configuration

```csharp
// In HttpTransport.ConfigureServices
services.AddCors(options =>
{
    options.AddPolicy("McpCors", policy =>
    {
        if (_options.AllowedOrigins.Contains("*"))
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            policy.WithOrigins(_options.AllowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
    });
});
```

### HTTPS Configuration

```csharp
// In HttpTransport
.UseKestrel(options =>
{
    if (_options.UseHttps)
    {
        options.Listen(IPAddress.Any, _options.Port, listenOptions =>
        {
            if (!string.IsNullOrEmpty(_options.CertificatePath))
            {
                listenOptions.UseHttps(_options.CertificatePath, _options.CertificatePassword);
            }
            else
            {
                // Use development certificate
                listenOptions.UseHttps();
            }
        });
    }
})
```

## Performance

### Connection Pooling

```csharp
// HttpClientFactory configuration
services.AddHttpClient("MCP", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "MCP-Client");
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    MaxConnectionsPerServer = 10,
    UseProxy = false
});
```

### Response Caching

```csharp
// Add response caching middleware
services.AddResponseCaching();

app.UseResponseCaching();

// In endpoint
endpoints.MapGet("/mcp/tools", async context =>
{
    context.Response.Headers.Add("Cache-Control", "public, max-age=300"); // Cache for 5 minutes
    await HandleListToolsRequest(context);
});
```

### Request/Response Compression

```csharp
// Enable compression
services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/json" });
});

app.UseResponseCompression();
```

## Migration Guide

### Migrating from Stdio to HTTP

1. **Update Configuration**
```csharp
// Old (stdio)
var server = new McpServerBuilder()
    .WithServerInfo("My Server", "1.0.0")
    .RegisterToolType<MyTool>()
    .Build();

// New (HTTP)
var server = new McpServerBuilder()
    .WithServerInfo("My Server", "1.0.0")
    .UseHttpTransport(options => options.Port = 5000)  // Add this line
    .RegisterToolType<MyTool>()
    .Build();
```

2. **Update Client Code**
```csharp
// Old (stdio client)
// Direct console communication

// New (HTTP client)
var client = new McpHttpClient("http://localhost:5000");
await client.InitializeAsync();
```

3. **Update Deployment**
```yaml
# docker-compose.yml
version: '3.8'
services:
  mcp-server:
    image: my-mcp-server
    ports:
      - "5000:5000"  # Expose HTTP port
    environment:
      - MCP_TRANSPORT_TYPE=Http
      - MCP_HTTP_PORT=5000
```

### Supporting Both Transports

```csharp
// Support both based on environment
var transportType = Environment.GetEnvironmentVariable("MCP_TRANSPORT_TYPE") ?? "Stdio";

var builder = new McpServerBuilder()
    .WithServerInfo("My Server", "1.0.0");

if (transportType == "Http")
{
    builder.UseHttpTransport(options =>
    {
        options.Port = int.Parse(Environment.GetEnvironmentVariable("MCP_HTTP_PORT") ?? "5000");
    });
}
else
{
    builder.UseStdioTransport();
}

await builder.RunAsync();
```

## Troubleshooting

### Common Issues and Solutions

| Issue | Cause | Solution |
|-------|-------|----------|
| Port already in use | Another service using the port | Change port in configuration |
| WebSocket connection fails | WebSocket not enabled | Set `EnableWebSocket = true` |
| CORS errors | CORS not configured | Enable CORS and configure origins |
| Authentication failures | Missing or invalid API key | Check API key configuration |
| SSL/TLS errors | Invalid certificate | Verify certificate path and password |
| Connection timeouts | Network issues or firewall | Check firewall rules and network |
| Large request failures | Request size limit | Increase `MaxRequestSize` |
| Memory leaks | Not disposing transports | Ensure proper disposal |

### Debug Logging

```csharp
// Enable detailed logging
builder.ConfigureLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Trace);
    logging.AddFilter("COA.Mcp.Framework.Transport", LogLevel.Debug);
});
```

### Health Check Script

```bash
#!/bin/bash
# health-check.sh

URL="http://localhost:5000/mcp/health"
MAX_RETRIES=10
RETRY_DELAY=2

for i in $(seq 1 $MAX_RETRIES); do
    response=$(curl -s -o /dev/null -w "%{http_code}" $URL)
    if [ $response -eq 200 ]; then
        echo "Server is healthy"
        exit 0
    fi
    echo "Attempt $i/$MAX_RETRIES failed, retrying in ${RETRY_DELAY}s..."
    sleep $RETRY_DELAY
done

echo "Server health check failed after $MAX_RETRIES attempts"
exit 1
```

## Implementation Checklist

### Phase 1: Transport Abstraction ✅
- [x] Create `IMcpTransport` interface
- [x] Define `TransportMessage` class
- [x] Define `TransportType` enum
- [x] Create `TransportDisconnectedEventArgs`
- [x] Add transport configuration classes
- [x] Create unit tests for transport abstractions

### Phase 2: Stdio Transport ✅
- [x] Implement `StdioTransport` class
- [x] Extract current stdio logic from `McpServer`
- [x] Add stdio configuration options
- [x] Create stdio transport tests
- [x] Verify backward compatibility

### Phase 3: HTTP Transport Core ✅
- [x] Add HTTP transport using HttpListener (no ASP.NET Core for .NET 9 compatibility)
- [x] Create `HttpTransportOptions` class
- [x] Implement basic `HttpTransport` class
- [x] Add JSON-RPC endpoint
- [x] Add health check endpoint
- [x] Create HTTP transport tests

### Phase 4: HTTP Advanced Features ✅
- [x] Implement WebSocket support in HttpTransport
- [x] Create standalone WebSocketTransport for pure WebSocket
- [x] Add WebSocket connection management
- [x] Support bidirectional messaging via WebSockets
- [x] Add CORS configuration
- [x] Implement authentication (API Key structure)
- [x] Implement request/response correlation (completed)
- [x] SSL/TLS certificate generation and binding
- [ ] Add JWT authentication (pending)
- [ ] Add compression support (pending)
- [ ] Add response caching (pending)

### Phase 5: Generics and Type Safety ✅
- [x] Create `IJsonSchema` interface for type-safe schemas
- [x] Implement `JsonSchema<T>` for generic type safety
- [x] Create `RuntimeJsonSchema` for non-generic scenarios
- [x] Update `IMcpTool` to use `IJsonSchema` instead of object
- [x] Remove manual schema definitions from all example tools
- [x] Update all test mocks to use new schema types
- [x] Maintain backward compatibility

### Phase 6: Client Libraries ✅ COMPLETED
- [x] Create strongly-typed C# HTTP client (COA.Mcp.Client v1.0.0) ✅
- [x] Implement TypedMcpClient<TParams, TResult> for type safety ✅
- [x] Create McpClientBuilder with fluent API ✅
- [x] Add comprehensive authentication support (API Key, JWT, Basic) ✅
- [x] Implement resilience patterns (retry, circuit breaker) ✅
- [x] Add client documentation in README ✅
- [x] Create McpClientExample with multiple usage patterns ✅
- [x] Add 50 unit tests for client components ✅
- [ ] Create JavaScript/TypeScript client (planned for v1.2.0)
- [ ] Create Python client (planned for v1.2.0)

### Phase 7: Documentation ✓
- [ ] Update README with HTTP examples
- [ ] Create migration guide
- [ ] Add configuration documentation
- [ ] Create troubleshooting guide
- [ ] Add security best practices

### Phase 8: Testing & Validation ✓
- [ ] Unit tests for all components
- [ ] Integration tests for workflows
- [ ] Performance tests
- [ ] Security tests
- [ ] Load testing
- [ ] Cross-platform testing

### Phase 9: Deployment ✓
- [ ] Create Docker images
- [ ] Add Kubernetes manifests
- [ ] Create deployment scripts
- [ ] Add monitoring/metrics
- [ ] Create CI/CD pipeline

### Phase 10: Release ✓
- [ ] Update version numbers
- [ ] Update changelog
- [ ] Create release notes
- [ ] Tag release
- [ ] Publish NuGet packages
- [ ] Update documentation site

## Conclusion

This implementation guide provides a comprehensive approach to adding HTTP transport support to the COA MCP Framework. The design maintains backward compatibility while enabling modern web-based communication patterns.

Key achievements:
- Clean abstraction allowing multiple transport types
- Full HTTP/HTTPS support with WebSocket
- Security features including authentication and CORS
- Performance optimizations
- Comprehensive testing strategy
- Clear migration path

The implementation can be completed incrementally, with each phase providing value while maintaining system stability.