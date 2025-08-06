using System.ComponentModel;
using System.Runtime.InteropServices;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Models;

namespace SimpleMcpServer.Tools;

/// <summary>
/// A tool that provides system information.
/// </summary>
public class SystemInfoTool : McpToolBase<SystemInfoParameters, SystemInfoResult>
{
    public override string Name => "system_info";
    public override string Description => "Get information about the system and runtime environment";
    public override ToolCategory Category => ToolCategory.Query;

    protected override async Task<SystemInfoResult> ExecuteInternalAsync(
        SystemInfoParameters parameters, 
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var info = new SystemInfoResult
        {
            Success = true,
            Timestamp = DateTime.UtcNow,
            System = new SystemDetails
            {
                MachineName = Environment.MachineName,
                UserName = Environment.UserName,
                OSVersion = Environment.OSVersion.ToString(),
                OSDescription = RuntimeInformation.OSDescription,
                OSArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                ProcessorCount = Environment.ProcessorCount,
                Is64BitOperatingSystem = Environment.Is64BitOperatingSystem,
                Is64BitProcess = Environment.Is64BitProcess
            },
            Runtime = new RuntimeDetails
            {
                DotNetVersion = Environment.Version.ToString(),
                RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier,
                FrameworkDescription = RuntimeInformation.FrameworkDescription,
                ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                CurrentDirectory = Environment.CurrentDirectory,
                SystemDirectory = Environment.SystemDirectory,
                TempPath = Path.GetTempPath()
            },
            Process = new ProcessDetails
            {
                ProcessId = Environment.ProcessId,
                WorkingSet = Environment.WorkingSet,
                TickCount = Environment.TickCount64,
                CommandLine = Environment.CommandLine,
                CurrentManagedThreadId = Environment.CurrentManagedThreadId
            }
        };

        // Add environment variables if requested
        if (parameters.IncludeEnvironmentVariables == true)
        {
            info.EnvironmentVariables = new Dictionary<string, string>();
            foreach (System.Collections.DictionaryEntry env in Environment.GetEnvironmentVariables())
            {
                if (env.Key != null && env.Value != null)
                {
                    var key = env.Key.ToString()!;
                    var value = env.Value.ToString()!;
                    
                    // Optionally filter sensitive information
                    if (parameters.FilterSensitive == true)
                    {
                        if (key.Contains("PASSWORD", StringComparison.OrdinalIgnoreCase) ||
                            key.Contains("SECRET", StringComparison.OrdinalIgnoreCase) ||
                            key.Contains("KEY", StringComparison.OrdinalIgnoreCase) ||
                            key.Contains("TOKEN", StringComparison.OrdinalIgnoreCase))
                        {
                            value = "***FILTERED***";
                        }
                    }
                    
                    info.EnvironmentVariables[key] = value;
                }
            }
        }

        // Add drives information if requested
        if (parameters.IncludeDrives == true)
        {
            info.Drives = new List<DriveDetails>();
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.IsReady)
                    {
                        info.Drives.Add(new DriveDetails
                        {
                            Name = drive.Name,
                            DriveType = drive.DriveType.ToString(),
                            VolumeLabel = drive.VolumeLabel,
                            DriveFormat = drive.DriveFormat,
                            TotalSize = drive.TotalSize,
                            AvailableFreeSpace = drive.AvailableFreeSpace,
                            TotalFreeSpace = drive.TotalFreeSpace
                        });
                    }
                }
                catch
                {
                    // Skip drives that can't be accessed
                }
            }
        }

        return info;
    }
}

public class SystemInfoParameters
{
    [Description("Include environment variables in the response")]
    public bool? IncludeEnvironmentVariables { get; set; }

    [Description("Include drive information in the response")]
    public bool? IncludeDrives { get; set; }

    [Description("Filter sensitive information from environment variables")]
    public bool? FilterSensitive { get; set; } = true;
}

public class SystemInfoResult : ToolResultBase
{
    public override string Operation => "system_info";
    public DateTime Timestamp { get; set; }
    public SystemDetails? System { get; set; }
    public RuntimeDetails? Runtime { get; set; }
    public ProcessDetails? Process { get; set; }
    public Dictionary<string, string>? EnvironmentVariables { get; set; }
    public List<DriveDetails>? Drives { get; set; }
}

public class SystemDetails
{
    public string? MachineName { get; set; }
    public string? UserName { get; set; }
    public string? OSVersion { get; set; }
    public string? OSDescription { get; set; }
    public string? OSArchitecture { get; set; }
    public int ProcessorCount { get; set; }
    public bool Is64BitOperatingSystem { get; set; }
    public bool Is64BitProcess { get; set; }
}

public class RuntimeDetails
{
    public string? DotNetVersion { get; set; }
    public string? RuntimeIdentifier { get; set; }
    public string? FrameworkDescription { get; set; }
    public string? ProcessArchitecture { get; set; }
    public string? CurrentDirectory { get; set; }
    public string? SystemDirectory { get; set; }
    public string? TempPath { get; set; }
}

public class ProcessDetails
{
    public int ProcessId { get; set; }
    public long WorkingSet { get; set; }
    public long TickCount { get; set; }
    public string? CommandLine { get; set; }
    public int CurrentManagedThreadId { get; set; }
}

public class DriveDetails
{
    public string? Name { get; set; }
    public string? DriveType { get; set; }
    public string? VolumeLabel { get; set; }
    public string? DriveFormat { get; set; }
    public long TotalSize { get; set; }
    public long AvailableFreeSpace { get; set; }
    public long TotalFreeSpace { get; set; }
}