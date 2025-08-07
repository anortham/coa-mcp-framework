using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace COA.Mcp.Framework.Migration.Analyzers;

/// <summary>
/// Analyzes existing MCP projects to identify migration opportunities and patterns
/// </summary>
public class MigrationAnalyzer
{
    private readonly ILogger<MigrationAnalyzer> _logger;
    private readonly List<IPatternAnalyzer> _patternAnalyzers;

    public MigrationAnalyzer(ILogger<MigrationAnalyzer> logger)
    {
        _logger = logger;
        _patternAnalyzers = new List<IPatternAnalyzer>
        {
            new ToolRegistrationPatternAnalyzer(),
            new ResponseFormatPatternAnalyzer(),
            new TokenManagementPatternAnalyzer(),
            new AttributePatternAnalyzer(),
            new BaseClassPatternAnalyzer()
        };
    }

    /// <summary>
    /// Analyzes a project directory for MCP patterns and generates a migration report
    /// </summary>
    public async Task<MigrationReport> AnalyzeProjectAsync(string projectPath)
    {
        _logger.LogInformation("Starting migration analysis for project: {ProjectPath}", projectPath);

        var report = new MigrationReport
        {
            ProjectPath = projectPath,
            AnalysisTimestamp = DateTime.UtcNow,
            Patterns = new List<PatternMatch>(),
            EstimatedEffort = EffortLevel.Unknown,
            Recommendations = new List<MigrationRecommendation>()
        };

        // Find all C# files
        var matcher = new Matcher();
        matcher.AddInclude("**/*.cs");
        matcher.AddExclude("**/bin/**");
        matcher.AddExclude("**/obj/**");
        matcher.AddExclude("**/.vs/**");

        var files = matcher.GetResultsInFullPath(projectPath);
        report.TotalFiles = files.Count();

        // Analyze each file
        foreach (var file in files)
        {
            try
            {
                var fileContent = await File.ReadAllTextAsync(file);
                var tree = CSharpSyntaxTree.ParseText(fileContent);
                var root = await tree.GetRootAsync();

                // Run each pattern analyzer
                foreach (var analyzer in _patternAnalyzers)
                {
                    var patterns = await analyzer.AnalyzeAsync(root, file);
                    report.Patterns.AddRange(patterns);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to analyze file: {File}", file);
                report.Errors.Add($"Failed to analyze {file}: {ex.Message}");
            }
        }

        // Categorize patterns and generate recommendations
        report.PatternSummary = CategorizePatterns(report.Patterns);
        report.Recommendations = GenerateRecommendations(report.PatternSummary);
        report.EstimatedEffort = EstimateEffort(report.PatternSummary);

        _logger.LogInformation("Migration analysis completed. Found {PatternCount} patterns requiring migration", 
            report.Patterns.Count);

        return report;
    }

    private Dictionary<string, PatternSummary> CategorizePatterns(List<PatternMatch> patterns)
    {
        var summary = new Dictionary<string, PatternSummary>();

        foreach (var pattern in patterns)
        {
            if (!summary.ContainsKey(pattern.Type))
            {
                summary[pattern.Type] = new PatternSummary
                {
                    Type = pattern.Type,
                    Count = 0,
                    Files = new HashSet<string>(),
                    Examples = new List<PatternMatch>()
                };
            }

            summary[pattern.Type].Count++;
            summary[pattern.Type].Files.Add(pattern.FilePath);
            
            // Keep up to 3 examples
            if (summary[pattern.Type].Examples.Count < 3)
            {
                summary[pattern.Type].Examples.Add(pattern);
            }
        }

        return summary;
    }

    private List<MigrationRecommendation> GenerateRecommendations(Dictionary<string, PatternSummary> patternSummary)
    {
        var recommendations = new List<MigrationRecommendation>();

        // Tool registration recommendations
        if (patternSummary.ContainsKey("ManualToolRegistration"))
        {
            recommendations.Add(new MigrationRecommendation
            {
                Priority = Priority.High,
                Category = "Tool Registration",
                Title = "Migrate to Attribute-Based Tool Registration",
                Description = "Replace manual tool registration with [McpServerToolType] and [McpServerTool] attributes",
                AutomationAvailable = true,
                EstimatedTimeMinutes = patternSummary["ManualToolRegistration"].Count * 5
            });
        }

        // Response format recommendations
        if (patternSummary.ContainsKey("LegacyResponseFormat"))
        {
            recommendations.Add(new MigrationRecommendation
            {
                Priority = Priority.High,
                Category = "Response Format",
                Title = "Adopt AI-Optimized Response Format",
                Description = "Update responses to use framework's AI-optimized format with insights and actions",
                AutomationAvailable = true,
                EstimatedTimeMinutes = patternSummary["LegacyResponseFormat"].Count * 10
            });
        }

        // Token management recommendations
        if (patternSummary.ContainsKey("ManualTokenCounting"))
        {
            recommendations.Add(new MigrationRecommendation
            {
                Priority = Priority.Medium,
                Category = "Token Management",
                Title = "Use Framework Token Management",
                Description = "Replace manual token counting with framework's TokenEstimator and progressive reduction",
                AutomationAvailable = true,
                EstimatedTimeMinutes = patternSummary["ManualTokenCounting"].Count * 15
            });
        }

        // Base class recommendations
        if (patternSummary.ContainsKey("NoBaseClass"))
        {
            recommendations.Add(new MigrationRecommendation
            {
                Priority = Priority.Medium,
                Category = "Tool Structure",
                Title = "Inherit from McpToolBase",
                Description = "Extend McpToolBase to get validation helpers and token management",
                AutomationAvailable = true,
                EstimatedTimeMinutes = patternSummary["NoBaseClass"].Count * 10
            });
        }

        // Testing recommendations
        if (patternSummary.ContainsKey("LegacyTestPattern"))
        {
            recommendations.Add(new MigrationRecommendation
            {
                Priority = Priority.Low,
                Category = "Testing",
                Title = "Migrate to Framework Testing Infrastructure",
                Description = "Use ToolTestBase and fluent assertions for better test maintainability",
                AutomationAvailable = false,
                EstimatedTimeMinutes = patternSummary["LegacyTestPattern"].Count * 20
            });
        }

        return recommendations.OrderBy(r => r.Priority).ToList();
    }

    private EffortLevel EstimateEffort(Dictionary<string, PatternSummary> patternSummary)
    {
        var totalPatterns = patternSummary.Values.Sum(p => p.Count);

        if (totalPatterns == 0) return EffortLevel.None;
        if (totalPatterns <= 10) return EffortLevel.Low;
        if (totalPatterns <= 50) return EffortLevel.Medium;
        if (totalPatterns <= 100) return EffortLevel.High;
        return EffortLevel.VeryHigh;
    }

    public Task<string> GenerateReportAsync(MigrationReport report, ReportFormat format = ReportFormat.Markdown)
    {
        var result = format switch
        {
            ReportFormat.Json => JsonSerializer.Serialize(report, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }),
            ReportFormat.Markdown => GenerateMarkdownReport(report),
            _ => throw new NotSupportedException($"Report format {format} is not supported")
        };
        
        return Task.FromResult(result);
    }

