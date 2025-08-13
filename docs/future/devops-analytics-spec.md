# DevOps Analytics MCP Technical Specification

## Overview

The DevOps Analytics MCP provides comprehensive Azure DevOps integration for sprint analytics, team performance metrics, and portfolio management with intelligent visualization and predictive insights.

## Architecture

### Core Components

```
DevOps Analytics MCP
├── Tools/
│   ├── SprintAnalyticsTool.cs         # Burndown, velocity, capacity analytics
│   ├── TeamMetricsTool.cs             # Individual and team performance
│   ├── PortfolioAnalyticsTool.cs      # Epic/feature progress tracking
│   ├── WorkItemAnalyticsTool.cs       # Work item lifecycle analysis
│   ├── BuildMetricsTool.cs            # Build/deployment analytics
│   └── PredictiveAnalyticsTool.cs     # Forecasting and trend analysis
├── Services/
│   ├── AzureDevOpsService.cs          # Azure DevOps API client
│   ├── AnalyticsService.cs            # Core analytics calculations
│   ├── CacheService.cs                # Data caching and aggregation
│   └── VisualizationService.cs        # Chart and dashboard generation
├── Models/
│   ├── SprintModels.cs                # Sprint, iteration data models
│   ├── TeamModels.cs                  # Team member, capacity models
│   ├── WorkItemModels.cs              # Work item, relationship models
│   └── MetricModels.cs                # Calculated metrics and KPIs
└── Configuration/
    ├── AzureDevOpsSettings.cs         # Connection and authentication
    ├── AnalyticsSettings.cs           # Calculation parameters
    └── CacheSettings.cs               # Caching configuration
```

## Azure DevOps Integration

### Authentication and Connection

```csharp
public class AzureDevOpsService : IAzureDevOpsService
{
    private readonly HttpClient _httpClient;
    private readonly AzureDevOpsSettings _settings;
    private readonly ILogger<AzureDevOpsService> _logger;
    
    public AzureDevOpsService(
        HttpClient httpClient,
        IOptions<AzureDevOpsSettings> settings,
        ILogger<AzureDevOpsService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        
        ConfigureHttpClient();
    }
    
    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri($"https://dev.azure.com/{_settings.Organization}/");
        
        // Personal Access Token authentication
        var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{_settings.PersonalAccessToken}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
        
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "COA-DevOps-Analytics-MCP/1.0.0");
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.RequestTimeout);
    }
    
    public async Task<List<Project>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("_apis/projects?api-version=7.0", cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ProjectsResponse>(content);
            
            _logger.LogInformation($"Retrieved {result.Value.Count} projects from Azure DevOps");
            return result.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve projects from Azure DevOps");
            throw new AzureDevOpsException("Failed to retrieve projects", ex);
        }
    }
    
    public async Task<List<Team>> GetTeamsAsync(string project, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"{project}/_apis/teams?api-version=7.0", 
            cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<TeamsResponse>(content);
        return result.Value;
    }
    
    public async Task<List<Iteration>> GetIterationsAsync(
        string project, 
        string team, 
        DateTime? startDate = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"{project}/{team}/_apis/work/teamsettings/iterations?api-version=7.0";
        
        if (startDate.HasValue)
        {
            url += $"&$filter=startDate ge {startDate.Value:yyyy-MM-dd}";
        }
        
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<IterationsResponse>(content);
        return result.Value;
    }
    
    public async Task<List<WorkItem>> GetWorkItemsAsync(
        string project,
        string iterationPath,
        CancellationToken cancellationToken = default)
    {
        // Use WIQL (Work Item Query Language) for complex queries
        var wiql = new
        {
            query = $@"
                SELECT [System.Id], [System.Title], [System.WorkItemType], 
                       [System.State], [System.AssignedTo], [System.CreatedDate],
                       [Microsoft.VSTS.Scheduling.StoryPoints], [Microsoft.VSTS.Scheduling.OriginalEstimate],
                       [Microsoft.VSTS.Scheduling.CompletedWork], [Microsoft.VSTS.Scheduling.RemainingWork]
                FROM WorkItems 
                WHERE [System.IterationPath] UNDER '{iterationPath}'
                  AND [System.WorkItemType] IN ('User Story', 'Bug', 'Task', 'Feature')
                ORDER BY [System.Id]"
        };
        
        var wiqlResponse = await _httpClient.PostAsync(
            $"{project}/_apis/wit/wiql?api-version=7.0",
            new StringContent(JsonSerializer.Serialize(wiql), Encoding.UTF8, "application/json"),
            cancellationToken);
        
        wiqlResponse.EnsureSuccessStatusCode();
        var wiqlContent = await wiqlResponse.Content.ReadAsStringAsync();
        var wiqlResult = JsonSerializer.Deserialize<WiqlResponse>(wiqlContent);
        
        if (wiqlResult.WorkItems?.Any() != true)
        {
            return new List<WorkItem>();
        }
        
        // Get detailed work item information
        var ids = string.Join(",", wiqlResult.WorkItems.Select(wi => wi.Id));
        var detailResponse = await _httpClient.GetAsync(
            $"{project}/_apis/wit/workitems?ids={ids}&$expand=all&api-version=7.0",
            cancellationToken);
        
        detailResponse.EnsureSuccessStatusCode();
        var detailContent = await detailResponse.Content.ReadAsStringAsync();
        var detailResult = JsonSerializer.Deserialize<WorkItemsResponse>(detailContent);
        
        return detailResult.Value;
    }
}
```

