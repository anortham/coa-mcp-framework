using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.Pipeline.SimpleMiddleware;

/// <summary>
/// Simple middleware that counts tokens used during tool execution.
/// </summary>
public class TokenCountingSimpleMiddleware : SimpleMiddlewareBase
{
    private readonly ILogger<TokenCountingSimpleMiddleware>? _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Initializes a new instance of the TokenCountingSimpleMiddleware class.
    /// </summary>
    /// <param name="logger">Optional logger.</param>
    public TokenCountingSimpleMiddleware(ILogger<TokenCountingSimpleMiddleware>? logger = null)
    {
        _logger = logger;
        Order = 100; // Run early in the pipeline
    }

    /// <inheritdoc/>
    public override Task OnBeforeExecutionAsync(string toolName, object? parameters)
    {
        var inputTokens = EstimateTokens(parameters);
        
        _logger?.LogDebug("Tool '{ToolName}' estimated input tokens: {Tokens}", 
            toolName, inputTokens);

        // Store for later use if needed
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task OnAfterExecutionAsync(string toolName, object? parameters, object? result, long elapsedMs)
    {
        var inputTokens = EstimateTokens(parameters);
        var outputTokens = EstimateTokens(result);
        var totalTokens = inputTokens + outputTokens;
        
        _logger?.LogInformation("Tool '{ToolName}' token usage: {Input} + {Output} = {Total} tokens", 
            toolName, inputTokens, outputTokens, totalTokens);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Estimates the token count for an object.
    /// </summary>
    /// <param name="obj">The object to estimate.</param>
    /// <returns>The estimated token count.</returns>
    private static int EstimateTokens(object? obj)
    {
        if (obj == null) return 0;

        try
        {
            var json = JsonSerializer.Serialize(obj, JsonOptions);
            // Rough estimation: 1 token per 4 characters
            return Math.Max(1, json.Length / 4);
        }
        catch
        {
            // Fallback for non-serializable objects
            return 100;
        }
    }
}