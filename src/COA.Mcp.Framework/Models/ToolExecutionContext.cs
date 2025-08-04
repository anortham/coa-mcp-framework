using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework;

/// <summary>
/// Context information passed during tool execution.
/// </summary>
public class ToolExecutionContext
{
    /// <summary>
    /// Gets or sets the tool name being executed.
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Gets or sets the execution ID for tracing.
    /// </summary>
    public string ExecutionId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the start time of execution.
    /// </summary>
    public DateTimeOffset StartTime { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the cancellation token.
    /// </summary>
    public CancellationToken CancellationToken { get; init; } = CancellationToken.None;

    /// <summary>
    /// Gets or sets the logger for the execution.
    /// </summary>
    public ILogger? Logger { get; init; }

    /// <summary>
    /// Gets or sets custom context data.
    /// </summary>
    public Dictionary<string, object?> CustomData { get; init; } = new();

    /// <summary>
    /// Gets or sets the user context (if available).
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Gets or sets the session ID (if available).
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Gets or sets the token budget for this execution.
    /// </summary>
    public int? TokenBudget { get; init; }

    /// <summary>
    /// Gets or sets the response mode preference.
    /// </summary>
    public ResponseMode? ResponseMode { get; init; }

    /// <summary>
    /// Gets or sets whether to enable caching for this execution.
    /// </summary>
    public bool EnableCaching { get; init; } = true;

    /// <summary>
    /// Gets or sets performance metrics collection.
    /// </summary>
    public PerformanceMetrics Metrics { get; } = new();
}

/// <summary>
/// Response modes for tool execution.
/// </summary>
public enum ResponseMode
{
    /// <summary>
    /// Automatic mode selection based on data size.
    /// </summary>
    Auto,

    /// <summary>
    /// Summary mode with limited token usage.
    /// </summary>
    Summary,

    /// <summary>
    /// Full mode with detailed responses.
    /// </summary>
    Full,

    /// <summary>
    /// Streaming mode for large responses.
    /// </summary>
    Streaming
}

/// <summary>
/// Performance metrics for tool execution.
/// </summary>
public class PerformanceMetrics
{
    /// <summary>
    /// Gets or sets the token estimation time.
    /// </summary>
    public TimeSpan TokenEstimationTime { get; set; }

    /// <summary>
    /// Gets or sets the execution time.
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the response building time.
    /// </summary>
    public TimeSpan ResponseBuildingTime { get; set; }

    /// <summary>
    /// Gets or sets the total time.
    /// </summary>
    public TimeSpan TotalTime { get; set; }

    /// <summary>
    /// Gets or sets the estimated tokens used.
    /// </summary>
    public int EstimatedTokens { get; set; }

    /// <summary>
    /// Gets or sets the actual tokens used (if available).
    /// </summary>
    public int? ActualTokens { get; set; }

    /// <summary>
    /// Gets or sets custom metrics.
    /// </summary>
    public Dictionary<string, object> CustomMetrics { get; } = new();
}