### Analytics Service

```csharp
public class AnalyticsService : IAnalyticsService
{
    private readonly IAzureDevOpsService _devOpsService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<AnalyticsService> _logger;
    
    public async Task<SprintAnalytics> CalculateSprintAnalyticsAsync(
        string project,
        string team,
        string iterationPath,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"sprint-analytics:{project}:{team}:{iterationPath}";
        
        if (await _cacheService.TryGetAsync<SprintAnalytics>(cacheKey) is { } cached)
        {
            return cached;
        }
        
        // Get iteration details
        var iterations = await _devOpsService.GetIterationsAsync(project, team, cancellationToken: cancellationToken);
        var currentIteration = iterations.FirstOrDefault(i => i.Path == iterationPath);
        
        if (currentIteration == null)
        {
            throw new ArgumentException($"Iteration '{iterationPath}' not found");
        }
        
        // Get work items for the iteration
        var workItems = await _devOpsService.GetWorkItemsAsync(project, iterationPath, cancellationToken);
        
        var analytics = new SprintAnalytics
        {
            Project = project,
            Team = team,
            IterationPath = iterationPath,
            StartDate = currentIteration.Attributes.StartDate,
            EndDate = currentIteration.Attributes.FinishDate,
            GeneratedAt = DateTime.UtcNow
        };
        
        // Calculate basic metrics
        analytics.TotalWorkItems = workItems.Count;
        analytics.CompletedWorkItems = workItems.Count(wi => IsCompleted(wi.Fields["System.State"]?.ToString()));
        analytics.InProgressWorkItems = workItems.Count(wi => IsInProgress(wi.Fields["System.State"]?.ToString()));
        analytics.NotStartedWorkItems = workItems.Count(wi => IsNotStarted(wi.Fields["System.State"]?.ToString()));
        
        // Story points calculation
        analytics.TotalStoryPoints = workItems
            .Where(wi => wi.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StoryPoints"))
            .Sum(wi => Convert.ToDouble(wi.Fields["Microsoft.VSTS.Scheduling.StoryPoints"] ?? 0));
            
        analytics.CompletedStoryPoints = workItems
            .Where(wi => IsCompleted(wi.Fields["System.State"]?.ToString()) && 
                         wi.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StoryPoints"))
            .Sum(wi => Convert.ToDouble(wi.Fields["Microsoft.VSTS.Scheduling.StoryPoints"] ?? 0));
        
        // Calculate burndown data
        analytics.BurndownData = await CalculateBurndownDataAsync(workItems, currentIteration, cancellationToken);
        
        // Calculate velocity (if historical data available)
        analytics.Velocity = await CalculateVelocityAsync(project, team, cancellationToken);
        
        // Predictive analytics
        analytics.PredictedCompletion = PredictSprintCompletion(analytics);
        analytics.CompletionConfidence = CalculateCompletionConfidence(analytics);
        
        // Team capacity analysis
        analytics.TeamCapacity = await CalculateTeamCapacityAsync(project, team, iterationPath, cancellationToken);
        
        // Cache results for 15 minutes
        await _cacheService.SetAsync(cacheKey, analytics, TimeSpan.FromMinutes(15));
        
        return analytics;
    }
    
    private async Task<List<BurndownDataPoint>> CalculateBurndownDataAsync(
        List<WorkItem> workItems,
        Iteration iteration,
        CancellationToken cancellationToken)
    {
        var burndownData = new List<BurndownDataPoint>();
        var totalStoryPoints = workItems
            .Where(wi => wi.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StoryPoints"))
            .Sum(wi => Convert.ToDouble(wi.Fields["Microsoft.VSTS.Scheduling.StoryPoints"] ?? 0));
        
        var workingDays = GetWorkingDays(iteration.Attributes.StartDate, iteration.Attributes.FinishDate);
        var idealBurndownRate = totalStoryPoints / workingDays.Count;
        
        for (int i = 0; i < workingDays.Count; i++)
        {
            var date = workingDays[i];
            var remainingIdeal = totalStoryPoints - (idealBurndownRate * i);
            
            // For past dates, calculate actual remaining work
            double remainingActual = totalStoryPoints;
            if (date <= DateTime.Now.Date)
            {
                remainingActual = await CalculateRemainingWorkAsync(workItems, date, cancellationToken);
            }
            
            burndownData.Add(new BurndownDataPoint
            {
                Date = date,
                RemainingIdeal = Math.Max(0, remainingIdeal),
                RemainingActual = Math.Max(0, remainingActual),
                CompletedActual = totalStoryPoints - remainingActual
            });
        }
        
        return burndownData;
    }
    
    private async Task<double> CalculateVelocityAsync(
        string project, 
        string team,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get last 6 completed iterations
            var iterations = await _devOpsService.GetIterationsAsync(
                project, 
                team, 
                DateTime.Now.AddMonths(-6),
                cancellationToken);
            
            var completedIterations = iterations
                .Where(i => i.Attributes.FinishDate < DateTime.Now)
                .OrderByDescending(i => i.Attributes.FinishDate)
                .Take(6)
                .ToList();
            
            if (completedIterations.Count == 0)
            {
                return 0;
            }
            
            var totalVelocity = 0.0;
            foreach (var iteration in completedIterations)
            {
                var workItems = await _devOpsService.GetWorkItemsAsync(project, iteration.Path, cancellationToken);
                var completedStoryPoints = workItems
                    .Where(wi => IsCompleted(wi.Fields["System.State"]?.ToString()) &&
                                 wi.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StoryPoints"))
                    .Sum(wi => Convert.ToDouble(wi.Fields["Microsoft.VSTS.Scheduling.StoryPoints"] ?? 0));
                
                totalVelocity += completedStoryPoints;
            }
            
            return totalVelocity / completedIterations.Count;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to calculate velocity, returning 0");
            return 0;
        }
    }
    
    private DateTime? PredictSprintCompletion(SprintAnalytics analytics)
    {
        if (analytics.BurndownData?.Any() != true)
            return null;
        
        var recentData = analytics.BurndownData
            .Where(bd => bd.Date <= DateTime.Now.Date)
            .TakeLast(5)
            .ToList();
        
        if (recentData.Count < 3)
            return null;
        
        // Linear regression on recent burn rate
        var burnRates = new List<double>();
        for (int i = 1; i < recentData.Count; i++)
        {
            var rate = recentData[i - 1].RemainingActual - recentData[i].RemainingActual;
            burnRates.Add(rate);
        }
        
        var averageBurnRate = burnRates.Average();
        if (averageBurnRate <= 0)
            return null;
        
        var currentRemaining = recentData.Last().RemainingActual;
        var daysToComplete = Math.Ceiling(currentRemaining / averageBurnRate);
        
        return DateTime.Now.Date.AddDays(daysToComplete);
    }
    
    private double CalculateCompletionConfidence(SprintAnalytics analytics)
    {
        if (analytics.EndDate <= DateTime.Now)
        {
            // Sprint already ended
            return analytics.CompletedStoryPoints / analytics.TotalStoryPoints;
        }
        
        var daysRemaining = (analytics.EndDate - DateTime.Now.Date).TotalDays;
        var progressRate = analytics.CompletedStoryPoints / 
                          ((DateTime.Now.Date - analytics.StartDate).TotalDays + 1);
        
        var projectedCompletion = analytics.CompletedStoryPoints + (progressRate * daysRemaining);
        var confidence = Math.Min(1.0, projectedCompletion / analytics.TotalStoryPoints);
        
        // Adjust for velocity consistency
        if (analytics.Velocity > 0)
        {
            var velocityBasedConfidence = Math.Min(1.0, analytics.Velocity / analytics.TotalStoryPoints);
            confidence = (confidence + velocityBasedConfidence) / 2;
        }
        
        return confidence;
    }
}
```

