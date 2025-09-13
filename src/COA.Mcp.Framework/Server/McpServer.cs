using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework.Prompts;
using COA.Mcp.Framework.Registration;
using COA.Mcp.Framework.Resources;
using COA.Mcp.Framework.Transport;
using COA.Mcp.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.Server;

/// <summary>
/// Complete MCP server implementation that handles all protocol communication and tool management.
/// This is the main entry point for creating and running an MCP server.
/// </summary>
public class McpServer : IHostedService, IDisposable
{
    private readonly McpToolRegistry _toolRegistry;
    private readonly ResourceRegistry _resourceRegistry;
    private readonly IPromptRegistry _promptRegistry;
    private readonly ILogger<McpServer>? _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ServerCapabilities _capabilities;
    private readonly Implementation _serverInfo;
    private readonly IMcpTransport _transport;
    private readonly string? _instructions;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the McpServer class.
    /// </summary>
    /// <param name="transport">The transport for communication.</param>
    /// <param name="toolRegistry">The tool registry for managing tools.</param>
    /// <param name="resourceRegistry">The resource registry for managing resources.</param>
    /// <param name="promptRegistry">The prompt registry for managing prompts.</param>
    /// <param name="serverInfo">Information about this server implementation.</param>
    /// <param name="instructions">Optional instructions for behavioral guidance.</param>
    /// <param name="logger">Optional logger.</param>
    public McpServer(
        IMcpTransport transport,
        McpToolRegistry toolRegistry,
        ResourceRegistry resourceRegistry,
        IPromptRegistry promptRegistry,
        Implementation serverInfo,
        string? instructions = null,
        ILogger<McpServer>? logger = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _resourceRegistry = resourceRegistry ?? throw new ArgumentNullException(nameof(resourceRegistry));
        _promptRegistry = promptRegistry ?? throw new ArgumentNullException(nameof(promptRegistry));
        _serverInfo = serverInfo ?? throw new ArgumentNullException(nameof(serverInfo));
        _instructions = instructions;
        _logger = logger;
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        _capabilities = new ServerCapabilities
        {
            Tools = new ToolsCapabilityMarker(), // Type-safe marker for tool support
            Resources = new ResourceCapabilities
            {
                Subscribe = false,
                ListChanged = false
            },
            Prompts = new PromptsCapabilityMarker(), // Type-safe marker for prompt support
            Sampling = new SamplingCapabilityMarker(), // Type-safe marker for sampling support
            Completion = new CompletionCapabilityMarker(), // Type-safe marker for completion support
            Logging = new LoggingCapabilities
            {
                Levels = new List<LoggingLevel>
                {
                    LoggingLevel.Debug,
                    LoggingLevel.Info,
                    LoggingLevel.Notice,
                    LoggingLevel.Warning,
                    LoggingLevel.Error,
                    LoggingLevel.Critical,
                    LoggingLevel.Alert,
                    LoggingLevel.Emergency
                },
                Structured = true
            }
        };
    }

