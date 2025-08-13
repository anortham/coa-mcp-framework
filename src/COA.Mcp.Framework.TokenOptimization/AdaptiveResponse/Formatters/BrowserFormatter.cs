using System.Data;
using System.Text;
using COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Models;

namespace COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Formatters;

/// <summary>
/// Formatter optimized for browser environments with HTML-friendly output.
/// </summary>
public class BrowserFormatter : BaseOutputFormatter
{
    public BrowserFormatter(IDEEnvironment environment) : base(environment) { }
    
    public override string FormatSummary(string summary, object? data = null)
    {
        var sb = new StringBuilder();
        
        // HTML-friendly header
        sb.AppendLine($"<h2>🎯 {EscapeHtml(summary)}</h2>");
        sb.AppendLine($"<p><em>Environment: {EscapeHtml(_environment.GetDisplayName())}</em><br>");
        sb.AppendLine($"<em>Generated: {DateTime.Now:HH:mm:ss}</em></p>");
        sb.AppendLine();
        
        return sb.ToString();
    }
    
    public override string FormatFileReferences(IEnumerable<FileReference> references)
    {
        var refList = references.ToList();
        if (!refList.Any()) return string.Empty;
        
        var sb = new StringBuilder();
        sb.AppendLine("<h3>📁 Files</h3>");
        sb.AppendLine("<ul>");
        
        foreach (var fileRef in refList.Take(20))
        {
            var fileName = Path.GetFileName(fileRef.FilePath);
            var relativePath = GetRelativePath(fileRef.FilePath);
            
            sb.AppendLine("<li>");
            sb.AppendLine($"  <strong><a href=\"file://{EscapeHtml(fileRef.FilePath)}\">{EscapeHtml(fileName)}:{fileRef.Line}</a></strong><br>");
            sb.AppendLine($"  <code>{EscapeHtml(relativePath)}</code>");
            
            if (!string.IsNullOrEmpty(fileRef.Description))
            {
                sb.AppendLine($" - {EscapeHtml(fileRef.Description)}");
            }
            
            if (!string.IsNullOrEmpty(fileRef.CodePreview))
            {
                sb.AppendLine($"  <pre><code>{EscapeHtml(fileRef.CodePreview.Trim())}</code></pre>");
            }
            
            sb.AppendLine("</li>");
        }
        
        sb.AppendLine("</ul>");
        
        if (refList.Count > 20)
        {
            sb.AppendLine($"<p><em>...and {refList.Count - 20} more files</em></p>");
        }
        
        sb.AppendLine();
        return sb.ToString();
    }
    
    public override string FormatActions(IEnumerable<ActionItem> actions)
    {
        var actionList = actions.ToList();
        if (!actionList.Any()) return string.Empty;
        
        var sb = new StringBuilder();
        sb.AppendLine("<h3>🚀 Available Actions</h3>");
        sb.AppendLine("<ul>");
        
        foreach (var action in actionList)
        {
            sb.AppendLine("<li>");
            sb.AppendLine($"  <strong>{EscapeHtml(action.Title)}</strong>");
            
            if (!string.IsNullOrEmpty(action.Description))
            {
                sb.AppendLine($"<br>{EscapeHtml(action.Description)}");
            }
            
            if (!string.IsNullOrEmpty(action.Command))
            {
                sb.AppendLine($"<br><code>Command: {EscapeHtml(action.Command)}</code>");
            }
            
            if (!string.IsNullOrEmpty(action.KeyboardShortcut))
            {
                sb.AppendLine($"<br><em>Shortcut: {EscapeHtml(action.KeyboardShortcut)}</em>");
            }
            
            sb.AppendLine("</li>");
        }
        
        sb.AppendLine("</ul>");
        sb.AppendLine();
        
        return sb.ToString();
    }
    
    public override string FormatTable(DataTable data)
    {
        if (data.Rows.Count == 0)
        {
            return "<p>📊 <strong>No data available</strong></p>\n\n";
        }
        
        var sb = new StringBuilder();
        sb.AppendLine($"<h3>📊 Data Table ({data.Rows.Count:N0} rows)</h3>");
        
        if (data.Rows.Count <= 100)
        {
            sb.AppendLine(FormatHtmlTable(data));
        }
        else
        {
            sb.AppendLine($"<p><strong>Large dataset:</strong> {data.Rows.Count:N0} rows, {data.Columns.Count} columns</p>");
            sb.AppendLine(FormatTableSummary(data));
        }
        
        sb.AppendLine();
        return sb.ToString();
    }
    
