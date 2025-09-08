using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework.Utilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.Server.Services;

/// <summary>
/// Manages auto-started services for the MCP framework.
/// </summary>
public class ServiceManager : IServiceManager, IHostedService, IDisposable
{
    private readonly ILogger<ServiceManager>? _logger;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, ManagedService> _services;
    private readonly CancellationTokenSource _shutdownCts;
    private Task? _monitorTask;

    public ServiceManager(ILogger<ServiceManager>? logger = null)
    {
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        _services = new ConcurrentDictionary<string, ManagedService>();
        _shutdownCts = new CancellationTokenSource();
    }

    public async Task<ServiceStatus> EnsureServiceRunningAsync(ServiceConfiguration config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        if (string.IsNullOrEmpty(config.ServiceId))
            throw new ArgumentException("ServiceId is required", nameof(config));

        // Check if service already exists
        if (_services.TryGetValue(config.ServiceId, out var existingService))
        {
            _logger?.LogDebug("Service {ServiceId} already exists with status {Status}", 
                config.ServiceId, existingService.Status);
            return existingService.Status;
        }

        // Check if port is already in use
        if (await IsPortInUseAsync(config.Port))
        {
            _logger?.LogInformation("Port {Port} is already in use, assuming service is running", config.Port);
            
            // Check if it's actually our service by trying the health endpoint
            if (!string.IsNullOrEmpty(config.HealthEndpoint))
            {
                var isHealthy = await IsServiceHealthyAsync(config.HealthEndpoint);
                if (isHealthy)
                {
                    // Port is in use and health check passes - service is already running
                    var managedService = new ManagedService
                    {
                        Config = config,
                        Status = ServiceStatus.Healthy,
                        LastHealthCheck = DateTime.UtcNow
                    };
                    _services[config.ServiceId] = managedService;
                    return ServiceStatus.Healthy;
                }
            }
            
            // Port is in use but not by our service
            _logger?.LogWarning("Port {Port} is in use by another process", config.Port);
            return ServiceStatus.Failed;
        }

        // Start the service
        return await StartServiceAsync(config);
    }

    public async Task<bool> IsServiceHealthyAsync(string healthEndpoint)
    {
        if (string.IsNullOrEmpty(healthEndpoint))
            return false;

        try
        {
            var response = await _httpClient.GetAsync(healthEndpoint);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Health check failed for {Endpoint}", healthEndpoint);
            return false;
        }
    }

    public async Task StopServiceAsync(string serviceId)
    {
        if (_services.TryRemove(serviceId, out var service))
        {
            await StopServiceInternalAsync(service);
        }
    }

    public ServiceStatus GetServiceStatus(string serviceId)
    {
        return _services.TryGetValue(serviceId, out var service)
            ? service.Status
            : ServiceStatus.NotStarted;
    }

    public void RegisterHealthCheck(string serviceId, Func<Task<bool>> healthCheck)
    {
        if (_services.TryGetValue(serviceId, out var service))
        {
            service.CustomHealthCheck = healthCheck;
        }
    }

    // IHostedService implementation
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _monitorTask = Task.Run(() => MonitorServicesAsync(_shutdownCts.Token), cancellationToken);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _shutdownCts.Cancel();

        // Stop all managed services
        var stopTasks = new Task[_services.Count];
        var index = 0;
        foreach (var service in _services.Values)
        {
            stopTasks[index++] = StopServiceInternalAsync(service);
        }
        await Task.WhenAll(stopTasks);

