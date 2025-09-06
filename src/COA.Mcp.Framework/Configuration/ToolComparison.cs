namespace COA.Mcp.Framework.Configuration;

/// <summary>
/// Represents a comparison between a server tool and built-in Claude tools.
/// Used for generating professional guidance about optimal tool selection.
/// </summary>
public class ToolComparison
{
    /// <summary>
    /// Gets or sets the task description this comparison applies to.
    /// Example: "Find code patterns", "Navigate to definition"
    /// </summary>
    public string Task { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the server's tool name for this task.
    /// Example: "text_search", "goto_definition"
    /// </summary>
    public string ServerTool { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the built-in Claude tool this replaces.
    /// Example: "grep", "Read", "Bash"
    /// </summary>
    public string BuiltInTool { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the advantage of using the server tool.
    /// Example: "Lucene-indexed with Tree-sitter parsing"
    /// </summary>
    public string Advantage { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the limitation of the built-in tool.
    /// Example: "Line-by-line search without type awareness"
    /// </summary>
    public string Limitation { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets specific performance metrics.
    /// Example: "100x faster, searches millions of lines in &lt;500ms"
    /// </summary>
    public string PerformanceMetric { get; set; } = string.Empty;
}

/// <summary>
/// Defines the enforcement level for workflow recommendations.
/// Controls how strongly the server promotes its tools over built-ins.
/// </summary>
public enum WorkflowEnforcement
{
    /// <summary>
    /// Gentle suggestion. Example: "Consider using..."
    /// </summary>
    Suggest,
    
    /// <summary>
    /// Clear recommendation. Example: "RECOMMENDED: Use..."
    /// </summary>
    Recommend,
    
    /// <summary>
    /// Strong urging with technical justification. Example: "ALWAYS use... for these scenarios"
    /// </summary>
    StronglyUrge
}