using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Client.Configuration;
using COA.Mcp.Client.Interfaces;
using COA.Mcp.Protocol;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace COA.Mcp.Client
{
    /// <summary>
    /// HTTP-based MCP client implementation.
    /// </summary>
    public class McpHttpClient : IMcpClient
    {
        private readonly HttpClient _httpClient;
        private readonly McpClientOptions _options;
        private readonly ILogger<McpHttpClient>? _logger;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
        private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonDocument>> _pendingRequests;
        private int _requestId;
        private bool _isConnected;
        private InitializeResult? _serverInfo;
        private bool _disposed;

        public bool IsConnected => _isConnected;
        public InitializeResult? ServerInfo => _serverInfo;

        public event EventHandler<ConnectedEventArgs>? Connected;
        public event EventHandler<DisconnectedEventArgs>? Disconnected;
#pragma warning disable CS0067 // The event is never used - part of public API interface
        public event EventHandler<NotificationEventArgs>? NotificationReceived;
#pragma warning restore CS0067

        public McpHttpClient(McpClientOptions options, HttpClient? httpClient = null, ILogger<McpHttpClient>? logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _httpClient = httpClient ?? new HttpClient();
            _logger = logger;
            _pendingRequests = new ConcurrentDictionary<int, TaskCompletionSource<JsonDocument>>();
            _requestId = 0;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            // Configure HTTP client
            _httpClient.BaseAddress = new Uri(_options.BaseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Add("User-Agent", $"{_options.ClientInfo.Name}/{_options.ClientInfo.Version}");

            // Add custom headers
            foreach (var header in _options.CustomHeaders)
            {
                _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
            }

            // Configure authentication
            ConfigureAuthentication();

            // Build retry policy
            _retryPolicy = BuildRetryPolicy();
        }

        private void ConfigureAuthentication()
        {
            if (_options.Authentication == null) return;

            switch (_options.Authentication.Type)
            {
                case AuthenticationType.ApiKey:
                    if (!string.IsNullOrEmpty(_options.Authentication.ApiKey))
                    {
                        _httpClient.DefaultRequestHeaders.Add(
                            _options.Authentication.ApiKeyHeader,
                            _options.Authentication.ApiKey);
                    }
                    break;

                case AuthenticationType.Jwt:
                    if (!string.IsNullOrEmpty(_options.Authentication.JwtToken))
                    {
                        _httpClient.DefaultRequestHeaders.Authorization =
                            new AuthenticationHeaderValue("Bearer", _options.Authentication.JwtToken);
                    }
                    break;

                case AuthenticationType.Basic:
                    if (!string.IsNullOrEmpty(_options.Authentication.Username) &&
                        !string.IsNullOrEmpty(_options.Authentication.Password))
                    {
                        var credentials = Convert.ToBase64String(
                            Encoding.UTF8.GetBytes($"{_options.Authentication.Username}:{_options.Authentication.Password}"));
                        _httpClient.DefaultRequestHeaders.Authorization =
                            new AuthenticationHeaderValue("Basic", credentials);
                    }
                    break;
            }
        }

        private IAsyncPolicy<HttpResponseMessage> BuildRetryPolicy()
        {
            var policyBuilder = Policy<HttpResponseMessage>
                .HandleResult(r => !r.IsSuccessStatusCode)
                .OrResult(r => r.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
                .OrResult(r => r.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable);

            IAsyncPolicy<HttpResponseMessage> retryPolicy = Policy.NoOpAsync<HttpResponseMessage>();

            if (_options.EnableRetries)
            {
                retryPolicy = policyBuilder
                    .WaitAndRetryAsync(
                        _options.MaxRetryAttempts,
                        retryAttempt => TimeSpan.FromMilliseconds(_options.RetryDelayMs * Math.Pow(2, retryAttempt - 1)),
                        onRetry: (outcome, timespan, retryCount, context) =>
                        {
                            _logger?.LogWarning(
                                "Retry {RetryCount} after {Delay}ms due to: {StatusCode}",
                                retryCount,
                                timespan.TotalMilliseconds,
                                outcome.Result?.StatusCode);
                        });
            }

            if (_options.EnableCircuitBreaker)
            {
                var circuitBreakerPolicy = policyBuilder
                    .CircuitBreakerAsync(
                        handledEventsAllowedBeforeBreaking: _options.CircuitBreakerFailureThreshold,
                        durationOfBreak: TimeSpan.FromSeconds(_options.CircuitBreakerDurationSeconds),
                        onBreak: (result, duration) =>
                        {
                            _logger?.LogError(
                                "Circuit breaker opened for {Duration}s after {Threshold} failures",
                                duration.TotalSeconds,
                                _options.CircuitBreakerFailureThreshold);
                        },
                        onReset: () =>
                        {
                            _logger?.LogInformation("Circuit breaker reset");
                        });

                retryPolicy = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
            }

            return retryPolicy;
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_isConnected)
            {
                _logger?.LogDebug("Already connected to {BaseUrl}", _options.BaseUrl);
                return;
            }

            try
            {
                _logger?.LogInformation("Connecting to MCP server at {BaseUrl}", _options.BaseUrl);

                // Test connection with health check - apply retry policy if enabled
                HttpResponseMessage healthResponse;
                if (_options.EnableRetries || _options.EnableCircuitBreaker)
                {
                    healthResponse = await _retryPolicy.ExecuteAsync(
                        async () => await _httpClient.GetAsync("/mcp/health", cancellationToken));
                }
                else
                {
                    healthResponse = await _httpClient.GetAsync("/mcp/health", cancellationToken);
                }
                
                healthResponse.EnsureSuccessStatusCode();

                _isConnected = true;
                _logger?.LogInformation("Successfully connected to MCP server");

                Connected?.Invoke(this, new ConnectedEventArgs
                {
                    ConnectedAt = DateTime.UtcNow,
                    ServerUrl = _options.BaseUrl
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to connect to MCP server at {BaseUrl}", _options.BaseUrl);
                throw new InvalidOperationException($"Failed to connect to MCP server: {ex.Message}", ex);
            }
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            if (!_isConnected)
            {
                return Task.CompletedTask;
            }

            _isConnected = false;
            _serverInfo = null;

            Disconnected?.Invoke(this, new DisconnectedEventArgs
            {
                DisconnectedAt = DateTime.UtcNow,
                Reason = "Client disconnected"
            });

            _logger?.LogInformation("Disconnected from MCP server");
            return Task.CompletedTask;
        }

        public async Task<InitializeResult> InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (!_isConnected)
            {
                await ConnectAsync(cancellationToken);
            }

            var request = new InitializeRequest
            {
                ProtocolVersion = COA.Mcp.Protocol.McpProtocol.Version,
                Capabilities = new ClientCapabilities
                {
                    // ClientCapabilities has different properties
                },
                ClientInfo = new Implementation
                {
                    Name = _options.ClientInfo.Name,
                    Version = _options.ClientInfo.Version
                }
            };

            var response = await SendRequestAsync<InitializeResult>("initialize", request, cancellationToken);
            _serverInfo = response;

            _logger?.LogInformation(
                "Initialized MCP session with server {ServerName} v{ServerVersion}",
                response.ServerInfo?.Name,
                response.ServerInfo?.Version);

            return response;
        }

        public async Task<ListToolsResult> ListToolsAsync(CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await SendRequestAsync<ListToolsResult>("tools/list", null, cancellationToken);
        }

        public async Task<CallToolResult> CallToolAsync(string toolName, object? parameters = null, CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            var request = new CallToolRequest
            {
                Name = toolName,
                Arguments = parameters ?? new { }
            };

            return await SendRequestAsync<CallToolResult>("tools/call", request, cancellationToken);
        }

        public async Task<ListResourcesResult> ListResourcesAsync(CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await SendRequestAsync<ListResourcesResult>("resources/list", null, cancellationToken);
        }

        public async Task<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            var request = new ReadResourceRequest { Uri = uri };
            return await SendRequestAsync<ReadResourceResult>("resources/read", request, cancellationToken);
        }

        public async Task<ListPromptsResult> ListPromptsAsync(CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await SendRequestAsync<ListPromptsResult>("prompts/list", null, cancellationToken);
        }

        public async Task<GetPromptResult> GetPromptAsync(string name, Dictionary<string, string>? arguments = null, CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            var request = new GetPromptRequest
            {
                Name = name,
                Arguments = arguments?.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value) ?? new Dictionary<string, object>()
            };

            return await SendRequestAsync<GetPromptResult>("prompts/get", request, cancellationToken);
        }

        public async Task<TResponse> SendRequestAsync<TResponse>(string method, object? parameters = null, CancellationToken cancellationToken = default)
            where TResponse : class
        {
            var requestId = Interlocked.Increment(ref _requestId);

            var jsonRpcRequest = new
            {
                jsonrpc = "2.0",
                method = method,
                @params = parameters,
                id = requestId
            };

            var json = JsonSerializer.Serialize(jsonRpcRequest, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            if (_options.EnableRequestLogging)
            {
                _logger?.LogDebug("Sending request {RequestId}: {Method}", requestId, method);
                _logger?.LogTrace("Request body: {Json}", json);
            }

            try
            {
                HttpResponseMessage httpResponse;

                if (_options.EnableRetries || _options.EnableCircuitBreaker)
                {
                    httpResponse = await _retryPolicy.ExecuteAsync(
                        async () => await _httpClient.PostAsync(_options.JsonRpcPath, content, cancellationToken));
                }
                else
                {
                    httpResponse = await _httpClient.PostAsync(_options.JsonRpcPath, content, cancellationToken);
                }

                httpResponse.EnsureSuccessStatusCode();

                var responseJson = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

                if (_options.EnableRequestLogging)
                {
                    _logger?.LogTrace("Response body: {Json}", responseJson);
                }

                var jsonResponse = JsonDocument.Parse(responseJson);
                var root = jsonResponse.RootElement;

                // Check for JSON-RPC error
                if (root.TryGetProperty("error", out var errorElement))
                {
                    var errorCode = errorElement.GetProperty("code").GetInt32();
                    var errorMessage = errorElement.GetProperty("message").GetString();
                    var errorData = errorElement.TryGetProperty("data", out var data) ? data.GetRawText() : null;

                    throw new JsonRpcException(errorCode, errorMessage ?? "Unknown error", errorData);
                }

                // Extract result
                if (root.TryGetProperty("result", out var resultElement))
                {
                    var resultJson = resultElement.GetRawText();
                    var result = JsonSerializer.Deserialize<TResponse>(resultJson, _jsonOptions);
                    
                    if (result == null)
                    {
                        throw new InvalidOperationException("Failed to deserialize response");
                    }

                    return result;
                }

                throw new InvalidOperationException("Response missing both 'result' and 'error' properties");
            }
            catch (HttpRequestException ex)
            {
                _logger?.LogError(ex, "HTTP request failed for method {Method}", method);
                throw new McpClientException($"HTTP request failed: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex)
            {
                _logger?.LogError(ex, "Request timeout for method {Method}", method);
                throw new McpClientException($"Request timeout after {_options.TimeoutSeconds} seconds", ex);
            }
            catch (BrokenCircuitException ex)
            {
                _logger?.LogError(ex, "Circuit breaker is open for method {Method}", method);
                throw new McpClientException("Service is temporarily unavailable (circuit breaker open)", ex);
            }
        }

        private void EnsureConnected()
        {
            if (!_isConnected)
            {
                throw new InvalidOperationException("Client is not connected. Call ConnectAsync first.");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            DisconnectAsync().GetAwaiter().GetResult();
            _httpClient?.Dispose();
            _disposed = true;
        }
    }

    /// <summary>
    /// Exception thrown by MCP client operations.
    /// </summary>
    public class McpClientException : Exception
    {
        public McpClientException(string message) : base(message) { }
        public McpClientException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown for JSON-RPC errors.
    /// </summary>
    public class JsonRpcException : McpClientException
    {
        public int Code { get; }
        public new string? Data { get; }

        public JsonRpcException(int code, string message, string? data = null)
            : base($"JSON-RPC error {code}: {message}")
        {
            Code = code;
            Data = data;
        }
    }
}
