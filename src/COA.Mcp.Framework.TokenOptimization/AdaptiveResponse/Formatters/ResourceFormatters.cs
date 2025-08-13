using System.Data;
using System.Text;
using System.Text.Json;
using COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Models;

namespace COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Formatters;

/// <summary>
/// Base class for resource formatters with common functionality.
/// </summary>
public abstract class BaseResourceFormatter : IResourceFormatter
{
    public abstract Task<string> FormatResourceAsync<T>(T data);
    public abstract string GetMimeType();
    public abstract string GetFileExtension();
    
    protected string EscapeHtml(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }
}

/// <summary>
/// HTML resource formatter with interactive features for large datasets.
/// </summary>
public class HTMLResourceFormatter : BaseResourceFormatter
{
    private readonly IDEEnvironment _environment;
    
    public HTMLResourceFormatter(IDEEnvironment environment)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }
    
    public override async Task<string> FormatResourceAsync<T>(T data)
    {
        var html = new StringBuilder();
        
        html.AppendLine(GenerateHTMLHeader("MCP Result"));
        html.AppendLine(GenerateCSS());
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
        
        html.AppendLine(GenerateJavaScript());
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        
        return await Task.FromResult(html.ToString());
    }
    
    public override string GetMimeType() => "text/html";
    public override string GetFileExtension() => ".html";
    
    private string GenerateHTMLHeader(string title)
    {
        return $"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>{EscapeHtml(title)}</title>
        """;
    }
    
    private string GenerateCSS()
    {
        return """
        <style>
            :root {
                --bg-primary: #1e1e1e;
                --bg-secondary: #2d2d30;
                --bg-tertiary: #3e3e42;
                --text-primary: #cccccc;
                --text-secondary: #9cdcfe;
                --border: #333;
                --accent: #4fc3f7;
                --success: #28a745;
                --warning: #ffc107;
                --danger: #dc3545;
            }
            
            * { box-sizing: border-box; }
            
            body { 
                font-family: 'Segoe UI', 'SF Pro Display', system-ui, sans-serif;
                margin: 0;
                padding: 20px;
                background-color: var(--bg-primary);
                color: var(--text-primary);
                line-height: 1.6;
            }
            
            .container {
                max-width: 1400px;
                margin: 0 auto;
            }
            
            .header {
                margin-bottom: 30px;
                padding: 20px;
                background: linear-gradient(135deg, var(--bg-secondary), var(--bg-tertiary));
                border-radius: 8px;
                border: 1px solid var(--border);
            }
            
            .header h1 {
                margin: 0 0 10px 0;
                color: var(--text-secondary);
                font-size: 24px;
                font-weight: 600;
            }
            
            .stats {
                display: flex;
                gap: 20px;
                margin-bottom: 20px;
                flex-wrap: wrap;
            }
            
            .stat-card {
                background-color: var(--bg-secondary);
                padding: 15px 20px;
                border-radius: 6px;
                border: 1px solid var(--border);
                min-width: 120px;
            }
            
            .stat-value {
                font-size: 24px;
                font-weight: bold;
                color: var(--accent);
                margin-bottom: 5px;
            }
            
            .stat-label {
                font-size: 12px;
                text-transform: uppercase;
                letter-spacing: 0.5px;
                opacity: 0.8;
            }
            
            .controls {
                margin-bottom: 20px;
                display: flex;
                gap: 10px;
                align-items: center;
                flex-wrap: wrap;
            }
            
            .filter-input {
                padding: 8px 12px;
                background-color: var(--bg-secondary);
                border: 1px solid var(--border);
                color: var(--text-primary);
                border-radius: 4px;
                min-width: 250px;
                font-size: 14px;
            }
            
            .filter-input:focus {
                outline: none;
                border-color: var(--accent);
                box-shadow: 0 0 0 2px rgba(79, 195, 247, 0.2);
            }
            
            .btn {
                padding: 8px 16px;
                background-color: var(--accent);
                color: white;
                border: none;
                border-radius: 4px;
                cursor: pointer;
                font-size: 14px;
                transition: background-color 0.2s;
            }
            
            .btn:hover {
                background-color: #45b7e8;
            }
            
            .mcp-table {
                width: 100%;
                border-collapse: collapse;
                background-color: var(--bg-secondary);
                border-radius: 8px;
                overflow: hidden;
                box-shadow: 0 4px 6px rgba(0, 0, 0, 0.3);
            }
            
            .mcp-table th,
            .mcp-table td {
                border: 1px solid var(--border);
                padding: 12px 16px;
                text-align: left;
                vertical-align: top;
            }
            
            .mcp-table th {
                background-color: var(--bg-tertiary);
                font-weight: 600;
                cursor: pointer;
                user-select: none;
                position: sticky;
                top: 0;
                z-index: 10;
            }
            
            .mcp-table th:hover {
                background-color: #4a4a4f;
            }
            
            .mcp-table th.sorted-asc::after {
                content: ' ↑';
                color: var(--accent);
            }
            
            .mcp-table th.sorted-desc::after {
                content: ' ↓';
                color: var(--accent);
            }
            
            .mcp-table tbody tr:nth-child(even) {
                background-color: #252526;
            }
            
            .mcp-table tbody tr:hover {
                background-color: #2a2d2e;
            }
            
            .clickable {
                color: var(--accent);
                text-decoration: none;
                cursor: pointer;
            }
            
            .clickable:hover {
                text-decoration: underline;
            }
            
            .table-container {
                max-height: 70vh;
                overflow: auto;
                border: 1px solid var(--border);
                border-radius: 8px;
            }
            
            .object-viewer {
                background-color: var(--bg-secondary);
                border: 1px solid var(--border);
                border-radius: 8px;
                padding: 20px;
            }
            
            .json-viewer {
                background-color: #0d1117;
                padding: 20px;
                border-radius: 6px;
                overflow-x: auto;
                font-family: 'Consolas', 'Monaco', monospace;
                font-size: 13px;
                line-height: 1.5;
            }
            
            .footer {
                margin-top: 30px;
                padding: 15px;
                text-align: center;
                opacity: 0.7;
                font-size: 12px;
            }
            
            @media (max-width: 768px) {
                body { padding: 10px; }
                .stats { flex-direction: column; }
                .controls { flex-direction: column; align-items: stretch; }
                .filter-input { min-width: auto; }
            }
        </style>
        """;
    }
    
    private string FormatDataTable(DataTable table)
    {
        var html = new StringBuilder();
        
        html.AppendLine("<div class='container'>");
        
        // Header
        html.AppendLine("<div class='header'>");
        html.AppendLine("<h1>📊 Data Table</h1>");
        html.AppendLine("<div class='stats'>");
        html.AppendLine($"<div class='stat-card'><div class='stat-value'>{table.Rows.Count:N0}</div><div class='stat-label'>Rows</div></div>");
        html.AppendLine($"<div class='stat-card'><div class='stat-value'>{table.Columns.Count}</div><div class='stat-label'>Columns</div></div>");
        html.AppendLine($"<div class='stat-card'><div class='stat-value'>{DateTime.Now:HH:mm:ss}</div><div class='stat-label'>Generated</div></div>");
        html.AppendLine("</div>");
        html.AppendLine("</div>");
        
        // Controls
        html.AppendLine("<div class='controls'>");
        html.AppendLine("<input type='text' class='filter-input' id='tableFilter' placeholder='Filter rows...' onkeyup='filterTable()'>");
        html.AppendLine("<button class='btn' onclick='exportToCSV()'>Export CSV</button>");
        html.AppendLine("<button class='btn' onclick='resetTable()'>Reset</button>");
        html.AppendLine("</div>");
        
        // Table
        html.AppendLine("<div class='table-container'>");
        html.AppendLine("<table class='mcp-table' id='dataTable'>");
        
        // Headers
        html.AppendLine("<thead><tr>");
        for (int i = 0; i < table.Columns.Count; i++)
        {
            var column = table.Columns[i];
            html.AppendLine($"<th onclick='sortTable({i})' title='Click to sort'>{EscapeHtml(column.ColumnName)}</th>");
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
                    var uri = CreateFileUri(value);
                    value = $"<a href='{uri}' class='clickable'>{EscapeHtml(value)}</a>";
                }
                else
                {
                    value = EscapeHtml(value);
                }
                
                html.AppendLine($"<td>{value}</td>");
            }
            html.AppendLine("</tr>");
        }
        html.AppendLine("</tbody>");
        html.AppendLine("</table>");
        html.AppendLine("</div>");
        
        html.AppendLine("</div>"); // container
        
        return html.ToString();
    }
    
    private string FormatObjectList(IEnumerable<object> list)
    {
        var items = list.ToList();
        if (!items.Any()) return "<div class='container'><p>No data available</p></div>";
        
        var html = new StringBuilder();
        html.AppendLine("<div class='container'>");
        html.AppendLine("<div class='header'>");
        html.AppendLine("<h1>📋 Object List</h1>");
        html.AppendLine($"<div class='stats'><div class='stat-card'><div class='stat-value'>{items.Count:N0}</div><div class='stat-label'>Items</div></div></div>");
        html.AppendLine("</div>");
        
        // Convert to table format if possible
        var properties = new HashSet<string>();
        foreach (var item in items.Take(100))
        {
            if (item != null)
            {
                properties.UnionWith(item.GetType().GetProperties().Select(p => p.Name));
            }
        }
        
        if (properties.Any())
        {
            html.AppendLine("<div class='table-container'>");
            html.AppendLine("<table class='mcp-table'>");
            
            // Headers
            html.AppendLine("<thead><tr>");
            foreach (var prop in properties)
            {
                html.AppendLine($"<th>{EscapeHtml(prop)}</th>");
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
                    html.AppendLine($"<td>{EscapeHtml(value)}</td>");
                }
                html.AppendLine("</tr>");
            }
            html.AppendLine("</tbody>");
            html.AppendLine("</table>");
            html.AppendLine("</div>");
        }
        
        html.AppendLine("</div>");
        return html.ToString();
    }
    
    private string FormatGenericObject(object? data)
    {
        var html = new StringBuilder();
        html.AppendLine("<div class='container'>");
        html.AppendLine("<div class='header'>");
        html.AppendLine($"<h1>📄 {data?.GetType().Name ?? "Object"}</h1>");
        html.AppendLine("</div>");
        
        if (data != null)
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            html.AppendLine("<div class='object-viewer'>");
            html.AppendLine("<div class='json-viewer'>");
            html.AppendLine($"<pre>{EscapeHtml(json)}</pre>");
            html.AppendLine("</div>");
            html.AppendLine("</div>");
        }
        
        html.AppendLine("</div>");
        return html.ToString();
    }
    
    private string GenerateJavaScript()
    {
        return """
        <script>
            let sortDirection = {};
            
            function sortTable(columnIndex) {
                const table = document.getElementById('dataTable');
                const tbody = table.querySelector('tbody');
                const rows = Array.from(tbody.querySelectorAll('tr'));
                const header = table.querySelectorAll('th')[columnIndex];
                
                // Clear other sorted headers
                table.querySelectorAll('th').forEach(th => {
                    th.classList.remove('sorted-asc', 'sorted-desc');
                });
                
                // Determine sort direction
                const isAsc = !sortDirection[columnIndex];
                sortDirection[columnIndex] = isAsc;
                
                // Add sorted class
                header.classList.add(isAsc ? 'sorted-asc' : 'sorted-desc');
                
                rows.sort((a, b) => {
                    const aValue = a.cells[columnIndex].textContent.trim();
                    const bValue = b.cells[columnIndex].textContent.trim();
                    
                    // Try to parse as numbers
                    const aNum = parseFloat(aValue);
                    const bNum = parseFloat(bValue);
                    
                    if (!isNaN(aNum) && !isNaN(bNum)) {
                        return isAsc ? aNum - bNum : bNum - aNum;
                    }
                    
                    // String comparison
                    const result = aValue.localeCompare(bValue);
                    return isAsc ? result : -result;
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
            
            function resetTable() {
                document.getElementById('tableFilter').value = '';
                filterTable();
                
                // Clear sort indicators
                document.querySelectorAll('th').forEach(th => {
                    th.classList.remove('sorted-asc', 'sorted-desc');
                });
                sortDirection = {};
            }
            
            function exportToCSV() {
                const table = document.getElementById('dataTable');
                const rows = table.querySelectorAll('tr');
                const csv = [];
                
                rows.forEach(row => {
                    const cols = row.querySelectorAll('th, td');
                    const rowData = Array.from(cols).map(col => {
                        let text = col.textContent.trim();
                        // Escape quotes and wrap in quotes if contains comma
                        if (text.includes(',') || text.includes('"')) {
                            text = '"' + text.replace(/"/g, '""') + '"';
                        }
                        return text;
                    });
                    csv.push(rowData.join(','));
                });
                
                const csvContent = csv.join('\n');
                const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
                const link = document.createElement('a');
                link.href = URL.createObjectURL(blob);
                link.download = 'mcp-data.csv';
                link.click();
            }
            
            // Initialize
            document.addEventListener('DOMContentLoaded', function() {
                console.log('MCP HTML Resource loaded successfully');
            });
        </script>
        """;
    }
    
    private bool IsFilePath(string value)
    {
        return !string.IsNullOrEmpty(value) && 
               (value.Contains(":\\") || (value.Contains("/") && value.Contains(":")));
    }
    
    private string CreateFileUri(string filePath)
    {
        var parts = filePath.Split(':');
        if (parts.Length >= 2)
        {
            var file = parts[0];
            var line = parts.Length > 1 ? parts[1] : "1";
            
            return _environment.IDE switch
            {
                IDEType.VSCode => $"vscode://file/{file}#{line}",
                _ => $"file:///{file}#{line}"
            };
        }
        
        return $"file:///{filePath}";
    }
}

/// <summary>
/// CSV resource formatter for tabular data export.
/// </summary>
public class CSVResourceFormatter : BaseResourceFormatter
{
    public override async Task<string> FormatResourceAsync<T>(T data)
    {
        if (data is DataTable table)
        {
            return await FormatDataTableAsync(table);
        }
        else if (data is IEnumerable<object> list)
        {
            return await FormatObjectListAsync(list);
        }
        
        // Fallback to JSON-like CSV
        return await Task.FromResult($"Data,\"{data?.ToString() ?? "null"}\"");
    }
    
    public override string GetMimeType() => "text/csv";
    public override string GetFileExtension() => ".csv";
    
    private async Task<string> FormatDataTableAsync(DataTable table)
    {
        var csv = new StringBuilder();
        
        // Headers
        var headers = table.Columns.Cast<DataColumn>().Select(c => EscapeCsvField(c.ColumnName));
        csv.AppendLine(string.Join(",", headers));
        
        // Data rows
        foreach (DataRow row in table.Rows)
        {
            var values = row.ItemArray.Select(v => EscapeCsvField(v?.ToString() ?? ""));
            csv.AppendLine(string.Join(",", values));
        }
        
        return await Task.FromResult(csv.ToString());
    }
    
    private async Task<string> FormatObjectListAsync(IEnumerable<object> list)
    {
        var items = list.ToList();
        if (!items.Any()) return await Task.FromResult("No data");
        
        var csv = new StringBuilder();
        
        // Get all properties
        var properties = new HashSet<string>();
        foreach (var item in items)
        {
            if (item != null)
            {
                properties.UnionWith(item.GetType().GetProperties().Select(p => p.Name));
            }
        }
        
        // Headers
        csv.AppendLine(string.Join(",", properties.Select(EscapeCsvField)));
        
        // Data
        foreach (var item in items)
        {
            var values = properties.Select(prop => 
            {
                var value = item?.GetType().GetProperty(prop)?.GetValue(item)?.ToString() ?? "";
                return EscapeCsvField(value);
            });
            csv.AppendLine(string.Join(",", values));
        }
        
        return await Task.FromResult(csv.ToString());
    }
    
    private string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        
        // Escape quotes and wrap in quotes if contains comma, quote, or newline
        if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
        {
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }
        
        return field;
    }
}

/// <summary>
/// JSON resource formatter for structured data.
/// </summary>
public class JSONResourceFormatter : BaseResourceFormatter
{
    public override async Task<string> FormatResourceAsync<T>(T data)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        
        var json = JsonSerializer.Serialize(data, options);
        return await Task.FromResult(json);
    }
    
    public override string GetMimeType() => "application/json";
    public override string GetFileExtension() => ".json";
}

/// <summary>
/// Markdown resource formatter for documentation-style output.
/// </summary>
public class MarkdownResourceFormatter : BaseResourceFormatter
{
    public override async Task<string> FormatResourceAsync<T>(T data)
    {
        if (data is DataTable table)
        {
            return await FormatDataTableAsync(table);
        }
        else if (data is IEnumerable<object> list)
        {
            return await FormatObjectListAsync(list);
        }
        
        var md = new StringBuilder();
        md.AppendLine($"# {data?.GetType().Name ?? "Object"}");
        md.AppendLine();
        md.AppendLine("```json");
        md.AppendLine(JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        md.AppendLine("```");
        
        return await Task.FromResult(md.ToString());
    }
    
    public override string GetMimeType() => "text/markdown";
    public override string GetFileExtension() => ".md";
    
    private async Task<string> FormatDataTableAsync(DataTable table)
    {
        var md = new StringBuilder();
        md.AppendLine($"# Data Table ({table.Rows.Count:N0} rows)");
        md.AppendLine();
        
        if (table.Rows.Count == 0)
        {
            md.AppendLine("No data available.");
            return await Task.FromResult(md.ToString());
        }
        
        // Markdown table
        var headers = table.Columns.Cast<DataColumn>().Select(c => EscapeMarkdown(c.ColumnName));
        md.AppendLine($"| {string.Join(" | ", headers)} |");
        
        var separators = table.Columns.Cast<DataColumn>().Select(_ => "---");
        md.AppendLine($"| {string.Join(" | ", separators)} |");
        
        foreach (DataRow row in table.Rows.Cast<DataRow>().Take(1000)) // Limit for markdown
        {
            var values = row.ItemArray.Select(v => EscapeMarkdown(v?.ToString() ?? ""));
            md.AppendLine($"| {string.Join(" | ", values)} |");
        }
        
        if (table.Rows.Count > 1000)
        {
            md.AppendLine();
            md.AppendLine($"*...and {table.Rows.Count - 1000:N0} more rows*");
        }
        
        return await Task.FromResult(md.ToString());
    }
    
    private async Task<string> FormatObjectListAsync(IEnumerable<object> list)
    {
        var items = list.ToList();
        var md = new StringBuilder();
        md.AppendLine($"# Object List ({items.Count:N0} items)");
        md.AppendLine();
        
        foreach (var item in items.Take(100))
        {
            md.AppendLine($"- {EscapeMarkdown(item?.ToString() ?? "(null)")}");
        }
        
        if (items.Count > 100)
        {
            md.AppendLine($"- *...and {items.Count - 100:N0} more items*");
        }
        
        return await Task.FromResult(md.ToString());
    }
    
    private string EscapeMarkdown(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        
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
}

/// <summary>
/// XML resource formatter for structured data.
/// </summary>
public class XMLResourceFormatter : BaseResourceFormatter
{
    public override async Task<string> FormatResourceAsync<T>(T data)
    {
        var xml = new StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine($"<{data?.GetType().Name ?? "Data"}>");
        
        if (data is DataTable table)
        {
            xml.AppendLine(FormatDataTable(table));
        }
        else if (data is IEnumerable<object> list)
        {
            xml.AppendLine(FormatObjectList(list));
        }
        else
        {
            xml.AppendLine($"  <Value>{EscapeXml(data?.ToString() ?? "")}</Value>");
        }
        
        xml.AppendLine($"</{data?.GetType().Name ?? "Data"}>");
        
        return await Task.FromResult(xml.ToString());
    }
    
    public override string GetMimeType() => "application/xml";
    public override string GetFileExtension() => ".xml";
    
    private string FormatDataTable(DataTable table)
    {
        var xml = new StringBuilder();
        
        foreach (DataRow row in table.Rows)
        {
            xml.AppendLine("  <Row>");
            for (int i = 0; i < table.Columns.Count; i++)
            {
                var column = table.Columns[i];
                var value = row[i]?.ToString() ?? "";
                xml.AppendLine($"    <{column.ColumnName}>{EscapeXml(value)}</{column.ColumnName}>");
            }
            xml.AppendLine("  </Row>");
        }
        
        return xml.ToString();
    }
    
    private string FormatObjectList(IEnumerable<object> list)
    {
        var xml = new StringBuilder();
        var index = 0;
        
        foreach (var item in list)
        {
            xml.AppendLine($"  <Item{index++}>");
            xml.AppendLine($"    <Value>{EscapeXml(item?.ToString() ?? "")}</Value>");
            xml.AppendLine($"  </Item{index - 1}>");
        }
        
        return xml.ToString();
    }
    
    private string EscapeXml(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }
}