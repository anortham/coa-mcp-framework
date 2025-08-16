# MCP Multi-IDE Output Formatting Guide

## Overview

This guide describes how MCP servers should format their output to provide optimal integration with both VS Code and Visual Studio 2022, while maintaining compatibility with terminal-based tools like Claude Code.

## Core Formatting Principles

1. **Adaptive Formatting**: Detect IDE environment and optimize output accordingly
2. **Clickable Navigation**: All file references should be clickable in supported environments
3. **Rich Visualizations**: Use HTML, charts, and tables for complex data
4. **Progressive Disclosure**: Show summaries with expandable details
5. **Universal Fallbacks**: Ensure content works in all environments

## IDE Detection and Adaptive Response

### Environment Detection
```csharp
public class IDEEnvironment
{
    public static IDEEnvironment Detect()
    {
        var vsCodePid = Environment.GetEnvironmentVariable("VSCODE_PID");
        var vsVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");
        
        return new IDEEnvironment
        {
            IDE = vsCodePid != null ? IDEType.VSCode :
                  vsVersion != null ? IDEType.VS2022 :
                  IDEType.Terminal,
            SupportsHTML = vsCodePid != null || vsVersion != null,
            SupportsMarkdown = true, // All support markdown
            SupportsInteractive = vsCodePid != null || vsVersion != null
        };
    }
}

public enum IDEType { VSCode, VS2022, Terminal }
```

## File Reference Formats

### Universal File:Line:Column Format

Both VS Code and VS 2022 recognize and make these patterns clickable:

```csharp
public class FileReference
{
    public string FilePath { get; set; }
    public int Line { get; set; }
    public int Column { get; set; }
    
    public string FormatForIDE(IDEEnvironment env)
    {
        return env.IDE switch
        {
            IDEType.VSCode => FormatForVSCode(),
            IDEType.VS2022 => FormatForVS2022(), 
            IDEType.Terminal => FormatForTerminal(),
            _ => FormatUniversal()
        };
    }
    
    private string FormatForVSCode()
    {
        // VS Code clickable format with enhanced context
        var fileName = Path.GetFileName(FilePath);
        return $"📁 **{fileName}:{Line}:{Column}**\n   `{FilePath}`";
    }
    
    private string FormatForVS2022()
    {
        // VS 2022 Solution Explorer integration
        var projectFile = FindProjectFile(FilePath);
        return $"🔍 **{Path.GetFileName(FilePath)}** (Line {Line})\n" +
               $"   Project: {projectFile}\n" +
               $"   Path: `{FilePath}`";
    }
    
    private string FormatUniversal()
    {
        // Universal clickable format (works in both IDEs)
        return $"{FilePath}:{Line}:{Column}";
    }
}
```

### Markdown Link Formats by IDE

```csharp
public string FormatAsMarkdownLink(IDEEnvironment env)
{
    var display = $"{Path.GetFileName(FilePath)}:{Line}";
    
    return env.IDE switch
    {
        IDEType.VSCode => $"[{display}](file:///{FilePath.Replace('\\', '/')}#L{Line})",
        IDEType.VS2022 => $"[{display}](file:///{FilePath}#{Line})",
        _ => $"`{display}` - {FilePath}"
    };
}
```

### IDE-Specific URI Schemes

