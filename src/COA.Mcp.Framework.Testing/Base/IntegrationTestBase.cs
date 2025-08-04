using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace COA.Mcp.Framework.Testing.Base
{
    /// <summary>
    /// Base class for integration tests that require a full host environment.
    /// </summary>
    public abstract class IntegrationTestBase : McpTestBase
    {
        /// <summary>
        /// Gets the test host.
        /// </summary>
        protected IHost Host { get; private set; } = null!;

        /// <summary>
        /// Gets the configuration root.
        /// </summary>
        protected IConfiguration Configuration { get; private set; } = null!;

        /// <summary>
        /// Gets the host cancellation token source.
        /// </summary>
        protected CancellationTokenSource HostCancellationTokenSource { get; private set; } = null!;

        /// <summary>
        /// Setup method that creates the host.
        /// </summary>
        public override void SetUp()
        {
            HostCancellationTokenSource = new CancellationTokenSource();

            var hostBuilder = new HostBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    // Add test configuration
                    var testConfig = GetTestConfiguration();
                    if (testConfig.Count > 0)
                    {
                        config.AddInMemoryCollection(testConfig);
                    }

                    // Allow derived classes to configure
                    ConfigureAppConfiguration(context, config);
                })
                .ConfigureServices((context, services) =>
                {
                    Configuration = context.Configuration;
                    Services = services;

                    // Configure logging
                    services.AddLogging(logging =>
                    {
                        logging.ClearProviders();
                        logging.SetMinimumLevel(LogLevel.Debug);
                        ConfigureLogging(logging);
                    });

                    // Allow base class to configure services
                    base.ConfigureServices(services);

                    // Allow derived class to configure services
                    ConfigureHostServices(context, services);
                })
                .ConfigureHostConfiguration(config =>
                {
                    ConfigureHostConfiguration(config);
                });

            // Allow derived classes to further configure the host
            ConfigureHost(hostBuilder);

            // Build and start the host
            Host = hostBuilder.Build();
            ServiceProvider = Host.Services;

            // Start the host
            _ = Task.Run(async () =>
            {
                try
                {
                    await Host.RunAsync(HostCancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                }
            });

            // Allow time for host to start
            Thread.Sleep(100);

            // Call base setup
            OnSetUp();
        }

        /// <summary>
        /// Teardown method that stops the host.
        /// </summary>
        public override void TearDown()
        {
            OnTearDown();

            // Stop the host
            HostCancellationTokenSource?.Cancel();

            // Wait for host to stop (with timeout)
            var stopTask = Host?.StopAsync();
            if (stopTask != null)
            {
                stopTask.Wait(TimeSpan.FromSeconds(5));
            }

            // Dispose host
            Host?.Dispose();

            // Dispose cancellation token source
            HostCancellationTokenSource?.Dispose();
        }

        /// <summary>
        /// Gets test-specific configuration values.
        /// </summary>
        /// <returns>Configuration key-value pairs.</returns>
        protected virtual Dictionary<string, string?> GetTestConfiguration()
        {
            return new Dictionary<string, string?>
            {
                ["Environment"] = "Test",
                ["Logging:LogLevel:Default"] = "Debug"
            };
        }

        /// <summary>
        /// Override to configure the application configuration.
        /// </summary>
        /// <param name="context">The host builder context.</param>
        /// <param name="config">The configuration builder.</param>
        protected virtual void ConfigureAppConfiguration(
            HostBuilderContext context, 
            IConfigurationBuilder config)
        {
            // Default implementation does nothing
        }

        /// <summary>
        /// Override to configure the host configuration.
        /// </summary>
        /// <param name="config">The configuration builder.</param>
        protected virtual void ConfigureHostConfiguration(IConfigurationBuilder config)
        {
            // Default implementation does nothing
        }

        /// <summary>
        /// Override to configure host services.
        /// </summary>
        /// <param name="context">The host builder context.</param>
        /// <param name="services">The service collection.</param>
        protected virtual void ConfigureHostServices(
            HostBuilderContext context, 
            IServiceCollection services)
        {
            // Default implementation does nothing
        }

        /// <summary>
        /// Override to configure logging.
        /// </summary>
        /// <param name="logging">The logging builder.</param>
        protected virtual void ConfigureLogging(ILoggingBuilder logging)
        {
            // Default implementation adds console logging for tests
            logging.AddConsole();
        }

        /// <summary>
        /// Override to further configure the host builder.
        /// </summary>
        /// <param name="hostBuilder">The host builder.</param>
        protected virtual void ConfigureHost(IHostBuilder hostBuilder)
        {
            // Default implementation does nothing
        }

        /// <summary>
        /// Creates a test server for MCP integration testing.
        /// </summary>
        /// <param name="configureServices">Action to configure services.</param>
        /// <returns>A test server instance.</returns>
        protected TestMcpServer CreateTestServer(Action<IServiceCollection>? configureServices = null)
        {
            var services = new ServiceCollection();
            
            // Add logging
            services.AddLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Debug);
                logging.AddConsole();
            });

            // Allow custom configuration
            configureServices?.Invoke(services);

            var serviceProvider = services.BuildServiceProvider();
            return new TestMcpServer(serviceProvider);
        }

        /// <summary>
        /// Waits for a condition to be true with timeout.
        /// </summary>
        /// <param name="condition">The condition to wait for.</param>
        /// <param name="timeout">The timeout duration.</param>
        /// <param name="pollInterval">The polling interval.</param>
        /// <returns>True if condition was met, false if timed out.</returns>
        protected async Task<bool> WaitForConditionAsync(
            Func<bool> condition,
            TimeSpan? timeout = null,
            TimeSpan? pollInterval = null)
        {
            timeout ??= TimeSpan.FromSeconds(5);
            pollInterval ??= TimeSpan.FromMilliseconds(100);

            var endTime = DateTime.UtcNow + timeout.Value;

            while (DateTime.UtcNow < endTime)
            {
                if (condition())
                {
                    return true;
                }

                await Task.Delay(pollInterval.Value);
            }

            return false;
        }
    }

    /// <summary>
    /// Represents a test MCP server for integration testing.
    /// </summary>
    public class TestMcpServer
    {
        private readonly IServiceProvider _serviceProvider;

        public TestMcpServer(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Lists available tools.
        /// </summary>
        public async Task<IEnumerable<object>> ListToolsAsync()
        {
            // This would use the actual tool registry
            await Task.CompletedTask;
            return Array.Empty<object>();
        }

        /// <summary>
        /// Calls a tool by name.
        /// </summary>
        public async Task<object> CallToolAsync(string toolName, object parameters)
        {
            // This would use the actual tool execution
            await Task.CompletedTask;
            return new { success = true };
        }
    }
}