using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;
using COA.Mcp.Framework.Migration.Analyzers;
using COA.Mcp.Framework.Migration.Migrators;

namespace COA.Mcp.Framework.Migration;

/// <summary>
/// Orchestrates the entire migration process from analysis to code transformation
/// </summary>
public class MigrationOrchestrator
{
    private readonly ILogger<MigrationOrchestrator> _logger;
    private readonly MigrationAnalyzer _analyzer;
    private readonly List<IMigrator> _migrators;
    private readonly MSBuildWorkspace _workspace;

    public MigrationOrchestrator(ILogger<MigrationOrchestrator> logger)
    {
        _logger = logger;
        
        // Create analyzer
        var analyzerLogger = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => { })
            .CreateLogger<MigrationAnalyzer>();
        _analyzer = new MigrationAnalyzer(analyzerLogger);

        // Create migrators
        var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => { });
        _migrators = new List<IMigrator>
        {
            new ToolRegistrationMigrator(loggerFactory.CreateLogger<ToolRegistrationMigrator>()),
            new ResponseFormatMigrator(loggerFactory.CreateLogger<ResponseFormatMigrator>()),
            new TokenManagementMigrator(loggerFactory.CreateLogger<TokenManagementMigrator>())
        };

        // Create workspace
        _workspace = MSBuildWorkspace.Create();
    }

    /// <summary>
    /// Performs a complete migration of a project
    /// </summary>
    public async Task<MigrationSummary> MigrateProjectAsync(string projectPath, MigrationOptions options)
    {
        var summary = new MigrationSummary
        {
            ProjectPath = projectPath,
            StartTime = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Starting migration for project: {ProjectPath}", projectPath);

            // Step 1: Analyze the project
            var projectDir = Path.GetDirectoryName(projectPath) ?? throw new InvalidOperationException("Invalid project path");
            var report = await _analyzer.AnalyzeProjectAsync(projectDir);
            summary.AnalysisReport = report;

            if (report.Patterns.Count == 0)
            {
                _logger.LogInformation("No migration patterns found. Project may already be using the framework.");
                summary.Success = true;
                summary.EndTime = DateTime.UtcNow;
                return summary;
            }

            // Step 2: Load the project
            var project = await _workspace.OpenProjectAsync(projectPath);
            summary.ProjectName = project.Name;

            // Step 3: Apply migrations
            if (!options.DryRun)
            {
                var migratedDocuments = new Dictionary<DocumentId, Document>();
                
                foreach (var pattern in report.Patterns)
                {
                    if (!pattern.CanBeAutomated && !options.IncludeManualMigrations)
                    {
                        summary.SkippedPatterns.Add(pattern);
                        continue;
                    }

                    var result = await MigratePatternAsync(project, pattern, migratedDocuments);
                    if (result.Success)
                    {
                        summary.SuccessfulMigrations.Add((pattern, result));
                        if (result.ModifiedDocument != null)
                        {
                            migratedDocuments[result.ModifiedDocument.Id] = result.ModifiedDocument;
                        }
                    }
                    else
                    {
                        summary.FailedMigrations.Add((pattern, result));
                    }
                }

                // Step 4: Apply changes to workspace
                if (migratedDocuments.Any())
                {
                    foreach (var (docId, doc) in migratedDocuments)
                    {
                        project = doc.Project;
                    }

                    // Save changes
                    if (options.SaveChanges)
                    {
                        var solution = project.Solution;
                        foreach (var doc in migratedDocuments.Values)
                        {
                            var text = await doc.GetTextAsync();
                            await File.WriteAllTextAsync(doc.FilePath!, text.ToString());
                        }
                    }
                }

                // Step 5: Update project file
                if (options.UpdateProjectFile)
                {
                    await UpdateProjectFileAsync(projectPath);
                    summary.ProjectFileUpdated = true;
                }
            }

            summary.Success = summary.FailedMigrations.Count == 0;
            summary.EndTime = DateTime.UtcNow;

            _logger.LogInformation("Migration completed. Success: {Success}, Migrated: {MigratedCount}, Failed: {FailedCount}", 
                summary.Success, summary.SuccessfulMigrations.Count, summary.FailedMigrations.Count);

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration failed for project: {ProjectPath}", projectPath);
            summary.Success = false;
            summary.Error = ex.Message;
            summary.EndTime = DateTime.UtcNow;
            return summary;
        }
    }

    private async Task<MigrationResult> MigratePatternAsync(
        Project project, 
        PatternMatch pattern, 
        Dictionary<DocumentId, Document> migratedDocuments)
    {
        try
        {
            // Find the appropriate migrator
            var migrator = _migrators.FirstOrDefault(m => m.CanMigrate(pattern));
            if (migrator == null)
            {
                return new MigrationResult
                {
                    Success = false,
                    Error = $"No migrator found for pattern type: {pattern.Type}"
                };
            }

            // Find the document
            var document = project.Documents
                .FirstOrDefault(d => d.FilePath == pattern.FilePath);
            
            if (document == null)
            {
                return new MigrationResult
                {
                    Success = false,
                    Error = $"Document not found: {pattern.FilePath}"
                };
            }

            // Check if we have a more recent version from previous migrations
            if (migratedDocuments.TryGetValue(document.Id, out var migratedDoc))
            {
                document = migratedDoc;
            }

            // Apply the migration
            var result = await migrator.MigrateAsync(pattern, document);
            
            if (result.Success && result.ModifiedDocument != null)
            {
                migratedDocuments[document.Id] = result.ModifiedDocument;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to migrate pattern {PatternType} at {FilePath}:{Line}", 
                pattern.Type, pattern.FilePath, pattern.Line);
            
            return new MigrationResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task UpdateProjectFileAsync(string projectPath)
    {
        var projectContent = await File.ReadAllTextAsync(projectPath);
        
        // Check if framework packages are already referenced
        if (projectContent.Contains("COA.Mcp.Framework"))
        {
            return;
        }

        // Find the ItemGroup with PackageReference elements
        var lines = projectContent.Split('\n').ToList();
        var packageGroupIndex = -1;
        
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Contains("<ItemGroup>") && 
                i + 1 < lines.Count && 
                lines[i + 1].Contains("<PackageReference"))
            {
                packageGroupIndex = i;
                break;
            }
        }

        if (packageGroupIndex == -1)
        {
            // Create a new ItemGroup
            var projectEndIndex = lines.FindIndex(l => l.Contains("</Project>"));
            if (projectEndIndex > 0)
            {
                lines.Insert(projectEndIndex, "  <ItemGroup>");
                lines.Insert(projectEndIndex + 1, "    <PackageReference Include=\"COA.Mcp.Framework\" Version=\"1.0.0\" />");
                lines.Insert(projectEndIndex + 2, "    <PackageReference Include=\"COA.Mcp.Framework.TokenOptimization\" Version=\"1.0.0\" />");
                lines.Insert(projectEndIndex + 3, "  </ItemGroup>");
                lines.Insert(projectEndIndex + 4, "");
            }
        }
        else
        {
            // Add to existing ItemGroup
            var insertIndex = packageGroupIndex + 1;
            while (insertIndex < lines.Count && !lines[insertIndex].Contains("</ItemGroup>"))
            {
                insertIndex++;
            }
            
            lines.Insert(insertIndex, "    <PackageReference Include=\"COA.Mcp.Framework\" Version=\"1.0.0\" />");
            lines.Insert(insertIndex + 1, "    <PackageReference Include=\"COA.Mcp.Framework.TokenOptimization\" Version=\"1.0.0\" />");
        }

        await File.WriteAllTextAsync(projectPath, string.Join('\n', lines));
    }

    public void Dispose()
    {
        _workspace?.Dispose();
    }
}

public class MigrationOptions
{
    public bool DryRun { get; set; }
    public bool SaveChanges { get; set; } = true;
    public bool UpdateProjectFile { get; set; } = true;
    public bool IncludeManualMigrations { get; set; }
    public bool CreateBackups { get; set; } = true;
}

public class MigrationSummary
{
    public string ProjectPath { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public MigrationReport? AnalysisReport { get; set; }
    public bool ProjectFileUpdated { get; set; }
    
    public List<PatternMatch> SkippedPatterns { get; set; } = new();
    public List<(PatternMatch Pattern, MigrationResult Result)> SuccessfulMigrations { get; set; } = new();
    public List<(PatternMatch Pattern, MigrationResult Result)> FailedMigrations { get; set; } = new();
    
    public TimeSpan Duration => EndTime - StartTime;
}