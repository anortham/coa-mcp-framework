namespace COA.Mcp.Framework.Transport.Configuration
{
    /// <summary>
    /// Configuration options for HTTP transport.
    /// </summary>
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
    
    /// <summary>
    /// Authentication types supported by HTTP transport.
    /// </summary>
    public enum AuthenticationType
    {
        /// <summary>
        /// No authentication required.
        /// </summary>
        None,
        
        /// <summary>
        /// API key authentication.
        /// </summary>
        ApiKey,
        
        /// <summary>
        /// JWT bearer token authentication.
        /// </summary>
        Jwt,
        
        /// <summary>
        /// Basic HTTP authentication.
        /// </summary>
        Basic,
        
        /// <summary>
        /// Custom authentication handler.
        /// </summary>
        Custom
    }
    
    /// <summary>
    /// JWT authentication settings.
    /// </summary>
    public class JwtSettings
    {
        /// <summary>
        /// Secret key for JWT signing.
        /// </summary>
        public string? SecretKey { get; set; }
        
        /// <summary>
        /// JWT issuer.
        /// </summary>
        public string? Issuer { get; set; }
        
        /// <summary>
        /// JWT audience.
        /// </summary>
        public string? Audience { get; set; }
        
        /// <summary>
        /// Token expiration in minutes (default: 60).
        /// </summary>
        public int ExpirationMinutes { get; set; } = 60;
    }
}