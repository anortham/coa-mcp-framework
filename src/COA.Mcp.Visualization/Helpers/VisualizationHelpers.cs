using COA.Mcp.Visualization.Builders;
using System.Collections.Generic;
using System.Linq;

namespace COA.Mcp.Visualization.Helpers;

/// <summary>
/// Static helper methods for creating common visualization patterns
/// </summary>
public static class VisualizationHelpers
{
    /// <summary>
    /// Creates a search results visualization descriptor
    /// </summary>
    /// <param name="query">The search query</param>
    /// <param name="results">The search results</param>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>A configured visualization descriptor</returns>
    public static VisualizationDescriptor CreateSearchResults(
        string query,
        IEnumerable<SearchResult> results,
        Action<VisualizationBuilder>? configure = null)
    {
        var builder = VisualizationBuilder.SearchResults(query, results);
        configure?.Invoke(builder);
        return builder.Build();
    }

    /// <summary>
    /// Creates a data grid visualization descriptor
    /// </summary>
    /// <param name="columns">The column definitions</param>
    /// <param name="rows">The data rows</param>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>A configured visualization descriptor</returns>
    public static VisualizationDescriptor CreateDataGrid(
        IEnumerable<GridColumn> columns,
        IEnumerable<Dictionary<string, object>> rows,
        Action<VisualizationBuilder>? configure = null)
    {
        var builder = VisualizationBuilder.DataGrid(columns, rows);
        configure?.Invoke(builder);
        return builder.Build();
    }

    /// <summary>
    /// Creates a hierarchy visualization descriptor
    /// </summary>
    /// <param name="root">The root node</param>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>A configured visualization descriptor</returns>
    public static VisualizationDescriptor CreateHierarchy(
        HierarchyNode root,
        Action<VisualizationBuilder>? configure = null)
    {
        var builder = VisualizationBuilder.Hierarchy(root);
        configure?.Invoke(builder);
        return builder.Build();
    }

    /// <summary>
    /// Creates a metrics visualization descriptor
    /// </summary>
    /// <param name="metrics">The metrics data</param>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>A configured visualization descriptor</returns>
    public static VisualizationDescriptor CreateMetrics(
        object metrics,
        Action<VisualizationBuilder>? configure = null)
    {
        var builder = VisualizationBuilder.Metrics(metrics);
        configure?.Invoke(builder);
        return builder.Build();
    }

    /// <summary>
    /// Creates a timeline visualization descriptor
    /// </summary>
    /// <param name="events">The timeline events</param>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>A configured visualization descriptor</returns>
    public static VisualizationDescriptor CreateTimeline(
        IEnumerable<TimelineEvent> events,
        Action<VisualizationBuilder>? configure = null)
    {
        var builder = VisualizationBuilder.Timeline(events);
        configure?.Invoke(builder);
        return builder.Build();
    }

    /// <summary>
    /// Creates a diagnostic visualization descriptor for errors and warnings
    /// </summary>
    /// <param name="diagnostics">The diagnostic items</param>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>A configured visualization descriptor</returns>
    public static VisualizationDescriptor CreateDiagnostics(
        IEnumerable<DiagnosticItem> diagnostics,
        Action<VisualizationBuilder>? configure = null)
    {
        var data = new
        {
            diagnostics,
            totalCount = diagnostics.Count(),
            errorCount = diagnostics.Count(d => d.Severity == "error"),
            warningCount = diagnostics.Count(d => d.Severity == "warning")
        };

        var builder = VisualizationBuilder.Create()
            .WithType(StandardVisualizationTypes.Diagnostic)
            .WithData(data)
            .WithPreferredView("grid")
            .WithFallback("json")
            .WithPriority(VisualizationPriority.High);

        configure?.Invoke(builder);
        return builder.Build();
    }

    /// <summary>
    /// Creates a progress visualization descriptor for long-running operations
    /// </summary>
    /// <param name="current">Current progress value</param>
    /// <param name="total">Total progress value</param>
    /// <param name="message">Progress message</param>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>A configured visualization descriptor</returns>
    public static VisualizationDescriptor CreateProgress(
        int current,
        int total,
        string message,
        Action<VisualizationBuilder>? configure = null)
    {
        var data = new
        {
            current,
            total,
            percentage = total > 0 ? (double)current / total * 100 : 0,
            message
        };

        var builder = VisualizationBuilder.Create()
            .WithType(StandardVisualizationTypes.Progress)
            .WithData(data)
            .WithPreferredView("progress")
            .WithFallback("text")
            .WithPriority(VisualizationPriority.High);

        configure?.Invoke(builder);
        return builder.Build();
    }
}

/// <summary>
/// Represents a search result item
/// </summary>
public class SearchResult
{
    public string FilePath { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
    public string Snippet { get; set; } = string.Empty;
    public double Score { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Represents a data grid column
/// </summary>
public class GridColumn
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public bool Sortable { get; set; } = true;
    public bool Filterable { get; set; } = true;
    public int? Width { get; set; }
}

/// <summary>
/// Represents a hierarchy node
/// </summary>
public class HierarchyNode
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public List<HierarchyNode> Children { get; set; } = new();
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Represents a timeline event
/// </summary>
public class TimelineEvent
{
    public DateTime Timestamp { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = "event";
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Represents a diagnostic item (error, warning, info)
/// </summary>
public class DiagnosticItem
{
    public string Severity { get; set; } = "info";
    public string Message { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public int? Line { get; set; }
    public int? Column { get; set; }
    public string? Code { get; set; }
    public string? Source { get; set; }
}