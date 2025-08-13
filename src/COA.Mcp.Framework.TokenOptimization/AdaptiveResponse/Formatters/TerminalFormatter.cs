using System.Data;
using System.Text;
using COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Models;

namespace COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Formatters;

/// <summary>
/// Formatter optimized for terminal/command-line environments with clean, readable text output.
/// </summary>
public class TerminalFormatter : BaseOutputFormatter
{
    private const int MaxLineLength = 120; // Standard terminal width
    
    public TerminalFormatter(IDEEnvironment environment) : base(environment) { }
    
    public override string FormatSummary(string summary, object? data = null)
    {
        var sb = new StringBuilder();
        
        // Simple, clean terminal header
        var headerLine = new string('=', Math.Min(summary.Length + 8, MaxLineLength));
        sb.AppendLine(headerLine);
        sb.AppendLine($"=== {summary} ===");
        sb.AppendLine(headerLine);
        sb.AppendLine($"Time: {DateTime.Now:HH:mm:ss} | Environment: {_environment.GetDisplayName()}");
        sb.AppendLine();
        
        return sb.ToString();
    }
    
    public override string FormatFileReferences(IEnumerable<FileReference> references)
    {
        var refList = references.ToList();
        if (!refList.Any()) return string.Empty;
        
        var sb = new StringBuilder();
        sb.AppendLine($"Files ({refList.Count}):");
        
        foreach (var fileRef in refList)
        {
            // Simple clickable format (works in many terminals)
            var relativePath = GetRelativePath(fileRef.FilePath);
            sb.AppendLine($"  {fileRef.GetTerminalReference()}");
            
            if (!string.IsNullOrEmpty(fileRef.Description))
            {
                var wrappedDesc = WrapText(fileRef.Description, MaxLineLength - 4, "    ");
                sb.AppendLine(wrappedDesc);
            }
            
            if (!string.IsNullOrEmpty(fileRef.CodePreview))
            {
                var preview = TruncateText(fileRef.CodePreview.Trim().Replace('\n', ' '), 100);
                sb.AppendLine($"    → {preview}");
            }
        }
        
        sb.AppendLine();
        return sb.ToString();
    }
    
    public override string FormatActions(IEnumerable<ActionItem> actions)
    {
        var actionList = actions.ToList();
        if (!actionList.Any()) return string.Empty;
        
        var sb = new StringBuilder();
        sb.AppendLine("Actions:");
        
        for (int i = 0; i < actionList.Count; i++)
        {
            var action = actionList[i];
            sb.AppendLine($"  {i + 1}. {action.Title}");
            
            if (!string.IsNullOrEmpty(action.Description))
            {
                var wrappedDesc = WrapText(action.Description, MaxLineLength - 6, "     ");
                sb.AppendLine(wrappedDesc);
            }
            
            if (!string.IsNullOrEmpty(action.Command))
            {
                sb.AppendLine($"     Command: {action.Command}");
            }
            
            if (!string.IsNullOrEmpty(action.KeyboardShortcut))
            {
                sb.AppendLine($"     Shortcut: {action.KeyboardShortcut}");
            }
        }
        
        sb.AppendLine();
        return sb.ToString();
    }
    
    public override string FormatTable(DataTable data)
    {
        if (data.Rows.Count == 0)
        {
            return "No data available.\n\n";
        }
        
        var sb = new StringBuilder();
        sb.AppendLine($"Table: {data.Rows.Count:N0} rows × {data.Columns.Count} columns");
        sb.AppendLine();
        
        if (data.Rows.Count <= 20 && data.Columns.Count <= 6)
        {
            // ASCII table for terminal
            return FormatASCIITable(data);
        }
        else
        {
            // Large table - show summary
            sb.AppendLine("Large dataset - showing summary:");
            sb.AppendLine();
            
            // Column information
            sb.AppendLine("Columns:");
            foreach (DataColumn column in data.Columns.Cast<DataColumn>().Take(10))
            {
                sb.AppendLine($"  • {column.ColumnName} ({column.DataType.Name})");
            }
            
            if (data.Columns.Count > 10)
                sb.AppendLine($"  • ... and {data.Columns.Count - 10} more columns");
            
            sb.AppendLine();
            
            // Sample data
            if (data.Rows.Count > 0)
            {
                sb.AppendLine("Sample row:");
                var sampleRow = data.Rows[0];
                foreach (DataColumn column in data.Columns.Cast<DataColumn>().Take(5))
                {
                    var value = sampleRow[column]?.ToString() ?? "(null)";
                    sb.AppendLine($"  {column.ColumnName}: {TruncateText(value, 60)}");
                }
                
                if (data.Columns.Count > 5)
                    sb.AppendLine($"  ... and {data.Columns.Count - 5} more fields");
            }
        }
        
        sb.AppendLine();
        return sb.ToString();
    }
    
