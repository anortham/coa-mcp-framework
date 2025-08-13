using System.Data;
using System.Text;
using COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Models;

namespace COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Formatters;

/// <summary>
/// Formatter optimized for Visual Studio 2022 Output Window with clickable file references and command mapping.
/// </summary>
public class VS2022InlineFormatter : BaseOutputFormatter
{
    public VS2022InlineFormatter(IDEEnvironment environment) : base(environment) { }
    
    public override string FormatSummary(string summary, object? data = null)
    {
        var sb = new StringBuilder();
        
        // VS 2022 Output Window style with clear separators
        sb.AppendLine($"========== {summary.ToUpperInvariant()} ==========");
        sb.AppendLine($"Generated at: {DateTime.Now:HH:mm:ss}");
        sb.AppendLine($"Environment: {_environment.GetDisplayName()}");
        sb.AppendLine($"================================================================================");
        sb.AppendLine();
        
        return sb.ToString();
    }
    
    public override string FormatFileReferences(IEnumerable<FileReference> references)
    {
        var refList = references.ToList();
        if (!refList.Any()) return string.Empty;
        
        var sb = new StringBuilder();
        sb.AppendLine($"Files found ({refList.Count}):");
        sb.AppendLine();
        
        foreach (var fileRef in refList)
        {
            // VS 2022 Error List format (clickable in Output Window)
            sb.AppendLine($"  {fileRef.GetVS2022Reference()}");
            
            if (!string.IsNullOrEmpty(fileRef.Description))
            {
                sb.AppendLine($"    Description: {fileRef.Description}");
            }
            
            if (!string.IsNullOrEmpty(fileRef.ProjectName))
            {
                sb.AppendLine($"    Project: {fileRef.ProjectName}");
            }
            
            if (!string.IsNullOrEmpty(fileRef.CodePreview))
            {
                sb.AppendLine($"    Preview: {TruncateText(fileRef.CodePreview.Trim().Replace('\n', ' '), 100)}");
            }
            
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
    
    public override string FormatActions(IEnumerable<ActionItem> actions)
    {
        var actionList = actions.ToList();
        if (!actionList.Any()) return string.Empty;
        
        var sb = new StringBuilder();
        sb.AppendLine("Available actions:");
        sb.AppendLine();
        
        for (int i = 0; i < actionList.Count; i++)
        {
            var action = actionList[i];
            sb.AppendLine($"  {i + 1}. {action.Title}");
            
            if (!string.IsNullOrEmpty(action.Description))
            {
                sb.AppendLine($"     {action.Description}");
            }
            
            // Map to VS 2022 command if possible
            var vs2022Command = MapToVS2022Command(action.Command);
            if (!string.IsNullOrEmpty(vs2022Command))
            {
                sb.AppendLine($"     Command: {vs2022Command}");
            }
            
            if (!string.IsNullOrEmpty(action.KeyboardShortcut))
            {
                sb.AppendLine($"     Shortcut: {action.KeyboardShortcut}");
            }
            
            if (!string.IsNullOrEmpty(action.Category))
            {
                sb.AppendLine($"     Category: {action.Category}");
            }
            
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
    
    public override string FormatTable(DataTable data)
    {
        if (data.Rows.Count == 0)
        {
            return "No data available.\n\n";
        }
        
        var sb = new StringBuilder();
        sb.AppendLine($"Data Table: {data.Rows.Count:N0} rows, {data.Columns.Count} columns");
        sb.AppendLine();
        
        if (data.Rows.Count <= 50 && data.Columns.Count <= 8)
        {
            // Format as fixed-width table for Output Window
            var columnWidths = CalculateColumnWidths(data);
            
            // Header
            sb.AppendLine(FormatTableRow(data.Columns.Cast<DataColumn>().Select(c => c.ColumnName), columnWidths));
            sb.AppendLine(new string('-', columnWidths.Sum() + (columnWidths.Length - 1) * 3));
            
            // Rows
            foreach (DataRow row in data.Rows.Cast<DataRow>().Take(50))
            {
                sb.AppendLine(FormatTableRow(row.ItemArray.Select(v => v?.ToString() ?? ""), columnWidths));
            }
            
            if (data.Rows.Count > 50)
            {
                sb.AppendLine($"... and {data.Rows.Count - 50:N0} more rows (export to see all)");
            }
        }
        else
        {
            // Large table - show summary and suggest export
            sb.AppendLine("Large dataset detected. Consider exporting to Excel or CSV for better viewing.");
            sb.AppendLine();
            sb.AppendLine("Column Summary:");
            foreach (DataColumn column in data.Columns.Cast<DataColumn>().Take(10))
            {
                sb.AppendLine($"  - {column.ColumnName} ({column.DataType.Name})");
            }
            
            if (data.Columns.Count > 10)
                sb.AppendLine($"  ... and {data.Columns.Count - 10} more columns");
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
        sb.AppendLine();
        
        for (int i = 0; i < Math.Min(itemList.Count, 25); i++)
        {
            var item = itemList[i];
            var itemText = item?.ToString() ?? "(null)";
            sb.AppendLine($"  {i + 1:000}. {TruncateText(itemText, 120)}");
        }
        
        if (itemList.Count > 25)
        {
            sb.AppendLine($"  ... and {itemList.Count - 25:N0} more items");
        }
        
        sb.AppendLine();
        return sb.ToString();
    }
    
    public override string FormatError(COA.Mcp.Framework.Models.ErrorInfo error)
    {
        var sb = new StringBuilder();
        
        // VS 2022 style error formatting
        sb.AppendLine($"ERROR {error.Code}: {error.Message}");
        sb.AppendLine();
        
        if (error.Recovery?.Steps?.Any() == true)
        {
            sb.AppendLine("Recovery actions:");
            for (int i = 0; i < error.Recovery.Steps.Count(); i++)
            {
                sb.AppendLine($"  {i + 1}. {error.Recovery.Steps[i]}");
            }
            sb.AppendLine();
        }
        
        sb.AppendLine($"Time: {DateTime.Now:HH:mm:ss}");
        sb.AppendLine();
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Maps MCP commands to Visual Studio 2022 command equivalents.
    /// </summary>
    /// <param name="mcpCommand">The MCP command name.</param>
    /// <returns>The corresponding VS 2022 command or null if no mapping exists.</returns>
    private string? MapToVS2022Command(string mcpCommand)
    {
        return mcpCommand switch
        {
            "find_references" => "Edit.FindAllReferences",
            "goto_definition" => "Edit.GoToDefinition",
            "find_implementations" => "Edit.GoToImplementation",
            "rename_symbol" => "Refactor.Rename",
            "format_document" => "Edit.FormatDocument",
            "organize_usings" => "Edit.RemoveAndSort",
            "build_solution" => "Build.BuildSolution",
            "run_tests" => "TestExplorer.RunAllTests",
            "debug_test" => "TestExplorer.DebugAllTests",
            "extract_method" => "Refactor.ExtractMethod",
            "extract_interface" => "Refactor.ExtractInterface",
            "find_in_files" => "Edit.FindinFiles",
            "replace_in_files" => "Edit.ReplaceinFiles",
            "solution_explorer" => "View.SolutionExplorer",
            "error_list" => "View.ErrorList",
            "output_window" => "View.Output",
            "immediate_window" => "Debug.Immediate",
            "call_hierarchy" => "View.CallHierarchy",
            "class_view" => "View.ClassView",
            "object_browser" => "View.ObjectBrowser",
            "package_manager" => "Tools.NuGetPackageManager",
            "git_changes" => "View.GitChanges",
            "team_explorer" => "View.TeamExplorer",
            _ => null
        };
    }
    
    /// <summary>
    /// Calculates appropriate column widths for table formatting.
    /// </summary>
    private int[] CalculateColumnWidths(DataTable data)
    {
        var widths = new int[data.Columns.Count];
        
        // Initialize with header widths
        for (int i = 0; i < data.Columns.Count; i++)
        {
            widths[i] = data.Columns[i].ColumnName.Length;
        }
        
        // Check data rows (sample first 100 for performance)
        foreach (DataRow row in data.Rows.Cast<DataRow>().Take(100))
        {
            for (int i = 0; i < row.ItemArray.Length; i++)
            {
                var value = row.ItemArray[i]?.ToString() ?? "";
                widths[i] = Math.Max(widths[i], Math.Min(value.Length, 50)); // Cap at 50 chars
            }
        }
        
        return widths;
    }
    
    /// <summary>
    /// Formats a table row with proper spacing.
    /// </summary>
    private string FormatTableRow(IEnumerable<string> values, int[] columnWidths)
    {
        var paddedValues = values.Select((v, i) => 
        {
            var truncated = TruncateText(v ?? "", columnWidths[i]);
            return truncated.PadRight(columnWidths[i]);
        });
        
        return string.Join(" | ", paddedValues);
    }
}