    private string GenerateMarkdownReport(MigrationReport report)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"# Migration Analysis Report");
        sb.AppendLine($"**Project:** {report.ProjectPath}");
        sb.AppendLine($"**Analysis Date:** {report.AnalysisTimestamp:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Total Files Analyzed:** {report.TotalFiles}");
        sb.AppendLine($"**Estimated Effort:** {report.EstimatedEffort}");
        sb.AppendLine();

        sb.AppendLine("## Pattern Summary");
        sb.AppendLine();
        sb.AppendLine("| Pattern Type | Count | Files Affected |");
        sb.AppendLine("|--------------|-------|----------------|");
        
        foreach (var pattern in report.PatternSummary.Values.OrderByDescending(p => p.Count))
        {
            sb.AppendLine($"| {pattern.Type} | {pattern.Count} | {pattern.Files.Count} |");
        }
        sb.AppendLine();

        sb.AppendLine("## Recommendations");
        sb.AppendLine();

        foreach (var rec in report.Recommendations)
        {
            sb.AppendLine($"### {rec.Title}");
            sb.AppendLine($"**Priority:** {rec.Priority}");
            sb.AppendLine($"**Category:** {rec.Category}");
            sb.AppendLine($"**Automation Available:** {(rec.AutomationAvailable ? "Yes" : "No")}");
            sb.AppendLine($"**Estimated Time:** {rec.EstimatedTimeMinutes} minutes");
            sb.AppendLine();
            sb.AppendLine(rec.Description);
            sb.AppendLine();
        }

        if (report.Errors.Any())
        {
            sb.AppendLine("## Analysis Errors");
            sb.AppendLine();
            foreach (var error in report.Errors)
            {
                sb.AppendLine($"- {error}");
            }
        }

        return sb.ToString();
    }
}

public enum ReportFormat
{
    Markdown,
    Json
}

public enum EffortLevel
{
    None,
    Low,
    Medium,
    High,
    VeryHigh,
    Unknown
}

public enum Priority
{
    High = 1,
    Medium = 2,
    Low = 3
}

public class MigrationReport
{
    public string ProjectPath { get; set; } = "";
    public DateTime AnalysisTimestamp { get; set; }
    public int TotalFiles { get; set; }
    public List<PatternMatch> Patterns { get; set; } = new();
    public Dictionary<string, PatternSummary> PatternSummary { get; set; } = new();
    public List<MigrationRecommendation> Recommendations { get; set; } = new();
    public EffortLevel EstimatedEffort { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class PatternMatch
{
    public string Type { get; set; } = "";
    public string FilePath { get; set; } = "";
    public int Line { get; set; }
    public int Column { get; set; }
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public bool CanBeAutomated { get; set; }
}

public class PatternSummary
{
    public string Type { get; set; } = "";
    public int Count { get; set; }
    public HashSet<string> Files { get; set; } = new();
    public List<PatternMatch> Examples { get; set; } = new();
}

public class MigrationRecommendation
{
    public Priority Priority { get; set; }
    public string Category { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public bool AutomationAvailable { get; set; }
    public int EstimatedTimeMinutes { get; set; }
}