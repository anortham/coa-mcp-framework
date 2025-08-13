using System;
using System.Text.Json.Serialization;

namespace COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Models;

/// <summary>
/// Represents a file reference that can be formatted for different IDEs.
/// </summary>
public class FileReference
{
    /// <summary>
    /// Gets or sets the full file path.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the line number (1-based).
    /// </summary>
    public int Line { get; set; } = 1;
    
    /// <summary>
    /// Gets or sets the column number (1-based).
    /// </summary>
    public int Column { get; set; } = 1;
    
    /// <summary>
    /// Gets or sets a description of the reference.
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Gets or sets the programming language for syntax highlighting.
    /// </summary>
    public string? Language { get; set; }
    
    /// <summary>
    /// Gets or sets a code preview snippet.
    /// </summary>
    public string? CodePreview { get; set; }
    
    /// <summary>
    /// Gets or sets the project name containing this file.
    /// </summary>
    public string? ProjectName { get; set; }
    
    /// <summary>
    /// Gets or sets the relative path from the workspace root.
    /// </summary>
    public string? RelativePath { get; set; }
    
    /// <summary>
    /// Generates a VS Code URI for this file reference.
    /// </summary>
    /// <returns>A VS Code URI string.</returns>
    public string GetVSCodeUri()
    {
        var encodedPath = Uri.EscapeDataString(FilePath);
        return $"vscode://file/{encodedPath}:{Line}:{Column}";
    }
    
    /// <summary>
    /// Generates a Visual Studio URI for this file reference.
    /// </summary>
    /// <returns>A Visual Studio file reference string.</returns>
    public string GetVS2022Reference()
    {
        return $"{FilePath}({Line},{Column})";
    }
    
    /// <summary>
    /// Generates a terminal-friendly file reference.
    /// </summary>
    /// <returns>A simple file:line:column reference.</returns>
    public string GetTerminalReference()
    {
        return $"{FilePath}:{Line}:{Column}";
    }
}

/// <summary>
/// Represents an action item that can be formatted for different IDEs.
/// </summary>
public class ActionItem
{
    /// <summary>
    /// Gets or sets the action title.
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the command to execute.
    /// </summary>
    public string Command { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the action description.
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Gets or sets the keyboard shortcut (if applicable).
    /// </summary>
    public string? KeyboardShortcut { get; set; }
    
    /// <summary>
    /// Gets or sets the command parameters.
    /// </summary>
    public Dictionary<string, object>? Parameters { get; set; }
    
    /// <summary>
    /// Gets or sets the action category.
    /// </summary>
    public string? Category { get; set; }
    
    /// <summary>
    /// Gets or sets the action priority for ordering.
    /// </summary>
    public int Priority { get; set; } = 50;
}

/// <summary>
/// Extended tool result with IDE-specific display hints.
/// </summary>
public class AdaptiveToolResult : COA.Mcp.Framework.Models.ToolResultBase
{
    /// <summary>
    /// Gets or sets the display hint for the IDE.
    /// Values: "markdown", "table", "tree", "chart", "html"
    /// </summary>
    [JsonPropertyName("ideDisplayHint")]
    public string? IDEDisplayHint { get; set; }
    
    /// <summary>
    /// Gets or sets file references for IDE navigation.
    /// </summary>
    [JsonPropertyName("fileReferences")]
    public List<FileReference>? FileReferences { get; set; }
    
    /// <summary>
    /// Gets or sets IDE-specific action items.
    /// </summary>
    [JsonPropertyName("actionItems")]
    public List<ActionItem>? ActionItems { get; set; }
    
    /// <summary>
    /// Gets or sets a summary of the operation results.
    /// </summary>
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }
    
    /// <summary>
    /// Gets or sets additional metadata for the response.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
    
    /// <inheritdoc/>
    public override string Operation => "adaptive-tool-result";
}

/// <summary>
/// Context for response formatting operations.
/// </summary>
public class FormattingContext
{
    /// <summary>
    /// Gets or sets the IDE environment information.
    /// </summary>
    public IDEEnvironment Environment { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the response mode (summary/full).
    /// </summary>
    public string ResponseMode { get; set; } = "full";
    
    /// <summary>
    /// Gets or sets the token limit for the response.
    /// </summary>
    public int? TokenLimit { get; set; }
    
    /// <summary>
    /// Gets or sets whether to create resources for large data.
    /// </summary>
    public bool CreateResources { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the workspace root path for relative file references.
    /// </summary>
    public string? WorkspaceRoot { get; set; }
    
    /// <summary>
    /// Gets or sets custom formatting options.
    /// </summary>
    public Dictionary<string, object> Options { get; set; } = new();
}