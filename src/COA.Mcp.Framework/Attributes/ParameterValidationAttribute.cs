using System;

namespace COA.Mcp.Framework.Attributes;

/// <summary>
/// Base class for parameter validation attributes.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
public abstract class ParameterValidationAttribute : Attribute
{
    /// <summary>
    /// Error message to display when validation fails.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Validates the parameter value.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <returns>True if validation passes; otherwise, false.</returns>
    public abstract bool IsValid(object? value, string parameterName);

    /// <summary>
    /// Gets the validation error message.
    /// </summary>
    /// <param name="parameterName">The name of the parameter that failed validation.</param>
    /// <returns>The error message.</returns>
    public virtual string GetErrorMessage(string parameterName)
    {
        return ErrorMessage ?? $"Validation failed for parameter '{parameterName}'.";
    }
}

/// <summary>
/// Validates that a parameter is not null or empty.
/// </summary>
public sealed class RequiredAttribute : ParameterValidationAttribute
{
    public override bool IsValid(object? value, string parameterName)
    {
        return value switch
        {
            null => false,
            string str => !string.IsNullOrWhiteSpace(str),
            _ => true
        };
    }

    public override string GetErrorMessage(string parameterName)
    {
        return ErrorMessage ?? $"Parameter '{parameterName}' is required.";
    }
}

/// <summary>
/// Validates that a numeric parameter falls within a specified range.
/// </summary>
public sealed class RangeAttribute : ParameterValidationAttribute
{
    public double Minimum { get; }
    public double Maximum { get; }

    public RangeAttribute(double minimum, double maximum)
    {
        if (minimum > maximum)
        {
            throw new ArgumentException("Minimum cannot be greater than maximum.");
        }

        Minimum = minimum;
        Maximum = maximum;
    }

    public override bool IsValid(object? value, string parameterName)
    {
        if (value == null) return true; // Let RequiredAttribute handle null

        return value switch
        {
            int i => i >= Minimum && i <= Maximum,
            long l => l >= Minimum && l <= Maximum,
            float f => f >= Minimum && f <= Maximum,
            double d => d >= Minimum && d <= Maximum,
            decimal dec => (double)dec >= Minimum && (double)dec <= Maximum,
            _ => false
        };
    }

    public override string GetErrorMessage(string parameterName)
    {
        return ErrorMessage ?? $"Parameter '{parameterName}' must be between {Minimum} and {Maximum}.";
    }
}

/// <summary>
/// Validates string length constraints.
/// </summary>
public sealed class StringLengthAttribute : ParameterValidationAttribute
{
    public int MinimumLength { get; }
    public int MaximumLength { get; }

    public StringLengthAttribute(int maximumLength) : this(0, maximumLength)
    {
    }

    public StringLengthAttribute(int minimumLength, int maximumLength)
    {
        if (minimumLength < 0)
            throw new ArgumentException("Minimum length cannot be negative.");
        if (maximumLength < minimumLength)
            throw new ArgumentException("Maximum length cannot be less than minimum length.");

        MinimumLength = minimumLength;
        MaximumLength = maximumLength;
    }

    public override bool IsValid(object? value, string parameterName)
    {
        if (value == null) return true; // Let RequiredAttribute handle null

        if (value is string str)
        {
            return str.Length >= MinimumLength && str.Length <= MaximumLength;
        }

        return false;
    }

    public override string GetErrorMessage(string parameterName)
    {
        if (MinimumLength > 0)
        {
            return ErrorMessage ?? $"Parameter '{parameterName}' must be between {MinimumLength} and {MaximumLength} characters.";
        }
        return ErrorMessage ?? $"Parameter '{parameterName}' must not exceed {MaximumLength} characters.";
    }
}