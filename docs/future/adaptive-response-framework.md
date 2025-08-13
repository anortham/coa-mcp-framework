# Adaptive Response Framework Technical Specification

## Overview

The Adaptive Response Framework provides intelligent IDE-aware output formatting for MCP tools, automatically optimizing responses for VS Code, Visual Studio 2022, and terminal environments.

## Core Architecture

### Environment Detection System

```csharp
public enum IDEType
{
    Unknown,
    VSCode,
    VS2022,
    Terminal,
    Browser
}

public class IDEEnvironment
{
    public IDEType IDE { get; set; }
    public bool SupportsHTML { get; set; }
    public bool SupportsMarkdown { get; set; }
    public bool SupportsInteractive { get; set; }
    public bool SupportsWebView { get; set; }
    public string Version { get; set; }
    
    public static IDEEnvironment Detect()
    {
        var vsCodePid = Environment.GetEnvironmentVariable("VSCODE_PID");
        var vsVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");
        var termProgram = Environment.GetEnvironmentVariable("TERM_PROGRAM");
        
        if (!string.IsNullOrEmpty(vsCodePid))
        {
            return new IDEEnvironment
            {
                IDE = IDEType.VSCode,
                SupportsHTML = true,
                SupportsMarkdown = true,
                SupportsInteractive = true,
                SupportsWebView = true,
                Version = Environment.GetEnvironmentVariable("VSCODE_VERSION") ?? "unknown"
            };
        }
        
        if (!string.IsNullOrEmpty(vsVersion))
        {
            return new IDEEnvironment
            {
                IDE = IDEType.VS2022,
                SupportsHTML = true,
                SupportsMarkdown = true,
                SupportsInteractive = true,
                SupportsWebView = false, // Limited webview support
                Version = vsVersion
            };
        }
        
        return new IDEEnvironment
        {
            IDE = IDEType.Terminal,
            SupportsHTML = false,
            SupportsMarkdown = true,
            SupportsInteractive = false,
            SupportsWebView = false,
            Version = termProgram ?? "unknown"
        };
    }
}
```

### Adaptive Response Builder Base Class

```csharp
public abstract class AdaptiveResponseBuilder<TInput, TResult> : BaseResponseBuilder<TInput, TResult>
    where TResult : ToolResultBase, new()
{
    private readonly IDEEnvironment _environment;
    private readonly IOutputFormatterFactory _formatterFactory;
    private readonly IResourceProvider _resourceProvider;
    
    protected AdaptiveResponseBuilder(
        ILogger? logger = null,
        ProgressiveReductionEngine? reductionEngine = null,
        IResourceProvider? resourceProvider = null)
        : base(logger, reductionEngine)
    {
        _environment = IDEEnvironment.Detect();
        _formatterFactory = new OutputFormatterFactory();
        _resourceProvider = resourceProvider ?? new DefaultResourceProvider();
    }
    
    public override async Task<TResult> BuildResponseAsync(TInput data, ResponseContext context)
    {
        // Create base result
        var result = new TResult
        {
            Success = true,
            Operation = GetOperationName(),
            Meta = new ToolExecutionMetadata
            {
                Mode = context.ResponseMode,
                ExecutionTime = GetExecutionTime()
            }
        };
        
        // Apply adaptive formatting
        await ApplyAdaptiveFormattingAsync(result, data, context);
        
        // Apply token optimization if needed
        if (ShouldOptimizeTokens(result, context))
        {
            await OptimizeForTokensAsync(result, data, context);
        }
        
        return result;
    }
    
    protected abstract string GetOperationName();
    protected abstract Task ApplyAdaptiveFormattingAsync(TResult result, TInput data, ResponseContext context);
    
    private async Task OptimizeForTokensAsync(TResult result, TInput data, ResponseContext context)
    {
        var estimatedTokens = EstimateTokenUsage(result);
        var maxTokens = context.TokenLimit ?? TokenBudget.MaxTokens;
        
        if (estimatedTokens > maxTokens)
        {
            _logger?.LogInformation($"Response too large ({estimatedTokens} tokens), creating resource");
            
            // Create resource for full data
            var resourceUri = await CreateLargeDataResourceAsync(data);
            
            // Reduce inline content
            result.Message = $"Large dataset ({estimatedTokens} tokens) - see resource for details";
            result.ResourceUri = resourceUri;
            result.Meta.Truncated = true;
        }
    }
    
    private async Task<string> CreateLargeDataResourceAsync(TInput data)
    {
        var format = _environment.IDE switch
        {
            IDEType.VSCode => "html",
            IDEType.VS2022 => "html",
            _ => "json"
        };
        
        var resourceId = Guid.NewGuid().ToString("N")[..8];
        var formatter = _formatterFactory.CreateResourceFormatter(format, _environment);
        var content = await formatter.FormatResourceAsync(data);
        
        await _resourceProvider.StoreAsync($"data/{resourceId}.{format}", content);
        return $"mcp://data/{resourceId}.{format}";
    }
}
```

