using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace McpServerTemplate.Tools;

/// <summary>
/// Example tool that provides system information.
/// </summary>
public class SystemInfoTool
{
    private readonly ILogger<SystemInfoTool> _logger;

    public SystemInfoTool(ILogger<SystemInfoTool> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets system information.
    /// </summary>
    public async Task<object> ExecuteAsync()
    {
        _logger.LogInformation("Executing get_system_info tool");

        // Simulate async operation
        await Task.Delay(50);

        var systemInfo = new
        {
            OS = new
            {
                Platform = RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.OSArchitecture.ToString(),
                Is64Bit = Environment.Is64BitOperatingSystem
            },
            Runtime = new
            {
                Framework = RuntimeInformation.FrameworkDescription,
                ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                DotNetVersion = Environment.Version.ToString()
            },
            Machine = new
            {
                MachineName = Environment.MachineName,
                ProcessorCount = Environment.ProcessorCount,
                UserName = Environment.UserName,
                UserDomainName = Environment.UserDomainName
            },
            Process = new
            {
                CurrentDirectory = Environment.CurrentDirectory,
                ProcessId = Environment.ProcessId,
                MemoryUsageMB = GC.GetTotalMemory(false) / (1024 * 1024)
            }
        };

        return new
        {
            Success = true,
            SystemInfo = systemInfo,
            Insights = new[]
            {
                $"Running on {RuntimeInformation.OSDescription}",
                $"Using {Environment.ProcessorCount} processor cores",
                $"Process memory usage: {systemInfo.Process.MemoryUsageMB} MB",
                "System information retrieved successfully"
            },
            Actions = new[]
            {
                new { Tool = "hello_world", Description = "Try the hello world example" }
            },
            Meta = new
            {
                Timestamp = DateTime.UtcNow,
                ToolVersion = "1.0.0"
            }
        };
    }
}