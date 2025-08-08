using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.Server.Services;

/// <summary>
/// Hosted service that manages the lifecycle of auto-started services.
/// </summary>
public class ServiceLifecycleHost : IHostedService
{
    private readonly IServiceManager _serviceManager;
    private readonly ServiceConfiguration _configuration;
    private readonly ILogger<ServiceLifecycleHost>? _logger;

    public ServiceLifecycleHost(
        IServiceManager serviceManager,
        ServiceConfiguration configuration,
        ILogger<ServiceLifecycleHost>? logger = null)
    {
        _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Starting auto-service {ServiceId}", _configuration.ServiceId);
        
        try
        {
            var status = await _serviceManager.EnsureServiceRunningAsync(_configuration);
            
            if (status == ServiceStatus.Healthy || status == ServiceStatus.Running)
            {
                _logger?.LogInformation("Auto-service {ServiceId} started successfully with status {Status}", 
                    _configuration.ServiceId, status);
            }
            else
            {
                _logger?.LogWarning("Auto-service {ServiceId} failed to start properly, status: {Status}", 
                    _configuration.ServiceId, status);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start auto-service {ServiceId}", _configuration.ServiceId);
            // Don't throw - allow the MCP server to continue even if auto-service fails
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Stopping auto-service {ServiceId}", _configuration.ServiceId);
        
        try
        {
            await _serviceManager.StopServiceAsync(_configuration.ServiceId);
            _logger?.LogInformation("Auto-service {ServiceId} stopped", _configuration.ServiceId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error stopping auto-service {ServiceId}", _configuration.ServiceId);
        }
    }
}