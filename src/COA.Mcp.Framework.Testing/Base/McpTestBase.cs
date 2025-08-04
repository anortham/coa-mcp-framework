using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System;

namespace COA.Mcp.Framework.Testing.Base
{
    /// <summary>
    /// Base class for all MCP framework tests, providing common test infrastructure.
    /// </summary>
    public abstract class McpTestBase
    {
        /// <summary>
        /// Gets the service provider for the current test.
        /// </summary>
        protected IServiceProvider ServiceProvider { get; set; } = null!;

        /// <summary>
        /// Gets the service collection for test configuration.
        /// </summary>
        protected IServiceCollection Services { get; set; } = null!;

        /// <summary>
        /// Gets the mock logger factory.
        /// </summary>
        protected Mock<ILoggerFactory> LoggerFactoryMock { get; private set; } = null!;

        /// <summary>
        /// Gets the test logger.
        /// </summary>
        protected ILogger Logger { get; private set; } = null!;

        /// <summary>
        /// Setup method called before each test.
        /// </summary>
        [SetUp]
        public virtual void SetUp()
        {
            Services = new ServiceCollection();
            LoggerFactoryMock = new Mock<ILoggerFactory>();
            
            var loggerMock = new Mock<ILogger>();
            LoggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>()))
                .Returns(loggerMock.Object);
            
            Logger = loggerMock.Object;

            // Add logging to services
            Services.AddSingleton(LoggerFactoryMock.Object);
            Services.AddSingleton(Logger);

            // Allow derived classes to configure services
            ConfigureServices(Services);

            // Build service provider
            ServiceProvider = Services.BuildServiceProvider();

            // Allow derived classes to perform additional setup
            OnSetUp();
        }

        /// <summary>
        /// Teardown method called after each test.
        /// </summary>
        [TearDown]
        public virtual void TearDown()
        {
            OnTearDown();

            // Dispose service provider if it implements IDisposable
            if (ServiceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        /// <summary>
        /// Override to configure services for the test.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        protected virtual void ConfigureServices(IServiceCollection services)
        {
            // Default implementation does nothing
        }

        /// <summary>
        /// Override to perform additional setup after services are configured.
        /// </summary>
        protected virtual void OnSetUp()
        {
            // Default implementation does nothing
        }

        /// <summary>
        /// Override to perform cleanup before the service provider is disposed.
        /// </summary>
        protected virtual void OnTearDown()
        {
            // Default implementation does nothing
        }

        /// <summary>
        /// Gets a required service from the service provider.
        /// </summary>
        /// <typeparam name="TService">The type of service to get.</typeparam>
        /// <returns>The service instance.</returns>
        protected TService GetRequiredService<TService>() where TService : notnull
        {
            return ServiceProvider.GetRequiredService<TService>();
        }

        /// <summary>
        /// Gets a service from the service provider.
        /// </summary>
        /// <typeparam name="TService">The type of service to get.</typeparam>
        /// <returns>The service instance or null if not registered.</returns>
        protected TService? GetService<TService>() where TService : class
        {
            return ServiceProvider.GetService<TService>();
        }

        /// <summary>
        /// Creates a mock of the specified type and adds it to the service collection.
        /// </summary>
        /// <typeparam name="T">The type to mock.</typeparam>
        /// <returns>The mock instance.</returns>
        protected Mock<T> CreateMock<T>() where T : class
        {
            var mock = new Mock<T>();
            Services.AddSingleton(mock.Object);
            Services.AddSingleton(mock);
            return mock;
        }

        /// <summary>
        /// Asserts that an action throws the expected exception.
        /// </summary>
        /// <typeparam name="TException">The expected exception type.</typeparam>
        /// <param name="action">The action to execute.</param>
        /// <returns>The thrown exception.</returns>
        protected TException AssertThrows<TException>(Action action) where TException : Exception
        {
            return Assert.Throws<TException>(() => action());
        }

        /// <summary>
        /// Asserts that an async function throws the expected exception.
        /// </summary>
        /// <typeparam name="TException">The expected exception type.</typeparam>
        /// <param name="asyncFunc">The async function to execute.</param>
        /// <returns>The thrown exception.</returns>
        protected TException AssertThrowsAsync<TException>(AsyncTestDelegate asyncFunc) where TException : Exception
        {
            return Assert.ThrowsAsync<TException>(asyncFunc);
        }
    }
}