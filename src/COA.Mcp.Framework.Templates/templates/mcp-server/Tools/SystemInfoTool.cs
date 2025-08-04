using COA.Mcp.Framework;
using COA.Mcp.Framework.Attributes;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework.TokenOptimization;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace McpServerTemplate.Tools;

[McpServerToolType]
public class SystemInfoTool : McpToolBase
{
    private readonly ILogger<SystemInfoTool> _logger;
    private readonly ITokenEstimator _tokenEstimator;

    public override string ToolName => "get_system_info";
    public override ToolCategory Category => ToolCategory.Query;

    public SystemInfoTool(ILogger<SystemInfoTool> logger, ITokenEstimator tokenEstimator)
    {
        _logger = logger;
        _tokenEstimator = tokenEstimator;
    }

    [McpServerTool(Name = "get_system_info")]
    [Description(@"Gets system information including OS, runtime, and environment details.
    Returns: System information with OS details, runtime info, and environment variables.
    Prerequisites: None.
    Use cases: Debugging, system requirements verification, environment troubleshooting.
    Token-aware: Automatically reduces environment variables if response would be too large.")]
    public async Task<object> ExecuteAsync(SystemInfoParams parameters)
    {
        _logger.LogInformation("Executing get_system_info tool");

        var includeEnvironment = parameters.IncludeEnvironment ?? false;
        var tokenLimit = parameters.MaxTokens ?? 5000;

        return await ExecuteWithTokenManagement(async () =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            // Gather system information
            var systemInfo = new SystemInfo
            {
                OperatingSystem = new OSInfo
                {
                    Platform = Environment.OSVersion.Platform.ToString(),
                    Version = Environment.OSVersion.Version.ToString(),
                    VersionString = Environment.OSVersion.VersionString,
                    Is64BitOS = Environment.Is64BitOperatingSystem,
                    MachineName = Environment.MachineName,
                    UserName = Environment.UserName,
                    RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier,
                    OSDescription = RuntimeInformation.OSDescription,
                    OSArchitecture = RuntimeInformation.OSArchitecture.ToString()
                },
                Runtime = new RuntimeInfo
                {
                    FrameworkDescription = RuntimeInformation.FrameworkDescription,
                    ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                    ProcessorCount = Environment.ProcessorCount,
                    Is64BitProcess = Environment.Is64BitProcess,
                    CLRVersion = Environment.Version.ToString(),
                    WorkingSetMemory = Environment.WorkingSet / (1024 * 1024), // MB
                    TickCount = Environment.TickCount64,
                    SystemPageSize = Environment.SystemPageSize
                },
                Environment = includeEnvironment ? GetEnvironmentVariables(tokenLimit) : null
            };

            sw.Stop();

            // Generate insights based on the system info
            var insights = GenerateSystemInsights(systemInfo);
            var actions = GenerateSystemActions(systemInfo);

            return new
            {
                Success = true,
                Data = systemInfo,
                Insights = insights,
                Actions = actions,
                Meta = new ToolMetadata
                {
                    ExecutionTime = $"{sw.ElapsedMilliseconds}ms",
                    TokensEstimated = _tokenEstimator.EstimateObject(systemInfo),
                    Truncated = systemInfo.Environment?.Truncated ?? false,
                    ToolVersion = "1.0.0"
                }
            };
        });
    }

    private EnvironmentInfo GetEnvironmentVariables(int tokenLimit)
    {
        var allVars = Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .Select(de => new EnvironmentVariable
            {
                Key = de.Key.ToString()!,
                Value = de.Value?.ToString() ?? string.Empty
            })
            .OrderBy(ev => ev.Key)
            .ToList();

        // Apply progressive reduction if needed
        var estimatedTokens = _tokenEstimator.EstimateCollection(
            allVars, 
            ev => _tokenEstimator.EstimateObject(ev)
        );

        if (estimatedTokens > tokenLimit)
        {
            _logger.LogInformation("Applying token reduction: {EstimatedTokens} > {TokenLimit}", 
                estimatedTokens, tokenLimit);

            // Filter to important variables only
            var importantPrefixes = new[] { "PATH", "TEMP", "TMP", "HOME", "USER", "DOTNET", "MCP" };
            var filteredVars = allVars
                .Where(ev => importantPrefixes.Any(prefix => 
                    ev.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            return new EnvironmentInfo
            {
                Variables = filteredVars,
                TotalCount = allVars.Count,
                IncludedCount = filteredVars.Count,
                Truncated = true
            };
        }

        return new EnvironmentInfo
        {
            Variables = allVars,
            TotalCount = allVars.Count,
            IncludedCount = allVars.Count,
            Truncated = false
        };
    }

    private List<string> GenerateSystemInsights(SystemInfo info)
    {
        var insights = new List<string>();

        // OS insights
        insights.Add($"Running on {info.OperatingSystem.OSDescription}");
        
        if (info.OperatingSystem.Is64BitOS != info.Runtime.Is64BitProcess)
        {
            insights.Add($"Process is running as {(info.Runtime.Is64BitProcess ? "64-bit" : "32-bit")} on a {(info.OperatingSystem.Is64BitOS ? "64-bit" : "32-bit")} OS");
        }

        // Runtime insights
        insights.Add($"Using {info.Runtime.FrameworkDescription}");
        insights.Add($"System has {info.Runtime.ProcessorCount} processor cores available");

        // Memory insights
        if (info.Runtime.WorkingSetMemory > 1000)
        {
            insights.Add($"High memory usage detected: {info.Runtime.WorkingSetMemory}MB");
        }

        // Environment insights
        if (info.Environment != null && info.Environment.Truncated)
        {
            insights.Add($"Environment variables were filtered from {info.Environment.TotalCount} to {info.Environment.IncludedCount} due to token limits");
        }

        return insights.Take(5).ToList(); // Limit to 5 insights
    }

    private List<AIAction> GenerateSystemActions(SystemInfo info)
    {
        var actions = new List<AIAction>();

        if (info.Environment == null)
        {
            actions.Add(new AIAction
            {
                Tool = "get_system_info",
                Description = "Get system info including environment variables",
                Parameters = new { includeEnvironment = true }
            });
        }

        if (info.Runtime.WorkingSetMemory > 500)
        {
            actions.Add(new AIAction
            {
                Tool = "analyze_memory_usage",
                Description = "Analyze memory usage patterns"
            });
        }

        return actions;
    }
}

public class SystemInfoParams
{
    [Description("Include environment variables in the response (optional, defaults to false)")]
    public bool? IncludeEnvironment { get; set; }

    [Description("Maximum tokens for response (optional, defaults to 5000)")]
    public int? MaxTokens { get; set; }
}

public class SystemInfo
{
    public OSInfo OperatingSystem { get; set; } = new();
    public RuntimeInfo Runtime { get; set; } = new();
    public EnvironmentInfo? Environment { get; set; }
}

public class OSInfo
{
    public string Platform { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string VersionString { get; set; } = string.Empty;
    public bool Is64BitOS { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string RuntimeIdentifier { get; set; } = string.Empty;
    public string OSDescription { get; set; } = string.Empty;
    public string OSArchitecture { get; set; } = string.Empty;
}

public class RuntimeInfo
{
    public string FrameworkDescription { get; set; } = string.Empty;
    public string ProcessArchitecture { get; set; } = string.Empty;
    public int ProcessorCount { get; set; }
    public bool Is64BitProcess { get; set; }
    public string CLRVersion { get; set; } = string.Empty;
    public long WorkingSetMemory { get; set; }
    public long TickCount { get; set; }
    public int SystemPageSize { get; set; }
}

public class EnvironmentInfo
{
    public List<EnvironmentVariable> Variables { get; set; } = new();
    public int TotalCount { get; set; }
    public int IncludedCount { get; set; }
    public bool Truncated { get; set; }
}

public class EnvironmentVariable
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}