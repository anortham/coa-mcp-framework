using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DataAnnotations = System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        // Use centralized JSON configuration from DI container
        _jsonOptions = serviceProvider?.GetService<JsonSerializerOptions>() ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
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
            return await ExecuteWithMiddlewareAsync(parameters, cancellationToken).ConfigureAwait(false);
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
                parameters,
                cancellationToken).ConfigureAwait(false);
            
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
                await middleware.OnBeforeExecutionAsync(Name, parameters).ConfigureAwait(false);
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
                parameters,
                cancellationToken).ConfigureAwait(false);
            
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("Tool '{ToolName}' executed successfully in {ElapsedMs}ms", 
                    Name, stopwatch.ElapsedMilliseconds);
            }

            // Return result directly

            // After execution hooks (in reverse order)
            foreach (var middleware in sortedMiddleware.AsEnumerable().Reverse())
            {
                await middleware.OnAfterExecutionAsync(Name, parameters, result, stopwatch.ElapsedMilliseconds).ConfigureAwait(false);
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
                    await middleware.OnErrorAsync(Name, parameters, ex, stopwatch.ElapsedMilliseconds).ConfigureAwait(false);
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
        var result = await ExecuteAsync(typedParams, cancellationToken).ConfigureAwait(false);
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
        return await ExecuteWithTokenManagement(operation, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes an operation with parameter-aware token management and monitoring.
    /// </summary>
    protected async Task<T> ExecuteWithTokenManagement<T>(
        Func<Task<T>> operation,
        TParams? parameters,
        CancellationToken cancellationToken = default)
    {
        var tokenEstimate = EstimateTokenUsage(parameters);
        var budget = TokenBudget;

        _logger?.LogDebug("Tool '{ToolName}' token estimate: {Estimate}, Budget: {Budget}, Strategy: {Strategy}",
            Name, tokenEstimate, budget.MaxTokens, budget.Strategy);

        // Check token budget
        if (tokenEstimate > budget.MaxTokens)
        {
            var errorMessage = $"Tool '{Name}' estimated tokens ({tokenEstimate:N0}) exceeds budget ({budget.MaxTokens:N0})";

            switch (budget.Strategy)
            {
                case TokenLimitStrategy.Throw:
                    throw new InvalidOperationException(errorMessage);

                case TokenLimitStrategy.Warn:
                    _logger?.LogWarning("Token budget exceeded: {Message}", errorMessage);
                    break;

                case TokenLimitStrategy.Truncate:
                    _logger?.LogInformation("Token budget exceeded, output may be truncated: {Message}", errorMessage);
                    break;

                case TokenLimitStrategy.Ignore:
                    _logger?.LogDebug("Token budget exceeded but ignored: {Message}", errorMessage);
                    break;
            }
        }
        else if (tokenEstimate > budget.WarningThreshold)
        {
            _logger?.LogDebug("Tool '{ToolName}' approaching token limit. Estimated: {Tokens:N0}, Warning: {Threshold:N0}",
                Name, tokenEstimate, budget.WarningThreshold);
        }

        // Execute operation and measure actual result size
        var startTime = DateTime.UtcNow;
        var result = await operation().ConfigureAwait(false);
        var executionTime = DateTime.UtcNow - startTime;

        // Estimate actual tokens in result for telemetry
        var actualTokens = EstimateActualResultTokens(result);

        // Log token usage telemetry
        LogTokenUsageTelemetry(tokenEstimate, actualTokens, executionTime);

        return result;
    }

    /// <summary>
    /// Estimates actual tokens in the result for telemetry purposes.
    /// </summary>
    private int EstimateActualResultTokens<T>(T result)
    {
        try
        {
            var json = JsonSerializer.Serialize(result, _jsonOptions);
            return EstimateTokensFromText(json);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to estimate actual result tokens for tool '{ToolName}'", Name);
            return 0;
        }
    }

    /// <summary>
    /// Logs token usage telemetry for monitoring and optimization.
    /// </summary>
    private void LogTokenUsageTelemetry(int estimatedTokens, int actualTokens, TimeSpan executionTime)
    {
        if (_logger?.IsEnabled(LogLevel.Information) == true)
        {
            var accuracy = actualTokens > 0 ? (double)Math.Min(estimatedTokens, actualTokens) / Math.Max(estimatedTokens, actualTokens) : 1.0;

            _logger.LogInformation(
                "Token Usage - Tool: {ToolName}, Estimated: {Estimated:N0}, Actual: {Actual:N0}, " +
                "Accuracy: {Accuracy:P1}, ExecutionTime: {ExecutionTime:N0}ms",
                Name, estimatedTokens, actualTokens, accuracy, executionTime.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Estimates the token usage for this tool execution.
    /// Override in derived classes for accurate estimation.
    /// </summary>
    protected virtual int EstimateTokenUsage()
    {
        return EstimateTokenUsage(null);
    }

    /// <summary>
    /// Estimates the token usage for this tool execution with specific parameters.
    /// This provides a more accurate estimation than the parameterless version.
    /// Override in derived classes for tool-specific estimation logic.
    /// </summary>
    /// <param name="parameters">The parameters for this tool execution (null for generic estimation)</param>
    /// <returns>Estimated token count for this tool execution</returns>
    protected virtual int EstimateTokenUsage(TParams? parameters)
    {
        try
        {
            var baseEstimate = CalculateBaseTokenEstimate();
            var parameterEstimate = parameters != null ? EstimateParameterTokens(parameters) : 0;
            var resultEstimate = EstimateResultTokens();

            var totalEstimate = baseEstimate + parameterEstimate + resultEstimate;

            // Apply estimation multiplier from token budget configuration
            var multiplier = TokenBudget.EstimationMultiplier;
            return (int)(totalEstimate * multiplier);
        }
        catch (Exception ex)
        {
            // Fallback to conservative estimate if calculation fails
            _logger?.LogWarning(ex, "Token estimation failed for tool '{ToolName}', using fallback", Name);
            return TokenBudget.MaxTokens / 2; // Use half of max budget as fallback
        }
    }

    /// <summary>
    /// Calculates base token estimate for this tool type.
    /// Override to provide tool-specific base estimates.
    /// </summary>
    protected virtual int CalculateBaseTokenEstimate()
    {
        // Base estimate varies by tool category
        return Category switch
        {
            ToolCategory.Query => 2000,         // Query/Search tools typically return many results
            ToolCategory.Analysis => 1500,      // Analysis tools return structured data
            ToolCategory.Resources => 800,      // Resource tools usually return paths/metadata
            ToolCategory.Utility => 500,        // Utility tools typically have small outputs
            ToolCategory.Integration => 1200,   // Integration tools return records/metadata
            ToolCategory.Monitoring => 1000,    // Monitoring tools return status/data
            _ => 1000                           // Default for General and other categories
        };
    }

    /// <summary>
    /// Estimates tokens consumed by tool parameters.
    /// Override for parameter-specific estimation logic.
    /// </summary>
    protected virtual int EstimateParameterTokens(TParams parameters)
    {
        // Basic JSON size estimation without external dependencies
        try
        {
            var json = JsonSerializer.Serialize(parameters, _jsonOptions);
            return EstimateTokensFromText(json);
        }
        catch
        {
            // Fallback if serialization fails
            return 100;
        }
    }

    /// <summary>
    /// Estimates tokens that will be produced in the result.
    /// Uses dynamic estimation based on result type complexity and expected data patterns.
    /// Override for result-specific estimation logic.
    /// </summary>
    protected virtual int EstimateResultTokens()
    {
        var resultType = typeof(TResult);

        // Check if it's a collection type
        if (resultType.IsGenericType)
        {
            var genericDef = resultType.GetGenericTypeDefinition();
            if (genericDef == typeof(List<>) || genericDef == typeof(IEnumerable<>) ||
                genericDef == typeof(ICollection<>) || genericDef == typeof(IList<>))
            {
                // Estimate collection tokens based on generic type
                var itemType = resultType.GetGenericArguments()[0];
                var tokensPerItem = EstimateTokensForType(itemType);

                // Estimate collection size based on tool category
                var expectedItems = Category switch
                {
                    ToolCategory.Query => 25,      // Search results typically return many items
                    ToolCategory.Analysis => 15,   // Analysis usually returns fewer, more detailed items
                    ToolCategory.Resources => 50,  // Resource listings can be large
                    ToolCategory.Utility => 5,     // Utilities typically return few items
                    _ => 20                        // Default moderate collection size
                };

                // Collection overhead (array brackets, commas, etc.)
                var collectionOverhead = expectedItems * 2; // Rough estimate for JSON structure

                return (tokensPerItem * expectedItems) + collectionOverhead;
            }
        }

        // Check for complex response types
        if (resultType.Name.Contains("Response") || resultType.Name.Contains("Result"))
        {
            // Response objects typically have metadata + data
            var baseResponseTokens = 200; // Headers, metadata, status
            var dataEstimate = EstimateTokensForType(resultType) - baseResponseTokens;
            return baseResponseTokens + Math.Max(dataEstimate, 500);
        }

        // Check for simple types
        if (resultType.IsPrimitive || resultType == typeof(string) || resultType == typeof(DateTime))
        {
            return 50; // Simple types produce minimal tokens
        }

        return EstimateTokensForType(resultType);
    }

    /// <summary>
    /// Estimates token count for a specific type based on its complexity.
    /// </summary>
    private int EstimateTokensForType(Type type)
    {
        // Simple types
        if (type.IsPrimitive || type == typeof(string) || type == typeof(DateTime))
            return 30;

        // Known complex types
        if (type.Name.EndsWith("SearchResult") || type.Name.EndsWith("Response"))
            return 800;

        // Objects with file paths (common in CodeSearch)
        if (type.Name.Contains("File") || type.Name.Contains("Path"))
            return 150;

        // Symbol/code-related objects
        if (type.Name.Contains("Symbol") || type.Name.Contains("Definition"))
            return 250;

        // Count properties if it's a public type we can reflect on
        try
        {
            if (type.IsClass && type.IsPublic)
            {
                var properties = type.GetProperties();
                var methods = type.GetMethods().Where(m => m.DeclaringType == type).Count();

                // Rough estimation: 25 tokens per property, 15 per method name
                return (properties.Length * 25) + (methods * 15) + 100; // Base object overhead
            }
        }
        catch
        {
            // If reflection fails, fall back to default
        }

        return 400; // Default for unknown complex objects
    }

    /// <summary>
    /// Estimates tokens from text using improved character/word ratios and JSON structure analysis.
    /// Uses research-backed token ratios and accounts for JSON overhead.
    /// </summary>
    /// <param name="text">The text to estimate</param>
    /// <returns>Estimated token count</returns>
    protected virtual int EstimateTokensFromText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        var charCount = text.Length;
        var isJson = IsJsonContent(text);

        // More accurate ratios based on GPT tokenization research:
        // - Natural language: ~3.8 chars/token
        // - Code/JSON: ~3.2 chars/token due to more punctuation/structure
        var baseRatio = isJson ? 3.2 : 3.8;

        // Count different content types for better estimation
        var punctuationCount = CountJsonStructureTokens(text);
        var wordCount = CountWords(text);
        var numberCount = CountNumbers(text);

        // Base character estimate with improved ratio
        var charBasedEstimate = (int)Math.Ceiling(charCount / baseRatio);

        // Word-based estimate (adjusted for technical content)
        var wordsPerToken = isJson ? 0.9 : 0.75; // JSON has more single-token words
        var wordBasedEstimate = (int)Math.Ceiling(wordCount / wordsPerToken);

        // JSON overhead: structural tokens (brackets, commas, colons, quotes)
        var structuralOverhead = isJson ? (int)(punctuationCount * 0.8) : 0;

        // Numbers and special tokens often map 1:1
        var specialTokens = numberCount;

        // Use the most conservative estimate but add structural overhead
        var baseEstimate = Math.Max(charBasedEstimate, wordBasedEstimate);
        var totalEstimate = baseEstimate + structuralOverhead + specialTokens;

        // Add 5% buffer for estimation uncertainty
        return (int)(totalEstimate * 1.05);
    }

    /// <summary>
    /// Determines if the text content appears to be JSON.
    /// </summary>
    private bool IsJsonContent(string text)
    {
        if (text.Length < 10) return false;

        var trimmed = text.Trim();
        var hasJsonMarkers = (trimmed.StartsWith("{") && trimmed.EndsWith("}")) ||
                           (trimmed.StartsWith("[") && trimmed.EndsWith("]"));

        // Check for high density of JSON punctuation
        var jsonChars = text.Count(c => c is '{' or '}' or '[' or ']' or ':' or ',' or '"');
        var jsonDensity = jsonChars / (double)text.Length;

        return hasJsonMarkers || jsonDensity > 0.15;
    }

    /// <summary>
    /// Counts JSON structural tokens that typically map to individual tokens.
    /// </summary>
    private int CountJsonStructureTokens(string text)
    {
        return text.Count(c => c is '{' or '}' or '[' or ']' or ':' or ',' or '"');
    }

    /// <summary>
    /// Counts words using improved tokenization that handles technical content.
    /// </summary>
    private int CountWords(string text)
    {
        // Split on whitespace and common separators
        var words = text.Split(new char[] { ' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?' },
                              StringSplitOptions.RemoveEmptyEntries);

        // Filter out single characters and empty strings
        return words.Count(w => w.Length > 0);
    }

    /// <summary>
    /// Counts numeric sequences which often tokenize as single tokens.
    /// </summary>
    private int CountNumbers(string text)
    {
        var numberMatches = 0;
        var inNumber = false;

        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsDigit(text[i]) || text[i] == '.')
            {
                if (!inNumber)
                {
                    numberMatches++;
                    inNumber = true;
                }
            }
            else
            {
                inNumber = false;
            }
        }

        return numberMatches;
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