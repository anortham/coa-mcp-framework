using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.Configuration;

/// <summary>
/// Configuration options for framework-wide behavior including logging control.
/// </summary>
public class FrameworkOptions
{
    /// <summary>
    /// Gets or sets whether framework logging is enabled.
    /// When disabled, the framework will not add any logging providers or configuration.
    /// Default is true.
    /// </summary>
    public bool EnableFrameworkLogging { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum log level for framework components.
    /// This affects middleware, transport, and tool execution logging.
    /// Default is Warning to reduce verbosity.
    /// </summary>
    public LogLevel FrameworkLogLevel { get; set; } = LogLevel.Warning;

    /// <summary>
    /// Gets or sets whether to suppress startup and initialization logs.
    /// When true, only errors and warnings during startup are logged.
    /// Default is false.
    /// </summary>
    public bool SuppressStartupLogs { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to enable detailed tool execution logging.
    /// When false, only errors and warnings from tool execution are logged.
    /// Default is false to reduce output.
    /// </summary>
    public bool EnableDetailedToolLogging { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to enable detailed middleware logging.
    /// When false, middleware operations are logged at debug level only.
    /// Default is false to reduce output.
    /// </summary>
    public bool EnableDetailedMiddlewareLogging { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to enable detailed transport logging.
    /// When false, transport operations are logged at debug level only.
    /// Default is false to reduce output.
    /// </summary>
    public bool EnableDetailedTransportLogging { get; set; } = false;

    /// <summary>
    /// Gets or sets whether the framework should configure logging if none is already configured.
    /// When false, the framework will not add any logging configuration.
    /// Default is true for backward compatibility.
    /// </summary>
    public bool ConfigureLoggingIfNotConfigured { get; set; } = true;
}