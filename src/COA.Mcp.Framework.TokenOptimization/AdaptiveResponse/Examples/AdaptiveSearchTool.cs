using System.Data;
using COA.Mcp.Framework.Attributes;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Formatters;
using COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Models;
using COA.Mcp.Framework.TokenOptimization.ResponseBuilders;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Examples;

/// <summary>
/// Example search parameters for demonstration.
/// </summary>
public class SearchParams
{
    public string Query { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public string? ResponseMode { get; set; } = "full";
    public int? MaxTokens { get; set; }
}

/// <summary>
/// Example search result model.
/// </summary>
public class SearchMatch
{
    public string FilePath { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
    public string ContextLine { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string? CodePreview { get; set; }
    public string? ProjectName { get; set; }
}

/// <summary>
/// Adaptive search result that extends ToolResultBase.
/// </summary>
public class AdaptiveSearchResult : AdaptiveToolResult
{
    public List<SearchMatch>? Results { get; set; }
    public int TotalMatches { get; set; }
    public string? Query { get; set; }
}

/// <summary>
/// Example tool demonstrating the adaptive response framework.
/// Shows how to implement IDE-aware search results with intelligent formatting.
/// Note: In real usage, this would be in an MCP server project that references the framework.
/// </summary>
public class AdaptiveSearchTool : AdaptiveResponseBuilder<SearchParams, AdaptiveSearchResult>
{
    public AdaptiveSearchTool(ILogger<AdaptiveSearchTool>? logger = null) : base(logger) { }
    
    protected override string GetOperationName() => "adaptive_search";
    
    /// <summary>
    /// Builds the search response using the adaptive system.
    /// This overrides the base BuildResponseAsync to provide custom search logic.
    /// </summary>
    public override async Task<AdaptiveSearchResult> BuildResponseAsync(SearchParams data, ResponseContext context)
    {
        // Use the base adaptive response building
        return await base.BuildResponseAsync(data, context);
    }
    
    protected override async Task ApplyAdaptiveFormattingAsync(AdaptiveSearchResult result, SearchParams data, ResponseContext context)
    {
        var formatter = _formatterFactory.CreateInlineFormatter(_environment);
        var searchResults = await SimulateSearchAsync(data);
        
        // Set basic result properties
        result.Query = data.Query;
        result.Results = searchResults;
        result.TotalMatches = searchResults.Count;
        result.Summary = $"Found {searchResults.Count} matches for '{data.Query}'";
        
        // Format the main message
        var formattingContext = new FormattingContext
        {
            Environment = _environment,
            ResponseMode = context.ResponseMode,
            TokenLimit = context.TokenLimit,
            WorkspaceRoot = Environment.GetEnvironmentVariable("WORKSPACE_ROOT")
        };
        
        // Generate summary
        result.Message = formatter.FormatSummary(result.Summary, searchResults);
        
        // Convert search matches to file references
        result.FileReferences = searchResults.Select(match => new FileReference
        {
            FilePath = match.FilePath,
            Line = match.Line,
            Column = match.Column,
            Description = match.ContextLine,
            Language = match.Language,
            CodePreview = match.CodePreview,
            ProjectName = match.ProjectName,
            RelativePath = GetRelativePath(match.FilePath)
        }).ToList();
        
        // Format file references
        if (result.FileReferences.Any())
        {
            result.Message += formatter.FormatFileReferences(result.FileReferences);
        }
        
        // Generate IDE-specific actions
        result.ActionItems = GenerateSearchActions(data, searchResults);
        if (result.ActionItems.Any())
        {
            result.Message += formatter.FormatActions(result.ActionItems);
        }
        
        // Set IDE display hint
        result.IDEDisplayHint = DetermineDisplayHint(searchResults.Count);
        
        // Add metadata
        result.Metadata = CreateResponseMetadata(data, formattingContext);
        
        // Create data table for demonstration (if many results)
        if (searchResults.Count > 20)
        {
            var dataTable = CreateSearchResultsTable(searchResults);
            result.Message += formatter.FormatTable(dataTable);
        }
    }
    
    /// <summary>
    /// Simulates a search operation returning mock results.
    /// </summary>
    private async Task<List<SearchMatch>> SimulateSearchAsync(SearchParams parameters)
    {
        // Simulate async search delay
        await Task.Delay(50);
        
        var results = new List<SearchMatch>();
        var random = new Random(parameters.Query.GetHashCode()); // Deterministic for demo
        
        // Generate mock search results based on query
        var fileExtensions = new[] { ".cs", ".ts", ".js", ".py", ".java", ".cpp", ".h" };
        var projectNames = new[] { "Core.Engine", "Web.Frontend", "Data.Layer", "Test.Suite", "Utils.Common" };
        var languages = new[] { "csharp", "typescript", "javascript", "python", "java", "cpp", "c" };
        
        var resultCount = Math.Min(random.Next(5, 50), 100); // Limit for demo
        
        for (int i = 0; i < resultCount; i++)
        {
            var extension = fileExtensions[random.Next(fileExtensions.Length)];
            var language = languages[random.Next(languages.Length)];
            var project = projectNames[random.Next(projectNames.Length)];
            
            results.Add(new SearchMatch
            {
                FilePath = $@"C:\source\{project}\src\Components\Example{i + 1}{extension}",
                Line = random.Next(1, 500),
                Column = random.Next(1, 80),
                ContextLine = $"Found '{parameters.Query}' in method implementation",
                Language = language,
                CodePreview = GenerateCodePreview(parameters.Query, language),
                ProjectName = project
            });
        }
        
        return results;
    }
    
    /// <summary>
    /// Generates a realistic code preview for demonstration.
    /// </summary>
    private string GenerateCodePreview(string query, string language)
    {
        return language switch
        {
            "csharp" => $"public void Process{query}() {{\n    // Implementation here\n    var result = {query}.Execute();\n}}",
            "typescript" => $"function process{query}(): void {{\n    // Implementation here\n    const result = {query}.execute();\n}}",
            "python" => $"def process_{query.ToLower()}():\n    # Implementation here\n    result = {query.ToLower()}.execute()",
            _ => $"// Found '{query}' in this context\nfunction process() {{\n    return {query};\n}}"
        };
    }
    
    /// <summary>
    /// Generates IDE-specific action items for search results.
    /// </summary>
    private List<ActionItem> GenerateSearchActions(SearchParams parameters, List<SearchMatch> results)
    {
        var actions = new List<ActionItem>();
        
        // Universal actions
        actions.Add(new ActionItem
        {
            Title = "Refine Search",
            Command = "mcp.refineSearch",
            Description = "Modify search parameters to narrow results",
            Parameters = new Dictionary<string, object> { ["originalQuery"] = parameters.Query },
            Priority = 90
        });
        
        if (results.Count > 10)
        {
            actions.Add(new ActionItem
            {
                Title = "Export Results",
                Command = "mcp.exportResults",
                Description = "Export search results to CSV or JSON",
                Parameters = new Dictionary<string, object> 
                { 
                    ["query"] = parameters.Query,
                    ["resultCount"] = results.Count
                },
                Priority = 70
            });
        }
        
        // IDE-specific actions
        if (_environment.IDE == IDEType.VSCode)
        {
            actions.Add(new ActionItem
            {
                Title = "Open in Search Editor",
                Command = "search.action.openInEditor",
                Description = "Open results in VS Code search editor",
                KeyboardShortcut = "Ctrl+Shift+F",
                Priority = 85
            });
            
            actions.Add(new ActionItem
            {
                Title = "Replace in Files",
                Command = "editor.action.startFindReplaceAction",
                Description = "Start find and replace across all matches",
                KeyboardShortcut = "Ctrl+H",
                Priority = 75
            });
        }
        else if (_environment.IDE == IDEType.VS2022)
        {
            actions.Add(new ActionItem
            {
                Title = "Find in Files",
                Command = "Edit.FindinFiles",
                Description = "Open in Find in Files window",
                KeyboardShortcut = "Ctrl+Shift+F",
                Priority = 85
            });
            
            actions.Add(new ActionItem
            {
                Title = "Replace in Files",
                Command = "Edit.ReplaceinFiles",
                Description = "Open Replace in Files dialog",
                KeyboardShortcut = "Ctrl+Shift+H",
                Priority = 75
            });
        }
        
        return actions;
    }
    
    /// <summary>
    /// Determines the appropriate display hint based on result characteristics.
    /// </summary>
    private string DetermineDisplayHint(int resultCount)
    {
        return resultCount switch
        {
            <= 10 => "markdown",
            <= 50 => "table",
            _ => "html"
        };
    }
    
    /// <summary>
    /// Creates a DataTable from search results for table formatting.
    /// </summary>
    private DataTable CreateSearchResultsTable(List<SearchMatch> results)
    {
        var table = new DataTable("SearchResults");
        
        // Define columns
        table.Columns.Add("File", typeof(string));
        table.Columns.Add("Line", typeof(int));
        table.Columns.Add("Column", typeof(int));
        table.Columns.Add("Context", typeof(string));
        table.Columns.Add("Project", typeof(string));
        table.Columns.Add("Language", typeof(string));
        
        // Add rows
        foreach (var result in results)
        {
            var row = table.NewRow();
            row["File"] = GetRelativePath(result.FilePath);
            row["Line"] = result.Line;
            row["Column"] = result.Column;
            row["Context"] = result.ContextLine;
            row["Project"] = result.ProjectName ?? "";
            row["Language"] = result.Language;
            table.Rows.Add(row);
        }
        
        return table;
    }
    
    /// <summary>
    /// Gets the relative path for a file from the workspace root.
    /// </summary>
    private string GetRelativePath(string fullPath)
    {
        var workspaceRoot = Environment.GetEnvironmentVariable("WORKSPACE_ROOT") 
                           ?? Environment.CurrentDirectory;
        
        if (!string.IsNullOrEmpty(workspaceRoot) && fullPath.StartsWith(workspaceRoot))
        {
            return Path.GetRelativePath(workspaceRoot, fullPath);
        }
        
        return fullPath;
    }
}