using System;
using System.Linq;
using COA.Mcp.Framework.Interfaces;

namespace COA.Mcp.Framework.Exceptions;

/// <summary>
/// Base exception for MCP framework errors.
/// </summary>
public class McpException : Exception
{
    /// <summary>
    /// Gets the error code.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Gets additional error details.
    /// </summary>
    public object? Details { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="McpException"/> class.
    /// </summary>
    public McpException(string message, string errorCode = "MCP_ERROR") 
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="McpException"/> class.
    /// </summary>
    public McpException(string message, string errorCode, Exception innerException) 
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="McpException"/> class with details.
    /// </summary>
    public McpException(string message, string errorCode, object details) 
        : base(message)
    {
        ErrorCode = errorCode;
        Details = details;
    }
}

/// <summary>
/// Exception thrown when a tool is not found.
/// </summary>
public class ToolNotFoundException : McpException
{
    /// <summary>
    /// Gets the tool name that was not found.
    /// </summary>
    public string ToolName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolNotFoundException"/> class.
    /// </summary>
    public ToolNotFoundException(string toolName)
        : base($"Tool '{toolName}' not found.", "TOOL_NOT_FOUND")
    {
        ToolName = toolName;
    }
}

/// <summary>
/// Exception thrown when tool validation fails.
/// </summary>
public class ToolValidationException : McpException
{
    /// <summary>
    /// Gets the validation result.
    /// </summary>
    public ToolValidationResult ValidationResult { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolValidationException"/> class.
    /// </summary>
    public ToolValidationException(ToolValidationResult validationResult)
        : base(validationResult.GetFormattedMessage(), "TOOL_VALIDATION_FAILED", validationResult)
    {
        ValidationResult = validationResult;
    }
}

/// <summary>
/// Exception thrown when parameter validation fails.
/// </summary>
public class ParameterValidationException : McpException
{
    /// <summary>
    /// Gets the validation result.
    /// </summary>
    public ValidationResult ValidationResult { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParameterValidationException"/> class.
    /// </summary>
    public ParameterValidationException(ValidationResult validationResult)
        : base(GetMessage(validationResult), "PARAMETER_VALIDATION_FAILED", validationResult)
    {
        ValidationResult = validationResult;
    }

    private static string GetMessage(ValidationResult result)
    {
        var errors = string.Join("; ", result.Errors.Select(e => e.FullMessage));
        return $"Parameter validation failed: {errors}";
    }
}

/// <summary>
/// Exception thrown when tool execution fails.
/// </summary>
public class ToolExecutionException : McpException
{
    /// <summary>
    /// Gets the tool name.
    /// </summary>
    public string ToolName { get; }

    /// <summary>
    /// Gets the execution context.
    /// </summary>
    public ToolExecutionContext? Context { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolExecutionException"/> class.
    /// </summary>
    public ToolExecutionException(string toolName, string message, Exception? innerException = null)
        : base($"Tool '{toolName}' execution failed: {message}", "TOOL_EXECUTION_FAILED", innerException!)
    {
        ToolName = toolName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolExecutionException"/> class with context.
    /// </summary>
    public ToolExecutionException(string toolName, string message, ToolExecutionContext context, Exception? innerException = null)
        : base($"Tool '{toolName}' execution failed: {message}", "TOOL_EXECUTION_FAILED", innerException!)
    {
        ToolName = toolName;
        Context = context;
    }
}

/// <summary>
/// Exception thrown when tool registration fails.
/// </summary>
public class ToolRegistrationException : McpException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ToolRegistrationException"/> class.
    /// </summary>
    public ToolRegistrationException(string message, Exception? innerException = null)
        : base(message, "TOOL_REGISTRATION_FAILED", innerException!)
    {
    }
}