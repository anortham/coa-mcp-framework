using COA.Mcp.Framework.TokenOptimization.Models;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework.TokenOptimization.Reduction;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.TokenOptimization.ResponseBuilders;

/// <summary>
/// Non-generic interface for response builders (backward compatibility).
/// </summary>
public interface IResponseBuilder
{
    /// <summary>
    /// Builds a response with token optimization.
    /// </summary>
    Task<object> BuildResponseAsync(object data, ResponseContext context);
}

/// <summary>
/// Base class for building token-aware responses with automatic optimization and strong typing.
/// </summary>
/// <typeparam name="TInput">The type of input data being processed.</typeparam>
/// <typeparam name="TResult">The type of result being returned.</typeparam>
public abstract class BaseResponseBuilder<TInput, TResult> : IResponseBuilder
    where TResult : new()
{
    /// <summary>
    /// Default token budget for summary responses.
    /// </summary>
    protected const int SummaryTokenBudget = 5000;
    
    /// <summary>
    /// Default token budget for full responses.
    /// </summary>
    protected const int FullTokenBudget = 24000;
    
    protected readonly ILogger? _logger;
    protected readonly ProgressiveReductionEngine _reductionEngine;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="BaseResponseBuilder{TInput,TResult}"/> class.
    /// </summary>
    /// <param name="logger">Optional logger.</param>
    /// <param name="reductionEngine">Optional reduction engine (creates default if not provided).</param>
    protected BaseResponseBuilder(
        ILogger? logger = null,
        ProgressiveReductionEngine? reductionEngine = null)
    {
        _logger = logger;
        _reductionEngine = reductionEngine ?? new ProgressiveReductionEngine();
    }
    
    /// <summary>
    /// Builds a response with token optimization.
    /// </summary>
    /// <param name="data">The data to include in the response.</param>
    /// <param name="context">The response building context.</param>
    /// <returns>A strongly typed optimized response.</returns>
    public abstract Task<TResult> BuildResponseAsync(TInput data, ResponseContext context);
    
    /// <summary>
    /// Non-generic version for backward compatibility.
    /// </summary>
    async Task<object> IResponseBuilder.BuildResponseAsync(object data, ResponseContext context)
    {
        if (data is TInput typedData)
        {
            return await BuildResponseAsync(typedData, context);
        }
        throw new ArgumentException($"Expected data of type {typeof(TInput).Name}, got {data?.GetType().Name ?? "null"}");
    }
    
    /// <summary>
    /// Generates insights for the given data.
    /// </summary>
    /// <param name="data">The data to analyze.</param>
    /// <param name="responseMode">The response mode (summary/full).</param>
    /// <returns>List of insights.</returns>
    protected abstract List<string> GenerateInsights(TInput data, string responseMode);
    
    /// <summary>
    /// Generates suggested actions based on the data.
    /// </summary>
    /// <param name="data">The data to analyze.</param>
    /// <param name="tokenBudget">Available token budget for actions.</param>
    /// <returns>List of suggested actions.</returns>
    protected abstract List<AIAction> GenerateActions(TInput data, int tokenBudget);
    
    /// <summary>
    /// Calculates the token budget based on response mode and context.
    /// </summary>
    /// <param name="context">The response context.</param>
    /// <returns>Token budget.</returns>
    protected virtual int CalculateTokenBudget(ResponseContext context)
    {
        var baseBudget = context.ResponseMode.ToLowerInvariant() switch
        {
            "summary" => SummaryTokenBudget,
            "full" => FullTokenBudget,
            _ => context.TokenLimit ?? FullTokenBudget
        };
        
        // Apply safety margin
        var safetyMode = context.SafetyMode ?? TokenSafetyMode.Default;
        return TokenEstimator.CalculateTokenBudget(baseBudget, 0, safetyMode);
    }
    
    /// <summary>
    /// Creates standard metadata for the response.
    /// </summary>
    /// <param name="startTime">The operation start time.</param>
    /// <param name="wasTruncated">Whether data was truncated.</param>
    /// <param name="resourceUri">Optional resource URI for full data.</param>
    /// <returns>Response metadata.</returns>
    protected AIResponseMeta CreateMetadata(
        DateTime startTime,
        bool wasTruncated,
        string? resourceUri = null)
    {
        var duration = DateTime.UtcNow - startTime;
        
        return new AIResponseMeta
        {
            ExecutionTime = $"{duration.TotalMilliseconds:F0}ms",
            Truncated = wasTruncated,
            ResourceUri = resourceUri,
            TokenInfo = new TokenInfo
            {
                Estimated = 0, // Will be set by derived classes
                Limit = CalculateTokenBudget(new ResponseContext()),
                ReductionStrategy = wasTruncated ? "progressive" : null
            }
        };
    }
    
    /// <summary>
    /// Reduces insights to fit within token budget.
    /// </summary>
    /// <param name="insights">Original insights.</param>
    /// <param name="tokenBudget">Token budget for insights.</param>
    /// <returns>Reduced insights list.</returns>
    protected List<string> ReduceInsights(List<string> insights, int tokenBudget)
    {
        if (insights.Count == 0)
            return insights;
        
        var result = _reductionEngine.Reduce(
            insights,
            insight => TokenEstimator.EstimateString(insight),
            tokenBudget,
            "standard");
        
        return result.Items;
    }
    
    /// <summary>
    /// Reduces actions to fit within token budget.
    /// </summary>
    /// <param name="actions">Original actions.</param>
    /// <param name="tokenBudget">Token budget for actions.</param>
    /// <returns>Reduced actions list.</returns>
    protected List<AIAction> ReduceActions(List<AIAction> actions, int tokenBudget)
    {
        if (actions.Count == 0)
            return actions;
        
        var context = new ReductionContext
        {
            PriorityFunction = obj => obj is AIAction action ? action.Priority : 0
        };
        
        var result = _reductionEngine.Reduce(
            actions,
            action => TokenEstimator.EstimateObject(action),
            tokenBudget,
            "priority",
            context);
        
        return result.Items;
    }
}

