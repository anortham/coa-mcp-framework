using System.Collections.Generic;

namespace COA.Mcp.Framework.Server.Services;

/// <summary>
/// Configuration for an auto-started service managed by the MCP framework.
/// </summary>
public class ServiceConfiguration
{
    /// <summary>
    /// Gets or sets the unique identifier for this service.
    /// </summary>
    public string ServiceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path to the executable to run.
    /// </summary>
    public string ExecutablePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the command-line arguments to pass to the service.
    /// </summary>
    public string[] Arguments { get; set; } = System.Array.Empty<string>();

    /// <summary>
    /// Gets or sets the port the service will listen on.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Gets or sets the health check endpoint URL.
    /// </summary>
    public string HealthEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the startup timeout in seconds.
    /// </summary>
    public int StartupTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the health check interval in seconds.
    /// </summary>
    public int HealthCheckIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets whether to automatically restart the service if it fails.
    /// </summary>
    public bool AutoRestart { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of restart attempts.
    /// </summary>
    public int MaxRestartAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets environment variables to set for the service process.
    /// </summary>
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets whether to redirect standard output.
    /// </summary>
    public bool RedirectStandardOutput { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to redirect standard error.
    /// </summary>
    public bool RedirectStandardError { get; set; } = true;

    /// <summary>
    /// Gets or sets the working directory for the service process.
    /// </summary>
    public string? WorkingDirectory { get; set; }
}