### Output Formatter Factory

```csharp
public interface IOutputFormatterFactory
{
    IOutputFormatter CreateInlineFormatter(IDEEnvironment environment);
    IResourceFormatter CreateResourceFormatter(string format, IDEEnvironment environment);
}

public class OutputFormatterFactory : IOutputFormatterFactory
{
    public IOutputFormatter CreateInlineFormatter(IDEEnvironment environment)
    {
        return environment.IDE switch
        {
            IDEType.VSCode => new VSCodeInlineFormatter(environment),
            IDEType.VS2022 => new VS2022InlineFormatter(environment),
            IDEType.Terminal => new TerminalFormatter(environment),
            _ => new UniversalFormatter(environment)
        };
    }
    
    public IResourceFormatter CreateResourceFormatter(string format, IDEEnvironment environment)
    {
        return format.ToLower() switch
        {
            "html" => new HTMLResourceFormatter(environment),
            "csv" => new CSVResourceFormatter(),
            "json" => new JSONResourceFormatter(),
            "markdown" => new MarkdownResourceFormatter(),
            _ => new JSONResourceFormatter()
        };
    }
}
```

## IDE-Specific Formatters

### VS Code Formatter

```csharp
public class VSCodeInlineFormatter : IOutputFormatter
{
    private readonly IDEEnvironment _environment;
    
    public VSCodeInlineFormatter(IDEEnvironment environment)
    {
        _environment = environment;
    }
    
    public string FormatSummary(string summary, object data)
    {
        var sb = new StringBuilder();
        
        // Rich markdown header with emoji
        sb.AppendLine($"## 🎯 {summary}");
        sb.AppendLine();
        
        // Add environment context
        sb.AppendLine($"*Environment: VS Code {_environment.Version}*");
        sb.AppendLine();
        
        return sb.ToString();
    }
    
    public string FormatFileReferences(List<FileReference> references)
    {
        if (!references.Any()) return string.Empty;
        
        var sb = new StringBuilder();
        sb.AppendLine("### 📁 Files");
        sb.AppendLine();
        
        foreach (var fileRef in references.Take(20))
        {
            var fileName = Path.GetFileName(fileRef.FilePath);
            var relativePath = GetRelativePath(fileRef.FilePath);
            
            // VS Code clickable format with enhanced display
            sb.AppendLine($"- **[{fileName}:{fileRef.Line}]({fileRef.GetVSCodeUri()})**");
            sb.AppendLine($"  `{relativePath}` - {fileRef.Description ?? "No description"}");
            
            if (!string.IsNullOrEmpty(fileRef.CodePreview))
            {
                sb.AppendLine($"  ```{fileRef.Language ?? "text"}");
                sb.AppendLine($"  {fileRef.CodePreview.Trim()}");
                sb.AppendLine($"  ```");
            }
            sb.AppendLine();
        }
        
        if (references.Count > 20)
        {
            sb.AppendLine($"*...and {references.Count - 20} more files (see resource for complete list)*");
        }
        
        return sb.ToString();
    }
    
    public string FormatActions(List<ActionItem> actions)
    {
        if (!actions.Any()) return string.Empty;
        
        var sb = new StringBuilder();
        sb.AppendLine("### 🚀 Available Actions");
        sb.AppendLine();
        
        foreach (var action in actions)
        {
            var commandUri = $"command:{action.Command}?{Uri.EscapeDataString(JsonSerializer.Serialize(action.Parameters))}";
            sb.AppendLine($"- **[{action.Title}]({commandUri})**");
            
            if (!string.IsNullOrEmpty(action.Description))
            {
                sb.AppendLine($"  {action.Description}");
            }
            
            if (!string.IsNullOrEmpty(action.KeyboardShortcut))
            {
                sb.AppendLine($"  *Shortcut: {action.KeyboardShortcut}*");
            }
            
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
    
    public string FormatTable(DataTable data)
    {
        if (data.Rows.Count <= 10)
        {
            // Small table - use markdown
            return FormatMarkdownTable(data);
        }
        else
        {
            // Large table - create HTML resource
            return $"📊 **{data.Rows.Count} rows** - [View Interactive Table](mcp://table/{Guid.NewGuid():N})";
        }
    }
    
    private string FormatMarkdownTable(DataTable data)
    {
        var sb = new StringBuilder();
        
        // Headers
        sb.AppendLine($"| {string.Join(" | ", data.Columns.Cast<DataColumn>().Select(c => c.ColumnName))} |");
        sb.AppendLine($"| {string.Join(" | ", data.Columns.Cast<DataColumn>().Select(_ => "---"))} |");
        
        // Rows
        foreach (DataRow row in data.Rows)
        {
            var values = row.ItemArray.Select(v => v?.ToString()?.Replace("|", "\\|") ?? "");
            sb.AppendLine($"| {string.Join(" | ", values)} |");
        }
        
        return sb.ToString();
    }
    
    private string GetRelativePath(string fullPath)
    {
        var workspaceRoot = Environment.GetEnvironmentVariable("WORKSPACE_ROOT");
        if (!string.IsNullOrEmpty(workspaceRoot) && fullPath.StartsWith(workspaceRoot))
        {
            return Path.GetRelativePath(workspaceRoot, fullPath);
        }
        return fullPath;
    }
}
```

### Visual Studio 2022 Formatter

```csharp
public class VS2022InlineFormatter : IOutputFormatter
{
    private readonly IDEEnvironment _environment;
    
    public VS2022InlineFormatter(IDEEnvironment environment)
    {
        _environment = environment;
    }
    
    public string FormatSummary(string summary, object data)
    {
        var sb = new StringBuilder();
        
        // VS 2022 Output Window style
        sb.AppendLine($"========== {summary.ToUpper()} ==========");
        sb.AppendLine($"Generated at: {DateTime.Now:HH:mm:ss}");
        sb.AppendLine($"Environment: Visual Studio {_environment.Version}");
        sb.AppendLine();
        
        return sb.ToString();
    }
    
    public string FormatFileReferences(List<FileReference> references)
    {
        if (!references.Any()) return string.Empty;
        
        var sb = new StringBuilder();
        sb.AppendLine($"Files found ({references.Count}):");
        sb.AppendLine();
        
        foreach (var fileRef in references)
        {
            // VS 2022 Error List format (clickable)
            sb.AppendLine($"  {fileRef.FilePath}({fileRef.Line},{fileRef.Column})");
            
            if (!string.IsNullOrEmpty(fileRef.Description))
            {
                sb.AppendLine($"    Description: {fileRef.Description}");
            }
            
            if (!string.IsNullOrEmpty(fileRef.ProjectName))
            {
                sb.AppendLine($"    Project: {fileRef.ProjectName}");
            }
            
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
    
    public string FormatActions(List<ActionItem> actions)
    {
        if (!actions.Any()) return string.Empty;
        
        var sb = new StringBuilder();
        sb.AppendLine("Available actions:");
        sb.AppendLine();
        
        for (int i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
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
            
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
    
    public string FormatTable(DataTable data)
    {
        var sb = new StringBuilder();
        
        // Calculate column widths
        var columnWidths = CalculateColumnWidths(data);
        
        // Header
        sb.AppendLine(FormatTableRow(data.Columns.Cast<DataColumn>().Select(c => c.ColumnName), columnWidths));
        sb.AppendLine(new string('-', columnWidths.Sum() + (columnWidths.Count - 1) * 3));
        
        // Rows (limit to first 50 for performance)
        foreach (DataRow row in data.Rows.Cast<DataRow>().Take(50))
        {
            sb.AppendLine(FormatTableRow(row.ItemArray.Select(v => v?.ToString() ?? ""), columnWidths));
        }
        
        if (data.Rows.Count > 50)
        {
            sb.AppendLine($"... and {data.Rows.Count - 50} more rows (export to see all)");
        }
        
        return sb.ToString();
    }
    
    private string MapToVS2022Command(string mcpCommand)
    {
        return mcpCommand switch
        {
            "find_references" => "Edit.FindAllReferences",
            "goto_definition" => "Edit.GoToDefinition",
            "find_implementations" => "Edit.GoToImplementation",
            "rename_symbol" => "Refactor.Rename",
            _ => null
        };
    }
    
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
    
    private string FormatTableRow(IEnumerable<string> values, int[] columnWidths)
    {
        var paddedValues = values.Select((v, i) => (v ?? "").PadRight(columnWidths[i]));
        return string.Join(" | ", paddedValues);
    }
}
```

### Terminal Formatter

```csharp
public class TerminalFormatter : IOutputFormatter
{
    private readonly IDEEnvironment _environment;
    
    public TerminalFormatter(IDEEnvironment environment)
    {
        _environment = environment;
    }
    
    public string FormatSummary(string summary, object data)
    {
        var sb = new StringBuilder();
        
        // Simple, clean terminal output
        sb.AppendLine($"=== {summary} ===");
        sb.AppendLine($"Time: {DateTime.Now:HH:mm:ss}");
        sb.AppendLine();
        
        return sb.ToString();
    }
    
    public string FormatFileReferences(List<FileReference> references)
    {
        if (!references.Any()) return string.Empty;
        
        var sb = new StringBuilder();
        sb.AppendLine($"Files ({references.Count}):");
        
        foreach (var fileRef in references)
        {
            // Simple clickable format
            sb.AppendLine($"  {fileRef.FilePath}:{fileRef.Line}:{fileRef.Column}");
            
            if (!string.IsNullOrEmpty(fileRef.Description))
            {
                sb.AppendLine($"    {fileRef.Description}");
            }
        }
        
        sb.AppendLine();
        return sb.ToString();
    }
    
    public string FormatActions(List<ActionItem> actions)
    {
        if (!actions.Any()) return string.Empty;
        
        var sb = new StringBuilder();
        sb.AppendLine("Actions:");
        
        for (int i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            sb.AppendLine($"  {i + 1}. {action.Title}");
            
            if (!string.IsNullOrEmpty(action.Description))
            {
                sb.AppendLine($"     {action.Description}");
            }
        }
        
        sb.AppendLine();
        return sb.ToString();
    }
    
    public string FormatTable(DataTable data)
    {
        // ASCII table for terminal
        var columnWidths = CalculateColumnWidths(data);
        var sb = new StringBuilder();
        
        // Header
        sb.AppendLine("+" + string.Join("+", columnWidths.Select(w => new string('-', w + 2))) + "+");
        
        var headers = data.Columns.Cast<DataColumn>().Select((c, i) => $" {c.ColumnName.PadRight(columnWidths[i])} ");
        sb.AppendLine("|" + string.Join("|", headers) + "|");
        
        sb.AppendLine("+" + string.Join("+", columnWidths.Select(w => new string('-', w + 2))) + "+");
        
        // Rows (limit to 20 for terminal display)
        foreach (DataRow row in data.Rows.Cast<DataRow>().Take(20))
        {
            var values = row.ItemArray.Select((v, i) => $" {(v?.ToString() ?? "").PadRight(columnWidths[i])} ");
            sb.AppendLine("|" + string.Join("|", values) + "|");
        }
        
        sb.AppendLine("+" + string.Join("+", columnWidths.Select(w => new string('-', w + 2))) + "+");
        
        if (data.Rows.Count > 20)
        {
            sb.AppendLine($"({data.Rows.Count - 20} more rows...)");
        }
        
        return sb.ToString();
    }
    
    private int[] CalculateColumnWidths(DataTable data)
    {
        var widths = new int[data.Columns.Count];
        
        // Initialize with header widths
        for (int i = 0; i < data.Columns.Count; i++)
        {
            widths[i] = data.Columns[i].ColumnName.Length;
        }
        
        // Check data rows
        foreach (DataRow row in data.Rows.Cast<DataRow>().Take(50))
        {
            for (int i = 0; i < row.ItemArray.Length; i++)
            {
                var value = row.ItemArray[i]?.ToString() ?? "";
                widths[i] = Math.Max(widths[i], Math.Min(value.Length, 30)); // Cap at 30 for terminal
            }
        }
        
        return widths;
    }
}
```

## Resource Formatters

### HTML Resource Formatter

```csharp
public class HTMLResourceFormatter : IResourceFormatter
{
    private readonly IDEEnvironment _environment;
    
    public HTMLResourceFormatter(IDEEnvironment environment)
    {
        _environment = environment;
    }
    
    public async Task<string> FormatResourceAsync<T>(T data)
    {
        var html = new StringBuilder();
        
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html>");
        html.AppendLine("<head>");
        html.AppendLine("    <meta charset='utf-8'>");
        html.AppendLine("    <title>MCP Result</title>");
        html.AppendLine(GetCSS());
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        
        if (data is DataTable table)
        {
            html.AppendLine(FormatDataTable(table));
        }
        else if (data is IEnumerable<object> list)
        {
            html.AppendLine(FormatObjectList(list));
        }
        else
        {
            html.AppendLine(FormatGenericObject(data));
        }
        
        html.AppendLine(GetJavaScript());
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        
        return html.ToString();
    }
    
    private string GetCSS()
    {
        return """
        <style>
            body { 
                font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; 
                margin: 20px; 
                background-color: #1e1e1e; 
                color: #cccccc; 
            }
            .mcp-table { 
                width: 100%; 
                border-collapse: collapse; 
                margin-top: 20px; 
            }
            .mcp-table th, .mcp-table td { 
                border: 1px solid #333; 
                padding: 8px 12px; 
                text-align: left; 
            }
            .mcp-table th { 
                background-color: #2d2d30; 
                font-weight: bold; 
                cursor: pointer; 
                user-select: none; 
            }
            .mcp-table th:hover { 
                background-color: #3e3e42; 
            }
            .mcp-table tr:nth-child(even) { 
                background-color: #252526; 
            }
            .mcp-table tr:hover { 
                background-color: #2a2d2e; 
            }
            .filter-input { 
                width: 300px; 
                padding: 8px; 
                margin-bottom: 10px; 
                background-color: #3c3c3c; 
                border: 1px solid #555; 
                color: #cccccc; 
                border-radius: 4px; 
            }
            .stats { 
                margin-bottom: 15px; 
                padding: 10px; 
                background-color: #2d2d30; 
                border-radius: 4px; 
            }
            .clickable { 
                color: #4fc3f7; 
                text-decoration: none; 
                cursor: pointer; 
            }
            .clickable:hover { 
                text-decoration: underline; 
            }
        </style>
        """;
    }
    
    private string FormatDataTable(DataTable table)
    {
        var html = new StringBuilder();
        
        // Statistics
        html.AppendLine($"<div class='stats'>");
        html.AppendLine($"    <strong>Total Rows:</strong> {table.Rows.Count:N0}<br>");
        html.AppendLine($"    <strong>Columns:</strong> {table.Columns.Count}<br>");
        html.AppendLine($"    <strong>Generated:</strong> {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        html.AppendLine($"</div>");
        
        // Filter input
        html.AppendLine($"<input type='text' class='filter-input' id='tableFilter' placeholder='Filter rows...' onkeyup='filterTable()'>");
        
        // Table
        html.AppendLine($"<table class='mcp-table' id='dataTable'>");
        
        // Headers
        html.AppendLine("<thead><tr>");
        for (int i = 0; i < table.Columns.Count; i++)
        {
            var column = table.Columns[i];
            html.AppendLine($"<th onclick='sortTable({i})'>{column.ColumnName}</th>");
        }
        html.AppendLine("</tr></thead>");
        
        // Body
        html.AppendLine("<tbody>");
        foreach (DataRow row in table.Rows)
        {
            html.AppendLine("<tr>");
            foreach (var cell in row.ItemArray)
            {
                var value = cell?.ToString() ?? "";
                
                // Make file paths clickable
                if (IsFilePath(value))
                {
                    var parts = value.Split(':');
                    if (parts.Length >= 2)
                    {
                        var filePath = parts[0];
                        var line = parts.Length > 1 ? parts[1] : "1";
                        var uri = _environment.IDE == IDEType.VSCode 
                            ? $"vscode://file/{filePath}#{line}"
                            : $"file:///{filePath}#{line}";
                        value = $"<a href='{uri}' class='clickable'>{value}</a>";
                    }
                }
                
                html.AppendLine($"<td>{value}</td>");
            }
            html.AppendLine("</tr>");
        }
        html.AppendLine("</tbody>");
        
        html.AppendLine("</table>");
        
        return html.ToString();
    }
    
    private string GetJavaScript()
    {
        return """
        <script>
            function sortTable(columnIndex) {
                const table = document.getElementById('dataTable');
                const tbody = table.querySelector('tbody');
                const rows = Array.from(tbody.querySelectorAll('tr'));
                
                rows.sort((a, b) => {
                    const aValue = a.cells[columnIndex].textContent.trim();
                    const bValue = b.cells[columnIndex].textContent.trim();
                    
                    // Try to parse as numbers
                    const aNum = parseFloat(aValue);
                    const bNum = parseFloat(bValue);
                    
                    if (!isNaN(aNum) && !isNaN(bNum)) {
                        return aNum - bNum;
                    }
                    
                    // String comparison
                    return aValue.localeCompare(bValue);
                });
                
                // Clear and re-append sorted rows
                tbody.innerHTML = '';
                rows.forEach(row => tbody.appendChild(row));
            }
            
            function filterTable() {
                const filter = document.getElementById('tableFilter').value.toLowerCase();
                const table = document.getElementById('dataTable');
                const rows = table.querySelectorAll('tbody tr');
                
                rows.forEach(row => {
                    const text = row.textContent.toLowerCase();
                    row.style.display = text.includes(filter) ? '' : 'none';
                });
                
                // Update visible count
                const visibleRows = Array.from(rows).filter(row => row.style.display !== 'none').length;
                console.log(`Showing ${visibleRows} of ${rows.length} rows`);
            }
        </script>
        """;
    }
    
    private bool IsFilePath(string value)
    {
        return value.Contains(":\\") || value.Contains("/") && value.Contains(":");
    }
    
    private string FormatObjectList(IEnumerable<object> list)
    {
        // Convert object list to table-like format
        var items = list.ToList();
        if (!items.Any()) return "<p>No data</p>";
        
        var html = new StringBuilder();
        html.AppendLine($"<div class='stats'>Total Items: {items.Count}</div>");
        
        // Get all unique properties
        var properties = new HashSet<string>();
        foreach (var item in items)
        {
            if (item != null)
            {
                properties.UnionWith(item.GetType().GetProperties().Select(p => p.Name));
            }
        }
        
        html.AppendLine("<table class='mcp-table'>");
        
        // Headers
        html.AppendLine("<thead><tr>");
        foreach (var prop in properties)
        {
            html.AppendLine($"<th>{prop}</th>");
        }
        html.AppendLine("</tr></thead>");
        
        // Rows
        html.AppendLine("<tbody>");
        foreach (var item in items)
        {
            html.AppendLine("<tr>");
            foreach (var prop in properties)
            {
                var value = item?.GetType().GetProperty(prop)?.GetValue(item)?.ToString() ?? "";
                html.AppendLine($"<td>{value}</td>");
            }
            html.AppendLine("</tr>");
        }
        html.AppendLine("</tbody>");
        
        html.AppendLine("</table>");
        
        return html.ToString();
    }
    
    private string FormatGenericObject(object data)
    {
        var html = new StringBuilder();
        
        html.AppendLine($"<div class='stats'>Data Type: {data?.GetType().Name ?? "null"}</div>");
        
        if (data != null)
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            html.AppendLine($"<pre style='background-color: #2d2d30; padding: 15px; border-radius: 4px; overflow-x: auto;'>");
            html.AppendLine(System.Web.HttpUtility.HtmlEncode(json));
            html.AppendLine($"</pre>");
        }
        
        return html.ToString();
    }
}
```

## Usage Examples

### Implementing an Adaptive Tool

```csharp
[Tool("search_code")]
public class SearchCodeTool : AdaptiveResponseBuilder<SearchParams, SearchResult>
{
    private readonly ICodeSearchService _searchService;
    
    public SearchCodeTool(ICodeSearchService searchService, ILogger<SearchCodeTool> logger) 
        : base(logger)
    {
        _searchService = searchService;
    }
    
    protected override string GetOperationName() => "code_search";
    
    protected override async Task<SearchResult> ExecuteInternalAsync(
        SearchParams parameters, 
        CancellationToken cancellationToken)
    {
        var matches = await _searchService.SearchAsync(parameters.Query, parameters.FilePath);
        
        var context = new ResponseContext
        {
            ResponseMode = parameters.ResponseMode ?? "full",
            TokenLimit = parameters.MaxTokens
        };
        
        return await BuildResponseAsync(matches, context);
    }
    
    protected override async Task ApplyAdaptiveFormattingAsync(
        SearchResult result, 
        List<CodeMatch> data, 
        ResponseContext context)
    {
        var formatter = _formatterFactory.CreateInlineFormatter(_environment);
        
        result.Summary = $"Found {data.Count} matches for '{parameters.Query}'";
        result.Message = formatter.FormatSummary(result.Summary, data);
        
        // File references
        result.FileReferences = data.Select(m => new FileReference
        {
            FilePath = m.FilePath,
            Line = m.Line,
            Column = m.Column,
            Description = m.ContextLine,
            Language = m.Language,
            CodePreview = m.CodePreview
        }).ToList();
        
        result.Message += formatter.FormatFileReferences(result.FileReferences);
        
        // Actions based on IDE
        var actions = new List<ActionItem>();
        
        if (_environment.IDE == IDEType.VSCode)
        {
            actions.Add(new ActionItem
            {
                Title = "Open in Search Editor",
                Command = "search.action.openInEditor",
                Description = "Open results in VS Code search editor"
            });
        }
        else if (_environment.IDE == IDEType.VS2022)
        {
            actions.Add(new ActionItem
            {
                Title = "Find in Files",
                Command = "Edit.FindinFiles",
                Description = "Open in Find in Files window"
            });
        }
        
        actions.Add(new ActionItem
        {
            Title = "Refine Search",
            Command = "mcp.refineSearch",
            Parameters = new { originalQuery = parameters.Query }
        });
        
        result.Message += formatter.FormatActions(actions);
        
        // Set display hint based on result size
        result.IDEDisplayHint = data.Count > 50 ? "table" : "markdown";
    }
}
```

### Configuration

```csharp
// In Program.cs or service configuration
builder.Services.AddSingleton<IOutputFormatterFactory, OutputFormatterFactory>();
builder.Services.AddScoped<IResourceProvider, FileSystemResourceProvider>();

// Register adaptive tools
builder.RegisterToolType<SearchCodeTool>();
builder.RegisterToolType<FindReferencesTool>();
builder.RegisterToolType<SchemaExplorerTool>();
```

## Integration with Existing Framework

The Adaptive Response Framework integrates seamlessly with the existing COA MCP Framework:

1. **Extends BaseResponseBuilder**: Builds on existing token optimization
2. **Uses ResourceRegistry**: Leverages existing resource caching
3. **Maintains Compatibility**: Works with existing tools without changes
4. **Progressive Enhancement**: Existing tools get basic IDE detection, new tools get full adaptive formatting

This framework ensures that all MCP tools provide the best possible experience across different development environments while maintaining backwards compatibility and the lean, fast architecture of the original framework.