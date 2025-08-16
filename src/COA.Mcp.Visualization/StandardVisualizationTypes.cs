namespace COA.Mcp.Visualization;

/// <summary>
/// Standard visualization types that clients should support
/// </summary>
public static class StandardVisualizationTypes
{
    /// <summary>
    /// Search results with file paths, line numbers, and snippets
    /// </summary>
    public const string SearchResults = "search-results";
    
    /// <summary>
    /// Hierarchical tree structure (folders, classes, etc.)
    /// </summary>
    public const string Hierarchy = "hierarchy";
    
    /// <summary>
    /// Data grid/table with rows and columns
    /// </summary>
    public const string DataGrid = "data-grid";
    
    /// <summary>
    /// Chart visualization (bar, line, pie, etc.)
    /// </summary>
    public const string Chart = "chart";
    
    /// <summary>
    /// Timeline of events
    /// </summary>
    public const string Timeline = "timeline";
    
    /// <summary>
    /// Markdown content with enhanced rendering
    /// </summary>
    public const string Markdown = "markdown";
    
    /// <summary>
    /// Code metrics and analysis
    /// </summary>
    public const string Metrics = "metrics";
    
    /// <summary>
    /// Diagram (Mermaid, PlantUML, etc.)
    /// </summary>
    public const string Diagram = "diagram";
    
    /// <summary>
    /// File diff comparison
    /// </summary>
    public const string Diff = "diff";
    
    /// <summary>
    /// Progress indicator for long-running operations
    /// </summary>
    public const string Progress = "progress";
    
    /// <summary>
    /// Error or warning display
    /// </summary>
    public const string Diagnostic = "diagnostic";
    
    /// <summary>
    /// Generic JSON tree view
    /// </summary>
    public const string JsonTree = "json-tree";
}