```csharp
public class IDEUriFormatter
{
    public static string CreateUri(string filePath, int line, int column, IDEEnvironment env)
    {
        return env.IDE switch
        {
            IDEType.VSCode => $"vscode://file/{filePath}:{line}:{column}",
            IDEType.VS2022 => $"vs://open/?file={Uri.EscapeDataString(filePath)}&line={line}&column={column}",
            _ => $"file:///{filePath}#{line}"
        };
    }
}

## Adaptive Result Structure

### Base Result Class with IDE Support

```csharp
public abstract class AdaptiveFormattedResult : ToolResultBase
{
    public string Summary { get; set; }
    public List<FileReference> FileReferences { get; set; } = new();
    public List<CodeSnippet> CodePreviews { get; set; } = new();
    public List<IDEAction> Actions { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public string IDEDisplayHint { get; set; } = "auto"; // table|chart|tree|markdown|diff
    
    public override string ToString()
    {
        var env = IDEEnvironment.Detect();
        var formatter = AdaptiveOutputFormatterFactory.Create(env);
        return formatter.Format(this);
    }
}
```

### Adaptive Output Formatter Factory

```csharp
public static class AdaptiveOutputFormatterFactory
{
    public static IOutputFormatter Create(IDEEnvironment env)
    {
        return env.IDE switch
        {
            IDEType.VSCode => new VSCodeOutputFormatter(),
            IDEType.VS2022 => new VS2022OutputFormatter(),
            IDEType.Terminal => new TerminalOutputFormatter(),
            _ => new UniversalOutputFormatter()
        };
    }
}

public interface IOutputFormatter
{
    string Format(AdaptiveFormattedResult result);
    string FormatTable(DataTable data);
    string FormatChart(ChartData data);
    string FormatTree(TreeNode root);
}
```

### VS Code Formatter Implementation

```csharp
public class VSCodeOutputFormatter : IOutputFormatter
{
    public string Format(AdaptiveFormattedResult result)
    {
        var output = new StringBuilder();
        
        // Rich markdown header
        output.AppendLine($"## {result.Summary}\n");
        
        // File references with VS Code styling
        if (result.FileReferences.Any())
        {
            output.AppendLine("### 📁 Files");
            foreach (var fileRef in result.FileReferences.Take(10))
            {
                output.AppendLine($"- {fileRef.FormatForVSCode()}");
            }
            
            if (result.FileReferences.Count > 10)
                output.AppendLine($"\n*...and {result.FileReferences.Count - 10} more files*");
        }
        
        // Interactive actions
        if (result.Actions.Any())
        {
            output.AppendLine("\n### 🚀 Available Actions");
            foreach (var action in result.Actions)
            {
                output.AppendLine($"- **{action.Title}**: {action.Description}");
            }
        }
        
        return output.ToString();
    }
    
    public string FormatTable(DataTable data)
    {
        // Generate HTML table with sorting and filtering
        return GenerateHTMLTable(data, interactive: true);
    }
}
```

### Visual Studio 2022 Formatter Implementation

```csharp
public class VS2022OutputFormatter : IOutputFormatter
{
    public string Format(AdaptiveFormattedResult result)
    {
        var output = new StringBuilder();
        
        // VS 2022 Output Window style
        output.AppendLine($"=== {result.Summary} ===\n");
        
        // Solution-aware file references
        if (result.FileReferences.Any())
        {
            output.AppendLine("Files found:");
            foreach (var fileRef in result.FileReferences)
            {
                output.AppendLine($"  {fileRef.FormatForVS2022()}");
            }
        }
        
        // Integration with VS 2022 commands
        if (result.Actions.Any())
        {
            output.AppendLine("\nSuggested actions:");
            foreach (var action in result.Actions)
            {
                var command = action.MapToVS2022Command();
                output.AppendLine($"  • {action.Title} - {command}");
            }
        }
        
        return output.ToString();
}
```

## Visualization Formats by IDE

### HTML Tables (Both IDEs Support)

```csharp
public class HTMLTableGenerator
{
    public static string GenerateTable(DataTable data, IDEEnvironment env)
    {
        var tableId = $"table_{Guid.NewGuid().ToString("N")[..8]}";
        
        if (env.IDE == IDEType.VSCode)
            return GenerateVSCodeTable(data, tableId);
        else if (env.IDE == IDEType.VS2022)
            return GenerateVS2022Table(data, tableId);
        else
            return GenerateMarkdownTable(data);
    }
    
    private static string GenerateVSCodeTable(DataTable data, string tableId)
    {
        return $"""
        <div id="{tableId}" class="mcp-table">
            <style>
                .mcp-table {{ font-family: 'Consolas', monospace; }}
                .mcp-table table {{ border-collapse: collapse; width: 100%; }}
                .mcp-table th, .mcp-table td {{ 
                    border: 1px solid #333; 
                    padding: 8px; 
                    text-align: left; 
                }}
                .mcp-table th {{ 
                    background-color: #2d2d30; 
                    color: #cccccc; 
                    cursor: pointer; 
                }}
            </style>
            <table>
                <thead>{GenerateTableHeader(data)}</thead>
                <tbody>{GenerateTableBody(data)}</tbody>
            </table>
            <script>
                // Add sorting functionality
                document.querySelectorAll('#{tableId} th').forEach(th => {{
                    th.addEventListener('click', () => sortTable('{tableId}', th.cellIndex));
                }});
            </script>
        </div>
        """;
    }
}
```

### Chart Generation

```csharp
public class ChartGenerator
{
    public static string GenerateChart(ChartData data, IDEEnvironment env)
    {
        return env.IDE switch
        {
            IDEType.VSCode => GenerateChartJS(data),
            IDEType.VS2022 => GenerateSimpleChart(data),
            _ => GenerateASCIIChart(data)
        };
    }
    
