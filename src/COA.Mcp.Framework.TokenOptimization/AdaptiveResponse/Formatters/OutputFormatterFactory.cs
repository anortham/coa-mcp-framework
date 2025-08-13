using COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Models;

namespace COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Formatters;

/// <summary>
/// Factory for creating appropriate formatters based on IDE environment and format requirements.
/// </summary>
public class OutputFormatterFactory : IOutputFormatterFactory
{
    /// <summary>
    /// Creates an inline formatter optimized for the specified IDE environment.
    /// </summary>
    /// <param name="environment">The IDE environment to format for.</param>
    /// <returns>An appropriate output formatter implementation.</returns>
    public IOutputFormatter CreateInlineFormatter(IDEEnvironment environment)
    {
        return environment.IDE switch
        {
            IDEType.VSCode => new VSCodeInlineFormatter(environment),
            IDEType.VS2022 => new VS2022InlineFormatter(environment),
            IDEType.Terminal => new TerminalFormatter(environment),
            IDEType.Browser => new BrowserFormatter(environment),
            _ => new UniversalFormatter(environment)
        };
    }
    
    /// <summary>
    /// Creates a resource formatter for the specified format and environment.
    /// </summary>
    /// <param name="format">The desired output format (html, csv, json, markdown).</param>
    /// <param name="environment">The IDE environment for optimization hints.</param>
    /// <returns>An appropriate resource formatter implementation.</returns>
    public IResourceFormatter CreateResourceFormatter(string format, IDEEnvironment environment)
    {
        return format.ToLowerInvariant() switch
        {
            "html" => new HTMLResourceFormatter(environment),
            "csv" => new CSVResourceFormatter(),
            "json" => new JSONResourceFormatter(),
            "markdown" => new MarkdownResourceFormatter(),
            "xml" => new XMLResourceFormatter(),
            _ => new JSONResourceFormatter() // Default fallback
        };
    }
}

/// <summary>
/// Base class for output formatters with common functionality.
/// </summary>
public abstract class BaseOutputFormatter : IOutputFormatter
{
    protected readonly IDEEnvironment _environment;
    
    protected BaseOutputFormatter(IDEEnvironment environment)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }
    
    /// <summary>
    /// Gets the relative path for a file from the workspace root.
    /// </summary>
    /// <param name="fullPath">The full file path.</param>
    /// <returns>The relative path or the full path if no workspace root is detected.</returns>
    protected string GetRelativePath(string fullPath)
    {
        var workspaceRoot = Environment.GetEnvironmentVariable("WORKSPACE_ROOT") 
                           ?? Environment.CurrentDirectory;
        
        if (!string.IsNullOrEmpty(workspaceRoot) && fullPath.StartsWith(workspaceRoot))
        {
            return Path.GetRelativePath(workspaceRoot, fullPath);
        }
        
        return fullPath;
    }
    
    /// <summary>
    /// Truncates text to a maximum length with ellipsis.
    /// </summary>
    /// <param name="text">The text to truncate.</param>
    /// <param name="maxLength">The maximum length.</param>
    /// <returns>The truncated text.</returns>
    protected string TruncateText(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text ?? string.Empty;
        
        return text.Substring(0, maxLength - 3) + "...";
    }
    
    /// <summary>
    /// Escapes special characters for the target format.
    /// </summary>
    /// <param name="text">The text to escape.</param>
    /// <returns>The escaped text.</returns>
    protected virtual string EscapeText(string? text)
    {
        return text ?? string.Empty;
    }
    
    public abstract string FormatSummary(string summary, object? data = null);
    public abstract string FormatFileReferences(IEnumerable<FileReference> references);
    public abstract string FormatActions(IEnumerable<ActionItem> actions);
    public abstract string FormatTable(System.Data.DataTable data);
    public abstract string FormatList(IEnumerable<object> items, string? title = null);
    public abstract string FormatError(COA.Mcp.Framework.Models.ErrorInfo error);
}

/// <summary>
/// Universal formatter that works across all environments with basic text formatting.
/// </summary>
public class UniversalFormatter : BaseOutputFormatter
{
    public UniversalFormatter(IDEEnvironment environment) : base(environment) { }
    
    public override string FormatSummary(string summary, object? data = null)
    {
        return $"=== {summary} ===\nEnvironment: {_environment.GetDisplayName()}\n";
    }
    
    public override string FormatFileReferences(IEnumerable<FileReference> references)
    {
        if (!references.Any()) return string.Empty;
        
        var result = "Files:\n";
        foreach (var fileRef in references.Take(20))
        {
            result += $"  {GetRelativePath(fileRef.FilePath)}:{fileRef.Line}:{fileRef.Column}";
            if (!string.IsNullOrEmpty(fileRef.Description))
                result += $" - {fileRef.Description}";
            result += "\n";
        }
        
        var total = references.Count();
        if (total > 20)
            result += $"  ... and {total - 20} more files\n";
        
        return result;
    }
    
    public override string FormatActions(IEnumerable<ActionItem> actions)
    {
        if (!actions.Any()) return string.Empty;
        
        var result = "Available Actions:\n";
        var actionList = actions.ToList();
        
        for (int i = 0; i < actionList.Count; i++)
        {
            var action = actionList[i];
            result += $"  {i + 1}. {action.Title}";
            if (!string.IsNullOrEmpty(action.Description))
                result += $" - {action.Description}";
            result += "\n";
        }
        
        return result;
    }
    
    public override string FormatTable(System.Data.DataTable data)
    {
        if (data.Rows.Count == 0)
            return "No data available.\n";
        
        // Simple text table formatting
        var result = $"Table: {data.Rows.Count} rows, {data.Columns.Count} columns\n";
        
        // Headers
        result += string.Join(" | ", data.Columns.Cast<System.Data.DataColumn>().Select(c => c.ColumnName)) + "\n";
        result += new string('-', 50) + "\n";
        
        // Sample rows (limit to 10 for readability)
        foreach (System.Data.DataRow row in data.Rows.Cast<System.Data.DataRow>().Take(10))
        {
            result += string.Join(" | ", row.ItemArray.Select(v => TruncateText(v?.ToString(), 20))) + "\n";
        }
        
        if (data.Rows.Count > 10)
            result += $"... and {data.Rows.Count - 10} more rows\n";
        
        return result;
    }
    
    public override string FormatList(IEnumerable<object> items, string? title = null)
    {
        var itemList = items.ToList();
        if (!itemList.Any()) return string.Empty;
        
        var result = string.IsNullOrEmpty(title) ? "Items:\n" : $"{title}:\n";
        
        foreach (var item in itemList.Take(20))
        {
            result += $"  - {TruncateText(item?.ToString(), 100)}\n";
        }
        
        if (itemList.Count > 20)
            result += $"  ... and {itemList.Count - 20} more items\n";
        
        return result;
    }
    
    public override string FormatError(COA.Mcp.Framework.Models.ErrorInfo error)
    {
        var result = $"Error ({error.Code}): {error.Message}\n";
        
        if (error.Recovery?.Steps?.Any() == true)
        {
            result += "Recovery Steps:\n";
            foreach (var step in error.Recovery.Steps)
            {
                result += $"  - {step}\n";
            }
        }
        
        return result;
    }
}