    public override string FormatList(IEnumerable<object> items, string? title = null)
    {
        var itemList = items.ToList();
        if (!itemList.Any()) return string.Empty;
        
        var sb = new StringBuilder();
        sb.AppendLine($"{title ?? "Items"} ({itemList.Count:N0}):");
        
        for (int i = 0; i < Math.Min(itemList.Count, 20); i++)
        {
            var item = itemList[i];
            var itemText = item?.ToString() ?? "(null)";
            var wrappedText = WrapText(itemText, MaxLineLength - 6, "      ");
            sb.AppendLine($"  • {wrappedText.Substring(4)}"); // Remove leading spaces for first line
        }
        
        if (itemList.Count > 20)
        {
            sb.AppendLine($"  ... and {itemList.Count - 20:N0} more items");
        }
        
        sb.AppendLine();
        return sb.ToString();
    }
    
    public override string FormatError(COA.Mcp.Framework.Models.ErrorInfo error)
    {
        var sb = new StringBuilder();
        
        // Terminal-friendly error formatting with ANSI color codes (if supported)
        var errorLine = new string('!', Math.Min(60, MaxLineLength));
        sb.AppendLine(errorLine);
        sb.AppendLine($"ERROR: {error.Code}");
        sb.AppendLine(errorLine);
        sb.AppendLine();
        
        var wrappedMessage = WrapText(error.Message, MaxLineLength - 2, "  ");
        sb.AppendLine(wrappedMessage);
        sb.AppendLine();
        
        if (error.Recovery?.Steps?.Any() == true)
        {
            sb.AppendLine("Recovery Steps:");
            for (int i = 0; i < error.Recovery.Steps.Count(); i++)
            {
                var step = error.Recovery.Steps[i];
                var wrappedStep = WrapText($"{i + 1}. {step}", MaxLineLength - 2, "   ");
                sb.AppendLine(wrappedStep);
            }
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Formats data as an ASCII table suitable for terminal display.
    /// </summary>
    private string FormatASCIITable(DataTable data)
    {
        var columnWidths = CalculateColumnWidths(data);
        var sb = new StringBuilder();
        
        // Top border
        sb.AppendLine("+" + string.Join("+", columnWidths.Select(w => new string('-', w + 2))) + "+");
        
        // Headers
        var headers = data.Columns.Cast<DataColumn>()
            .Select((c, i) => $" {c.ColumnName.PadRight(columnWidths[i])} ");
        sb.AppendLine("|" + string.Join("|", headers) + "|");
        
        // Header separator
        sb.AppendLine("+" + string.Join("+", columnWidths.Select(w => new string('-', w + 2))) + "+");
        
        // Data rows (limit to 20 for terminal display)
        foreach (DataRow row in data.Rows.Cast<DataRow>().Take(20))
        {
            var values = row.ItemArray.Select((v, i) => 
            {
                var text = v?.ToString() ?? "";
                var truncated = TruncateText(text, columnWidths[i]);
                return $" {truncated.PadRight(columnWidths[i])} ";
            });
            sb.AppendLine("|" + string.Join("|", values) + "|");
        }
        
        // Bottom border
        sb.AppendLine("+" + string.Join("+", columnWidths.Select(w => new string('-', w + 2))) + "+");
        
        if (data.Rows.Count > 20)
        {
            sb.AppendLine($"({data.Rows.Count - 20:N0} more rows...)");
        }
        
        sb.AppendLine();
        return sb.ToString();
    }
    
    /// <summary>
    /// Calculates column widths for ASCII table formatting.
    /// </summary>
    private int[] CalculateColumnWidths(DataTable data)
    {
        var widths = new int[data.Columns.Count];
        
        // Initialize with header widths
        for (int i = 0; i < data.Columns.Count; i++)
        {
            widths[i] = data.Columns[i].ColumnName.Length;
        }
        
        // Check data rows (sample for performance)
        foreach (DataRow row in data.Rows.Cast<DataRow>().Take(50))
        {
            for (int i = 0; i < row.ItemArray.Length; i++)
            {
                var value = row.ItemArray[i]?.ToString() ?? "";
                widths[i] = Math.Max(widths[i], Math.Min(value.Length, 30)); // Cap at 30 for terminal
            }
        }
        
        // Ensure total width doesn't exceed terminal width
        var totalWidth = widths.Sum() + (widths.Length - 1) * 3 + 2; // Include separators and borders
        if (totalWidth > MaxLineLength)
        {
            // Scale down proportionally
            var scale = (double)(MaxLineLength - (widths.Length - 1) * 3 - 2) / widths.Sum();
            for (int i = 0; i < widths.Length; i++)
            {
                widths[i] = Math.Max(3, (int)(widths[i] * scale)); // Minimum width of 3
            }
        }
        
        return widths;
    }
    
    /// <summary>
    /// Wraps text to fit within specified line length with proper indentation.
    /// </summary>
    private string WrapText(string text, int maxLength, string indent = "")
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return indent + text;
        
        var sb = new StringBuilder();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var currentLine = new StringBuilder(indent);
        
        foreach (var word in words)
        {
            if (currentLine.Length + word.Length + 1 > maxLength && currentLine.Length > indent.Length)
            {
                sb.AppendLine(currentLine.ToString());
                currentLine.Clear().Append(indent);
            }
            
            if (currentLine.Length > indent.Length)
                currentLine.Append(' ');
            
            currentLine.Append(word);
        }
        
        if (currentLine.Length > indent.Length)
            sb.Append(currentLine.ToString());
        
        return sb.ToString();
    }
}