    public override string FormatList(IEnumerable<object> items, string? title = null)
    {
        var itemList = items.ToList();
        if (!itemList.Any()) return string.Empty;
        
        var sb = new StringBuilder();
        sb.AppendLine($"<h3>📋 {EscapeHtml(title ?? "Items")} ({itemList.Count:N0})</h3>");
        sb.AppendLine("<ul>");
        
        foreach (var item in itemList.Take(25))
        {
            var itemText = item?.ToString() ?? "(null)";
            sb.AppendLine($"<li>{EscapeHtml(itemText)}</li>");
        }
        
        if (itemList.Count > 25)
        {
            sb.AppendLine($"<li><em>...and {itemList.Count - 25:N0} more items</em></li>");
        }
        
        sb.AppendLine("</ul>");
        sb.AppendLine();
        
        return sb.ToString();
    }
    
    public override string FormatError(COA.Mcp.Framework.Models.ErrorInfo error)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("<div style=\"border: 2px solid #ff4444; padding: 10px; background-color: #ffe6e6; border-radius: 5px;\">");
        sb.AppendLine($"<h3 style=\"color: #cc0000;\">❌ Error: {EscapeHtml(error.Code)}</h3>");
        sb.AppendLine($"<p><strong>Message:</strong> {EscapeHtml(error.Message)}</p>");
        
        if (error.Recovery?.Steps?.Any() == true)
        {
            sb.AppendLine("<h4>🔧 Recovery Steps:</h4>");
            sb.AppendLine("<ol>");
            foreach (var step in error.Recovery.Steps)
            {
                sb.AppendLine($"<li>{EscapeHtml(step)}</li>");
            }
            sb.AppendLine("</ol>");
        }
        
        sb.AppendLine("</div>");
        sb.AppendLine();
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Escapes special HTML characters.
    /// </summary>
    protected override string EscapeText(string? text)
    {
        return EscapeHtml(text);
    }
    
    private string EscapeHtml(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }
    
    private string FormatHtmlTable(DataTable data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<table border=\"1\" style=\"border-collapse: collapse; width: 100%;\">");
        
        // Headers
        sb.AppendLine("<thead>");
        sb.AppendLine("<tr style=\"background-color: #f0f0f0;\">");
        foreach (DataColumn column in data.Columns)
        {
            sb.AppendLine($"<th style=\"padding: 8px; text-align: left;\">{EscapeHtml(column.ColumnName)}</th>");
        }
        sb.AppendLine("</tr>");
        sb.AppendLine("</thead>");
        
        // Body
        sb.AppendLine("<tbody>");
        foreach (DataRow row in data.Rows.Cast<DataRow>().Take(50))
        {
            sb.AppendLine("<tr>");
            foreach (var cell in row.ItemArray)
            {
                var value = cell?.ToString() ?? "";
                sb.AppendLine($"<td style=\"padding: 8px;\">{EscapeHtml(TruncateText(value, 100))}</td>");
            }
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</tbody>");
        
        sb.AppendLine("</table>");
        
        if (data.Rows.Count > 50)
        {
            sb.AppendLine($"<p><em>Showing first 50 rows of {data.Rows.Count:N0} total rows</em></p>");
        }
        
        return sb.ToString();
    }
    
    private string FormatTableSummary(DataTable data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<h4>Column Summary:</h4>");
        sb.AppendLine("<ul>");
        
        foreach (DataColumn column in data.Columns.Cast<DataColumn>().Take(10))
        {
            sb.AppendLine($"<li><code>{EscapeHtml(column.ColumnName)}</code> ({EscapeHtml(column.DataType.Name)})</li>");
        }
        
        if (data.Columns.Count > 10)
            sb.AppendLine($"<li><em>...and {data.Columns.Count - 10} more columns</em></li>");
        
        sb.AppendLine("</ul>");
        
        if (data.Rows.Count > 0)
        {
            sb.AppendLine("<h4>Sample Data:</h4>");
            sb.AppendLine("<dl>");
            
            var sampleRow = data.Rows[0];
            foreach (DataColumn column in data.Columns.Cast<DataColumn>().Take(5))
            {
                var value = sampleRow[column]?.ToString() ?? "(null)";
                sb.AppendLine($"<dt><code>{EscapeHtml(column.ColumnName)}</code></dt>");
                sb.AppendLine($"<dd>{EscapeHtml(TruncateText(value, 100))}</dd>");
            }
            
            if (data.Columns.Count > 5)
                sb.AppendLine($"<dd><em>...and {data.Columns.Count - 5} more fields</em></dd>");
            
            sb.AppendLine("</dl>");
        }
        
        return sb.ToString();
    }
}