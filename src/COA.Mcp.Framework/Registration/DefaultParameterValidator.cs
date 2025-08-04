using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using COA.Mcp.Framework.Attributes;
using COA.Mcp.Framework.Interfaces;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.Registration;

/// <summary>
/// Default implementation of parameter validation.
/// </summary>
public class DefaultParameterValidator : IParameterValidator
{
    private readonly ILogger<DefaultParameterValidator>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultParameterValidator"/> class.
    /// </summary>
    public DefaultParameterValidator(ILogger<DefaultParameterValidator>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Interfaces.ValidationResult Validate(object? parameters, Type parameterType)
    {
        if (parameterType == null)
            throw new ArgumentNullException(nameof(parameterType));

        // Null parameters
        if (parameters == null)
        {
            // Check if the type has any required properties
            var requiredProps = GetRequiredProperties(parameterType);
            if (requiredProps.Any())
            {
                var errors = requiredProps
                    .Select(p => new ValidationError(p.Name, $"Parameter '{p.Name}' is required."))
                    .ToArray();
                return Interfaces.ValidationResult.Failure(errors);
            }
            return Interfaces.ValidationResult.Success();
        }

        // Type mismatch
        if (!parameterType.IsInstanceOfType(parameters))
        {
            return Interfaces.ValidationResult.Failure("parameters", 
                $"Expected parameter type '{parameterType.Name}' but got '{parameters.GetType().Name}'.");
        }

        // Validate properties
        var allErrors = new List<ValidationError>();
        var properties = parameterType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            var value = property.GetValue(parameters);
            var validationAttributes = property.GetCustomAttributes<ParameterValidationAttribute>().ToList();
            
            // Add standard validation attributes
            AddStandardValidationAttributes(property, validationAttributes);

            var result = ValidateParameter(value, property.Name, validationAttributes);
            if (!result.IsValid)
            {
                allErrors.AddRange(result.Errors);
            }
        }

        return allErrors.Any() 
            ? Interfaces.ValidationResult.Failure(allErrors.ToArray()) 
            : Interfaces.ValidationResult.Success();
    }

    /// <inheritdoc/>
    public Interfaces.ValidationResult ValidateParameter(
        object? value, 
        string parameterName, 
        IEnumerable<ParameterValidationAttribute> validationAttributes)
    {
        var errors = new List<ValidationError>();

        foreach (var attribute in validationAttributes)
        {
            try
            {
                if (!attribute.IsValid(value, parameterName))
                {
                    errors.Add(new ValidationError(parameterName, attribute.GetErrorMessage(parameterName)));
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error validating parameter {ParameterName} with {AttributeType}",
                    parameterName, attribute.GetType().Name);
                errors.Add(new ValidationError(parameterName, 
                    $"Validation error: {ex.Message}"));
            }
        }

        return errors.Any() 
            ? Interfaces.ValidationResult.Failure(errors.ToArray()) 
            : Interfaces.ValidationResult.Success();
    }

    private static IEnumerable<PropertyInfo> GetRequiredProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<Attributes.RequiredAttribute>() != null ||
                       (p.PropertyType.IsValueType && Nullable.GetUnderlyingType(p.PropertyType) == null));
    }

    private static void AddStandardValidationAttributes(
        PropertyInfo property, 
        List<ParameterValidationAttribute> validationAttributes)
    {
        // Convert System.ComponentModel.DataAnnotations attributes
        var standardRequired = property.GetCustomAttribute<System.ComponentModel.DataAnnotations.RequiredAttribute>();
        if (standardRequired != null && !validationAttributes.OfType<Attributes.RequiredAttribute>().Any())
        {
            validationAttributes.Add(new Attributes.RequiredAttribute 
            { 
                ErrorMessage = standardRequired.ErrorMessage 
            });
        }

        var standardRange = property.GetCustomAttribute<System.ComponentModel.DataAnnotations.RangeAttribute>();
        if (standardRange != null && !validationAttributes.OfType<Attributes.RangeAttribute>().Any())
        {
            validationAttributes.Add(new Attributes.RangeAttribute(
                Convert.ToDouble(standardRange.Minimum), 
                Convert.ToDouble(standardRange.Maximum))
            { 
                ErrorMessage = standardRange.ErrorMessage 
            });
        }

        var standardLength = property.GetCustomAttribute<System.ComponentModel.DataAnnotations.StringLengthAttribute>();
        if (standardLength != null && !validationAttributes.OfType<Attributes.StringLengthAttribute>().Any())
        {
            validationAttributes.Add(new Attributes.StringLengthAttribute(
                standardLength.MinimumLength, 
                standardLength.MaximumLength)
            { 
                ErrorMessage = standardLength.ErrorMessage 
            });
        }
    }
}