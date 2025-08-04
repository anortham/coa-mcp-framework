using System.Collections.Generic;

namespace COA.Mcp.Framework.Interfaces;

/// <summary>
/// Service for validating tool parameters.
/// </summary>
public interface IParameterValidator
{
    /// <summary>
    /// Validates parameters for a tool.
    /// </summary>
    /// <param name="parameters">The parameters to validate.</param>
    /// <param name="parameterType">The expected parameter type.</param>
    /// <returns>Validation result.</returns>
    ValidationResult Validate(object? parameters, Type parameterType);

    /// <summary>
    /// Validates a single parameter value.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="parameterName">The parameter name.</param>
    /// <param name="validationAttributes">Validation attributes to apply.</param>
    /// <returns>Validation result.</returns>
    ValidationResult ValidateParameter(object? value, string parameterName, IEnumerable<Attributes.ParameterValidationAttribute> validationAttributes);
}

/// <summary>
/// Result of parameter validation.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Gets whether the validation passed.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public List<ValidationError> Errors { get; init; } = new();

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static ValidationResult Failure(params ValidationError[] errors) => new() 
    { 
        IsValid = false, 
        Errors = errors.ToList() 
    };

    /// <summary>
    /// Creates a failed validation result with a single error.
    /// </summary>
    public static ValidationResult Failure(string parameterName, string errorMessage) => new()
    {
        IsValid = false,
        Errors = new List<ValidationError> { new(parameterName, errorMessage) }
    };
}

/// <summary>
/// Represents a validation error.
/// </summary>
public record ValidationError(string ParameterName, string ErrorMessage)
{
    /// <summary>
    /// Gets the full error message including parameter name.
    /// </summary>
    public string FullMessage => $"{ParameterName}: {ErrorMessage}";
}