        // Wait for monitor task to complete
        if (_monitorTask != null)
        {
            try
            {
                await _monitorTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation token is triggered
            }
        }
    }

    public void Dispose()
    {
        _shutdownCts?.Dispose();
        _httpClient?.Dispose();

        foreach (var service in _services.Values)
        {
            service.Process?.Dispose();
        }
    }

    private async Task<ServiceStatus> StartServiceAsync(ServiceConfiguration config)
    {
        var managedService = new ManagedService
        {
            Config = config,
            Status = ServiceStatus.Starting
        };

        _services[config.ServiceId] = managedService;

        try
        {
            _logger?.LogInformation("Starting service {ServiceId} on port {Port}", 
                config.ServiceId, config.Port);

            var process = StartServiceProcess(config);
            managedService.Process = process;

            // Wait for service to become healthy
            var startTime = DateTime.UtcNow;
            var timeout = TimeSpan.FromSeconds(config.StartupTimeoutSeconds);

            while (DateTime.UtcNow - startTime < timeout)
            {
                if (process.HasExited)
                {
                    _logger?.LogError("Service {ServiceId} process exited unexpectedly with code {ExitCode}", 
                        config.ServiceId, process.ExitCode);
                    managedService.Status = ServiceStatus.Failed;
                    return ServiceStatus.Failed;
                }

                if (!string.IsNullOrEmpty(config.HealthEndpoint))
                {
                    if (await IsServiceHealthyAsync(config.HealthEndpoint))
                    {
                        _logger?.LogInformation("Service {ServiceId} is healthy", config.ServiceId);
                        managedService.Status = ServiceStatus.Healthy;
                        managedService.LastHealthCheck = DateTime.UtcNow;
                        return ServiceStatus.Healthy;
                    }
                }
                else
                {
                    // No health endpoint, just check if process is running
                    managedService.Status = ServiceStatus.Running;
                    return ServiceStatus.Running;
                }

                await Task.Delay(1000);
            }

            _logger?.LogWarning("Service {ServiceId} failed to become healthy within timeout", config.ServiceId);
            managedService.Status = ServiceStatus.Unhealthy;
            return ServiceStatus.Unhealthy;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start service {ServiceId}", config.ServiceId);
            managedService.Status = ServiceStatus.Failed;
            return ServiceStatus.Failed;
        }
    }

    private Process StartServiceProcess(ServiceConfiguration config)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = config.ExecutablePath,
            Arguments = string.Join(" ", config.Arguments),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = config.RedirectStandardOutput,
            RedirectStandardError = config.RedirectStandardError,
            WorkingDirectory = config.WorkingDirectory
        };

        // Add environment variables
        foreach (var env in config.EnvironmentVariables)
        {
            startInfo.EnvironmentVariables[env.Key] = env.Value;
        }

        var process = new Process { StartInfo = startInfo };
        
        // Set up event handlers for output if redirecting
        if (config.RedirectStandardOutput)
        {
            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logger?.LogDebug("[{ServiceId}] {Output}", config.ServiceId, e.Data);
                }
            };
        }

        if (config.RedirectStandardError)
        {
            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logger?.LogWarning("[{ServiceId}] {Error}", config.ServiceId, e.Data);
                }
            };
        }

        process.EnableRaisingEvents = true;
        process.Exited += (sender, e) => OnServiceProcessExited(config.ServiceId);

        process.Start();

        if (config.RedirectStandardOutput)
            process.BeginOutputReadLine();
        
        if (config.RedirectStandardError)
            process.BeginErrorReadLine();

        return process;
    }

    private void OnServiceProcessExited(string serviceId)
    {
        if (_services.TryGetValue(serviceId, out var service))
        {
            _logger?.LogWarning("Service {ServiceId} process exited", serviceId);
            
            if (service.Status != ServiceStatus.Stopping && service.Status != ServiceStatus.Stopped)
            {
                service.Status = ServiceStatus.Failed;
                
                // Trigger restart if configured
                if (service.Config.AutoRestart && service.RestartAttempts < service.Config.MaxRestartAttempts)
                {
                    service.RestartAttempts++;
                    _logger?.LogInformation("Scheduling restart for service {ServiceId} (attempt {Attempt}/{Max})",
                        serviceId, service.RestartAttempts, service.Config.MaxRestartAttempts);
                    
                    Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5)); // Wait before restarting
                        await RestartServiceAsync(service);
                    });
                }
            }
        }
    }

    private async Task RestartServiceAsync(ManagedService service)
    {
        _logger?.LogInformation("Restarting service {ServiceId}", service.Config.ServiceId);
        
        // Stop the old process if it's still running
        if (service.Process != null && !service.Process.HasExited)
        {
            await StopServiceInternalAsync(service);
        }

        // Start the service again
        await StartServiceAsync(service.Config);
    }

    private async Task MonitorServicesAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Get services that need health checks - do this first to avoid holding locks during async operations
                var servicesToCheck = _services.Values
                    .Where(service => 
                    {
                        // Skip services that are not running
                        if (service.Status != ServiceStatus.Running && 
                            service.Status != ServiceStatus.Healthy && 
                            service.Status != ServiceStatus.Unhealthy)
                        {
                            return false;
                        }

                        // Check if enough time has passed since last health check
                        var timeSinceLastCheck = DateTime.UtcNow - service.LastHealthCheck;
                        return timeSinceLastCheck.TotalSeconds >= service.Config.HealthCheckIntervalSeconds;
                    })
                    .ToList();

                if (servicesToCheck.Any())
                {
                    // Perform health checks concurrently
                    await ConcurrentAsyncUtilities.ExecuteConcurrentlyAsync(
                        servicesToCheck,
                        async service =>
                        {
                            if (cancellationToken.IsCancellationRequested)
                                return;

                            // Perform health check
                            bool isHealthy = false;
                            if (service.CustomHealthCheck != null)
                            {
                                isHealthy = await service.CustomHealthCheck().ConfigureAwait(false);
                            }
                            else if (!string.IsNullOrEmpty(service.Config.HealthEndpoint))
                            {
                                isHealthy = await IsServiceHealthyAsync(service.Config.HealthEndpoint).ConfigureAwait(false);
                            }
                            else
                            {
                                // No health check configured, just check if process is running
                                isHealthy = service.Process != null && !service.Process.HasExited;
                            }

                            service.LastHealthCheck = DateTime.UtcNow;
                            var previousStatus = service.Status;
                            service.Status = isHealthy ? ServiceStatus.Healthy : ServiceStatus.Unhealthy;

                            if (previousStatus != service.Status)
                            {
                                _logger?.LogInformation("Service {ServiceId} status changed from {Previous} to {Current}",
                                    service.Config.ServiceId, previousStatus, service.Status);
                            }

                            // Restart if unhealthy and auto-restart is enabled
                            if (!isHealthy && service.Config.AutoRestart && 
                                service.RestartAttempts < service.Config.MaxRestartAttempts)
                            {
                                service.RestartAttempts++;
                                _logger?.LogWarning("Service {ServiceId} is unhealthy, attempting restart ({Attempt}/{Max})",
                                    service.Config.ServiceId, service.RestartAttempts, service.Config.MaxRestartAttempts);
                                await RestartServiceAsync(service).ConfigureAwait(false);
                            }
                        },
                        maxConcurrency: 10,
                        cancellationToken
                    ).ConfigureAwait(false);
                }

                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in service monitor loop");
            }
        }
    }

    private async Task StopServiceInternalAsync(ManagedService service)
    {
        service.Status = ServiceStatus.Stopping;
        
        try
        {
            if (service.Process != null && !service.Process.HasExited)
            {
                _logger?.LogInformation("Stopping service {ServiceId}", service.Config.ServiceId);
                
                // Try graceful shutdown first
                service.Process.CloseMainWindow();
                
                // Use async WaitForExitAsync for better async behavior
                var gracefulExitTask = service.Process.WaitForExitAsync();
                var gracefulTimeoutTask = Task.Delay(5000);
                
                if (await Task.WhenAny(gracefulExitTask, gracefulTimeoutTask) == gracefulTimeoutTask)
                {
                    _logger?.LogWarning("Service {ServiceId} did not stop gracefully, forcing termination",
                        service.Config.ServiceId);
                    service.Process.Kill();
                    
                    var forceExitTask = service.Process.WaitForExitAsync();
                    var forceTimeoutTask = Task.Delay(2000);
                    await Task.WhenAny(forceExitTask, forceTimeoutTask);
                }

                _logger?.LogInformation("Service {ServiceId} stopped", service.Config.ServiceId);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error stopping service {ServiceId}", service.Config.ServiceId);
        }
        finally
        {
            service.Status = ServiceStatus.Stopped;
            service.Process?.Dispose();
            service.Process = null;
        }
    }

    private async Task<bool> IsPortInUseAsync(int port)
    {
        try
        {
            using var tcpClient = new TcpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            await tcpClient.ConnectAsync(IPAddress.Loopback, port, cts.Token);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private class ManagedService
    {
        public ServiceConfiguration Config { get; set; } = null!;
        public Process? Process { get; set; }
        public ServiceStatus Status { get; set; }
        public int RestartAttempts { get; set; }
        public Func<Task<bool>>? CustomHealthCheck { get; set; }
        public DateTime LastHealthCheck { get; set; }
    }
}