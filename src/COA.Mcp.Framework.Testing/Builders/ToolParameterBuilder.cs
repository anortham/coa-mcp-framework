using System;
using System.Collections.Generic;
using System.Reflection;

namespace COA.Mcp.Framework.Testing.Builders
{
    /// <summary>
    /// Builder for creating tool parameters for testing.
    /// </summary>
    /// <typeparam name="TParameters">The type of parameters to build.</typeparam>
    public class ToolParameterBuilder<TParameters> where TParameters : class, new()
    {
        private readonly TParameters _parameters;
        private readonly Dictionary<string, object?> _propertyValues;

        /// <summary>
        /// Initializes a new instance of the <see cref="ToolParameterBuilder{TParameters}"/> class.
        /// </summary>
        public ToolParameterBuilder()
        {
            _parameters = new TParameters();
            _propertyValues = new Dictionary<string, object?>();
        }

        /// <summary>
        /// Sets a property value using a property expression.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>The builder for chaining.</returns>
        public ToolParameterBuilder<TParameters> With(string propertyName, object? value)
        {
            _propertyValues[propertyName] = value;
            return this;
        }

        /// <summary>
        /// Sets multiple property values from a dictionary.
        /// </summary>
        /// <param name="values">The property values to set.</param>
        /// <returns>The builder for chaining.</returns>
        public ToolParameterBuilder<TParameters> WithValues(Dictionary<string, object?> values)
        {
            foreach (var kvp in values)
            {
                _propertyValues[kvp.Key] = kvp.Value;
            }
            return this;
        }

        /// <summary>
        /// Sets default values for all properties.
        /// </summary>
        /// <returns>The builder for chaining.</returns>
        public ToolParameterBuilder<TParameters> WithDefaults()
        {
            var type = typeof(TParameters);
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                if (!property.CanWrite) continue;

                var defaultValue = GetDefaultValueForType(property.PropertyType);
                _propertyValues[property.Name] = defaultValue;
            }

            return this;
        }

        /// <summary>
        /// Sets random values for all properties.
        /// </summary>
        /// <param name="random">Optional random instance for consistent seeding.</param>
        /// <returns>The builder for chaining.</returns>
        public ToolParameterBuilder<TParameters> WithRandomValues(Random? random = null)
        {
            random ??= new Random();
            var type = typeof(TParameters);
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                if (!property.CanWrite) continue;

                var randomValue = GetRandomValueForType(property.PropertyType, random);
                _propertyValues[property.Name] = randomValue;
            }

