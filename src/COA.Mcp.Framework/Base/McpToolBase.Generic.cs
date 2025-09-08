using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DataAnnotations = System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework.Configuration;
using COA.Mcp.Framework.Exceptions;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using COA.Mcp.Framework.Schema;
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
    private readonly IReadOnlyList<ISimpleMiddleware> _globalMiddleware;
    private ErrorMessageProvider? _errorMessageProvider;
    private TokenBudgetConfiguration? _tokenBudgetConfiguration;

    /// <summary>
    /// Initializes a new instance of the McpToolBase class.
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency injection and global middleware resolution.</param>
    /// <param name="logger">Optional logger for the tool.</param>
    protected McpToolBase(IServiceProvider? serviceProvider = null, ILogger? logger = null)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        // Resolve global middleware from DI container
        _globalMiddleware = serviceProvider?.GetServices<ISimpleMiddleware>()
            ?.Where(m => m.IsEnabled)
            ?.OrderBy(m => m.Order)
            ?.ToList() ?? new List<ISimpleMiddleware>();
    }

    /// <summary>
    /// Gets the error message provider for this tool.
    /// Override to provide custom error messages and recovery information.
    /// </summary>
    protected virtual ErrorMessageProvider ErrorMessages => 
        _errorMessageProvider ??= new DefaultErrorMessageProvider();

    /// <summary>
    /// Gets the token budget configuration for this tool.
    /// Override to customize token limits and strategies.
    /// </summary>
    protected virtual TokenBudgetConfiguration TokenBudget =>
        _tokenBudgetConfiguration ??= new TokenBudgetConfiguration();

    /// <summary>
    /// Gets the simple middleware instances for this tool.
    /// Override to provide lifecycle hooks.
    /// </summary>
    protected virtual IReadOnlyList<ISimpleMiddleware>? ToolSpecificMiddleware { get; }

    /// <summary>
    /// Gets the combined middleware (global + tool-specific) for this tool.
    /// </summary>
    protected virtual IReadOnlyList<ISimpleMiddleware>? Middleware
    {
        get
        {
            var combined = new List<ISimpleMiddleware>(_globalMiddleware);
            
            if (ToolSpecificMiddleware != null)
            {
                combined.AddRange(ToolSpecificMiddleware);
            }
            
            // Sort by order and return
            return combined.OrderBy(m => m.Order).ToList();
        }
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
    public virtual async Task<TResult> ExecuteAsync(TParams parameters, CancellationToken cancellationToken = default)
    {
        // If middleware is configured, use it
        if (Middleware != null && Middleware.Count > 0)
        {
            return await ExecuteWithMiddlewareAsync(parameters, cancellationToken);
        }

        // Otherwise, use standard execution path
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Validate parameters
            ValidateParameters(parameters);
            
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("Executing tool '{ToolName}' with parameters: {Parameters}", 
                    Name, JsonSerializer.Serialize(parameters, _jsonOptions));
            }
            
            // Execute with token management
            var result = await ExecuteWithTokenManagement(
                () => ExecuteInternalAsync(parameters, cancellationToken),
                cancellationToken);
            
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("Tool '{ToolName}' executed successfully in {ElapsedMs}ms", 
                    Name, stopwatch.ElapsedMilliseconds);
            }
            
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
            var message = ErrorMessages.ValidationFailed("parameters", ex.Message);
            throw new ToolExecutionException(Name, message, ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Tool '{ToolName}' failed after {ElapsedMs}ms", 
                Name, stopwatch.ElapsedMilliseconds);
            var message = ErrorMessages.ToolExecutionFailed(Name, ex.Message);
            throw new ToolExecutionException(Name, message, ex);
        }
    }

    /// <summary>
    /// Executes the tool with middleware support.
    /// </summary>
    private async Task<TResult> ExecuteWithMiddlewareAsync(TParams parameters, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Sort middleware by order (enabled ones only)
        var sortedMiddleware = Middleware!
            .Where(m => m.IsEnabled)
            .OrderBy(m => m.Order)
            .ToList();

        TResult? result = default;
        Exception? caughtException = null;

        try
        {
            // Before execution hooks
            foreach (var middleware in sortedMiddleware)
            {
                await middleware.OnBeforeExecutionAsync(Name, parameters);
            }

            // Validate parameters
            ValidateParameters(parameters);
            
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("Executing tool '{ToolName}' with parameters: {Parameters}", 
                    Name, JsonSerializer.Serialize(parameters, _jsonOptions));
            }
            
            // Execute with token management
            result = await ExecuteWithTokenManagement(
                () => ExecuteInternalAsync(parameters, cancellationToken),
                cancellationToken);
            
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("Tool '{ToolName}' executed successfully in {ElapsedMs}ms", 
                    Name, stopwatch.ElapsedMilliseconds);
            }

            // Return result directly

            // After execution hooks (in reverse order)
            foreach (var middleware in sortedMiddleware.AsEnumerable().Reverse())
            {
                await middleware.OnAfterExecutionAsync(Name, parameters, result, stopwatch.ElapsedMilliseconds);
            }

            return result;
        }
        catch (Exception ex)
        {
            caughtException = ex;
            
            // Error hooks (in reverse order)
            foreach (var middleware in sortedMiddleware.AsEnumerable().Reverse())
            {
                try
                {
                    await middleware.OnErrorAsync(Name, parameters, ex, stopwatch.ElapsedMilliseconds);
                }
                catch (Exception hookEx)
                {
                    _logger?.LogError(hookEx, "Error in middleware error hook for tool '{ToolName}'", Name);
                }
            }

            // Re-throw with appropriate wrapping
            if (ex is OperationCanceledException)
            {
                _logger?.LogWarning("Tool '{ToolName}' was cancelled after {ElapsedMs}ms", 
                    Name, stopwatch.ElapsedMilliseconds);
                throw;
            }
            else if (ex is ValidationException vex)
            {
                _logger?.LogError(ex, "Validation failed for tool '{ToolName}'", Name);
                var message = ErrorMessages.ValidationFailed("parameters", vex.Message);
                throw new ToolExecutionException(Name, message, vex);
            }
            else if (ex is ToolExecutionException)
            {
                throw;
            }
            else
            {
                _logger?.LogError(ex, "Tool '{ToolName}' failed after {ElapsedMs}ms", 
                    Name, stopwatch.ElapsedMilliseconds);
                var message = ErrorMessages.ToolExecutionFailed(Name, ex.Message);
                throw new ToolExecutionException(Name, message, ex);
            }
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
                // Direct deserialization from JsonElement - more efficient
                typedParams = jsonElement.Deserialize<TParams>(_jsonOptions);
            }
            else if (parameters is JsonDocument jsonDocument)
            {
                // Direct deserialization from JsonDocument
                typedParams = jsonDocument.Deserialize<TParams>(_jsonOptions);
            }
            else
            {
                // Only as last resort - use UTF8 bytes to avoid string allocation
                var bytes = JsonSerializer.SerializeToUtf8Bytes(parameters, _jsonOptions);
                typedParams = JsonSerializer.Deserialize<TParams>(bytes, _jsonOptions);
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
    public virtual JsonSchema<TParams> GetInputSchema()
    {
        return new JsonSchema<TParams>();
    }

    /// <inheritdoc/>
    IJsonSchema IMcpTool.GetInputSchema()
    {
        return GetInputSchema();
    }



    /// <summary>
    /// Gets whether Data Annotations validation should be applied to parameters.
    /// Override in derived classes to disable automatic validation for graceful error handling.
    /// </summary>
    protected virtual bool ShouldValidateDataAnnotations => true;

    /// <summary>
    /// Validates the input parameters using data annotations and custom validation.
    /// </summary>
    /// <param name="parameters">The parameters to validate.</param>
    protected virtual void ValidateParameters(TParams parameters)
    {
        if (parameters == null && typeof(TParams) != typeof(EmptyParameters))
        {
            throw new ValidationException(ErrorMessages.ParameterRequired("parameters"));
        }

        if (parameters != null && ShouldValidateDataAnnotations)
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
        var tokenEstimate = EstimateTokenUsage();
        var budget = TokenBudget;
        
        // Check token budget
        if (tokenEstimate > budget.MaxTokens)
        {
            switch (budget.Strategy)
            {
                case TokenLimitStrategy.Throw:
                    throw new InvalidOperationException(
                        $"Tool '{Name}' estimated tokens ({tokenEstimate}) exceeds budget ({budget.MaxTokens})");
                case TokenLimitStrategy.Warn:
                    _logger?.LogWarning("Tool '{ToolName}' exceeds token budget. Estimated: {Tokens}, Max: {MaxTokens}", 
                        Name, tokenEstimate, budget.MaxTokens);
                    break;
                case TokenLimitStrategy.Truncate:
                    if (_logger?.IsEnabled(LogLevel.Debug) == true)
                    {
                        _logger.LogDebug("Tool '{ToolName}' may truncate output to stay within {MaxTokens} token budget", 
                            Name, budget.MaxTokens);
                    }
                    break;
            }
        }
        else if (tokenEstimate > budget.WarningThreshold)
        {
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("Tool '{ToolName}' approaching token limit. Estimated: {Tokens}, Warning: {Threshold}", 
                    Name, tokenEstimate, budget.WarningThreshold);
            }
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
            throw new ValidationException(ErrorMessages.ParameterRequired(parameterName));
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
            throw new ValidationException(ErrorMessages.MustBePositive(parameterName));
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
            throw new ValidationException(ErrorMessages.RangeValidationFailed(parameterName, min, max));
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
            throw new ValidationException(ErrorMessages.CannotBeEmpty(parameterName));
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
            Recovery = recoveryStep != null 
                ? new RecoveryInfo { Steps = new[] { recoveryStep } } 
                : ErrorMessages.GetRecoveryInfo("TOOL_ERROR", error)
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
            Message = ErrorMessages.ValidationFailed(paramName, requirement),
            Recovery = ErrorMessages.GetRecoveryInfo("VALIDATION_ERROR", $"{paramName}: {requirement}")
        };
    }

    #endregion
    
    #region Response Builder Helpers
    
    /// <summary>
    /// Creates a successful result with typed data.
    /// </summary>
    /// <typeparam name="TData">The type of the data.</typeparam>
    /// <param name="data">The result data.</param>
    /// <param name="message">Optional success message.</param>
    /// <returns>A successful tool result.</returns>
    protected ToolResult<TData> CreateSuccessResult<TData>(TData data, string? message = null)
    {
        return ToolResult<TData>.CreateSuccess(data, message);
    }
    
    /// <summary>
    /// Creates a failed result with error information.
    /// </summary>
    /// <typeparam name="TData">The type of the expected data.</typeparam>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="errorCode">Optional error code.</param>
    /// <returns>A failed tool result.</returns>
    protected ToolResult<TData> CreateErrorResult<TData>(string errorMessage, string errorCode = "TOOL_ERROR")
    {
        return ToolResult<TData>.CreateError(errorMessage, errorCode);
    }
    
    /// <summary>
    /// Builds a response using a response builder if the result type supports it.
    /// </summary>
    /// <typeparam name="TBuilder">The type of the response builder.</typeparam>
    /// <typeparam name="TData">The type of the input data.</typeparam>
    /// <param name="builder">The response builder instance.</param>
    /// <param name="data">The data to build the response from.</param>
    /// <param name="responseMode">The response mode ("summary" or "full").</param>
    /// <param name="tokenLimit">Optional token limit override.</param>
    /// <returns>The built response.</returns>
    protected async Task<TResult> BuildResponseAsync<TBuilder, TData>(
        TBuilder builder,
        TData data,
        string responseMode = "full",
        int? tokenLimit = null)
        where TBuilder : class
    {
        // This helper assumes TResult is compatible with the builder's output
        // The actual implementation would need reflection or a more sophisticated approach
        // For now, this is a pattern that derived classes can follow
        
        var builderType = typeof(TBuilder);
        var buildMethod = builderType.GetMethod("BuildResponseAsync");
        
        if (buildMethod != null)
        {
            var context = new Dictionary<string, object>
            {
                ["ResponseMode"] = responseMode,
                ["TokenLimit"] = tokenLimit ?? TokenBudget.MaxTokens,
                ["ToolName"] = Name
            };
            
            // Create a ResponseContext if the builder expects one
            var contextType = buildMethod.GetParameters()
                .FirstOrDefault(p => p.Name == "context")?.ParameterType;
                
            if (contextType != null)
            {
                var responseContext = Activator.CreateInstance(contextType);
                // Set properties via reflection if needed
                contextType.GetProperty("ResponseMode")?.SetValue(responseContext, responseMode);
                contextType.GetProperty("TokenLimit")?.SetValue(responseContext, tokenLimit);
                contextType.GetProperty("ToolName")?.SetValue(responseContext, Name);
                
                var task = buildMethod.Invoke(builder, new[] { data, responseContext }) as Task;
                if (task != null)
                {
                    await task;
                    var resultProperty = task.GetType().GetProperty("Result");
                    if (resultProperty != null)
                    {
                        var result = resultProperty.GetValue(task);
                        if (result is TResult typedResult)
                        {
                            return typedResult;
                        }
                    }
                }
            }
        }
        
        // Fallback: if builder doesn't work as expected, return default
        throw new InvalidOperationException($"Could not build response using {typeof(TBuilder).Name}");
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