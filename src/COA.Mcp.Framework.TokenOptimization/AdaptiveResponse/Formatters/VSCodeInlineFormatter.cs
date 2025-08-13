using System.Data;
using System.Text;
using System.Text.Json;
using COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Models;

namespace COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Formatters;

/// <summary>
/// Formatter optimized for VS Code with rich Markdown, clickable links, and command integration.
/// </summary>
public class VSCodeInlineFormatter : BaseOutputFormatter
{
    public VSCodeInlineFormatter(IDEEnvironment environment) : base(environment) { }
    
    public override string FormatSummary(string summary, object? data = null)
    {
        var sb = new StringBuilder();
        
        // Rich markdown header with emoji
        sb.AppendLine($"## 🎯 {summary}");
        sb.AppendLine();
        
        // Add environment context with version info
        sb.AppendLine($"*Environment: {_environment.GetDisplayName()}*");
        sb.AppendLine($"*Generated: {DateTime.Now:HH:mm:ss}*");
        sb.AppendLine();
        
        return sb.ToString();
    }
    
    public override string FormatFileReferences(IEnumerable<FileReference> references)
    {
        var refList = references.ToList();
        if (!refList.Any()) return string.Empty;
        
        var sb = new StringBuilder();
        sb.AppendLine("### 📁 Files");
        sb.AppendLine();
        
        foreach (var fileRef in refList.Take(20))
        {
            var fileName = Path.GetFileName(fileRef.FilePath);
            var relativePath = GetRelativePath(fileRef.FilePath);
            
            // VS Code clickable format with enhanced display
            sb.AppendLine($"- **[{EscapeMarkdown(fileName)}:{fileRef.Line}]({fileRef.GetVSCodeUri()})**");
            sb.AppendLine($"  `{EscapeMarkdown(relativePath)}` - {EscapeMarkdown(fileRef.Description ?? "No description")}");
            
            // Add code preview if available
            if (!string.IsNullOrEmpty(fileRef.CodePreview))
            {
                var language = fileRef.Language ?? DetectLanguageFromExtension(fileRef.FilePath);
                sb.AppendLine($"  ```{language}");
                sb.AppendLine($"  {fileRef.CodePreview.Trim()}");
                sb.AppendLine($"  ```");
            }
            
            // Add project info if available
            if (!string.IsNullOrEmpty(fileRef.ProjectName))
            {
                sb.AppendLine($"  *Project: {EscapeMarkdown(fileRef.ProjectName)}*");
            }
            
            sb.AppendLine();
        }
        
        if (refList.Count > 20)
        {
            sb.AppendLine($"*...and {refList.Count - 20} more files (see resource for complete list)*");
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
    
    public override string FormatActions(IEnumerable<ActionItem> actions)
    {
        var actionList = actions.ToList();
        if (!actionList.Any()) return string.Empty;
        
        var sb = new StringBuilder();
        sb.AppendLine("### 🚀 Available Actions");
        sb.AppendLine();
        
        foreach (var action in actionList)
        {
            // Create VS Code command URI
            var commandUri = CreateCommandUri(action.Command, action.Parameters);
            sb.AppendLine($"- **[{EscapeMarkdown(action.Title)}]({commandUri})**");
            
            if (!string.IsNullOrEmpty(action.Description))
            {
                sb.AppendLine($"  {EscapeMarkdown(action.Description)}");
            }
            
            if (!string.IsNullOrEmpty(action.KeyboardShortcut))
            {
                sb.AppendLine($"  *Shortcut: `{EscapeMarkdown(action.KeyboardShortcut)}`*");
            }
            
            if (!string.IsNullOrEmpty(action.Category))
            {
                sb.AppendLine($"  *Category: {EscapeMarkdown(action.Category)}*");
            }
            
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
    
    public override string FormatTable(DataTable data)
    {
        if (data.Rows.Count == 0)
        {
            return "📊 **No data available**\n\n";
        }
        
        if (data.Rows.Count <= 10 && data.Columns.Count <= 6)
        {
            // Small table - use markdown table
            return FormatMarkdownTable(data);
        }
        else
        {
            // Large table - suggest resource view
            var resourceHint = Guid.NewGuid().ToString("N")[..8];
            return $"📊 **{data.Rows.Count:N0} rows, {data.Columns.Count} columns** - [View Interactive Table](mcp://table/{resourceHint})\n\n" +
                   FormatTableSummary(data);
        }
    }
    
    public override string FormatList(IEnumerable<object> items, string? title = null)
    {
        var itemList = items.ToList();
        if (!itemList.Any()) return string.Empty;
        
        var sb = new StringBuilder();
        sb.AppendLine($"### 📋 {title ?? "Items"} ({itemList.Count:N0})");
        sb.AppendLine();
        
        foreach (var item in itemList.Take(15))
        {
            var itemText = item?.ToString() ?? "(null)";
            sb.AppendLine($"- {EscapeMarkdown(TruncateText(itemText, 150))}");
        }
        
        if (itemList.Count > 15)
        {
            sb.AppendLine($"- *...and {itemList.Count - 15:N0} more items*");
        }
        
        sb.AppendLine();
        return sb.ToString();
    }
    
    public override string FormatError(COA.Mcp.Framework.Models.ErrorInfo error)
    {
        var sb = new StringBuilder();
        
        // Error with emoji and formatting
        sb.AppendLine($"## ❌ Error: {error.Code}");
        sb.AppendLine();
        sb.AppendLine($"**Message:** {EscapeMarkdown(error.Message)}");
        sb.AppendLine();
        
        if (error.Recovery?.Steps?.Any() == true)
        {
            sb.AppendLine("### 🔧 Recovery Steps");
            sb.AppendLine();
            foreach (var step in error.Recovery.Steps)
            {
                sb.AppendLine($"1. {EscapeMarkdown(step)}");
            }
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Escapes special Markdown characters.
    /// </summary>
    protected override string EscapeText(string? text)
    {
        return EscapeMarkdown(text);
    }
    
    private string EscapeMarkdown(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        
        return text
            .Replace("\\", "\\\\")
            .Replace("`", "\\`")
            .Replace("*", "\\*")
            .Replace("_", "\\_")
            .Replace("{", "\\{")
            .Replace("}", "\\}")
            .Replace("[", "\\[")
            .Replace("]", "\\]")
            .Replace("(", "\\(")
            .Replace(")", "\\)")
            .Replace("#", "\\#")
            .Replace("+", "\\+")
            .Replace("-", "\\-")
            .Replace(".", "\\.")
            .Replace("!", "\\!")
            .Replace("|", "\\|");
    }
    
    private string FormatMarkdownTable(DataTable data)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"📊 **Table** ({data.Rows.Count:N0} rows)");
        sb.AppendLine();
        
        // Headers
        var headers = data.Columns.Cast<DataColumn>().Select(c => EscapeMarkdown(c.ColumnName));
        sb.AppendLine($"| {string.Join(" | ", headers)} |");
        
        // Separator
        var separators = data.Columns.Cast<DataColumn>().Select(_ => "---");
        sb.AppendLine($"| {string.Join(" | ", separators)} |");
        
        // Rows
        foreach (DataRow row in data.Rows)
        {
            var values = row.ItemArray.Select(v => 
                EscapeMarkdown(TruncateText(v?.ToString(), 30)));
            sb.AppendLine($"| {string.Join(" | ", values)} |");
        }
        
        sb.AppendLine();
        return sb.ToString();
    }
    
    private string FormatTableSummary(DataTable data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("**Table Summary:**");
        sb.AppendLine($"- **Columns:** {string.Join(", ", data.Columns.Cast<DataColumn>().Select(c => $"`{c.ColumnName}`"))}");
        
        if (data.Rows.Count > 0)
        {
            sb.AppendLine("- **Sample Data:**");
            var sampleRow = data.Rows[0];
            foreach (DataColumn column in data.Columns.Cast<DataColumn>().Take(3))
            {
                var value = sampleRow[column]?.ToString() ?? "(null)";
                sb.AppendLine($"  - {EscapeMarkdown(column.ColumnName)}: `{EscapeMarkdown(TruncateText(value, 50))}`");
            }
            
            if (data.Columns.Count > 3)
                sb.AppendLine($"  - *...and {data.Columns.Count - 3} more columns*");
        }
        
        sb.AppendLine();
        return sb.ToString();
    }
    
    private string CreateCommandUri(string command, Dictionary<string, object>? parameters)
    {
        if (parameters == null || !parameters.Any())
        {
            return $"command:{command}";
        }
        
        var encodedParams = Uri.EscapeDataString(JsonSerializer.Serialize(parameters));
        return $"command:{command}?{encodedParams}";
    }
    
    private string DetectLanguageFromExtension(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".cs" => "csharp",
            ".ts" => "typescript",
            ".js" => "javascript",
            ".py" => "python",
            ".java" => "java",
            ".cpp" or ".cc" or ".cxx" => "cpp",
            ".c" => "c",
            ".h" or ".hpp" => "cpp",
            ".rs" => "rust",
            ".go" => "go",
            ".sql" => "sql",
            ".json" => "json",
            ".xml" => "xml",
            ".yml" or ".yaml" => "yaml",
            ".md" => "markdown",
            ".html" => "html",
            ".css" => "css",
            ".scss" or ".sass" => "scss",
            ".ps1" => "powershell",
            ".sh" => "bash",
            _ => "text"
        };
    }
}