            return this;
        }

        /// <summary>
        /// Builds the parameter object with the configured values.
        /// </summary>
        /// <returns>The built parameter object.</returns>
        public TParameters Build()
        {
            var type = typeof(TParameters);

            foreach (var kvp in _propertyValues)
            {
                var property = type.GetProperty(kvp.Key, BindingFlags.Public | BindingFlags.Instance);
                if (property != null && property.CanWrite)
                {
                    try
                    {
                        property.SetValue(_parameters, kvp.Value);
                    }
                    catch (ArgumentException)
                    {
                        // Type mismatch - try to convert
                        if (kvp.Value != null)
                        {
                            var convertedValue = Convert.ChangeType(kvp.Value, property.PropertyType);
                            property.SetValue(_parameters, convertedValue);
                        }
                    }
                }
            }

            return _parameters;
        }

        /// <summary>
        /// Gets default value for a type.
        /// </summary>
        private static object? GetDefaultValueForType(Type type)
        {
            if (type == typeof(string))
                return "default";
            if (type == typeof(int) || type == typeof(int?))
                return 0;
            if (type == typeof(bool) || type == typeof(bool?))
                return false;
            if (type == typeof(DateTime) || type == typeof(DateTime?))
                return DateTime.UtcNow;
            if (type == typeof(double) || type == typeof(double?))
                return 0.0;
            if (type == typeof(decimal) || type == typeof(decimal?))
                return 0m;
            if (type.IsArray)
                return Array.CreateInstance(type.GetElementType()!, 0);
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                return Activator.CreateInstance(type);

            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        /// <summary>
        /// Gets random value for a type.
        /// </summary>
        private static object? GetRandomValueForType(Type type, Random random)
        {
            if (type == typeof(string))
                return $"random_{random.Next(1000, 9999)}";
            if (type == typeof(int) || type == typeof(int?))
                return random.Next(1, 100);
            if (type == typeof(bool) || type == typeof(bool?))
                return random.Next(2) == 1;
            if (type == typeof(DateTime) || type == typeof(DateTime?))
                return DateTime.UtcNow.AddDays(random.Next(-365, 365));
            if (type == typeof(double) || type == typeof(double?))
                return random.NextDouble() * 100;
            if (type == typeof(decimal) || type == typeof(decimal?))
                return (decimal)(random.NextDouble() * 100);
            
            return GetDefaultValueForType(type);
        }
    }

    /// <summary>
    /// Static factory methods for common parameter builders.
    /// </summary>
    public static class ToolParameterBuilder
    {
        /// <summary>
        /// Creates a new parameter builder for the specified type.
        /// </summary>
        /// <typeparam name="TParameters">The parameter type.</typeparam>
        /// <returns>A new parameter builder.</returns>
        public static ToolParameterBuilder<TParameters> Create<TParameters>() where TParameters : class, new()
        {
            return new ToolParameterBuilder<TParameters>();
        }

        /// <summary>
        /// Creates a parameter builder with default values.
        /// </summary>
        /// <typeparam name="TParameters">The parameter type.</typeparam>
        /// <returns>A parameter builder with defaults.</returns>
        public static ToolParameterBuilder<TParameters> CreateWithDefaults<TParameters>() where TParameters : class, new()
        {
            return new ToolParameterBuilder<TParameters>().WithDefaults();
        }

        /// <summary>
        /// Creates a parameter builder with random values.
        /// </summary>
        /// <typeparam name="TParameters">The parameter type.</typeparam>
        /// <param name="seed">Optional random seed.</param>
        /// <returns>A parameter builder with random values.</returns>
        public static ToolParameterBuilder<TParameters> CreateWithRandomValues<TParameters>(int? seed = null) 
            where TParameters : class, new()
        {
            var random = seed.HasValue ? new Random(seed.Value) : new Random();
            return new ToolParameterBuilder<TParameters>().WithRandomValues(random);
        }
    }

    /// <summary>
    /// Extension methods for parameter builder convenience.
    /// </summary>
    public static class ToolParameterBuilderExtensions
    {
        /// <summary>
        /// Adds a string property with a specific naming pattern.
        /// </summary>
        public static ToolParameterBuilder<T> WithLocation<T>(this ToolParameterBuilder<T> builder, string location) 
            where T : class, new()
        {
            return builder.With("Location", location);
        }

        /// <summary>
        /// Adds a query property.
        /// </summary>
        public static ToolParameterBuilder<T> WithQuery<T>(this ToolParameterBuilder<T> builder, string query) 
            where T : class, new()
        {
            return builder.With("Query", query);
        }

        /// <summary>
        /// Adds a file path property.
        /// </summary>
        public static ToolParameterBuilder<T> WithFilePath<T>(this ToolParameterBuilder<T> builder, string filePath) 
            where T : class, new()
        {
            return builder.With("FilePath", filePath);
        }

        /// <summary>
        /// Adds a max results property.
        /// </summary>
        public static ToolParameterBuilder<T> WithMaxResults<T>(this ToolParameterBuilder<T> builder, int maxResults) 
            where T : class, new()
        {
            return builder.With("MaxResults", maxResults);
        }

        /// <summary>
        /// Adds an include/exclude pattern.
        /// </summary>
        public static ToolParameterBuilder<T> WithPattern<T>(this ToolParameterBuilder<T> builder, string pattern, bool include = true) 
            where T : class, new()
        {
            var propertyName = include ? "IncludePattern" : "ExcludePattern";
            return builder.With(propertyName, pattern);
        }
    }
}