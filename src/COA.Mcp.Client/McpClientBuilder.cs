using System;
using System.Net.Http;
using System.Text.Json;
using COA.Mcp.Client.Configuration;
using COA.Mcp.Client.Interfaces;
using COA.Mcp.Framework.Models;
using COA.Mcp.Protocol;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Client
{
    /// <summary>
    /// Fluent builder for creating and configuring MCP clients.
    /// </summary>
    public class McpClientBuilder
    {
        private readonly McpClientOptions _options = new();
        private HttpClient? _httpClient;
        private ILoggerFactory? _loggerFactory;

        /// <summary>
        /// Creates a new MCP client builder.
        /// </summary>
        public static McpClientBuilder Create() => new();

        /// <summary>
        /// Creates a new MCP client builder with a base URL.
        /// </summary>
        public static McpClientBuilder Create(string baseUrl)
        {
            var builder = new McpClientBuilder();
            builder._options.BaseUrl = baseUrl;
            return builder;
        }

        /// <summary>
        /// Sets the base URL for the MCP server.
        /// </summary>
        public McpClientBuilder WithBaseUrl(string baseUrl)
        {
            _options.BaseUrl = baseUrl;
            return this;
        }

        /// <summary>
        /// Configures the client to use WebSocket instead of HTTP.
        /// </summary>
        public McpClientBuilder UseWebSocket(string? path = null)
        {
            _options.UseWebSocket = true;
            if (!string.IsNullOrEmpty(path))
            {
                _options.WebSocketPath = path;
            }
            return this;
        }

        /// <summary>
        /// Sets the request timeout.
        /// </summary>
        public McpClientBuilder WithTimeout(TimeSpan timeout)
        {
            _options.TimeoutSeconds = (int)timeout.TotalSeconds;
            return this;
        }

        /// <summary>
        /// Configures retry policy.
        /// </summary>
        public McpClientBuilder WithRetry(int maxAttempts = 3, int delayMs = 1000)
        {
            _options.EnableRetries = true;
            _options.MaxRetryAttempts = maxAttempts;
            _options.RetryDelayMs = delayMs;
            return this;
        }

        /// <summary>
        /// Disables automatic retries.
        /// </summary>
        public McpClientBuilder WithoutRetry()
        {
            _options.EnableRetries = false;
            return this;
        }

        /// <summary>
        /// Configures circuit breaker.
        /// </summary>
        public McpClientBuilder WithCircuitBreaker(int failureThreshold = 5, int durationSeconds = 30)
        {
            _options.EnableCircuitBreaker = true;
            _options.CircuitBreakerFailureThreshold = failureThreshold;
            _options.CircuitBreakerDurationSeconds = durationSeconds;
            return this;
        }

        /// <summary>
        /// Disables circuit breaker.
        /// </summary>
        public McpClientBuilder WithoutCircuitBreaker()
        {
            _options.EnableCircuitBreaker = false;
            return this;
        }

        /// <summary>
        /// Configures API key authentication.
        /// </summary>
        public McpClientBuilder WithApiKey(string apiKey, string? headerName = null)
        {
            _options.Authentication = new AuthenticationOptions
            {
                Type = AuthenticationType.ApiKey,
                ApiKey = apiKey,
                ApiKeyHeader = headerName ?? "X-API-Key"
            };
            return this;
        }

        /// <summary>
        /// Configures JWT authentication.
        /// </summary>
        public McpClientBuilder WithJwtToken(string token, Func<Task<string>>? refreshFunc = null)
        {
            _options.Authentication = new AuthenticationOptions
            {
                Type = AuthenticationType.Jwt,
                JwtToken = token,
                JwtTokenRefreshFunc = refreshFunc
            };
            return this;
        }

        /// <summary>
        /// Configures basic authentication.
        /// </summary>
        public McpClientBuilder WithBasicAuth(string username, string password)
        {
            _options.Authentication = new AuthenticationOptions
            {
                Type = AuthenticationType.Basic,
                Username = username,
                Password = password
            };
            return this;
        }

        /// <summary>
        /// Configures custom authentication.
        /// </summary>
        public McpClientBuilder WithCustomAuth(Func<HttpRequestMessage, Task> authHandler)
        {
            _options.Authentication = new AuthenticationOptions
            {
                Type = AuthenticationType.Custom,
                CustomAuthHandler = authHandler
            };
            return this;
        }

        /// <summary>
        /// Sets client information.
        /// </summary>
        public McpClientBuilder WithClientInfo(string name, string version, Dictionary<string, object>? metadata = null)
        {
            _options.ClientInfo = new ClientInfo
            {
                Name = name,
                Version = version,
                Metadata = metadata ?? new Dictionary<string, object>()
            };
            return this;
        }

        /// <summary>
        /// Adds a custom header to all requests.
        /// </summary>
        public McpClientBuilder WithHeader(string name, string value)
        {
            _options.CustomHeaders[name] = value;
            return this;
        }

        /// <summary>
        /// Enables request logging.
        /// </summary>
        public McpClientBuilder WithRequestLogging(bool enable = true)
        {
            _options.EnableRequestLogging = enable;
            return this;
        }

        /// <summary>
        /// Enables metrics collection.
        /// </summary>
        public McpClientBuilder WithMetrics(bool enable = true)
        {
            _options.EnableMetrics = enable;
            return this;
        }

        /// <summary>
        /// Uses a custom HttpClient instance.
        /// </summary>
        public McpClientBuilder UseHttpClient(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            return this;
        }

        /// <summary>
        /// Uses a logger factory for logging.
        /// </summary>
        public McpClientBuilder UseLoggerFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            return this;
        }

        /// <summary>
        /// Configures options using an action.
        /// </summary>
        public McpClientBuilder Configure(Action<McpClientOptions> configure)
        {
            configure?.Invoke(_options);
            return this;
        }

        /// <summary>
        /// Builds a standard MCP client.
        /// </summary>
        public IMcpClient Build()
        {
            var logger = _loggerFactory?.CreateLogger<McpHttpClient>();
            return new McpHttpClient(_options, _httpClient, logger);
        }

        /// <summary>
        /// Builds a typed MCP client.
        /// </summary>
        public ITypedMcpClient<TParams, TResult> BuildTyped<TParams, TResult>()
            where TParams : class
            where TResult : ToolResultBase, new()
        {
            var logger = _loggerFactory?.CreateLogger<TypedMcpClient<TParams, TResult>>();
            return new TypedMcpClient<TParams, TResult>(_options, _httpClient, logger);
        }

        /// <summary>
        /// Builds and connects the client.
        /// </summary>
        public async Task<IMcpClient> BuildAndConnectAsync(CancellationToken cancellationToken = default)
        {
            var client = Build();
            await client.ConnectAsync(cancellationToken);
            return client;
        }

        /// <summary>
        /// Builds, connects, and initializes the client.
        /// </summary>
        public async Task<IMcpClient> BuildAndInitializeAsync(CancellationToken cancellationToken = default)
        {
            var client = Build();
            await client.ConnectAsync(cancellationToken);
            await client.InitializeAsync(cancellationToken);
            return client;
        }

        /// <summary>
        /// Builds, connects, and initializes a typed client.
        /// </summary>
        public async Task<ITypedMcpClient<TParams, TResult>> BuildTypedAndInitializeAsync<TParams, TResult>(CancellationToken cancellationToken = default)
            where TParams : class
            where TResult : ToolResultBase, new()
        {
            var client = BuildTyped<TParams, TResult>();
            await client.ConnectAsync(cancellationToken);
            await client.InitializeAsync(cancellationToken);
            return client;
        }
    }

    /// <summary>
    /// Extension methods for fluent tool invocation.
    /// </summary>
    public static class McpClientExtensions
    {
        /// <summary>
        /// Creates a fluent tool invocation builder.
        /// </summary>
        public static ToolInvocationBuilder CallTool(this IMcpClient client, string toolName)
        {
            return new ToolInvocationBuilder(client, toolName);
        }

        /// <summary>
        /// Creates a typed fluent tool invocation builder.
        /// </summary>
        public static TypedToolInvocationBuilder<TParams, TResult> CallTool<TParams, TResult>(
            this ITypedMcpClient<TParams, TResult> client,
            string toolName)
            where TParams : class
            where TResult : ToolResultBase, new()
        {
            return new TypedToolInvocationBuilder<TParams, TResult>(client, toolName);
        }
    }

    /// <summary>
    /// Fluent builder for tool invocations.
    /// </summary>
    public class ToolInvocationBuilder
    {
        private readonly IMcpClient _client;
        private readonly string _toolName;
        private object? _parameters;
        private CancellationToken _cancellationToken = default;

        internal ToolInvocationBuilder(IMcpClient client, string toolName)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _toolName = toolName ?? throw new ArgumentNullException(nameof(toolName));
        }

        /// <summary>
        /// Sets the tool parameters.
        /// </summary>
        public ToolInvocationBuilder WithParameters(object parameters)
        {
            _parameters = parameters;
            return this;
        }

        /// <summary>
        /// Sets the cancellation token.
        /// </summary>
        public ToolInvocationBuilder WithCancellation(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            return this;
        }

        /// <summary>
        /// Executes the tool call.
        /// </summary>
        public Task<CallToolResult> ExecuteAsync()
        {
            return _client.CallToolAsync(_toolName, _parameters, _cancellationToken);
        }

        /// <summary>
        /// Executes the tool call and returns a typed result.
        /// </summary>
        public async Task<T?> ExecuteAsync<T>() where T : class
        {
            var response = await ExecuteAsync();
            
            if (response.Content == null || response.Content.Count == 0)
                return null;

            var content = response.Content.First();
            
            // ToolContent has a Text property that contains the actual content
            if (content.Type == "text" && !string.IsNullOrEmpty(content.Text))
            {
                return JsonSerializer.Deserialize<T>(content.Text);
            }

            return default(T);
        }
    }

    /// <summary>
    /// Fluent builder for typed tool invocations.
    /// </summary>
    public class TypedToolInvocationBuilder<TParams, TResult>
        where TParams : class
        where TResult : ToolResultBase, new()
    {
        private readonly ITypedMcpClient<TParams, TResult> _client;
        private readonly string _toolName;
        private TParams? _parameters;
        private CancellationToken _cancellationToken = default;
        private bool _useRetry = false;

        internal TypedToolInvocationBuilder(ITypedMcpClient<TParams, TResult> client, string toolName)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _toolName = toolName ?? throw new ArgumentNullException(nameof(toolName));
        }

        /// <summary>
        /// Sets the tool parameters.
        /// </summary>
        public TypedToolInvocationBuilder<TParams, TResult> WithParameters(TParams parameters)
        {
            _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            return this;
        }

        /// <summary>
        /// Enables automatic retry on failure.
        /// </summary>
        public TypedToolInvocationBuilder<TParams, TResult> WithRetry()
        {
            _useRetry = true;
            return this;
        }

        /// <summary>
        /// Sets the cancellation token.
        /// </summary>
        public TypedToolInvocationBuilder<TParams, TResult> WithCancellation(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            return this;
        }

        /// <summary>
        /// Executes the tool call.
        /// </summary>
        public Task<TResult> ExecuteAsync()
        {
            if (_parameters == null)
                throw new InvalidOperationException("Parameters must be set before executing");

            if (_useRetry)
            {
                return _client.CallToolWithRetryAsync(_toolName, _parameters, _cancellationToken);
            }
            else
            {
                return _client.CallToolAsync(_toolName, _parameters, _cancellationToken);
            }
        }
    }
}