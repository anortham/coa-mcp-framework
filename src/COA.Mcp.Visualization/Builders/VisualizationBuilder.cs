using System.Collections.Generic;
using COA.Mcp.Visualization.Validation;

namespace COA.Mcp.Visualization.Builders;

/// <summary>
/// Fluent builder for creating visualization descriptors
/// </summary>
public class VisualizationBuilder
{
    private string _type = string.Empty;
    private string _version = "1.0";
    private object _data = new object();
    private VisualizationHint? _hint;
    private Dictionary<string, object>? _metadata;

    /// <summary>
    /// Sets the visualization type
    /// </summary>
    /// <param name="type">The visualization type</param>
    /// <returns>The builder instance</returns>
    public VisualizationBuilder WithType(string type)
    {
        _type = type;
        return this;
    }

    /// <summary>
    /// Sets the visualization version
    /// </summary>
    /// <param name="version">The version string</param>
    /// <returns>The builder instance</returns>
    public VisualizationBuilder WithVersion(string version)
    {
        _version = version;
        return this;
    }

    /// <summary>
    /// Sets the visualization data
    /// </summary>
    /// <param name="data">The data to visualize</param>
    /// <returns>The builder instance</returns>
    public VisualizationBuilder WithData(object data)
    {
        _data = data;
        return this;
    }

    /// <summary>
    /// Sets the visualization hint
    /// </summary>
    /// <param name="hint">The visualization hint</param>
    /// <returns>The builder instance</returns>
    public VisualizationBuilder WithHint(VisualizationHint hint)
    {
        _hint = hint;
        return this;
    }

    /// <summary>
    /// Adds metadata to the visualization
    /// </summary>
    /// <param name="key">The metadata key</param>
    /// <param name="value">The metadata value</param>
    /// <returns>The builder instance</returns>
    public VisualizationBuilder WithMetadata(string key, object value)
    {
        _metadata ??= new Dictionary<string, object>();
        _metadata[key] = value;
        return this;
    }

    /// <summary>
    /// Sets the preferred view type
    /// </summary>
    /// <param name="viewType">The preferred view type</param>
    /// <returns>The builder instance</returns>
    public VisualizationBuilder WithPreferredView(string viewType)
    {
        _hint ??= new VisualizationHint();
        _hint.PreferredView = viewType;
        return this;
    }

    /// <summary>
    /// Sets the fallback format
    /// </summary>
    /// <param name="format">The fallback format</param>
    /// <returns>The builder instance</returns>
    public VisualizationBuilder WithFallback(string format)
    {
        _hint ??= new VisualizationHint();
        _hint.FallbackFormat = format;
        return this;
    }

    /// <summary>
    /// Sets the interactive flag
    /// </summary>
    /// <param name="interactive">Whether the visualization should be interactive</param>
    /// <returns>The builder instance</returns>
    public VisualizationBuilder WithInteractive(bool interactive)
    {
        _hint ??= new VisualizationHint();
        _hint.Interactive = interactive;
        return this;
    }

    /// <summary>
    /// Sets the priority level
    /// </summary>
    /// <param name="priority">The visualization priority</param>
    /// <returns>The builder instance</returns>
    public VisualizationBuilder WithPriority(VisualizationPriority priority)
    {
        _hint ??= new VisualizationHint();
        _hint.Priority = priority;
        return this;
    }

    /// <summary>
    /// Builds the visualization descriptor
    /// </summary>
    /// <returns>A configured visualization descriptor</returns>
    /// <exception cref="InvalidOperationException">Thrown when required fields are missing</exception>
    public VisualizationDescriptor Build()
    {
        if (string.IsNullOrEmpty(_type))
        {
            throw new InvalidOperationException("Visualization type is required");
        }

        var descriptor = new VisualizationDescriptor
        {
            Type = _type,
            Version = _version,
            Data = _data,
            Hint = _hint,
            Metadata = _metadata
        };

        return descriptor;
    }

    /// <summary>
    /// Builds and validates the visualization descriptor
    /// </summary>
    /// <returns>A configured and validated visualization descriptor</returns>
    /// <exception cref="InvalidOperationException">Thrown when validation fails</exception>
    public VisualizationDescriptor BuildAndValidate()
    {
        var descriptor = Build();
        
        var validation = VisualizationValidator.Validate(descriptor);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Visualization validation failed: {string.Join(", ", validation.Errors)}");
        }

        return descriptor;
    }

    /// <summary>
    /// Creates a new visualization builder
    /// </summary>
    /// <returns>A new builder instance</returns>
    public static VisualizationBuilder Create() => new();

    /// <summary>
    /// Creates a builder for search results visualization
    /// </summary>
    /// <param name="query">The search query</param>
    /// <param name="results">The search results</param>
    /// <returns>A configured builder for search results</returns>
    public static VisualizationBuilder SearchResults(string query, IEnumerable<object> results)
    {
        var data = new
        {
            query,
            totalHits = results.Count(),
            results
        };

        return Create()
            .WithType(StandardVisualizationTypes.SearchResults)
            .WithData(data)
            .WithPreferredView("grid")
            .WithFallback("json");
    }

    /// <summary>
    /// Creates a builder for data grid visualization
    /// </summary>
    /// <param name="columns">The column definitions</param>
    /// <param name="rows">The data rows</param>
    /// <returns>A configured builder for data grid</returns>
    public static VisualizationBuilder DataGrid(IEnumerable<object> columns, IEnumerable<object> rows)
    {
        var data = new
        {
            columns,
            rows,
            totalCount = rows.Count()
        };

        return Create()
            .WithType(StandardVisualizationTypes.DataGrid)
            .WithData(data)
            .WithPreferredView("grid")
            .WithFallback("csv");
    }

    /// <summary>
    /// Creates a builder for hierarchy visualization
    /// </summary>
    /// <param name="root">The root node</param>
    /// <returns>A configured builder for hierarchy</returns>
    public static VisualizationBuilder Hierarchy(object root)
    {
        var data = new { root };

        return Create()
            .WithType(StandardVisualizationTypes.Hierarchy)
            .WithData(data)
            .WithPreferredView("tree")
            .WithFallback("json");
    }

    /// <summary>
    /// Creates a builder for metrics visualization
    /// </summary>
    /// <param name="metrics">The metrics data</param>
    /// <returns>A configured builder for metrics</returns>
    public static VisualizationBuilder Metrics(object metrics)
    {
        return Create()
            .WithType(StandardVisualizationTypes.Metrics)
            .WithData(metrics)
            .WithPreferredView("chart")
            .WithFallback("json");
    }

    /// <summary>
    /// Creates a builder for timeline visualization
    /// </summary>
    /// <param name="events">The timeline events</param>
    /// <returns>A configured builder for timeline</returns>
    public static VisualizationBuilder Timeline(IEnumerable<object> events)
    {
        var data = new
        {
            events,
            totalEvents = events.Count()
        };

        return Create()
            .WithType(StandardVisualizationTypes.Timeline)
            .WithData(data)
            .WithPreferredView("timeline")
            .WithFallback("json");
    }
}