## Sprint Analytics Tool

```csharp
[Tool("sprint_analytics")]
public class SprintAnalyticsTool : AdaptiveResponseBuilder<SprintAnalyticsParams, SprintAnalyticsResult>
{
    private readonly IAnalyticsService _analyticsService;
    private readonly IVisualizationService _visualizationService;
    
    protected override string GetOperationName() => "sprint_analytics";
    
    protected override async Task<SprintAnalyticsResult> ExecuteInternalAsync(
        SprintAnalyticsParams parameters,
        CancellationToken cancellationToken)
    {
        ValidateRequired(parameters.Project, nameof(parameters.Project));
        ValidateRequired(parameters.Team, nameof(parameters.Team));
        
        var iterationPath = parameters.IterationPath ?? await GetCurrentIterationAsync(parameters.Project, parameters.Team);
        
        var analytics = await _analyticsService.CalculateSprintAnalyticsAsync(
            parameters.Project,
            parameters.Team,
            iterationPath,
            cancellationToken);
        
        var context = new ResponseContext
        {
            ResponseMode = parameters.ResponseMode ?? "full",
            TokenLimit = parameters.MaxTokens ?? 10000
        };
        
        return await BuildResponseAsync(analytics, context);
    }
    
    protected override async Task ApplyAdaptiveFormattingAsync(
        SprintAnalyticsResult result,
        SprintAnalytics analytics,
        ResponseContext context)
    {
        result.Success = true;
        result.Summary = $"Sprint Analytics: {analytics.Team} - {GetSprintStatusSummary(analytics)}";
        
        // Generate visualizations based on IDE capability
        if (_environment.SupportsHTML && context.ResponseMode == "full")
        {
            result.ResourceUri = await CreateInteractiveDashboard(analytics);
            result.IDEDisplayHint = "chart";
            result.Message = FormatSprintSummary(analytics) + $"\n\n📊 [Interactive Dashboard]({result.ResourceUri})";
        }
        else
        {
            result.Message = FormatSprintAnalyticsText(analytics);
            result.IDEDisplayHint = "markdown";
        }
        
        // Add metrics metadata
        result.Metadata = new Dictionary<string, object>
        {
            ["project"] = analytics.Project,
            ["team"] = analytics.Team,
            ["iterationPath"] = analytics.IterationPath,
            ["totalStoryPoints"] = analytics.TotalStoryPoints,
            ["completedStoryPoints"] = analytics.CompletedStoryPoints,
            ["completionPercentage"] = (analytics.CompletedStoryPoints / analytics.TotalStoryPoints) * 100,
            ["velocity"] = analytics.Velocity,
            ["daysRemaining"] = (analytics.EndDate - DateTime.Now.Date).TotalDays,
            ["completionConfidence"] = analytics.CompletionConfidence,
            ["predictedCompletion"] = analytics.PredictedCompletion
        };
        
        // Add contextual actions
        result.Actions = CreateSprintActions(analytics, parameters);
    }
    
    private string FormatSprintAnalyticsText(SprintAnalytics analytics)
    {
        var sb = new StringBuilder();
        
        // Header
        sb.AppendLine($"# 📊 Sprint Analytics: {analytics.Team}");
        sb.AppendLine($"**Iteration:** {analytics.IterationPath}");
        sb.AppendLine($"**Period:** {analytics.StartDate:MMM dd} - {analytics.EndDate:MMM dd, yyyy}");
        sb.AppendLine($"**Generated:** {analytics.GeneratedAt:MMM dd, yyyy HH:mm} UTC");
        sb.AppendLine();
        
        // Progress summary
        var completionPercent = (analytics.CompletedStoryPoints / analytics.TotalStoryPoints) * 100;
        var progressBar = GenerateProgressBar(completionPercent, 20);
        
        sb.AppendLine("## 🎯 Progress Overview");
        sb.AppendLine($"**Story Points:** {analytics.CompletedStoryPoints:F1} / {analytics.TotalStoryPoints:F1} ({completionPercent:F1}%)");
        sb.AppendLine($"{progressBar}");
        sb.AppendLine($"**Work Items:** {analytics.CompletedWorkItems} / {analytics.TotalWorkItems} completed");
        sb.AppendLine();
        
        // Status breakdown
        sb.AppendLine("### 📋 Work Item Status");
        sb.AppendLine($"- ✅ **Completed:** {analytics.CompletedWorkItems} items");
        sb.AppendLine($"- 🔄 **In Progress:** {analytics.InProgressWorkItems} items");
        sb.AppendLine($"- ⏳ **Not Started:** {analytics.NotStartedWorkItems} items");
        sb.AppendLine();
        
        // Velocity and predictions
        sb.AppendLine("## 📈 Velocity & Predictions");
        sb.AppendLine($"**Team Velocity:** {analytics.Velocity:F1} story points/sprint");
        
        if (analytics.PredictedCompletion.HasValue)
        {
            var daysToCompletion = (analytics.PredictedCompletion.Value - DateTime.Now.Date).TotalDays;
            var completionStatus = daysToCompletion <= (analytics.EndDate - DateTime.Now.Date).TotalDays 
                ? "🟢 On Track" 
                : "🔴 At Risk";
                
            sb.AppendLine($"**Predicted Completion:** {analytics.PredictedCompletion.Value:MMM dd} ({completionStatus})");
        }
        
        sb.AppendLine($"**Completion Confidence:** {analytics.CompletionConfidence:P0}");
        sb.AppendLine();
        
        // Burndown summary
        if (analytics.BurndownData?.Any() == true)
        {
            sb.AppendLine("## 🔥 Burndown Summary");
            
            var today = DateTime.Now.Date;
            var todayBurndown = analytics.BurndownData.FirstOrDefault(bd => bd.Date == today);
            
            if (todayBurndown != null)
            {
                var variance = todayBurndown.RemainingActual - todayBurndown.RemainingIdeal;
                var varianceStatus = variance > 0 ? "Behind" : variance < 0 ? "Ahead" : "On Track";
                var varianceColor = variance > 0 ? "🔴" : variance < 0 ? "🟢" : "🟡";
                
                sb.AppendLine($"**Current Status:** {varianceColor} {varianceStatus} ({Math.Abs(variance):F1} points)");
                sb.AppendLine($"**Remaining Work:** {todayBurndown.RemainingActual:F1} story points");
                sb.AppendLine($"**Ideal Remaining:** {todayBurndown.RemainingIdeal:F1} story points");
            }
            sb.AppendLine();
        }
        
        // Team capacity
        if (analytics.TeamCapacity != null)
        {
            sb.AppendLine("## 👥 Team Capacity");
            sb.AppendLine($"**Total Capacity:** {analytics.TeamCapacity.TotalCapacity:F1} hours");
            sb.AppendLine($"**Allocated Work:** {analytics.TeamCapacity.AllocatedWork:F1} hours");
            sb.AppendLine($"**Utilization:** {(analytics.TeamCapacity.AllocatedWork / analytics.TeamCapacity.TotalCapacity):P0}");
            
            if (analytics.TeamCapacity.TeamMembers?.Any() == true)
            {
                sb.AppendLine();
                sb.AppendLine("### Individual Utilization");
                foreach (var member in analytics.TeamCapacity.TeamMembers.OrderBy(m => m.Name))
                {
                    var utilization = member.Capacity > 0 ? (member.AllocatedWork / member.Capacity) : 0;
                    var utilizationIcon = utilization > 1.0 ? "🔴" : utilization > 0.8 ? "🟡" : "🟢";
                    sb.AppendLine($"- {utilizationIcon} **{member.Name}:** {utilization:P0} ({member.AllocatedWork:F1}h / {member.Capacity:F1}h)");
                }
            }
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
    
    private async Task<string> CreateInteractiveDashboard(SprintAnalytics analytics)
    {
        var dashboard = await _visualizationService.GenerateSprintDashboardAsync(analytics);
        var resourceId = Guid.NewGuid().ToString("N")[..8];
        await _resourceProvider.StoreAsync($"dashboard/{resourceId}.html", dashboard);
        return $"mcp://dashboard/{resourceId}.html";
    }
    
    private List<ActionItem> CreateSprintActions(SprintAnalytics analytics, SprintAnalyticsParams parameters)
    {
        var actions = new List<ActionItem>
        {
            new ActionItem
            {
                Title = "View Work Items",
                Command = "devops.viewWorkItems",
                Parameters = new 
                { 
                    project = analytics.Project, 
                    team = analytics.Team,
                    iterationPath = analytics.IterationPath
                }
            },
            new ActionItem
            {
                Title = "Export Sprint Report",
                Command = "devops.exportReport",
                Parameters = new 
                { 
                    project = analytics.Project, 
                    team = analytics.Team,
                    iterationPath = analytics.IterationPath,
                    format = "excel"
                }
            }
        };
        
        // Add conditional actions based on sprint status
        if (analytics.CompletionConfidence < 0.7)
        {
            actions.Add(new ActionItem
            {
                Title = "Analyze Sprint Risks",
                Command = "devops.analyzeRisks",
                Parameters = new 
                { 
                    project = analytics.Project, 
                    team = analytics.Team,
                    iterationPath = analytics.IterationPath
                }
            });
        }
        
        if (analytics.TeamCapacity?.TeamMembers?.Any(m => m.AllocatedWork / m.Capacity > 1.0) == true)
        {
            actions.Add(new ActionItem
            {
                Title = "Review Team Capacity",
                Command = "devops.reviewCapacity", 
                Parameters = new 
                { 
                    project = analytics.Project, 
                    team = analytics.Team,
                    iterationPath = analytics.IterationPath
                }
            });
        }
        
        return actions;
    }
    
    private string GenerateProgressBar(double percentage, int length)
    {
        var filled = (int)(percentage / 100.0 * length);
        var empty = length - filled;
        
        return $"[{'█'.Repeat(filled)}{'░'.Repeat(empty)}] {percentage:F1}%";
    }
    
    private string GetSprintStatusSummary(SprintAnalytics analytics)
    {
        var completionPercent = (analytics.CompletedStoryPoints / analytics.TotalStoryPoints) * 100;
        var daysRemaining = Math.Max(0, (analytics.EndDate - DateTime.Now.Date).TotalDays);
        
        if (daysRemaining == 0)
        {
            return completionPercent >= 100 ? "Sprint Completed Successfully" : $"Sprint Ended - {completionPercent:F0}% Complete";
        }
        
        var status = analytics.CompletionConfidence switch
        {
            >= 0.8 => "On Track",
            >= 0.6 => "At Risk", 
            _ => "Behind Schedule"
        };
        
        return $"{completionPercent:F0}% Complete - {status} ({daysRemaining:F0} days remaining)";
    }
}
```

