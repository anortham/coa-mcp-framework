# Manual Migration Example: Converting GetWorkspaceStatisticsTool

This document shows a **step-by-step manual migration** of a single tool from CodeNav to use the COA MCP Framework. No automated tools, no mess - just careful, incremental changes.

## Step 1: Add Framework References

First, add the framework packages to your project:

```xml
<!-- In COA.CodeNav.McpServer.csproj, add: -->
<PackageReference Include="COA.Mcp.Framework" Version="1.0.*" />
<PackageReference Include="COA.Mcp.Framework.TokenOptimization" Version="1.0.*" />
```

## Step 2: Create the Migrated Tool (Side-by-Side)

Create a NEW file `GetWorkspaceStatisticsToolV2.cs` alongside the original. This way we don't break anything:

```csharp
using COA.Mcp.Framework;
using COA.Mcp.Framework.Attributes;
using COA.Mcp.Framework.TokenOptimization.Models;
using COA.CodeNav.McpServer.Infrastructure;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

namespace COA.CodeNav.McpServer.Tools.V2;

[McpServerToolType]
public class GetWorkspaceStatisticsToolV2 : McpToolBase
{
    private readonly ILogger<GetWorkspaceStatisticsToolV2> _logger;
    private readonly MSBuildWorkspaceManager _workspaceManager;

    public override string ToolName => "csharp_get_workspace_statistics";
    public override ToolCategory Category => ToolCategory.Information;

    public GetWorkspaceStatisticsToolV2(
        ILogger<GetWorkspaceStatisticsToolV2> logger,
        MSBuildWorkspaceManager workspaceManager)
    {
        _logger = logger;
        _workspaceManager = workspaceManager;
    }

    [McpServerTool(Name = "csharp_get_workspace_statistics")]
    [Description(@"Get statistics about currently loaded workspaces and resource usage.
Returns: Workspace count, memory usage, idle times, and access patterns.
Use cases: Monitoring resource usage, debugging workspace issues, understanding cache behavior.")]
    public async Task<object> ExecuteAsync(GetWorkspaceStatisticsParams parameters, CancellationToken cancellationToken = default)
    {
        // Use framework's token management wrapper
        return await ExecuteWithTokenManagement(async () =>
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                _logger.LogInformation("Getting workspace statistics");
                
                var stats = _workspaceManager.GetStatistics();
                var process = System.Diagnostics.Process.GetCurrentProcess();
                var memoryMB = process.WorkingSet64 / (1024 * 1024);
                
                // Build response using framework patterns
                var response = new AIOptimizedResponse
                {
                    Format = "ai-optimized",
                    Data = new
                    {
                        TotalWorkspaces = stats.TotalWorkspaces,
                        MaxWorkspaces = stats.MaxWorkspaces,
                        AvailableSlots = stats.MaxWorkspaces - stats.TotalWorkspaces,
                        OldestIdleTime = stats.OldestIdleTime.ToString(@"hh\:mm\:ss"),
                        TotalAccessCount = stats.TotalAccessCount,
                        MemoryUsageMB = memoryMB,
                        GCMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024),
                        WorkspaceDetails = stats.WorkspaceDetails.Select(w => new
                        {
                            WorkspaceId = w.WorkspaceId,
                            LoadedPath = w.LoadedPath ?? "N/A",
                            CreatedAt = w.CreatedAt,
                            LastAccessedAt = w.LastAccessedAt,
                            AccessCount = w.AccessCount,
                            IdleTime = w.IdleTime.ToString(@"hh\:mm\:ss"),
                            IsStale = w.IdleTime > TimeSpan.FromMinutes(15)
                        }).ToList()
                    },
                    Insights = GenerateInsights(stats, memoryMB),
                    Actions = GenerateActions(stats, memoryMB),
                    Meta = new AIResponseMeta
                    {
                        ExecutionTime = $"{(DateTime.UtcNow - startTime).TotalMilliseconds:F2}ms",
                        Truncated = false,
                        TotalItems = stats.TotalWorkspaces
                    }
                };

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting workspace statistics");
                throw; // Let framework handle error response
            }
        });
    }

    private List<Insight> GenerateInsights(WorkspaceStatistics stats, long memoryMB)
    {
        var insights = new List<Insight>();
        
        // Workspace usage
        var usage = (double)stats.TotalWorkspaces / stats.MaxWorkspaces * 100;
        insights.Add(new Insight
        {
            Text = $"Workspace capacity at {usage:F0}% ({stats.TotalWorkspaces}/{stats.MaxWorkspaces})",
            Importance = usage > 80 ? InsightImportance.High : InsightImportance.Medium
        });
        
        // Memory usage
        insights.Add(new Insight
        {
            Text = $"Process memory usage: {memoryMB}MB",
            Importance = memoryMB > 1500 ? InsightImportance.High : InsightImportance.Low
        });
        
        // Idle workspaces
        var idleCount = stats.WorkspaceDetails.Count(w => w.IdleTime > TimeSpan.FromMinutes(15));
        if (idleCount > 0)
        {
            insights.Add(new Insight
            {
                Text = $"{idleCount} workspace(s) idle for more than 15 minutes",
                Importance = InsightImportance.Medium
            });
        }
        
        return insights;
    }

    private List<AIAction> GenerateActions(WorkspaceStatistics stats, long memoryMB)
    {
        var actions = new List<AIAction>();

        // If no workspaces loaded
        if (stats.TotalWorkspaces == 0)
        {
            actions.Add(new AIAction
            {
                Tool = "csharp_load_solution",
                Category = "setup",
                Rationale = "No workspaces loaded - load a solution to start",
                Parameters = new { solutionPath = "<path-to-solution.sln>" }
            });
        }

        // Always suggest refresh
        actions.Add(new AIAction
        {
            Tool = "csharp_get_workspace_statistics",
            Category = "monitoring",
            Rationale = "Refresh statistics to see current state"
        });

        return actions;
    }
}

// Keep the same params class
public class GetWorkspaceStatisticsParams
{
    // No parameters needed
}
```

