using System.Diagnostics;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Formatters;
using COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Models;
using COA.Mcp.Framework.TokenOptimization.Models;
using COA.Mcp.Framework.TokenOptimization.Reduction;
using COA.Mcp.Framework.TokenOptimization.ResponseBuilders;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.TokenOptimization.AdaptiveResponse;

/// <summary>
/// Advanced response builder that provides IDE-aware formatting and intelligent resource management.
/// Extends BaseResponseBuilder with adaptive capabilities based on the detected development environment.
/// </summary>
/// <typeparam name="TInput">The type of input data being processed.</typeparam>
/// <typeparam name="TResult">The type of result being returned (must inherit from ToolResultBase).</typeparam>
public abstract class AdaptiveResponseBuilder<TInput, TResult> : BaseResponseBuilder<TInput, TResult>
    where TResult : ToolResultBase, new()
{
    protected readonly IDEEnvironment _environment;
    protected readonly IOutputFormatterFactory _formatterFactory;
    protected readonly IResourceProvider _resourceProvider;
    
    /// <summary>
    /// Initializes a new instance of the AdaptiveResponseBuilder class.
    /// </summary>
    /// <param name="logger">Optional logger for debugging and monitoring.</param>
    /// <param name="reductionEngine">Optional token reduction engine (creates default if not provided).</param>
    /// <param name="resourceProvider">Optional resource provider for storing large data (creates default if not provided).</param>
    protected AdaptiveResponseBuilder(
        ILogger? logger = null,
        ProgressiveReductionEngine? reductionEngine = null,
        IResourceProvider? resourceProvider = null)
        : base(logger, reductionEngine)
    {
        _environment = IDEEnvironment.Detect();
        _formatterFactory = new OutputFormatterFactory();
        _resourceProvider = resourceProvider ?? new DefaultResourceProvider();
        
        _logger?.LogDebug("AdaptiveResponseBuilder initialized for {IDE} environment", _environment.IDE);
    }
    
    /// <summary>
    /// Builds an adaptive response optimized for the detected IDE environment.
    /// </summary>
    /// <param name="data">The data to include in the response.</param>
    /// <param name="context">The response building context.</param>
    /// <returns>A strongly typed, IDE-optimized response.</returns>
    public override async Task<TResult> BuildResponseAsync(TInput data, ResponseContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Create base result with operation info
            var result = new TResult
            {
                Success = true,
                Meta = new ToolExecutionMetadata
                {
                    Mode = context.ResponseMode,
                    ExecutionTime = "0ms" // Will be updated at the end
                }
            };
            
            // Apply adaptive formatting based on IDE environment
            await ApplyAdaptiveFormattingAsync(result, data, context);
            
            // Apply token optimization if needed
            if (ShouldOptimizeTokens(result, context))
            {
                await OptimizeForTokensAsync(result, data, context);
            }
            
            // Update execution time
            if (result.Meta != null)
            {
                result.Meta.ExecutionTime = $"{stopwatch.ElapsedMilliseconds}ms";
            }
            
            _logger?.LogDebug("AdaptiveResponseBuilder completed in {ElapsedMs}ms for {IDE}", 
                stopwatch.ElapsedMilliseconds, _environment.IDE);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "AdaptiveResponseBuilder failed after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            
            // Create error result
            var errorResult = new TResult
            {
                Success = false,
                Error = new ErrorInfo
                {
                    Code = "ADAPTIVE_RESPONSE_ERROR",
                    Message = $"Failed to build adaptive response: {ex.Message}"
                },
                Meta = new ToolExecutionMetadata
                {
                    Mode = context.ResponseMode,
                    ExecutionTime = $"{stopwatch.ElapsedMilliseconds}ms"
                }
            };
            
            return errorResult;
        }
    }
    
    /// <summary>
    /// Gets the operation name for this response builder.
    /// Override in derived classes to provide specific operation names.
    /// </summary>
    /// <returns>The operation name.</returns>
    protected abstract string GetOperationName();
    
    /// <summary>
    /// Applies IDE-specific formatting to the result.
    /// Override in derived classes to implement custom formatting logic.
    /// </summary>
    /// <param name="result">The result to format.</param>
    /// <param name="data">The input data.</param>
    /// <param name="context">The response context.</param>
    /// <returns>A task representing the formatting operation.</returns>
    protected abstract Task ApplyAdaptiveFormattingAsync(TResult result, TInput data, ResponseContext context);
    
    /// <summary>
    /// Determines whether token optimization should be applied to the result.
    /// </summary>
    /// <param name="result">The result to check.</param>
    /// <param name="context">The response context.</param>
    /// <returns>True if optimization should be applied.</returns>
    protected virtual bool ShouldOptimizeTokens(TResult result, ResponseContext context)
    {
        var estimatedTokens = EstimateTokenUsage(result);
        var maxTokens = context.TokenLimit ?? CalculateTokenBudget(context);
        
        return estimatedTokens > maxTokens;
    }
    
    /// <summary>
    /// Estimates the token usage for a result object.
    /// </summary>
    /// <param name="result">The result to estimate.</param>
    /// <returns>Estimated token count.</returns>
    protected virtual int EstimateTokenUsage(TResult result)
    {
        // Use the existing TokenEstimator from the framework
        return TokenEstimator.EstimateObject(result);
    }
    
    /// <summary>
    /// Optimizes the result for token limits by creating resources for large data.
    /// </summary>
    /// <param name="result">The result to optimize.</param>
    /// <param name="data">The original input data.</param>
    /// <param name="context">The response context.</param>
    /// <returns>A task representing the optimization operation.</returns>
    protected virtual async Task OptimizeForTokensAsync(TResult result, TInput data, ResponseContext context)
    {
        var estimatedTokens = EstimateTokenUsage(result);
        var maxTokens = context.TokenLimit ?? CalculateTokenBudget(context);
        
        _logger?.LogInformation("Response too large ({EstimatedTokens} tokens > {MaxTokens}), creating resource", 
            estimatedTokens, maxTokens);
        
        // Create resource for full data
        var resourceUri = await CreateLargeDataResourceAsync(data, context);
        
        // Reduce inline content
        result.Message = CreateTruncatedMessage(estimatedTokens, maxTokens);
        result.ResourceUri = resourceUri;
        
        if (result.Meta != null)
        {
            result.Meta.Truncated = true;
            result.Meta.Tokens = estimatedTokens;
        }
    }
    
    /// <summary>
    /// Creates a resource for large data sets.
    /// </summary>
    /// <param name="data">The data to store as a resource.</param>
    /// <param name="context">The response context.</param>
    /// <returns>The resource URI.</returns>
    protected virtual async Task<string> CreateLargeDataResourceAsync(TInput data, ResponseContext context)
    {
        var format = _environment.GetPreferredResourceFormat();
        var resourceId = Guid.NewGuid().ToString("N")[..8];
        var formatter = _formatterFactory.CreateResourceFormatter(format, _environment);
        
        var content = await formatter.FormatResourceAsync(data);
        var path = $"data/{context.ToolName ?? "unknown"}/{resourceId}{formatter.GetFileExtension()}";
        
        return await _resourceProvider.StoreAsync(path, content, formatter.GetMimeType());
    }
    
    /// <summary>
    /// Creates a formatted truncation message.
    /// </summary>
    /// <param name="estimatedTokens">The estimated token count.</param>
    /// <param name="maxTokens">The maximum allowed tokens.</param>
    /// <returns>A formatted truncation message.</returns>
    protected virtual string CreateTruncatedMessage(int estimatedTokens, int maxTokens)
    {
        var formatter = _formatterFactory.CreateInlineFormatter(_environment);
        
        var summary = $"Large dataset ({estimatedTokens:N0} tokens) exceeds limit ({maxTokens:N0} tokens)";
        var message = formatter.FormatSummary(summary);
        
        return message + GetResourceAccessInstructions();
    }
    
    /// <summary>
    /// Gets IDE-specific instructions for accessing the resource.
    /// </summary>
    /// <returns>Formatted instructions.</returns>
    protected virtual string GetResourceAccessInstructions()
    {
        return _environment.IDE switch
        {
            IDEType.VSCode => "\n\n💡 **Tip:** Use the resource URI above to view the complete data in an interactive format.",
            IDEType.VS2022 => "\n\nThe complete data has been stored as a resource. Use the resource URI to access the full dataset.",
            IDEType.Terminal => "\n\nComplete data available via resource URI (use browser or IDE to view).",
            _ => "\n\nComplete data available via the provided resource URI."
        };
    }
    
    /// <summary>
    /// Creates an adaptive tool result with IDE-specific enhancements.
    /// </summary>
    /// <param name="data">The data to include.</param>
    /// <param name="context">The formatting context.</param>
    /// <returns>An adaptive tool result.</returns>
    protected virtual AdaptiveToolResult CreateAdaptiveResult(TInput data, FormattingContext context)
    {
        return new AdaptiveToolResult
        {
            Success = true,
            Summary = GenerateDataSummary(data),
            Metadata = CreateResponseMetadata(data, context)
        };
    }
    
    /// <summary>
    /// Generates a summary of the input data.
    /// Override in derived classes for specific data types.
    /// </summary>
    /// <param name="data">The data to summarize.</param>
    /// <returns>A data summary string.</returns>
    protected virtual string GenerateDataSummary(TInput data)
    {
        if (data is System.Collections.ICollection collection)
        {
            return $"{collection.Count:N0} items";
        }
        
        if (data is System.Data.DataTable table)
        {
            return $"{table.Rows.Count:N0} rows × {table.Columns.Count} columns";
        }
        
        return data?.GetType().Name ?? "No data";
    }
    
    /// <summary>
    /// Creates response metadata for the adaptive result.
    /// </summary>
    /// <param name="data">The input data.</param>
    /// <param name="context">The formatting context.</param>
    /// <returns>Response metadata dictionary.</returns>
    protected virtual Dictionary<string, object> CreateResponseMetadata(TInput data, FormattingContext context)
    {
        return new Dictionary<string, object>
        {
            ["environment"] = _environment.GetDisplayName(),
            ["ide"] = _environment.IDE.ToString(),
            ["supportsHtml"] = _environment.SupportsHTML,
            ["supportsInteractive"] = _environment.SupportsInteractive,
            ["responseMode"] = context.ResponseMode,
            ["dataType"] = data?.GetType().Name ?? "unknown",
            ["generatedAt"] = DateTime.UtcNow.ToString("O")
        };
    }
    
    /// <summary>
    /// Implementation of the abstract GenerateInsights method from BaseResponseBuilder.
    /// Override in derived classes for specific insight generation.
    /// </summary>
    /// <param name="data">The data to analyze.</param>
    /// <param name="responseMode">The response mode.</param>
    /// <returns>List of insights.</returns>
    protected override List<string> GenerateInsights(TInput data, string responseMode)
    {
        var insights = new List<string>();
        
        // Environment-specific insights
        if (_environment.IDE == IDEType.VSCode)
        {
            insights.Add("💡 Use Ctrl+Click on file references to navigate directly");
            insights.Add("🔗 Command links are clickable and will execute VS Code commands");
        }
        else if (_environment.IDE == IDEType.VS2022)
        {
            insights.Add("🎯 File references follow Visual Studio error list format for easy navigation");
            insights.Add("⌨️ Mapped commands can be executed via Tools → Command Window");
        }
        else if (_environment.IDE == IDEType.Terminal)
        {
            insights.Add("📋 File references are terminal-friendly and clickable in most modern terminals");
            insights.Add("💾 Large datasets are available as resources for export or viewing");
        }
        
        // Data-specific insights
        if (data is System.Collections.ICollection collection && collection.Count > 100)
        {
            insights.Add($"📊 Large dataset detected ({collection.Count:N0} items) - consider filtering for better performance");
        }
        
        if (responseMode == "summary")
        {
            insights.Add("📝 Use 'full' response mode for complete details and interactive features");
        }
        
        return insights;
    }
    
    /// <summary>
    /// Implementation of the abstract GenerateActions method from BaseResponseBuilder.
    /// Override in derived classes for specific action generation.
    /// </summary>
    /// <param name="data">The data to analyze.</param>
    /// <param name="tokenBudget">Available token budget for actions.</param>
    /// <returns>List of suggested actions.</returns>
    protected override List<AIAction> GenerateActions(TInput data, int tokenBudget)
    {
        var actions = new List<AIAction>();
        
        // Environment-specific actions
        if (_environment.SupportsHTML && data is System.Data.DataTable)
        {
            actions.Add(new AIAction
            {
                Action = "export_interactive_table",
                Description = "Export data as interactive HTML table with sorting and filtering",
                Category = "Export",
                Priority = 80
            });
        }
        
        if (data is System.Collections.ICollection collection && collection.Count > 10)
        {
            actions.Add(new AIAction
            {
                Action = "export_csv",
                Description = "Export data as CSV file for analysis in Excel or other tools",
                Category = "Export",
                Priority = 70
            });
            
            actions.Add(new AIAction
            {
                Action = "apply_filter",
                Description = "Apply filters to reduce dataset size",
                Category = "Filter",
                Priority = 60
            });
        }
        
        // IDE-specific actions
        if (_environment.IDE == IDEType.VSCode)
        {
            actions.Add(new AIAction
            {
                Action = "open_in_editor",
                Description = "Open results in VS Code editor for further analysis",
                Category = "Navigation",
                Priority = 85
            });
        }
        
        return actions;
    }
}