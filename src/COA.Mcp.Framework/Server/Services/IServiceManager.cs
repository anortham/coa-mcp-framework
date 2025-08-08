using System;
using System.Threading.Tasks;

namespace COA.Mcp.Framework.Server.Services;

/// <summary>
/// Interface for managing auto-started services.
/// </summary>
public interface IServiceManager
{
    /// <summary>
    /// Ensures a service is running with the specified configuration.
    /// </summary>
    /// <param name="config">The service configuration.</param>
    /// <returns>The current status of the service.</returns>
    Task<ServiceStatus> EnsureServiceRunningAsync(ServiceConfiguration config);

    /// <summary>
    /// Checks if a service is healthy by calling its health endpoint.
    /// </summary>
    /// <param name="healthEndpoint">The health check endpoint URL.</param>
    /// <returns>True if the service is healthy, false otherwise.</returns>
    Task<bool> IsServiceHealthyAsync(string healthEndpoint);

    /// <summary>
    /// Stops a running service.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <returns>A task representing the async operation.</returns>
    Task StopServiceAsync(string serviceId);

    /// <summary>
    /// Gets the current status of a service.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <returns>The current service status.</returns>
    ServiceStatus GetServiceStatus(string serviceId);

    /// <summary>
    /// Registers a custom health check for a service.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="healthCheck">The health check function.</param>
    void RegisterHealthCheck(string serviceId, Func<Task<bool>> healthCheck);
}