    private static string GenerateChartJS(ChartData data)
    {
        var chartId = $"chart_{Guid.NewGuid().ToString("N")[..8]}";
        
        return $"""
        <div style="width: 600px; height: 400px;">
            <canvas id="{chartId}"></canvas>
        </div>
        <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
        <script>
            const ctx = document.getElementById('{chartId}').getContext('2d');
            new Chart(ctx, {{
                type: '{data.Type.ToLower()}',
                data: {JsonSerializer.Serialize(data.Data)},
                options: {{
                    responsive: true,
                    plugins: {{
                        title: {{ display: true, text: '{data.Title}' }}
                    }}
                }}
            }});
        </script>
        """;
    }
}
```

## Resource Management for Large Data

### Using MCP Resources for Heavy Content

```csharp
public class LargeDataHandler
{
    public async Task<AdaptiveFormattedResult> HandleLargeDataset<T>(
        List<T> data, 
        string operation,
        IDEEnvironment env)
    {
        var result = new AdaptiveFormattedResult { Summary = $"{operation} Results" };
        
        if (data.Count > 100) // Large dataset threshold
        {
            // Create resource for full data
            var resourceUri = await CreateDataResource(data, env);
            
            result.Summary = $"Found {data.Count} results (showing first 10)";
            result.ResourceUri = resourceUri;
            result.IDEDisplayHint = env.IDE == IDEType.Terminal ? "markdown" : "table";
            
            // Show summary with link to full data
            result.Metadata["preview"] = data.Take(10).ToList();
            result.Metadata["totalCount"] = data.Count;
        }
        else
        {
            // Small dataset - inline display
            result.Metadata["data"] = data;
            result.IDEDisplayHint = "table";
        }
        
        return result;
    }
    
    private async Task<string> CreateDataResource<T>(List<T> data, IDEEnvironment env)
    {
        var format = env.IDE switch
        {
            IDEType.VSCode => "html", // Rich interactive HTML
            IDEType.VS2022 => "csv",  // Excel-compatible
            _ => "json" // Universal
        };
        
        var resourceId = Guid.NewGuid().ToString();
        var content = format switch
        {
            "html" => GenerateHTMLResource(data),
            "csv" => GenerateCSVResource(data),
            _ => JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true })
        };
        
        await _resourceProvider.StoreAsync($"data/{resourceId}.{format}", content);
        return $"mcp://data/{resourceId}.{format}";
    }
}
```

## IDE-Specific Best Practices

### VS Code Optimization
- **Use Markdown**: Rich markdown rendering in chat
- **HTML Resources**: Interactive tables and charts via webview
- **File Links**: Support file:// URIs with line numbers
- **Syntax Highlighting**: Include language hints in code blocks
- **Progressive Disclosure**: Collapsible sections work well

### Visual Studio 2022 Optimization  
- **Output Window Style**: Familiar format for developers
- **Solution Integration**: Reference projects and solution structure
- **IntelliSense Hints**: Provide symbol information for better integration
- **Error List Format**: Use familiar error/warning/info patterns
- **Tool Window Links**: Support opening in specialized windows

### Terminal/Claude Code Compatibility
- **Plain Text**: Always provide readable plain text version
- **ANSI Colors**: Use sparingly, focus on structure
- **ASCII Art**: Simple diagrams and separators
- **Copy-Paste Friendly**: Ensure content works when copied

## Implementation Example

### Complete MCP Tool with Adaptive Formatting

```csharp
[Tool("find_references")]
public class FindReferencesTool : AdaptiveToolBase<FindReferencesParams, FindReferencesResult>
{
    protected override async Task<FindReferencesResult> ExecuteInternalAsync(
        FindReferencesParams parameters, 
        CancellationToken cancellationToken)
    {
        var references = await _codeAnalyzer.FindReferencesAsync(
            parameters.Symbol, parameters.FilePath);
            
        var env = IDEEnvironment.Detect();
        
        return new FindReferencesResult
        {
            Success = true,
            Summary = $"Found {references.Count} references to '{parameters.Symbol}'",
            FileReferences = references.Select(r => new FileReference 
            { 
                FilePath = r.FilePath, 
                Line = r.Line, 
                Column = r.Column 
            }).ToList(),
            IDEDisplayHint = references.Count > 20 ? "table" : "markdown",
            ResourceUri = references.Count > 100 ? 
                await CreateReferencesResource(references, env) : null
        };
    }
}
```

This multi-IDE formatting approach ensures that developers get the best possible experience regardless of their preferred development environment, while maintaining backwards compatibility with terminal-based tools.