## Visualization Service

```csharp
public class VisualizationService : IVisualizationService
{
    public async Task<string> GenerateSprintDashboardAsync(SprintAnalytics analytics)
    {
        var html = new StringBuilder();
        
        html.AppendLine(GenerateHTMLHeader("Sprint Analytics Dashboard"));
        html.AppendLine(GenerateDashboardCSS());
        
        html.AppendLine("<body>");
        
        // Dashboard header
        html.AppendLine($"<div class='dashboard-header'>");
        html.AppendLine($"    <h1>📊 Sprint Analytics: {analytics.Team}</h1>");
        html.AppendLine($"    <div class='sprint-info'>");
        html.AppendLine($"        <span><strong>Project:</strong> {analytics.Project}</span>");
        html.AppendLine($"        <span><strong>Iteration:</strong> {analytics.IterationPath}</span>");
        html.AppendLine($"        <span><strong>Period:</strong> {analytics.StartDate:MMM dd} - {analytics.EndDate:MMM dd, yyyy}</span>");
        html.AppendLine($"    </div>");
        html.AppendLine($"</div>");
        
        // KPI cards
        html.AppendLine(GenerateKPICards(analytics));
        
        // Charts container
        html.AppendLine("<div class='charts-container'>");
        
        // Burndown chart
        html.AppendLine("<div class='chart-card'>");
        html.AppendLine("    <h3>🔥 Burndown Chart</h3>");
        html.AppendLine("    <canvas id='burndownChart'></canvas>");
        html.AppendLine("</div>");
        
        // Work item distribution
        html.AppendLine("<div class='chart-card'>");
        html.AppendLine("    <h3>📋 Work Item Status</h3>");
        html.AppendLine("    <canvas id='statusChart'></canvas>");
        html.AppendLine("</div>");
        
        // Team capacity
        if (analytics.TeamCapacity?.TeamMembers?.Any() == true)
        {
            html.AppendLine("<div class='chart-card full-width'>");
            html.AppendLine("    <h3>👥 Team Capacity Utilization</h3>");
            html.AppendLine("    <canvas id='capacityChart'></canvas>");
            html.AppendLine("</div>");
        }
        
        html.AppendLine("</div>"); // charts-container
        
        // Data tables section
        html.AppendLine(GenerateDataTables(analytics));
        
        // JavaScript for charts
        html.AppendLine(GenerateChartJavaScript(analytics));
        
        html.AppendLine("</body></html>");
        
        return html.ToString();
    }
    
    private string GenerateKPICards(SprintAnalytics analytics)
    {
        var completionPercent = (analytics.CompletedStoryPoints / analytics.TotalStoryPoints) * 100;
        var daysRemaining = Math.Max(0, (analytics.EndDate - DateTime.Now.Date).TotalDays);
        
        var html = new StringBuilder();
        html.AppendLine("<div class='kpi-cards'>");
        
        // Progress card
        html.AppendLine("<div class='kpi-card progress-card'>");
        html.AppendLine($"    <div class='kpi-value'>{completionPercent:F1}%</div>");
        html.AppendLine($"    <div class='kpi-label'>Sprint Progress</div>");
        html.AppendLine($"    <div class='kpi-detail'>{analytics.CompletedStoryPoints:F1} / {analytics.TotalStoryPoints:F1} points</div>");
        html.AppendLine("</div>");
        
        // Velocity card
        html.AppendLine("<div class='kpi-card velocity-card'>");
        html.AppendLine($"    <div class='kpi-value'>{analytics.Velocity:F1}</div>");
        html.AppendLine($"    <div class='kpi-label'>Team Velocity</div>");
        html.AppendLine($"    <div class='kpi-detail'>story points/sprint</div>");
        html.AppendLine("</div>");
        
        // Days remaining card
        html.AppendLine("<div class='kpi-card days-card'>");
        html.AppendLine($"    <div class='kpi-value'>{daysRemaining:F0}</div>");
        html.AppendLine($"    <div class='kpi-label'>Days Remaining</div>");
        html.AppendLine($"    <div class='kpi-detail'>until {analytics.EndDate:MMM dd}</div>");
        html.AppendLine("</div>");
        
        // Confidence card
        var confidenceColor = analytics.CompletionConfidence >= 0.8 ? "success" : 
                             analytics.CompletionConfidence >= 0.6 ? "warning" : "danger";
        html.AppendLine($"<div class='kpi-card confidence-card {confidenceColor}'>");
        html.AppendLine($"    <div class='kpi-value'>{analytics.CompletionConfidence:P0}</div>");
        html.AppendLine($"    <div class='kpi-label'>Completion Confidence</div>");
        html.AppendLine($"    <div class='kpi-detail'>based on current trend</div>");
        html.AppendLine("</div>");
        
        html.AppendLine("</div>");
        return html.ToString();
    }
    
    private string GenerateChartJavaScript(SprintAnalytics analytics)
    {
        var js = new StringBuilder();
        
        js.AppendLine("<script src='https://cdn.jsdelivr.net/npm/chart.js'></script>");
        js.AppendLine("<script>");
        
        // Burndown chart
        if (analytics.BurndownData?.Any() == true)
        {
            js.AppendLine("// Burndown Chart");
            js.AppendLine("const burndownCtx = document.getElementById('burndownChart').getContext('2d');");
            js.AppendLine("new Chart(burndownCtx, {");
            js.AppendLine("    type: 'line',");
            js.AppendLine("    data: {");
            js.AppendLine($"        labels: {JsonSerializer.Serialize(analytics.BurndownData.Select(bd => bd.Date.ToString("MMM dd")).ToArray())},");
            js.AppendLine("        datasets: [{");
            js.AppendLine("            label: 'Ideal Burndown',");
            js.AppendLine($"            data: {JsonSerializer.Serialize(analytics.BurndownData.Select(bd => bd.RemainingIdeal).ToArray())},");
            js.AppendLine("            borderColor: '#17a2b8',");
            js.AppendLine("            backgroundColor: 'transparent',");
            js.AppendLine("            borderDash: [5, 5]");
            js.AppendLine("        }, {");
            js.AppendLine("            label: 'Actual Burndown',");
            js.AppendLine($"            data: {JsonSerializer.Serialize(analytics.BurndownData.Select(bd => bd.RemainingActual).ToArray())},");
            js.AppendLine("            borderColor: '#dc3545',");
            js.AppendLine("            backgroundColor: 'rgba(220, 53, 69, 0.1)',");
            js.AppendLine("            fill: true");
            js.AppendLine("        }]");
            js.AppendLine("    },");
            js.AppendLine("    options: {");
            js.AppendLine("        responsive: true,");
            js.AppendLine("        scales: {");
            js.AppendLine("            y: { beginAtZero: true, title: { display: true, text: 'Story Points' } },");
            js.AppendLine("            x: { title: { display: true, text: 'Date' } }");
            js.AppendLine("        },");
            js.AppendLine("        plugins: { legend: { display: true } }");
            js.AppendLine("    }");
            js.AppendLine("});");
        }
        
        // Status chart
        js.AppendLine("// Status Chart");
        js.AppendLine("const statusCtx = document.getElementById('statusChart').getContext('2d');");
        js.AppendLine("new Chart(statusCtx, {");
        js.AppendLine("    type: 'doughnut',");
        js.AppendLine("    data: {");
        js.AppendLine($"        labels: ['Completed', 'In Progress', 'Not Started'],");
        js.AppendLine($"        datasets: [{{");
        js.AppendLine($"            data: [{analytics.CompletedWorkItems}, {analytics.InProgressWorkItems}, {analytics.NotStartedWorkItems}],");
        js.AppendLine("            backgroundColor: ['#28a745', '#ffc107', '#6c757d']");
        js.AppendLine("        }]");
        js.AppendLine("    },");
        js.AppendLine("    options: {");
        js.AppendLine("        responsive: true,");
        js.AppendLine("        plugins: {");
        js.AppendLine("            legend: { position: 'bottom' }");
        js.AppendLine("        }");
        js.AppendLine("    }");
        js.AppendLine("});");
        
        // Capacity chart
        if (analytics.TeamCapacity?.TeamMembers?.Any() == true)
        {
            js.AppendLine("// Capacity Chart");
            js.AppendLine("const capacityCtx = document.getElementById('capacityChart').getContext('2d');");
            js.AppendLine("new Chart(capacityCtx, {");
            js.AppendLine("    type: 'bar',");
            js.AppendLine("    data: {");
            js.AppendLine($"        labels: {JsonSerializer.Serialize(analytics.TeamCapacity.TeamMembers.Select(tm => tm.Name).ToArray())},");
            js.AppendLine("        datasets: [{");
            js.AppendLine("            label: 'Capacity',");
            js.AppendLine($"            data: {JsonSerializer.Serialize(analytics.TeamCapacity.TeamMembers.Select(tm => tm.Capacity).ToArray())},");
            js.AppendLine("            backgroundColor: '#17a2b8'");
            js.AppendLine("        }, {");
            js.AppendLine("            label: 'Allocated Work',");
            js.AppendLine($"            data: {JsonSerializer.Serialize(analytics.TeamCapacity.TeamMembers.Select(tm => tm.AllocatedWork).ToArray())},");
            js.AppendLine("            backgroundColor: '#dc3545'");
            js.AppendLine("        }]");
            js.AppendLine("    },");
            js.AppendLine("    options: {");
            js.AppendLine("        responsive: true,");
            js.AppendLine("        scales: {");
            js.AppendLine("            y: { beginAtZero: true, title: { display: true, text: 'Hours' } },");
            js.AppendLine("            x: { title: { display: true, text: 'Team Members' } }");
            js.AppendLine("        }");
            js.AppendLine("    }");
            js.AppendLine("});");
        }
        
        js.AppendLine("</script>");
        
        return js.ToString();
    }
}
```