/// <summary>
/// Non-generic base class for backward compatibility.
/// Uses object types for both input and output.
/// </summary>
/// <typeparam name="TData">The type of data being processed.</typeparam>
public abstract class BaseResponseBuilder<TData> : BaseResponseBuilder<TData, object>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BaseResponseBuilder{TData}"/> class.
    /// </summary>
    /// <param name="logger">Optional logger.</param>
    /// <param name="reductionEngine">Optional reduction engine.</param>
    protected BaseResponseBuilder(
        ILogger? logger = null,
        ProgressiveReductionEngine? reductionEngine = null)
        : base(logger, reductionEngine)
    {
    }
}

/// <summary>
/// Context for building responses.
/// </summary>
public class ResponseContext
{
    /// <summary>
    /// Gets or sets the response mode ("summary" or "full").
    /// </summary>
    public string ResponseMode { get; set; } = "full";
    
    /// <summary>
    /// Gets or sets the token limit override.
    /// </summary>
    public int? TokenLimit { get; set; }
    
    /// <summary>
    /// Gets or sets the token safety mode.
    /// </summary>
    public TokenSafetyMode? SafetyMode { get; set; }
    
    /// <summary>
    /// Gets or sets whether to store full results as a resource.
    /// </summary>
    public bool StoreFullResults { get; set; } = true;
    
    /// <summary>
    /// Gets or sets custom metadata to include in the response.
    /// </summary>
    public Dictionary<string, object> CustomMetadata { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the tool name generating this response.
    /// </summary>
    public string? ToolName { get; set; }
    
    /// <summary>
    /// Gets or sets the cache key for this response.
    /// </summary>
    public string? CacheKey { get; set; }
}