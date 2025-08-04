using System;
using System.Diagnostics;
using System.Threading.Tasks;
using COA.Mcp.Framework.Interfaces;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.Base;

/// <summary>
/// Base class for MCP tools providing common functionality and validation helpers.
/// </summary>
public abstract class McpToolBase : ITool
{
    private readonly ILogger? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpToolBase"/> class.
    /// </summary>
    protected McpToolBase(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public abstract string ToolName { get; }

    /// <inheritdoc/>
    public abstract string Description { get; }

    /// <inheritdoc/>
    public abstract ToolCategory Category { get; }

    /// <inheritdoc/>
    public abstract Task<object> ExecuteAsync(object parameters);

    /// <summary>
    /// Gets the current execution context.
    /// </summary>
    protected ToolExecutionContext? CurrentContext { get; private set; }

    /// <summary>
    /// Executes an operation with token management.
    /// </summary>
    protected async Task<TResult> ExecuteWithTokenManagement<TResult>(
        Func<Task<TResult>> operation,
        ToolExecutionContext? context = null)
    {
        CurrentContext = context;
        var sw = Stopwatch.StartNew();

        try
        {
            _logger?.LogDebug("Executing tool {ToolName} with token management", ToolName);
            
            var result = await operation();
            
            if (context != null)
            {
                context.Metrics.ExecutionTime = sw.Elapsed;
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing tool {ToolName}", ToolName);
            throw;
        }
        finally
        {
            CurrentContext = null;
        }
    }

    #region Validation Helpers

    /// <summary>
    /// Validates that a parameter is not null or empty.
    /// </summary>
    protected T ValidateRequired<T>(T? value, string paramName)
    {
        if (value == null)
        {
            throw new ArgumentNullException(paramName, $"Parameter '{paramName}' is required.");
        }

        if (value is string str && string.IsNullOrWhiteSpace(str))
        {
            throw new ArgumentException($"Parameter '{paramName}' cannot be empty.", paramName);
        }

        return value;
    }

    /// <summary>
    /// Validates that a numeric value is positive.
    /// </summary>
    protected int ValidatePositive(int value, string paramName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, 
                $"Parameter '{paramName}' must be positive.");
        }

        return value;
    }

    /// <summary>
    /// Validates that a numeric value is non-negative.
    /// </summary>
    protected int ValidateNonNegative(int value, string paramName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, 
                $"Parameter '{paramName}' cannot be negative.");
        }

        return value;
    }

    /// <summary>
    /// Validates that a value is within a specified range.
    /// </summary>
    protected int ValidateRange(int value, int min, int max, string paramName)
    {
        if (value < min || value > max)
        {
            throw new ArgumentOutOfRangeException(paramName, value,
                $"Parameter '{paramName}' must be between {min} and {max}.");
        }

        return value;
    }

    /// <summary>
    /// Validates that a value is within a specified range.
    /// </summary>
    protected double ValidateRange(double value, double min, double max, string paramName)
    {
        if (value < min || value > max)
        {
            throw new ArgumentOutOfRangeException(paramName, value,
                $"Parameter '{paramName}' must be between {min} and {max}.");
        }

        return value;
    }

    /// <summary>
    /// Validates string length.
    /// </summary>
    protected string ValidateStringLength(string? value, int maxLength, string paramName, int minLength = 0)
    {
        value = ValidateRequired(value, paramName);

        if (value.Length < minLength)
        {
            throw new ArgumentException(
                $"Parameter '{paramName}' must be at least {minLength} characters long.", 
                paramName);
        }

        if (value.Length > maxLength)
        {
            throw new ArgumentException(
                $"Parameter '{paramName}' must not exceed {maxLength} characters.", 
                paramName);
        }

        return value;
    }

    /// <summary>
    /// Validates that a collection is not empty.
    /// </summary>
    protected T ValidateNotEmpty<T>(T? collection, string paramName) 
        where T : System.Collections.IEnumerable
    {
        collection = ValidateRequired(collection, paramName);

        var enumerator = collection.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            throw new ArgumentException($"Parameter '{paramName}' cannot be empty.", paramName);
        }

        return collection;
    }

    /// <summary>
    /// Validates enum values.
    /// </summary>
    protected TEnum ValidateEnum<TEnum>(TEnum value, string paramName) 
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(typeof(TEnum), value))
        {
            throw new ArgumentException(
                $"Parameter '{paramName}' has invalid value '{value}'. " +
                $"Valid values are: {string.Join(", ", Enum.GetNames(typeof(TEnum)))}",
                paramName);
        }

        return value;
    }

    #endregion

    #region Logging Helpers

    /// <summary>
    /// Logs a debug message.
    /// </summary>
    protected void LogDebug(string message, params object[] args)
    {
        _logger?.LogDebug(message, args);
    }

    /// <summary>
    /// Logs an information message.
    /// </summary>
    protected void LogInformation(string message, params object[] args)
    {
        _logger?.LogInformation(message, args);
    }

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    protected void LogWarning(string message, params object[] args)
    {
        _logger?.LogWarning(message, args);
    }

    /// <summary>
    /// Logs an error message.
    /// </summary>
    protected void LogError(Exception ex, string message, params object[] args)
    {
        _logger?.LogError(ex, message, args);
    }

    #endregion

    #region Response Helpers

    /// <summary>
    /// Creates a success response.
    /// </summary>
    protected static object CreateSuccessResponse(object data, string? message = null)
    {
        return new
        {
            Success = true,
            Message = message,
            Data = data,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Creates an error response.
    /// </summary>
    protected static object CreateErrorResponse(string error, string? details = null)
    {
        return new
        {
            Success = false,
            Error = error,
            Details = details,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Creates a response with insights and actions.
    /// </summary>
    protected static object CreateIntelligentResponse(
        object data,
        string[]? insights = null,
        object[]? actions = null,
        object? metadata = null)
    {
        return new
        {
            Success = true,
            Data = data,
            Insights = insights ?? Array.Empty<string>(),
            Actions = actions ?? Array.Empty<object>(),
            Meta = metadata,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    #endregion
}