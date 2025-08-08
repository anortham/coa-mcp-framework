namespace COA.Mcp.Framework.Server.Services;

/// <summary>
/// Represents the current status of a managed service.
/// </summary>
public enum ServiceStatus
{
    /// <summary>
    /// Service has not been started yet.
    /// </summary>
    NotStarted,

    /// <summary>
    /// Service is in the process of starting.
    /// </summary>
    Starting,

    /// <summary>
    /// Service process is running.
    /// </summary>
    Running,

    /// <summary>
    /// Service is running and responding to health checks.
    /// </summary>
    Healthy,

    /// <summary>
    /// Service is running but not responding to health checks.
    /// </summary>
    Unhealthy,

    /// <summary>
    /// Service is in the process of stopping.
    /// </summary>
    Stopping,

    /// <summary>
    /// Service has been stopped.
    /// </summary>
    Stopped,

    /// <summary>
    /// Service has failed and cannot be restarted.
    /// </summary>
    Failed
}