    /// <summary>
    /// Starts the MCP server.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Starting MCP server '{ServerName}' v{Version} with {Transport} transport", 
            _serverInfo.Name, _serverInfo.Version, _transport.Type);
        
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        // Start the transport
        await _transport.StartAsync(_cancellationTokenSource.Token).ConfigureAwait(false);
        
        // Subscribe to transport disconnection
        _transport.Disconnected += OnTransportDisconnected;
        
        // Start processing messages
        _ = Task.Run(() => ProcessMessagesAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
    }

    /// <summary>
    /// Stops the MCP server.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Stopping MCP server");
        
        _cancellationTokenSource?.Cancel();
        
        // Unsubscribe from transport events
        _transport.Disconnected -= OnTransportDisconnected;
        
        // Stop the transport
        await _transport.StopAsync(cancellationToken).ConfigureAwait(false);
    }
    
    private void OnTransportDisconnected(object? sender, TransportDisconnectedEventArgs e)
    {
        _logger?.LogInformation("Transport disconnected: {Reason}", e.Reason);
        _cancellationTokenSource?.Cancel();
    }

    /// <summary>
    /// Main message processing loop.
    /// </summary>
    private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _transport.IsConnected)
        {
            try
            {
                var transportMessage = await _transport.ReadMessageAsync(cancellationToken).ConfigureAwait(false);
                if (transportMessage == null)
                {
                    continue;
                }

                await ProcessMessageAsync(transportMessage.Content, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing message");
            }
        }
    }

    /// <summary>
    /// Processes a single JSON-RPC message.
    /// </summary>
    private async Task ProcessMessageAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            var jsonDocument = JsonDocument.Parse(message);
            var root = jsonDocument.RootElement;

            if (!root.TryGetProperty("jsonrpc", out var jsonrpcProp) || 
                jsonrpcProp.GetString() != "2.0")
            {
                await SendErrorResponseAsync(null, -32600, "Invalid Request").ConfigureAwait(false);
                return;
            }

            // Check if it's a request (has id) or notification (no id)
            var hasId = root.TryGetProperty("id", out var idElement);
            
            if (!root.TryGetProperty("method", out var methodElement))
            {
                if (hasId)
                {
                    await SendErrorResponseAsync(idElement, -32600, "Invalid Request").ConfigureAwait(false);
                }
                return;
            }

            var method = methodElement.GetString();
            var parameters = root.TryGetProperty("params", out var paramsElement) ? paramsElement : (JsonElement?)null;

            _logger?.LogDebug("Processing {MessageType}: {Method}", 
                hasId ? "request" : "notification", method);

            // Handle the method
            object? result = null;
            var isError = false;
            string? errorMessage = null;
            int errorCode = 0;

            switch (method)
            {
                case "initialize":
                    result = await HandleInitializeAsync(parameters, cancellationToken).ConfigureAwait(false);
                    break;
                    
                case "tools/list":
                    result = await HandleListToolsAsync(cancellationToken).ConfigureAwait(false);
                    break;
                    
                case "tools/call":
                    result = await HandleCallToolAsync(parameters, cancellationToken).ConfigureAwait(false);
                    break;
                    
                case "resources/list":
                    result = await HandleListResourcesAsync(cancellationToken).ConfigureAwait(false);
                    break;
                    
                case "resources/read":
                    result = await HandleReadResourceAsync(parameters, cancellationToken).ConfigureAwait(false);
                    break;
                    
                case "prompts/list":
                    result = await HandleListPromptsAsync(cancellationToken).ConfigureAwait(false);
                    break;
                    
                case "prompts/get":
                    result = await HandleGetPromptAsync(parameters, cancellationToken).ConfigureAwait(false);
                    break;
                    
                case "sampling/createMessage":
                    result = await HandleCreateMessageAsync(parameters, cancellationToken).ConfigureAwait(false);
                    break;
                    
                case "completion/complete":
                    result = await HandleCompleteAsync(parameters, cancellationToken).ConfigureAwait(false);
                    break;
                    
                case "logging/setLevel":
                    result = await HandleSetLevelAsync(parameters, cancellationToken).ConfigureAwait(false);
                    break;
                    
                default:
                    isError = true;
                    errorCode = -32601;
                    errorMessage = $"Method not found: {method}";
                    break;
            }

            // Send response if it was a request (not a notification)
            if (hasId)
            {
                if (isError)
                {
                    await SendErrorResponseAsync(idElement, errorCode, errorMessage!).ConfigureAwait(false);
                }
                else
                {
                    await SendSuccessResponseAsync(idElement, result).ConfigureAwait(false);
                }
            }
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "Failed to parse JSON-RPC message");
            await SendErrorResponseAsync(null, -32700, "Parse error").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error processing message");
            await SendErrorResponseAsync(null, -32603, "Internal error").ConfigureAwait(false);
        }
    }

    private Task<InitializeResult> HandleInitializeAsync(JsonElement? parameters, CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Handling initialize request");
        
        return Task.FromResult(new InitializeResult
        {
            ProtocolVersion = "2024-11-05",
            Capabilities = _capabilities,
            ServerInfo = _serverInfo,
            Instructions = _instructions
        });
    }

    private Task<ListToolsResult> HandleListToolsAsync(CancellationToken cancellationToken)
    {
        _logger?.LogDebug("Handling tools/list request");
        
        var tools = _toolRegistry.GetProtocolTools();
        
        return Task.FromResult(new ListToolsResult
        {
            Tools = tools
        });
    }

    private async Task<CallToolResult> HandleCallToolAsync(JsonElement? parameters, CancellationToken cancellationToken)
    {
        if (parameters == null)
        {
            return new CallToolResult
            {
                IsError = true,
                Content = new List<ToolContent>
                {
                    new ToolContent
                    {
                        Type = "text",
                        Text = "Missing parameters for tools/call"
                    }
                }
            };
        }

        var request = JsonSerializer.Deserialize<CallToolRequest>(parameters.Value.GetRawText(), _jsonOptions);
        if (request == null || string.IsNullOrEmpty(request.Name))
        {
            return new CallToolResult
            {
                IsError = true,
                Content = new List<ToolContent>
                {
                    new ToolContent
                    {
                        Type = "text",
                        Text = "Invalid tool call request"
                    }
                }
            };
        }

        _logger?.LogDebug("Calling tool '{ToolName}'", request.Name);
        
        // Convert arguments to JsonElement if they're not already
        JsonElement? args = null;
        if (request.Arguments != null)
        {
            if (request.Arguments is JsonElement element)
            {
                args = element;
            }
            else if (request.Arguments is JsonDocument document)
            {
                args = document.RootElement.Clone();
            }
            else
            {
                // More efficient: serialize directly to JsonElement
                args = JsonSerializer.SerializeToElement(request.Arguments, _jsonOptions);
            }
        }
        
        return await _toolRegistry.CallToolAsync(request.Name, args, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ListResourcesResult> HandleListResourcesAsync(CancellationToken cancellationToken)
    {
        _logger?.LogDebug("Handling resources/list request");
        
        var resources = await _resourceRegistry.ListResourcesAsync().ConfigureAwait(false);
        
        return new ListResourcesResult
        {
            Resources = resources
        };
    }

    private async Task<ReadResourceResult> HandleReadResourceAsync(JsonElement? parameters, CancellationToken cancellationToken)
    {
        if (parameters == null)
        {
            throw new InvalidOperationException("Missing parameters for resources/read");
        }

        var request = JsonSerializer.Deserialize<ReadResourceRequest>(parameters.Value.GetRawText(), _jsonOptions);
        if (request == null || string.IsNullOrEmpty(request.Uri))
        {
            throw new InvalidOperationException("Invalid resource read request");
        }

        _logger?.LogDebug("Reading resource '{Uri}'", request.Uri);
        
        var content = await _resourceRegistry.GetResourceAsync(request.Uri).ConfigureAwait(false);
        
        return new ReadResourceResult
        {
            Contents = new List<ResourceContent> { content }
        };
    }

    private async Task<ListPromptsResult> HandleListPromptsAsync(CancellationToken cancellationToken)
    {
        _logger?.LogDebug("Handling prompts/list request");
        
        var prompts = await _promptRegistry.ListPromptsAsync(cancellationToken).ConfigureAwait(false);
        
        return new ListPromptsResult
        {
            Prompts = prompts
        };
    }

    private async Task<GetPromptResult> HandleGetPromptAsync(JsonElement? parameters, CancellationToken cancellationToken)
    {
        if (parameters == null)
        {
            throw new InvalidOperationException("Missing parameters for prompts/get");
        }

        var request = JsonSerializer.Deserialize<GetPromptRequest>(parameters.Value.GetRawText(), _jsonOptions);
        if (request == null || string.IsNullOrEmpty(request.Name))
        {
            throw new InvalidOperationException("Invalid prompt get request");
        }

        _logger?.LogDebug("Getting prompt '{Name}'", request.Name);
        
        return await _promptRegistry.GetPromptAsync(request.Name, request.Arguments, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CreateMessageResult> HandleCreateMessageAsync(JsonElement? parameters, CancellationToken cancellationToken)
    {
        if (parameters == null)
        {
            throw new InvalidOperationException("Missing parameters for sampling/createMessage");
        }

        var request = JsonSerializer.Deserialize<CreateMessageRequest>(parameters.Value.GetRawText(), _jsonOptions);
        if (request == null)
        {
            throw new InvalidOperationException("Invalid sampling createMessage request");
        }

        _logger?.LogDebug("Creating message with {MessageCount} messages", request.Messages.Count);
        
        // Default implementation: Echo back the input with a simple response
        // Override this method in derived classes for custom sampling behavior
        return new CreateMessageResult
        {
            Model = "default-model",
            StopReason = "length",
            Role = "assistant",
            Content = new List<MessageContent>
            {
                new MessageContent
                {
                    Type = "text",
                    Text = "Default sampling response. To implement custom sampling, override HandleCreateMessageAsync in your McpServer implementation."
                }
            }
        };
    }

    private async Task<CompleteResult> HandleCompleteAsync(JsonElement? parameters, CancellationToken cancellationToken)
    {
        if (parameters == null)
        {
            throw new InvalidOperationException("Missing parameters for completion/complete");
        }

        var request = JsonSerializer.Deserialize<CompleteRequest>(parameters.Value.GetRawText(), _jsonOptions);
        if (request == null)
        {
            throw new InvalidOperationException("Invalid completion complete request");
        }

        _logger?.LogDebug("Completing for reference '{Type}:{Name}', argument '{ArgName}'", 
            request.Ref.Type, request.Ref.Name, request.Argument.Name);
        
        // Default implementation: Return empty completion list
        // Override this method in derived classes for custom completion behavior
        return new CompleteResult
        {
            Completion = new CompletionData
            {
                Values = new List<string>(), // Empty list - no completions available
                Total = 0,
                HasMore = false
            }
        };
    }

    private async Task<object> HandleSetLevelAsync(JsonElement? parameters, CancellationToken cancellationToken)
    {
        if (parameters == null)
        {
            throw new InvalidOperationException("Missing parameters for logging/setLevel");
        }

        var request = JsonSerializer.Deserialize<SetLevelRequest>(parameters.Value.GetRawText(), _jsonOptions);
        if (request == null)
        {
            throw new InvalidOperationException("Invalid logging setLevel request");
        }
        
        _logger?.LogDebug("Setting logging level to {Level}", request.Level);
        
        // Default implementation: Log the request but don't change actual log level
        // Override this method in derived classes to integrate with logging configuration
        _logger?.LogInformation("Log level change requested to {Level}. To implement dynamic log level changes, override HandleSetLevelAsync in your McpServer implementation.", request.Level);
        
        // Return empty object to indicate success
        return Task.FromResult<object>(new { success = true, message = "Log level request received but not applied (default implementation)" });
    }

    private async Task SendSuccessResponseAsync(JsonElement id, object? result)
    {
        var response = new JsonRpcResponse
        {
            Id = id,
            Result = result
        };

        var json = JsonSerializer.Serialize(response, _jsonOptions);
        var transportMessage = new TransportMessage
        {
            Content = json,
            Headers = { ["type"] = "response" }
        };
        await _transport.WriteMessageAsync(transportMessage).ConfigureAwait(false);
    }

    private async Task SendErrorResponseAsync(JsonElement? id, int code, string message)
    {
        var response = new JsonRpcResponse
        {
            Id = id ?? JsonDocument.Parse("null").RootElement,
            Error = new JsonRpcError
            {
                Code = code,
                Message = message
            }
        };

        var json = JsonSerializer.Serialize(response, _jsonOptions);
        var transportMessage = new TransportMessage
        {
            Content = json,
            Headers = { ["type"] = "error" }
        };
        await _transport.WriteMessageAsync(transportMessage).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a new McpServerBuilder for configuring and building an MCP server.
    /// </summary>
    public static McpServerBuilder CreateBuilder()
    {
        return new McpServerBuilder();
    }

    /// <summary>
    /// Releases all resources used by the McpServer.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the McpServer and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Dispose managed resources
            _cancellationTokenSource?.Dispose();
            
            // Dispose transport if it implements IDisposable
            if (_transport is IDisposable disposableTransport)
            {
                disposableTransport.Dispose();
            }
        }

        _disposed = true;
    }
}