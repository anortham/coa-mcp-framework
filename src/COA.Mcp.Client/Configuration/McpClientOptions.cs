using System;

namespace COA.Mcp.Client.Configuration
{
    /// <summary>
    /// Configuration options for MCP HTTP/WebSocket client.
    /// </summary>
    public class McpClientOptions
    {
        /// <summary>
        /// Base URL for the MCP server (e.g., "http://localhost:5000").
        /// </summary>
        public string BaseUrl { get; set; } = "http://localhost:5000";

        /// <summary>
        /// Use WebSocket connection instead of HTTP.
        /// </summary>
        public bool UseWebSocket { get; set; }

        /// <summary>
        /// WebSocket endpoint path (default: /mcp/ws).
        /// </summary>
        public string WebSocketPath { get; set; } = "/mcp/ws";

        /// <summary>
        /// JSON-RPC endpoint path (default: /mcp/rpc).
        /// </summary>
        public string JsonRpcPath { get; set; } = "/mcp/rpc";

        /// <summary>
        /// Request timeout in seconds (default: 30).
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Enable automatic retries on failure (default: true).
        /// </summary>
        public bool EnableRetries { get; set; } = true;

        /// <summary>
        /// Maximum number of retry attempts (default: 3).
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Delay between retries in milliseconds (default: 1000).
        /// </summary>
        public int RetryDelayMs { get; set; } = 1000;

        /// <summary>
        /// Enable circuit breaker pattern (default: true).
        /// </summary>
        public bool EnableCircuitBreaker { get; set; } = true;

        /// <summary>
        /// Number of failures before circuit opens (default: 5).
        /// </summary>
        public int CircuitBreakerFailureThreshold { get; set; } = 5;

        /// <summary>
        /// Duration circuit stays open in seconds (default: 30).
        /// </summary>
        public int CircuitBreakerDurationSeconds { get; set; } = 30;

        /// <summary>
        /// Authentication options.
        /// </summary>
        public AuthenticationOptions? Authentication { get; set; }

        /// <summary>
        /// Client information for identification.
        /// </summary>
        public ClientInfo ClientInfo { get; set; } = new ClientInfo();

        /// <summary>
        /// Enable request/response logging (default: false).
        /// </summary>
        public bool EnableRequestLogging { get; set; }

        /// <summary>
        /// Enable performance metrics collection (default: false).
        /// </summary>
        public bool EnableMetrics { get; set; }

        /// <summary>
        /// Custom headers to include in all requests.
        /// </summary>
        public Dictionary<string, string> CustomHeaders { get; set; } = new();
    }

    /// <summary>
    /// Authentication configuration for MCP client.
    /// </summary>
    public class AuthenticationOptions
    {
        /// <summary>
        /// Authentication type.
        /// </summary>
        public AuthenticationType Type { get; set; } = AuthenticationType.None;

        /// <summary>
        /// API key for ApiKey authentication.
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// API key header name (default: X-API-Key).
        /// </summary>
        public string ApiKeyHeader { get; set; } = "X-API-Key";

        /// <summary>
        /// JWT token for JWT authentication.
        /// </summary>
        public string? JwtToken { get; set; }

        /// <summary>
        /// Function to refresh JWT token when expired.
        /// </summary>
        public Func<Task<string>>? JwtTokenRefreshFunc { get; set; }

        /// <summary>
        /// Username for Basic authentication.
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Password for Basic authentication.
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Custom authentication handler.
        /// </summary>
        public Func<HttpRequestMessage, Task>? CustomAuthHandler { get; set; }
    }

    /// <summary>
    /// Authentication types supported by the client.
    /// </summary>
    public enum AuthenticationType
    {
        None,
        ApiKey,
        Jwt,
        Basic,
        Custom
    }

    /// <summary>
    /// Client identification information.
    /// </summary>
    public class ClientInfo
    {
        /// <summary>
        /// Client name.
        /// </summary>
        public string Name { get; set; } = "COA MCP Client";

        /// <summary>
        /// Client version.
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// Additional client metadata.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}