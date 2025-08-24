using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework.Pipeline;
using COA.Mcp.Framework.Pipeline.SimpleMiddleware;
using Microsoft.Extensions.Logging;

namespace SimpleMcpServer.Tools;

/// <summary>
/// Example tool that demonstrates lifecycle hooks and middleware usage.
/// This tool processes text and includes built-in logging and token counting middleware.
/// </summary>
public class LifecycleExampleTool : McpToolBase<LifecycleExampleParameters, LifecycleExampleResult>
{
    private readonly ILogger<LifecycleExampleTool> _logger;

    public LifecycleExampleTool(ILogger<LifecycleExampleTool> logger) : base(null, logger)
    {
        _logger = logger;
    }

    public override string Name => "lifecycle_example";
    public override string Description => "Demonstrates lifecycle hooks with text processing and middleware";
    public override ToolCategory Category => ToolCategory.Utility;

    /// <summary>
    /// Configure middleware for this tool.
    /// This demonstrates how to add lifecycle hooks to individual tools.
    /// </summary>
    protected override IReadOnlyList<ISimpleMiddleware>? Middleware => new List<ISimpleMiddleware>
    {
        // Add token counting middleware to track usage (no logger needed)
        new TokenCountingSimpleMiddleware(),
        
        // Add custom middleware for this specific tool
        new CustomTimingMiddleware(_logger)
    };

    protected override async Task<LifecycleExampleResult> ExecuteInternalAsync(
        LifecycleExampleParameters parameters, 
        CancellationToken cancellationToken)
    {
        // Simulate some processing work
        await Task.Delay(parameters.ProcessingDelayMs, cancellationToken);

        var processedText = parameters.Operation.ToLower() switch
        {
            "uppercase" => parameters.Text.ToUpperInvariant(),
            "lowercase" => parameters.Text.ToLowerInvariant(),
            "reverse" => new string(parameters.Text.ToCharArray().Reverse().ToArray()),
            "length" => parameters.Text.Length.ToString(),
            _ => $"Unknown operation: {parameters.Operation}"
        };

        return new LifecycleExampleResult(parameters.Operation)
        {
            OriginalText = parameters.Text,
            ProcessedText = processedText,
            ProcessingTimeMs = parameters.ProcessingDelayMs,
            Timestamp = DateTimeOffset.UtcNow
        };
    }
}

/// <summary>
/// Parameters for the lifecycle example tool.
/// </summary>
public class LifecycleExampleParameters
{
    /// <summary>
    /// The text to process.
    /// </summary>
    [Required]
    [StringLength(1000, MinimumLength = 1)]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// The operation to perform on the text.
    /// </summary>
    [Required]
    public string Operation { get; set; } = "uppercase";

    /// <summary>
    /// Simulated processing delay in milliseconds (for demonstration).
    /// </summary>
    [Range(0, 5000)]
    public int ProcessingDelayMs { get; set; } = 100;
}

/// <summary>
/// Result from the lifecycle example tool.
/// </summary>
public class LifecycleExampleResult : ToolResultBase
{
    private readonly string _operation;
    
    public LifecycleExampleResult(string operation)
    {
        _operation = operation;
        Success = true;
    }

    /// <summary>
    /// The original text that was processed.
    /// </summary>
    public string OriginalText { get; set; } = string.Empty;

    /// <summary>
    /// The processed text result.
    /// </summary>
    public string ProcessedText { get; set; } = string.Empty;

    /// <summary>
    /// The operation that was performed.
    /// </summary>
    public override string Operation => _operation;

    /// <summary>
    /// The processing time in milliseconds.
    /// </summary>
    public int ProcessingTimeMs { get; set; }

    /// <summary>
    /// When the processing was completed.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }
}

/// <summary>
/// Custom middleware example that tracks execution timing.
/// </summary>
public class CustomTimingMiddleware : SimpleMiddlewareBase
{
    private readonly ILogger _logger;

    public CustomTimingMiddleware(ILogger logger)
    {
        _logger = logger;
        Order = 50; // Run between logging (10) and token counting (100)
    }

    public override Task OnBeforeExecutionAsync(string toolName, object? parameters)
    {
        _logger.LogInformation("🚀 Custom timing middleware: Starting execution of '{ToolName}'", toolName);
        return Task.CompletedTask;
    }

    public override Task OnAfterExecutionAsync(string toolName, object? parameters, object? result, long elapsedMs)
    {
        var performanceCategory = elapsedMs switch
        {
            < 100 => "⚡ Fast",
            < 500 => "🚶 Normal", 
            < 1000 => "🐌 Slow",
            _ => "🚨 Very Slow"
        };

        _logger.LogInformation("⏱️  Custom timing middleware: '{ToolName}' completed - {Category} ({ElapsedMs}ms)", 
            toolName, performanceCategory, elapsedMs);
        return Task.CompletedTask;
    }

    public override Task OnErrorAsync(string toolName, object? parameters, Exception exception, long elapsedMs)
    {
        _logger.LogWarning("💥 Custom timing middleware: '{ToolName}' failed after {ElapsedMs}ms - {Error}", 
            toolName, elapsedMs, exception.Message);
        return Task.CompletedTask;
    }
}