## Step 3: Register the New Tool

In your Program.cs or service registration, temporarily register BOTH tools:

```csharp
// Keep the old registration
services.AddTransient<GetWorkspaceStatisticsTool>();

// Add the new one
services.AddTransient<GetWorkspaceStatisticsToolV2>();
```

## Step 4: Test Side-by-Side

1. Build and run your server
2. Test that the old tool still works
3. Test the new V2 tool
4. Compare outputs to ensure they're functionally equivalent

## Step 5: Switch Over (When Ready)

Once you've verified the V2 tool works correctly:

1. Remove the old tool registration
2. Delete the old tool file
3. Rename `GetWorkspaceStatisticsToolV2` to `GetWorkspaceStatisticsTool`
4. Update namespace from `Tools.V2` back to `Tools`

## Key Differences in the Migrated Version

1. **Inherits from McpToolBase** - Gets validation helpers and token management
2. **Uses ExecuteWithTokenManagement** - Automatic token optimization
3. **Returns AIOptimizedResponse** - Standardized response format
4. **Uses framework's Insight/AIAction types** - Better structure
5. **Simpler error handling** - Framework handles error responses

## Benefits You Get

1. **Automatic token counting** - No need to worry about context limits
2. **Response truncation** - Large responses automatically reduced
3. **Consistent error handling** - Framework provides standard error format
4. **Built-in validation** - Use `ValidateRequired`, `ValidateRange`, etc.
5. **Ready for testing** - Can now write unit tests using framework helpers

## Migration Tips

1. **Start with simple tools** - Information queries are easiest
2. **Keep tools side-by-side** - Don't delete old ones until new ones work
3. **Test thoroughly** - Ensure responses are compatible
4. **Migrate similar tools together** - Learn patterns and reuse
5. **Don't rush** - Better to migrate 2-3 tools correctly than 10 poorly

## What NOT to Do

❌ Don't use automated migration tools that don't understand your code
❌ Don't try to migrate everything at once
❌ Don't change the tool names or parameters (breaks compatibility)
❌ Don't skip testing - verify each tool works before moving on
❌ Don't delete old code until new code is proven

This manual approach takes more time but ensures you understand each change and nothing breaks. Once you've migrated a few tools, you'll see the patterns and it becomes much faster.