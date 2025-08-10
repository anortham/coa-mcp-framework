using System;
using COA.Mcp.Framework.Models;

namespace COA.Mcp.Framework.Base;

/// <summary>
/// Provides customizable error messages and recovery information for MCP tools.
/// Override this class to provide tool-specific error messages and guidance.
/// </summary>
public class ErrorMessageProvider
{
    /// <summary>
    /// Creates a validation error message.
    /// </summary>
    /// <param name="paramName">The parameter that failed validation.</param>
    /// <param name="requirement">The validation requirement that was not met.</param>
    /// <returns>The formatted error message.</returns>
    public virtual string ValidationFailed(string paramName, string requirement)
    {
        return $"Parameter '{paramName}' validation failed: {requirement}";
    }

    /// <summary>
    /// Creates a tool execution error message.
    /// </summary>
    /// <param name="toolName">The name of the tool that failed.</param>
    /// <param name="details">Details about the failure.</param>
    /// <returns>The formatted error message.</returns>
    public virtual string ToolExecutionFailed(string toolName, string details)
    {
        return $"Tool '{toolName}' execution failed: {details}";
    }

    /// <summary>
    /// Creates a required parameter error message.
    /// </summary>
    /// <param name="paramName">The name of the required parameter.</param>
    /// <returns>The formatted error message.</returns>
    public virtual string ParameterRequired(string paramName)
    {
        return $"Parameter '{paramName}' is required";
    }

    /// <summary>
    /// Creates a range validation error message.
    /// </summary>
    /// <param name="paramName">The parameter that failed validation.</param>
    /// <param name="min">The minimum allowed value.</param>
    /// <param name="max">The maximum allowed value.</param>
    /// <returns>The formatted error message.</returns>
    public virtual string RangeValidationFailed(string paramName, object min, object max)
    {
        return $"Parameter '{paramName}' must be between {min} and {max}";
    }

    /// <summary>
    /// Creates a positive value validation error message.
    /// </summary>
    /// <param name="paramName">The parameter that must be positive.</param>
    /// <returns>The formatted error message.</returns>
    public virtual string MustBePositive(string paramName)
    {
        return $"Parameter '{paramName}' must be positive";
    }

    /// <summary>
    /// Creates an empty collection error message.
    /// </summary>
    /// <param name="paramName">The parameter that cannot be empty.</param>
    /// <returns>The formatted error message.</returns>
    public virtual string CannotBeEmpty(string paramName)
    {
        return $"Parameter '{paramName}' cannot be empty";
    }

    /// <summary>
    /// Gets recovery information for a specific error code.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="context">Optional context about the error.</param>
    /// <param name="exception">Optional exception that caused the error.</param>
    /// <returns>Recovery information with steps and suggested actions.</returns>
    public virtual RecoveryInfo GetRecoveryInfo(string errorCode, string? context = null, Exception? exception = null)
    {
        return errorCode switch
        {
            "VALIDATION_ERROR" => new RecoveryInfo
            {
                Steps = new[]
                {
                    "Check the parameter requirements in the tool documentation",
                    "Ensure all required parameters are provided",
                    "Verify parameter types and ranges are correct"
                }
            },
            "TOOL_ERROR" => new RecoveryInfo
            {
                Steps = new[]
                {
                    "Review the error message for specific details",
                    "Check if resources are available and accessible",
                    "Retry the operation if the error is transient"
                }
            },
            "TIMEOUT" => new RecoveryInfo
            {
                Steps = new[]
                {
                    "Consider using smaller input data",
                    "Increase timeout if configurable",
                    "Check system resources and network connectivity"
                }
            },
            "RESOURCE_LIMIT_EXCEEDED" => new RecoveryInfo
            {
                Steps = new[]
                {
                    "Reduce the size of the request",
                    "Process data in smaller batches",
                    "Check token budget configuration"
                }
            },
            _ => new RecoveryInfo
            {
                Steps = new[] { "Check the error details and retry if appropriate" }
            }
        };
    }

    /// <summary>
    /// Creates suggested actions for error recovery.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="toolName">The name of the tool that failed.</param>
    /// <returns>List of suggested actions for recovery.</returns>
    public virtual List<SuggestedAction> GetSuggestedActions(string errorCode, string toolName)
    {
        return new List<SuggestedAction>();
    }
}

/// <summary>
/// Default error message provider with standard messages.
/// </summary>
public class DefaultErrorMessageProvider : ErrorMessageProvider
{
    // Uses all base implementations
}