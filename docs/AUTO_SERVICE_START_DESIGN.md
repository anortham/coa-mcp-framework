# Auto Service Start Design for COA MCP Framework

## Overview
This document describes the design for adding automatic service startup capability to the COA MCP Framework. This feature enables MCP servers to automatically start and manage background HTTP services when needed, supporting the dual-mode operation pattern where an MCP server can act as both a STDIO client and an HTTP service provider.

## Problem Statement
Currently, the COA MCP Framework supports HTTP/WebSocket transport but lacks the ability to:
1. Automatically start an HTTP service when the MCP server launches in STDIO mode
2. Detect if a service is already running on the configured port
3. Monitor and restart services if they fail
4. Manage the lifecycle of background services

This is needed for the ProjectKnowledge architecture where:
- The MCP server runs in STDIO mode for Claude Code communication
- The same server needs to expose an HTTP API for federation with other MCP servers
- The HTTP service should start automatically without manual intervention

## Design Goals
1. **Automatic Startup** - Services start without user intervention
2. **Idempotent** - Safe to call multiple times (won't start duplicate services)
3. **Resilient** - Monitors health and restarts failed services
4. **Cross-Platform** - Works on Windows, Linux, and macOS
5. **Configurable** - Flexible configuration for different deployment scenarios
6. **Non-Blocking** - Service management doesn't block MCP operations

## Proposed Architecture

### Core Components

#### 1. ServiceManager Class
```csharp
namespace COA.Mcp.Framework.Server.Services
{
    public class ServiceManager : IServiceManager, IDisposable
    {
        Task<ServiceStatus> EnsureServiceRunningAsync(ServiceConfiguration config);
        Task<bool> IsServiceHealthyAsync(string healthEndpoint);
        Task StopServiceAsync(string serviceId);
        ServiceStatus GetServiceStatus(string serviceId);
        void RegisterHealthCheck(string serviceId, Func<Task<bool>> healthCheck);
    }
}
```

#### 2. ServiceConfiguration Class
```csharp
public class ServiceConfiguration
{
    public string ServiceId { get; set; }
    public string ExecutablePath { get; set; }
    public string[] Arguments { get; set; }
    public int Port { get; set; }
    public string HealthEndpoint { get; set; }
    public int StartupTimeoutSeconds { get; set; } = 30;
    public int HealthCheckIntervalSeconds { get; set; } = 60;
    public bool AutoRestart { get; set; } = true;
    public int MaxRestartAttempts { get; set; } = 3;
    public Dictionary<string, string> EnvironmentVariables { get; set; }
}
```

#### 3. ServiceStatus Enum
```csharp
public enum ServiceStatus
{
    NotStarted,
    Starting,
    Running,
    Healthy,
    Unhealthy,
    Stopping,
    Stopped,
    Failed
}
```

### Integration with McpServerBuilder

```csharp
public class McpServerBuilder
{
    public McpServerBuilder UseAutoService(Action<ServiceConfiguration> configure)
    {
        var config = new ServiceConfiguration();
        configure(config);
        
        var serviceManager = new ServiceManager(_logger);
        _services.AddSingleton<IServiceManager>(serviceManager);
        _services.AddHostedService<ServiceLifecycleHost>(provider => 
            new ServiceLifecycleHost(serviceManager, config));
        
        return this;
    }
}
```

### Usage Example

```csharp
var server = McpServer.CreateBuilder()
    .WithServerInfo("ProjectKnowledge", "1.0.0")
    .UseStdioTransport()  // Primary transport for Claude
    .UseAutoService(config =>
    {
        config.ServiceId = "projectknowledge-http";
        config.ExecutablePath = Assembly.GetExecutingAssembly().Location;
        config.Arguments = new[] { "--mode", "http", "--port", "5100" };
        config.Port = 5100;
        config.HealthEndpoint = "http://localhost:5100/health";
        config.AutoRestart = true;
    })
    .Build();
```

## Implementation Details

### 1. Port Detection
Before starting a service, check if the port is already in use:

```csharp
private async Task<bool> IsPortInUse(int port)
{
    try
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var response = await client.GetAsync($"http://localhost:{port}/health");
        return response.IsSuccessStatusCode;
    }
    catch
    {
        return false;
    }
}
```

### 2. Process Management
Use `System.Diagnostics.Process` with proper handling:

```csharp
private Process StartServiceProcess(ServiceConfiguration config)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = config.ExecutablePath,
        Arguments = string.Join(" ", config.Arguments),
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    
    foreach (var env in config.EnvironmentVariables)
    {
        startInfo.EnvironmentVariables[env.Key] = env.Value;
    }
    
    var process = Process.Start(startInfo);
    
    // Register for exit event
    process.EnableRaisingEvents = true;
    process.Exited += OnServiceProcessExited;
    
    return process;
}
```

### 3. Health Monitoring
Background task that periodically checks service health:

```csharp
private async Task MonitorServiceHealth(ServiceConfiguration config, CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        try
        {
            var isHealthy = await IsServiceHealthyAsync(config.HealthEndpoint);
            
            if (!isHealthy && config.AutoRestart)
            {
                await RestartServiceAsync(config);
            }
            
            await Task.Delay(TimeSpan.FromSeconds(config.HealthCheckIntervalSeconds), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error monitoring service health");
        }
    }
}
```

### 4. Graceful Shutdown
Ensure proper cleanup when the MCP server stops:

```csharp
public async Task StopAsync(CancellationToken cancellationToken)
{
    foreach (var service in _managedServices.Values)
    {
        try
        {
            if (service.Process != null && !service.Process.HasExited)
            {
                // Try graceful shutdown first
                service.Process.CloseMainWindow();
                
                if (!service.Process.WaitForExit(5000))
                {
                    // Force kill if graceful shutdown fails
                    service.Process.Kill();
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error stopping service {ServiceId}", service.Config.ServiceId);
        }
    }
}
```

## Configuration Options

### Via appsettings.json
```json
{
  "McpFramework": {
    "AutoServices": [
      {
        "ServiceId": "projectknowledge-http",
        "ExecutablePath": "./ProjectKnowledge.dll",
        "Arguments": ["--mode", "http"],
        "Port": 5100,
        "HealthEndpoint": "http://localhost:5100/health",
        "AutoRestart": true,
        "MaxRestartAttempts": 3
      }
    ]
  }
}
```

### Via Environment Variables
```bash
MCP_AUTO_SERVICE_ENABLED=true
MCP_AUTO_SERVICE_PORT=5100
MCP_AUTO_SERVICE_HEALTH_ENDPOINT=http://localhost:5100/health
```

## Error Handling

### Startup Failures
- Log detailed error information
- Retry with exponential backoff
- Fall back to STDIO-only mode if HTTP service fails
- Notify through MCP protocol if possible

### Runtime Failures
- Automatic restart with configurable limits
- Circuit breaker pattern to prevent restart loops
- Health status exposed through MCP tools

## Testing Strategy

### Unit Tests
1. Port detection logic
2. Process lifecycle management
3. Health check mechanisms
4. Configuration parsing

### Integration Tests
1. Start/stop service scenarios
2. Port conflict resolution
3. Automatic restart on failure
4. Graceful shutdown

### Cross-Platform Tests
1. Windows service management
2. Linux daemon processes
3. macOS background services

## Security Considerations

1. **Process Isolation** - Child processes run with same permissions as parent
2. **Port Binding** - Only bind to localhost by default
3. **Authentication** - Support for API keys and JWT tokens
4. **Resource Limits** - Configurable CPU and memory limits for child processes

## Migration Path

### For Existing MCP Servers
1. Add NuGet package reference to updated framework
2. Add `UseAutoService()` call to builder
3. Configure service parameters
4. Test dual-mode operation

### Backward Compatibility
- Feature is opt-in via `UseAutoService()` method
- Existing servers continue to work without changes
- Can be enabled/disabled via configuration

## Performance Considerations

1. **Startup Time** - Service starts asynchronously, doesn't block MCP initialization
2. **Resource Usage** - Minimal overhead for health monitoring (~1 MB RAM)
3. **Network Traffic** - Health checks use local loopback only

## Alternative Designs Considered

### 1. External Service Manager
- Pros: Separation of concerns, language agnostic
- Cons: Additional deployment complexity, harder debugging
- Decision: Rejected in favor of integrated solution

### 2. Container-Based Approach
- Pros: Better isolation, standard deployment
- Cons: Requires Docker, not suitable for all environments
- Decision: Could be supported as additional option later

### 3. Windows Service / systemd Integration
- Pros: OS-level service management
- Cons: Platform-specific, requires elevated permissions
- Decision: Consider for future enhancement

## Implementation Timeline

1. **Week 1**: Core ServiceManager implementation
2. **Week 1**: Integration with McpServerBuilder
3. **Week 2**: Health monitoring and auto-restart
4. **Week 2**: Testing and documentation
5. **Week 2**: NuGet package update

## Open Questions

1. Should we support multiple auto-services per MCP server?
   - **Recommendation**: Yes, use collection of ServiceConfiguration

2. How to handle service dependencies?
   - **Recommendation**: Simple ordering via configuration

3. Should health checks be customizable per service?
   - **Recommendation**: Yes, via delegate registration

## Conclusion

This auto-service-start feature will enable the COA MCP Framework to support sophisticated dual-mode architectures like ProjectKnowledge, where MCP servers need to act as both clients (via STDIO) and services (via HTTP). The design prioritizes simplicity, reliability, and cross-platform compatibility while providing the flexibility needed for various deployment scenarios.

## Appendix A: Complete ServiceManager Implementation

```csharp
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.Server.Services
{
    public interface IServiceManager
    {
        Task<ServiceStatus> EnsureServiceRunningAsync(ServiceConfiguration config);
        Task<bool> IsServiceHealthyAsync(string healthEndpoint);
        Task StopServiceAsync(string serviceId);
        ServiceStatus GetServiceStatus(string serviceId);
        void RegisterHealthCheck(string serviceId, Func<Task<bool>> healthCheck);
    }

    public class ServiceManager : IServiceManager, IHostedService, IDisposable
    {
        private readonly ILogger<ServiceManager> _logger;
        private readonly HttpClient _httpClient;
        private readonly ConcurrentDictionary<string, ManagedService> _services;
        private readonly CancellationTokenSource _shutdownCts;
        private Task? _monitorTask;

        public ServiceManager(ILogger<ServiceManager> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            _services = new ConcurrentDictionary<string, ManagedService>();
            _shutdownCts = new CancellationTokenSource();
        }

        public async Task<ServiceStatus> EnsureServiceRunningAsync(ServiceConfiguration config)
        {
            // Implementation details in main document
            throw new NotImplementedException("See implementation details in design document");
        }

        public async Task<bool> IsServiceHealthyAsync(string healthEndpoint)
        {
            try
            {
                var response = await _httpClient.GetAsync(healthEndpoint);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public Task StopServiceAsync(string serviceId)
        {
            if (_services.TryRemove(serviceId, out var service))
            {
                return StopServiceInternalAsync(service);
            }
            return Task.CompletedTask;
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
            _monitorTask = Task.Run(() => MonitorServicesAsync(_shutdownCts.Token));
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _shutdownCts.Cancel();
            
            var stopTasks = _services.Values.Select(s => StopServiceInternalAsync(s));
            await Task.WhenAll(stopTasks);
            
            if (_monitorTask != null)
            {
                await _monitorTask;
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

        private async Task MonitorServicesAsync(CancellationToken cancellationToken)
        {
            // Monitoring implementation
        }

        private async Task StopServiceInternalAsync(ManagedService service)
        {
            // Graceful shutdown implementation
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
}
```

## Appendix B: Usage in ProjectKnowledge

```csharp
// Program.cs in COA.ProjectKnowledge.McpServer
public static async Task Main(string[] args)
{
    var builder = McpServer.CreateBuilder()
        .WithServerInfo("ProjectKnowledge", "1.0.0");

    // Determine mode from args
    if (args.Contains("--mode") && args.Contains("http"))
    {
        // HTTP mode - act as service
        builder.UseHttpTransport(options =>
        {
            options.Port = 5100;
            options.EnableWebSocket = true;
            options.EnableCors = true;
        });
    }
    else
    {
        // STDIO mode - act as MCP client with auto-service
        builder.UseStdioTransport()
            .UseAutoService(config =>
            {
                config.ServiceId = "projectknowledge-http";
                config.ExecutablePath = Assembly.GetExecutingAssembly().Location;
                config.Arguments = new[] { "--mode", "http" };
                config.Port = 5100;
                config.HealthEndpoint = "http://localhost:5100/health";
            });
    }

    builder.RegisterToolType<StoreKnowledgeTool>();
    builder.RegisterToolType<SearchKnowledgeTool>();
    builder.RegisterToolType<CheckpointTool>();
    builder.RegisterToolType<ChecklistTool>();

    await builder.RunAsync();
}
```