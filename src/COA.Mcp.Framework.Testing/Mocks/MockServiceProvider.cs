using System;
using System.Collections.Generic;

namespace COA.Mcp.Framework.Testing.Mocks
{
    /// <summary>
    /// Mock service provider for testing dependency injection scenarios.
    /// </summary>
    public class MockServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services = new();
        private readonly Dictionary<Type, Func<object>> _factories = new();

        /// <summary>
        /// Gets the list of service types that were requested.
        /// </summary>
        public List<Type> RequestedServices { get; } = new();

        /// <summary>
        /// Gets or sets whether to throw when a service is not found.
        /// </summary>
        public bool ThrowOnNotFound { get; set; }

        /// <summary>
        /// Registers a service instance.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <param name="instance">The service instance.</param>
        public void RegisterService<TService>(TService instance) where TService : class
        {
            _services[typeof(TService)] = instance;
        }

        /// <summary>
        /// Registers a service instance by type.
        /// </summary>
        /// <param name="serviceType">The service type.</param>
        /// <param name="instance">The service instance.</param>
        public void RegisterService(Type serviceType, object instance)
        {
            _services[serviceType] = instance ?? throw new ArgumentNullException(nameof(instance));
        }

        /// <summary>
        /// Registers a service factory.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <param name="factory">The factory function.</param>
        public void RegisterFactory<TService>(Func<TService> factory) where TService : class
        {
            _factories[typeof(TService)] = () => factory();
        }

        /// <summary>
        /// Registers a service factory by type.
        /// </summary>
        /// <param name="serviceType">The service type.</param>
        /// <param name="factory">The factory function.</param>
        public void RegisterFactory(Type serviceType, Func<object> factory)
        {
            _factories[serviceType] = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <inheritdoc/>
        public object? GetService(Type serviceType)
        {
            RequestedServices.Add(serviceType);

            // Check for direct registration
            if (_services.TryGetValue(serviceType, out var service))
            {
                return service;
            }

            // Check for factory registration
            if (_factories.TryGetValue(serviceType, out var factory))
            {
                return factory();
            }

            // Check for generic type definitions
            if (serviceType.IsGenericType)
            {
                var genericDef = serviceType.GetGenericTypeDefinition();
                
                // Check if we have a registration for the generic definition
                if (_services.TryGetValue(genericDef, out service))
                {
                    return service;
                }

                if (_factories.TryGetValue(genericDef, out factory))
                {
                    return factory();
                }
            }

            if (ThrowOnNotFound)
            {
                throw new InvalidOperationException($"Service of type '{serviceType.FullName}' not registered.");
            }

            return null;
        }

        /// <summary>
        /// Gets a service of the specified type.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <returns>The service instance or null if not found.</returns>
        public TService? GetService<TService>() where TService : class
        {
            return GetService(typeof(TService)) as TService;
        }

        /// <summary>
        /// Gets a required service of the specified type.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <returns>The service instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown when service is not found.</exception>
        public TService GetRequiredService<TService>() where TService : class
        {
            var service = GetService<TService>();
            if (service == null)
            {
                throw new InvalidOperationException($"Service of type '{typeof(TService).FullName}' not registered.");
            }
            return service;
        }

        /// <summary>
        /// Clears all registered services.
        /// </summary>
        public void Clear()
        {
            _services.Clear();
            _factories.Clear();
            RequestedServices.Clear();
        }

        /// <summary>
        /// Checks if a service is registered.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <returns>True if registered; otherwise, false.</returns>
        public bool IsRegistered<TService>()
        {
            return IsRegistered(typeof(TService));
        }

        /// <summary>
        /// Checks if a service is registered.
        /// </summary>
        /// <param name="serviceType">The service type.</param>
        /// <returns>True if registered; otherwise, false.</returns>
        public bool IsRegistered(Type serviceType)
        {
            return _services.ContainsKey(serviceType) || _factories.ContainsKey(serviceType);
        }

        /// <summary>
        /// Creates a child scope with isolated services.
        /// </summary>
        /// <returns>A new mock service provider.</returns>
        public MockServiceProvider CreateScope()
        {
            var scopedProvider = new MockServiceProvider
            {
                ThrowOnNotFound = ThrowOnNotFound
            };

            // Copy services to the scoped provider
            foreach (var kvp in _services)
            {
                scopedProvider._services[kvp.Key] = kvp.Value;
            }

            foreach (var kvp in _factories)
            {
                scopedProvider._factories[kvp.Key] = kvp.Value;
            }

            return scopedProvider;
        }
    }
}