## Configuration and Setup

### Configuration Models

```csharp
public class AzureDevOpsSettings
{
    public string Organization { get; set; } = "";
    public string PersonalAccessToken { get; set; } = "";
    public int RequestTimeout { get; set; } = 30;
    public int CacheExpirationMinutes { get; set; } = 15;
    public List<string> DefaultProjects { get; set; } = new();
}

public class AnalyticsSettings
{
    public int VelocityCalculationSprints { get; set; } = 6;
    public double MinimumCompletionConfidence { get; set; } = 0.5;
    public bool EnablePredictiveAnalytics { get; set; } = true;
    public bool IncludeWeekends { get; set; } = false;
}
```

### Program.cs Setup

```csharp
var builder = new McpServerBuilder()
    .WithServerInfo("DevOps Analytics MCP", "1.0.0")
    .ConfigureLogging(logging =>
    {
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
    });

// Configuration
builder.Services.Configure<AzureDevOpsSettings>(builder.Configuration.GetSection("AzureDevOps"));
builder.Services.Configure<AnalyticsSettings>(builder.Configuration.GetSection("Analytics"));

// HTTP client for Azure DevOps
builder.Services.AddHttpClient<IAzureDevOpsService, AzureDevOpsService>();

// Services
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IVisualizationService, VisualizationService>();
builder.Services.AddSingleton<ICacheService, MemoryCacheService>();

// Memory cache
builder.Services.AddMemoryCache();

// Tools
builder.RegisterToolType<SprintAnalyticsTool>();
builder.RegisterToolType<TeamMetricsTool>();
builder.RegisterToolType<PortfolioAnalyticsTool>();
builder.RegisterToolType<WorkItemAnalyticsTool>();
builder.RegisterToolType<BuildMetricsTool>();
builder.RegisterToolType<PredictiveAnalyticsTool>();

// HTTP transport for API access
builder.UseHttpTransport(options =>
{
    options.Port = 5002;
    options.AllowedOrigins = new[] { "*" };
});

await builder.RunAsync();
```

### Configuration File

```json
{
  "AzureDevOps": {
    "Organization": "your-organization",
    "PersonalAccessToken": "your-pat-token",
    "RequestTimeout": 30,
    "CacheExpirationMinutes": 15,
    "DefaultProjects": ["Project1", "Project2"]
  },
  "Analytics": {
    "VelocityCalculationSprints": 6,
    "MinimumCompletionConfidence": 0.5,
    "EnablePredictiveAnalytics": true,
    "IncludeWeekends": false
  }
}
```

## Security and Performance Considerations

### Authentication
- Personal Access Token (PAT) with minimal required scopes
- Secure credential storage and rotation
- Request rate limiting and retry logic

### Caching Strategy
- 15-minute cache for analytics data
- Intelligent cache invalidation
- Memory usage monitoring and cleanup

### Performance Optimization
- Parallel API requests where possible
- Data pagination for large datasets
- Resource generation for complex visualizations
- Async/await throughout for non-blocking operations

This DevOps Analytics MCP specification provides comprehensive Azure DevOps integration with rich visualizations and predictive capabilities, optimized for multi-IDE environments and intelligent token management.