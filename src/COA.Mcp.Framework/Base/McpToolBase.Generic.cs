using System;
using System.ComponentModel.DataAnnotations;
using DataAnnotations = System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework.Exceptions;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework.Utilities;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.Base;

/// <summary>
/// Base class for strongly-typed MCP tools with built-in validation, error handling, and token management.
/// </summary>
/// <typeparam name="TParams">The type of the tool's input parameters.</typeparam>
/// <typeparam name="TResult">The type of the tool's result.</typeparam>
public abstract class McpToolBase<TParams, TResult> : IMcpTool<TParams, TResult>, IMcpTool
    where TParams : class
{
    private readonly ILogger? _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the McpToolBase class.
    /// </summary>
    /// <param name="logger">Optional logger for the tool.</param>
    protected McpToolBase(ILogger? logger = null)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public abstract string Description { get; }

    /// <inheritdoc/>
    public virtual ToolCategory Category { get; } = ToolCategory.General;

    /// <inheritdoc/>
    Type IMcpTool.ParameterType => typeof(TParams);

    /// <inheritdoc/>
    Type IMcpTool.ResultType => typeof(TResult);

    /// <summary>
    /// Executes the tool's core logic. Override this in derived classes.
    /// </summary>
    /// <param name="parameters">The validated parameters for the tool.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The result of the tool execution.</returns>
    protected abstract Task<TResult> ExecuteInternalAsync(TParams parameters, CancellationToken cancellationToken);

    /// <inheritdoc/>
    public async Task<TResult> ExecuteAsync(TParams parameters, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Validate parameters
            ValidateParameters(parameters);
            
            _logger?.LogDebug("Executing tool '{ToolName}' with parameters: {Parameters}", 
                Name, JsonSerializer.Serialize(parameters, _jsonOptions));
            
            // Execute with token management
            var result = await ExecuteWithTokenManagement(
                () => ExecuteInternalAsync(parameters, cancellationToken),
                cancellationToken);
            
            _logger?.LogInformation("Tool '{ToolName}' executed successfully in {ElapsedMs}ms", 
                Name, stopwatch.ElapsedMilliseconds);
            
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("Tool '{ToolName}' was cancelled after {ElapsedMs}ms", 
                Name, stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (ValidationException ex)
        {
            _logger?.LogError(ex, "Validation failed for tool '{ToolName}'", Name);
            throw new ToolExecutionException(Name, $"Validation failed: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Tool '{ToolName}' failed after {ElapsedMs}ms", 
                Name, stopwatch.ElapsedMilliseconds);
            throw new ToolExecutionException(Name, $"Tool execution failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    async Task<object?> IMcpTool.ExecuteAsync(object? parameters, CancellationToken cancellationToken)
    {
        TParams? typedParams = null;
        
        if (parameters != null)
        {
            // Handle both direct type and JSON deserialization
            if (parameters is TParams directParams)
            {
                typedParams = directParams;
            }
            else if (parameters is JsonElement jsonElement)
            {
                var json = jsonElement.GetRawText();
                typedParams = JsonSerializer.Deserialize<TParams>(json, _jsonOptions);
            }
            else
            {
                // Try to serialize and deserialize to handle object -> TParams conversion
                var json = JsonSerializer.Serialize(parameters, _jsonOptions);
                typedParams = JsonSerializer.Deserialize<TParams>(json, _jsonOptions);
            }
        }
        
        if (typedParams == null && typeof(TParams) != typeof(EmptyParameters))
        {
            throw new ValidationException($"Parameters are required for tool '{Name}'");
        }
        
        typedParams ??= Activator.CreateInstance<TParams>();
        var result = await ExecuteAsync(typedParams, cancellationToken);
        return result;
    }

    /// <inheritdoc/>
    public virtual object GetInputSchema()
    {
        return JsonSchemaGenerator.GenerateSchema<TParams>();
    }

    /// <summary>
    /// Validates the input parameters using data annotations and custom validation.
    /// </summary>
    /// <param name="parameters">The parameters to validate.</param>
    protected virtual void ValidateParameters(TParams parameters)
    {
        if (parameters == null && typeof(TParams) != typeof(EmptyParameters))
        {
            throw new ValidationException($"Parameters are required for tool '{Name}'");
        }

        if (parameters != null)
        {
            var validationContext = new DataAnnotations.ValidationContext(parameters);
            var validationResults = new List<DataAnnotations.ValidationResult>();
            
            if (!Validator.TryValidateObject(parameters, validationContext, validationResults, true))
            {
                var errors = string.Join("; ", validationResults.Select(r => r.ErrorMessage));
                throw new ValidationException($"Parameter validation failed: {errors}");
            }
        }
    }

    /// <summary>
    /// Executes an operation with token management and monitoring.
    /// </summary>
    protected async Task<T> ExecuteWithTokenManagement<T>(
        Func<Task<T>> operation, 
        CancellationToken cancellationToken = default)
    {
        // Token estimation could be added here if needed
        var tokenEstimate = EstimateTokenUsage();
        
        if (tokenEstimate > TokenLimits.DefaultMaxTokens)
        {
            _logger?.LogWarning("Tool '{ToolName}' may exceed token limit. Estimated: {Tokens}", 
                Name, tokenEstimate);
        }
        
        return await operation();
    }

    /// <summary>
    /// Estimates the token usage for this tool execution.
    /// Override in derived classes for accurate estimation.
    /// </summary>
    protected virtual int EstimateTokenUsage()
    {
        // Basic estimation - override for more accuracy
        return 1000;
    }

    #region Validation Helpers from Original McpToolBase

    /// <summary>
    /// Validates that a required parameter is not null or empty.
    /// </summary>
    protected T ValidateRequired<T>(T? value, string parameterName)
    {
        if (value == null || (value is string str && string.IsNullOrWhiteSpace(str)))
        {
            throw new ValidationException($"Parameter '{parameterName}' is required");
        }
        return value;
    }

    /// <summary>
    /// Validates that a numeric value is positive.
    /// </summary>
    protected int ValidatePositive(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ValidationException($"Parameter '{parameterName}' must be positive");
        }
        return value;
    }

    /// <summary>
    /// Validates that a value is within a specified range.
    /// </summary>
    protected int ValidateRange(int value, int min, int max, string parameterName)
    {
        if (value < min || value > max)
        {
            throw new ValidationException($"Parameter '{parameterName}' must be between {min} and {max}");
        }
        return value;
    }

    /// <summary>
    /// Validates a collection is not null or empty.
    /// </summary>
    protected ICollection<T> ValidateNotEmpty<T>(ICollection<T>? collection, string parameterName)
    {
        if (collection == null || collection.Count == 0)
        {
            throw new ValidationException($"Parameter '{parameterName}' cannot be empty");
        }
        return collection;
    }

    #endregion

    #region Error Result Helpers

    /// <summary>
    /// Creates a standardized error result.
    /// </summary>
    protected ErrorInfo CreateErrorResult(string operation, string error, string? recoveryStep = null)
    {
        return new ErrorInfo
        {
            Code = "TOOL_ERROR",
            Message = error,
            Recovery = recoveryStep != null ? new RecoveryInfo { Steps = new[] { recoveryStep } } : null
        };
    }

    /// <summary>
    /// Creates a validation error result.
    /// </summary>
    protected ErrorInfo CreateValidationErrorResult(string operation, string paramName, string requirement)
    {
        return new ErrorInfo
        {
            Code = "VALIDATION_ERROR",
            Message = $"Parameter '{paramName}' validation failed: {requirement}",
            Recovery = new RecoveryInfo 
            { 
                Steps = new[] { $"Provide a valid value for '{paramName}' that {requirement}" } 
            }
        };
    }

    #endregion
}

/// <summary>
/// Represents empty parameters for tools that don't require input.
/// </summary>
public class EmptyParameters
{
}

/// <summary>
/// Static class containing token limit constants.
/// </summary>
public static class TokenLimits
{
    /// <summary>
    /// Default maximum tokens for a tool response.
    /// </summary>
    public const int DefaultMaxTokens = 10000;
    
    /// <summary>
    /// Conservative token limit for safety.
    /// </summary>
    public const int ConservativeMaxTokens = 5000;
    
    /// <summary>
    /// Maximum tokens for summary responses.
    /// </summary>
    public const int SummaryMaxTokens = 2000;
}