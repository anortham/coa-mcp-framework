using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Client.Configuration;
using COA.Mcp.Client.Interfaces;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework.Serialization;
using COA.Mcp.Protocol;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Client
{
    /// <summary>
    /// Strongly-typed MCP client for type-safe tool interactions.
    /// </summary>
    /// <typeparam name="TParams">Type of the tool parameters.</typeparam>
    /// <typeparam name="TResult">Type of the tool result.</typeparam>
    public class TypedMcpClient<TParams, TResult> : ITypedMcpClient<TParams, TResult>
        where TParams : class
        where TResult : ToolResultBase, new()
    {
        private readonly IMcpClient _baseClient;
        private readonly ILogger<TypedMcpClient<TParams, TResult>>? _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public bool IsConnected => _baseClient.IsConnected;
        public InitializeResult? ServerInfo => _baseClient.ServerInfo;

        public event EventHandler<ConnectedEventArgs>? Connected
        {
            add => _baseClient.Connected += value;
            remove => _baseClient.Connected -= value;
        }

        public event EventHandler<DisconnectedEventArgs>? Disconnected
        {
            add => _baseClient.Disconnected += value;
            remove => _baseClient.Disconnected -= value;
        }

        public event EventHandler<NotificationEventArgs>? NotificationReceived
        {
            add => _baseClient.NotificationReceived += value;
            remove => _baseClient.NotificationReceived -= value;
        }

        public TypedMcpClient(McpClientOptions options, HttpClient? httpClient = null, ILogger<TypedMcpClient<TParams, TResult>>? logger = null)
        {
            _baseClient = new McpHttpClient(options, httpClient, logger as ILogger<McpHttpClient>);
            _logger = logger;
            
            _jsonOptions = JsonDefaults.Standard;
        }

        public TypedMcpClient(IMcpClient baseClient, ILogger<TypedMcpClient<TParams, TResult>>? logger = null)
        {
            _baseClient = baseClient ?? throw new ArgumentNullException(nameof(baseClient));
            _logger = logger;
            
            _jsonOptions = JsonDefaults.Standard;
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default)
            => _baseClient.ConnectAsync(cancellationToken);

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
            => _baseClient.DisconnectAsync(cancellationToken);

        public Task<InitializeResult> InitializeAsync(CancellationToken cancellationToken = default)
            => _baseClient.InitializeAsync(cancellationToken);

        public Task<ListToolsResult> ListToolsAsync(CancellationToken cancellationToken = default)
            => _baseClient.ListToolsAsync(cancellationToken);

        public Task<CallToolResult> CallToolAsync(string toolName, object? parameters = null, CancellationToken cancellationToken = default)
            => _baseClient.CallToolAsync(toolName, parameters, cancellationToken);

        public Task<ListResourcesResult> ListResourcesAsync(CancellationToken cancellationToken = default)
            => _baseClient.ListResourcesAsync(cancellationToken);

        public Task<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken cancellationToken = default)
            => _baseClient.ReadResourceAsync(uri, cancellationToken);

        public Task<ListPromptsResult> ListPromptsAsync(CancellationToken cancellationToken = default)
            => _baseClient.ListPromptsAsync(cancellationToken);

        public Task<GetPromptResult> GetPromptAsync(string name, Dictionary<string, string>? arguments = null, CancellationToken cancellationToken = default)
            => _baseClient.GetPromptAsync(name, arguments, cancellationToken);

        public Task<TResponse> SendRequestAsync<TResponse>(string method, object? parameters = null, CancellationToken cancellationToken = default)
            where TResponse : class
            => _baseClient.SendRequestAsync<TResponse>(method, parameters, cancellationToken);

        /// <summary>
        /// Calls a tool with strongly-typed parameters and result.
        /// </summary>
        public async Task<TResult> CallToolAsync(string toolName, TParams parameters, CancellationToken cancellationToken = default)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            _logger?.LogDebug("Calling tool {ToolName} with typed parameters {ParamType}", toolName, typeof(TParams).Name);

            try
            {
                // Call the tool with typed parameters
                var response = await _baseClient.CallToolAsync(toolName, parameters, cancellationToken);

                // Handle different response content types
                if (response.Content == null || response.Content.Count == 0)
                {
                    var emptyResult = new TResult
                    {
                        Success = false,
                        Error = new ErrorInfo
                        {
                            Code = "EMPTY_RESPONSE",
                            Message = "Tool returned empty response"
                        }
                    };
                    return emptyResult;
                }

                // Get the first content item
                var content = response.Content.First();

                // Try to deserialize the content into our result type
                TResult? result = null;

                // ToolContent has a Text property that contains the actual content
                if (content.Type == "text" && !string.IsNullOrEmpty(content.Text))
                {
                    result = JsonSerializer.Deserialize<TResult>(content.Text, _jsonOptions);
                }
                else
                {
                    // Try to serialize the entire content object
                    var json = JsonSerializer.Serialize(content, _jsonOptions);
                    result = JsonSerializer.Deserialize<TResult>(json, _jsonOptions);
                }

                if (result == null)
                {
                    result = new TResult
                    {
                        Success = false,
                        Error = new ErrorInfo
                        {
                            Code = "DESERIALIZATION_ERROR",
                            Message = "Failed to deserialize tool response to expected type"
                        }
                    };
                }

                // Check if the response indicates an error
                if (response.IsError == true && result.Success)
                {
                    result.Success = false;
                    if (result.Error == null)
                    {
                        result.Error = new ErrorInfo
                        {
                            Code = "TOOL_ERROR",
                            Message = "Tool execution failed"
                        };
                    }
                }

                _logger?.LogDebug("Tool {ToolName} returned {ResultType} with success={Success}", 
                    toolName, typeof(TResult).Name, result.Success);

                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error calling tool {ToolName} with typed parameters", toolName);

                return new TResult
                {
                    Success = false,
                    Error = new ErrorInfo
                    {
                        Code = "CLIENT_ERROR",
                        Message = ex.Message,
                        // Details = ex.ToString() // ErrorInfo doesn't have Details property
                    }
                };
            }
        }

        /// <summary>
        /// Calls a tool with automatic retry on failure.
        /// </summary>
        public async Task<TResult> CallToolWithRetryAsync(string toolName, TParams parameters, CancellationToken cancellationToken = default)
        {
            const int maxAttempts = 3;
            const int delayMs = 1000;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var result = await CallToolAsync(toolName, parameters, cancellationToken);
                    
                    if (result.Success || attempt == maxAttempts)
                    {
                        return result;
                    }

                    _logger?.LogWarning("Tool {ToolName} failed on attempt {Attempt}, retrying...", toolName, attempt);
                    await Task.Delay(delayMs * attempt, cancellationToken);
                }
                catch (Exception ex) when (attempt < maxAttempts)
                {
                    _logger?.LogWarning(ex, "Tool {ToolName} threw exception on attempt {Attempt}, retrying...", toolName, attempt);
                    await Task.Delay(delayMs * attempt, cancellationToken);
                }
            }

            return new TResult
            {
                Success = false,
                Error = new ErrorInfo
                {
                    Code = "MAX_RETRIES_EXCEEDED",
                    Message = $"Failed after {maxAttempts} attempts"
                }
            };
        }

        /// <summary>
        /// Calls multiple tools in parallel.
        /// </summary>
        public async Task<Dictionary<string, TResult>> CallToolsBatchAsync(
            Dictionary<string, TParams> toolCalls,
            CancellationToken cancellationToken = default)
        {
            if (toolCalls == null || toolCalls.Count == 0)
                throw new ArgumentException("Tool calls dictionary cannot be null or empty", nameof(toolCalls));

            _logger?.LogDebug("Executing batch of {Count} tool calls", toolCalls.Count);

            var tasks = toolCalls.Select(async kvp =>
            {
                var result = await CallToolAsync(kvp.Key, kvp.Value, cancellationToken);
                return new KeyValuePair<string, TResult>(kvp.Key, result);
            });

            var results = await Task.WhenAll(tasks);
            return results.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public void Dispose()
        {
            _baseClient?.Dispose();
        }
    }

    /// <summary>
    /// Factory for creating typed MCP clients.
    /// </summary>
    public class TypedMcpClientFactory : ITypedMcpClientFactory
    {
        private readonly McpClientOptions _defaultOptions;
        private readonly HttpClient? _httpClient;
        private readonly ILoggerFactory? _loggerFactory;

        public TypedMcpClientFactory(McpClientOptions defaultOptions, HttpClient? httpClient = null, ILoggerFactory? loggerFactory = null)
        {
            _defaultOptions = defaultOptions ?? throw new ArgumentNullException(nameof(defaultOptions));
            _httpClient = httpClient;
            _loggerFactory = loggerFactory;
        }

        public ITypedMcpClient<TParams, TResult> CreateTypedClient<TParams, TResult>()
            where TParams : class
            where TResult : ToolResultBase, new()
        {
            var logger = _loggerFactory?.CreateLogger<TypedMcpClient<TParams, TResult>>();
            return new TypedMcpClient<TParams, TResult>(_defaultOptions, _httpClient, logger);
        }

        public ITypedMcpClient<TParams, TResult> CreateTypedClient<TParams, TResult>(Action<McpClientOptions> configure)
            where TParams : class
            where TResult : ToolResultBase, new()
        {
            var options = new McpClientOptions();
            
            // Copy default options
            options.BaseUrl = _defaultOptions.BaseUrl;
            options.UseWebSocket = _defaultOptions.UseWebSocket;
            options.TimeoutSeconds = _defaultOptions.TimeoutSeconds;
            options.EnableRetries = _defaultOptions.EnableRetries;
            options.MaxRetryAttempts = _defaultOptions.MaxRetryAttempts;
            options.Authentication = _defaultOptions.Authentication;
            
            // Apply custom configuration
            configure(options);

            var logger = _loggerFactory?.CreateLogger<TypedMcpClient<TParams, TResult>>();
            return new TypedMcpClient<TParams, TResult>(options, _httpClient, logger);
        }
    }
}