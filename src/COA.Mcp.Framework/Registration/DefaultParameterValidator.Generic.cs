using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using COA.Mcp.Framework.Attributes;
using COA.Mcp.Framework.Interfaces;
using Microsoft.Extensions.Logging;
using ValidationResult = COA.Mcp.Framework.Interfaces.ValidationResult;

namespace COA.Mcp.Framework.Registration;

/// <summary>
/// Generic implementation of parameter validation with strong typing.
/// </summary>
/// <typeparam name="TParams">The type of parameters to validate.</typeparam>
#pragma warning disable CS0618 // Type or member is obsolete - backward compatibility
public class DefaultParameterValidator<TParams> : IParameterValidator<TParams>, IParameterValidator
#pragma warning restore CS0618 // Type or member is obsolete
    where TParams : class
{
    private readonly ILogger? _logger;
    private readonly DefaultParameterValidator _baseValidator;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultParameterValidator{TParams}"/> class.
    /// </summary>
    public DefaultParameterValidator(ILogger? logger = null)
    {
        _logger = logger;
        _baseValidator = new DefaultParameterValidator(logger as ILogger<DefaultParameterValidator>);
    }

    /// <inheritdoc/>
    public ValidationResult Validate(TParams parameters)
    {
        // Use the base validator with the known type
        return _baseValidator.Validate(parameters, typeof(TParams));
    }

    /// <inheritdoc/>
    public ValidationResult Validate(object? parameters, Type parameterType)
    {
        // For the non-generic interface, delegate to base validator
        return _baseValidator.Validate(parameters, parameterType);
    }

    /// <inheritdoc/>
    public ValidationResult ValidateParameter(
        object? value, 
        string parameterName, 
        IEnumerable<ParameterValidationAttribute> validationAttributes)
    {
        return _baseValidator.ValidateParameter(value, parameterName, validationAttributes);
    }
}

/// <summary>
/// Extension methods for parameter validation.
/// </summary>
public static class ParameterValidatorExtensions
{
    /// <summary>
    /// Creates a generic validator for a specific parameter type.
    /// </summary>
    /// <typeparam name="TParams">The parameter type.</typeparam>
    /// <param name="validator">The non-generic validator.</param>
    /// <returns>A generic validator instance.</returns>
#pragma warning disable CS0618 // Type or member is obsolete - backward compatibility
    public static IParameterValidator<TParams> ForType<TParams>(this IParameterValidator validator)
#pragma warning restore CS0618 // Type or member is obsolete
        where TParams : class
    {
        if (validator is IParameterValidator<TParams> typedValidator)
        {
            return typedValidator;
        }

        // Wrap the non-generic validator in a generic adapter
        return new ParameterValidatorAdapter<TParams>(validator);
    }

    /// <summary>
    /// Adapter to wrap non-generic validators for generic use.
    /// </summary>
    private class ParameterValidatorAdapter<TParams> : IParameterValidator<TParams>
        where TParams : class
    {
#pragma warning disable CS0618 // Type or member is obsolete - backward compatibility
        private readonly IParameterValidator _innerValidator;

        public ParameterValidatorAdapter(IParameterValidator innerValidator)
#pragma warning restore CS0618 // Type or member is obsolete
        {
            _innerValidator = innerValidator ?? throw new ArgumentNullException(nameof(innerValidator));
        }

        public ValidationResult Validate(TParams parameters)
        {
            return _innerValidator.Validate(parameters, typeof(TParams));
        }

        public ValidationResult ValidateParameter(
            object? value, 
            string parameterName, 
            IEnumerable<ParameterValidationAttribute> validationAttributes)
        {
            return _innerValidator.ValidateParameter(value, parameterName, validationAttributes);
        }
    }
}