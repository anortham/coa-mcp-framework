using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using COA.Mcp.Framework.Configuration;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Pipeline;
using COA.Mcp.Framework.Pipeline.Middleware;
using COA.Mcp.Framework.Pipeline.SimpleMiddleware;
using COA.Mcp.Framework.Prompts;
using COA.Mcp.Framework.Registration;
using COA.Mcp.Framework.Resources;
using COA.Mcp.Framework.Server.Services;
using COA.Mcp.Framework.Transport;
using COA.Mcp.Framework.Transport.Configuration;
using COA.Mcp.Protocol;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace COA.Mcp.Framework.Server;

/// <summary>
/// Builder for configuring and creating an MCP server with fluent API.
/// </summary>
public class McpServerBuilder
{
    private readonly IServiceCollection _services;
    private readonly ServerConfiguration _configuration;
    private Action<McpToolRegistry>? _toolConfiguration;
    private Action<ResourceRegistry>? _resourceConfiguration;
    private Action<IPromptRegistry>? _promptConfiguration;
    private Assembly? _toolAssembly;
    private Assembly? _promptAssembly;
    private IMcpTransport? _transport;

    /// <summary>
    /// Initializes a new instance of the McpServerBuilder class.
    /// </summary>
    public McpServerBuilder()
    {
        _services = new ServiceCollection();
        _configuration = new ServerConfiguration();
        
        // Add default services
        ConfigureDefaultServices();
    }

    /// <summary>
    /// Gets the service collection for advanced configuration.
    /// </summary>
    public IServiceCollection Services => _services;

    /// <summary>
    /// Sets the server information.
    /// </summary>
    /// <param name="name">The server name.</param>
    /// <param name="version">The server version.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder WithServerInfo(string name, string version)
    {
        _configuration.ServerName = name;
        _configuration.ServerVersion = version;
        return this;
    }

    /// <summary>
    /// Configures the server to use stdio transport (default).
    /// </summary>
    /// <param name="configure">Optional action to configure stdio options.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder UseStdioTransport(Action<StdioTransportOptions>? configure = null)
    {
        var options = new StdioTransportOptions();
        configure?.Invoke(options);
        
        var logger = _services.BuildServiceProvider().GetService<ILogger<StdioTransport>>();
        _transport = new StdioTransport(options.Input, options.Output, logger);
        _services.AddSingleton(_transport);
        
        return this;
    }
    
    /// <summary>
    /// Configures the server to use HTTP transport.
    /// </summary>
    /// <param name="configure">Action to configure HTTP options.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder UseHttpTransport(Action<HttpTransportOptions> configure)
    {
        var options = new HttpTransportOptions();
        configure?.Invoke(options);
        
        var logger = _services.BuildServiceProvider().GetService<ILogger<HttpTransport>>();
        _transport = new HttpTransport(options, logger);
        _services.AddSingleton(_transport);
        _services.AddSingleton(options);
        
        return this;
    }
    
    /// <summary>
    /// Configures the server to use WebSocket transport for bidirectional real-time communication.
    /// </summary>
    /// <param name="configure">Action to configure WebSocket options.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder UseWebSocketTransport(Action<HttpTransportOptions> configure)
    {
        var options = new HttpTransportOptions();
        configure?.Invoke(options);
        
        // Ensure WebSocket is enabled in the options
        options.EnableWebSocket = true;
        
        var logger = _services.BuildServiceProvider().GetService<ILogger<WebSocketTransport>>();
        _transport = new WebSocketTransport(options, logger);
        _services.AddSingleton(_transport);
        _services.AddSingleton(options);
        
        return this;
    }
    
    /// <summary>
    /// Configures the server to use a custom transport.
    /// </summary>
    /// <param name="transport">The transport implementation to use.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder UseTransport(IMcpTransport transport)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _services.AddSingleton(_transport);
        return this;
    }

    /// <summary>
    /// Configures tools for the server.
    /// </summary>
    /// <param name="configure">Action to configure the tool registry.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder ConfigureTools(Action<McpToolRegistry> configure)
    {
        _toolConfiguration += configure;
        return this;
    }

    /// <summary>
    /// Discovers and registers tools from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan for tools (defaults to calling assembly).</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder DiscoverTools(Assembly? assembly = null)
    {
        _toolAssembly = assembly ?? Assembly.GetCallingAssembly();
        return this;
    }

    /// <summary>
    /// Registers a specific tool instance.
    /// </summary>
    /// <typeparam name="TParams">The type of the tool's parameters.</typeparam>
    /// <typeparam name="TResult">The type of the tool's result.</typeparam>
    /// <param name="tool">The tool instance to register.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder RegisterTool<TParams, TResult>(IMcpTool<TParams, TResult> tool)
        where TParams : class
    {
        _services.AddSingleton(tool);
        _toolConfiguration += registry => registry.RegisterTool(tool);
        return this;
    }

    /// <summary>
    /// Registers a tool type for dependency injection.
    /// </summary>
    /// <typeparam name="TTool">The tool type to register.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder RegisterToolType<TTool>()
        where TTool : class, IMcpTool
    {
        _services.AddScoped<TTool>();
        return this;
    }

    /// <summary>
    /// Configures resources for the server.
    /// </summary>
    /// <param name="configure">Action to configure the resource registry.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder ConfigureResources(Action<ResourceRegistry> configure)
    {
        _resourceConfiguration += configure;
        return this;
    }

    /// <summary>
    /// Configures prompts for the server.
    /// </summary>
    /// <param name="configure">Action to configure the prompt registry.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder ConfigurePrompts(Action<IPromptRegistry> configure)
    {
        _promptConfiguration += configure;
        return this;
    }

    /// <summary>
    /// Discovers and registers prompts from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan for prompts (defaults to calling assembly).</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder DiscoverPrompts(Assembly? assembly = null)
    {
        _promptAssembly = assembly ?? Assembly.GetCallingAssembly();
        return this;
    }

    /// <summary>
    /// Registers a specific prompt instance.
    /// </summary>
    /// <param name="prompt">The prompt instance to register.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder RegisterPrompt(IPrompt prompt)
    {
        _services.AddSingleton(prompt);
        _promptConfiguration += registry => registry.RegisterPrompt(prompt);
        return this;
    }

    /// <summary>
    /// Registers a prompt type for dependency injection.
    /// </summary>
    /// <typeparam name="TPrompt">The prompt type to register.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder RegisterPromptType<TPrompt>()
        where TPrompt : class, IPrompt
    {
        _services.AddScoped<TPrompt>();
        _promptConfiguration += registry => registry.RegisterPromptType<TPrompt>();
        return this;
    }

    /// <summary>
    /// Configures logging for the server.
    /// </summary>
    /// <param name="configure">Action to configure logging.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder ConfigureLogging(Action<ILoggingBuilder> configure)
    {
        _services.AddLogging(configure);
        return this;
    }

    /// <summary>
    /// Adds a custom service to the container.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <typeparam name="TImplementation">The implementation type.</typeparam>
    /// <param name="lifetime">The service lifetime.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder AddService<TService, TImplementation>(ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TService : class
        where TImplementation : class, TService
    {
        _services.Add(new ServiceDescriptor(typeof(TService), typeof(TImplementation), lifetime));
        return this;
    }

    /// <summary>
    /// Adds a singleton service to the container.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <param name="instance">The singleton instance.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder AddSingleton<TService>(TService instance)
        where TService : class
    {
        _services.AddSingleton(instance);
        return this;
    }

    /// <summary>
    /// Configures token budgets for tools.
    /// </summary>
    /// <param name="configure">Action to configure token budgets.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder ConfigureTokenBudgets(Action<TokenBudgetRegistry> configure)
    {
        var registry = new TokenBudgetRegistry();
        configure(registry);
        _services.AddSingleton(registry);
        return this;
    }

    #region Global Middleware Configuration

    /// <summary>
    /// Registers pre-configured global middleware instances that will be applied to all tools.
    /// Middleware is executed in the order specified by the Order property.
    /// </summary>
    /// <param name="middleware">The middleware instances to register.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder WithGlobalMiddleware(params ISimpleMiddleware[] middleware)
    {
        return WithGlobalMiddleware((IEnumerable<ISimpleMiddleware>)middleware);
    }

    /// <summary>
    /// Registers pre-configured global middleware instances that will be applied to all tools.
    /// Middleware is executed in the order specified by the Order property.
    /// </summary>
    /// <param name="middleware">The middleware instances to register.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder WithGlobalMiddleware(IEnumerable<ISimpleMiddleware> middleware)
    {
        foreach (var m in middleware)
        {
            _services.AddSingleton<ISimpleMiddleware>(m);
        }
        return this;
    }

    /// <summary>
    /// Registers a middleware type for dependency injection that will be applied to all tools.
    /// The middleware will be resolved from the DI container and applied globally.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type to register.</typeparam>
    /// <param name="lifetime">The service lifetime (default: Singleton for consistent behavior).</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder AddGlobalMiddleware<TMiddleware>(ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TMiddleware : class, ISimpleMiddleware
    {
        _services.Add(new ServiceDescriptor(typeof(ISimpleMiddleware), typeof(TMiddleware), lifetime));
        return this;
    }

    /// <summary>
    /// Registers a global middleware with a factory function.
    /// Useful for middleware that needs complex configuration or dependencies.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type to register.</typeparam>
    /// <param name="factory">Factory function to create the middleware.</param>
    /// <param name="lifetime">The service lifetime (default: Singleton).</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder AddGlobalMiddleware<TMiddleware>(
        Func<IServiceProvider, TMiddleware> factory, 
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TMiddleware : class, ISimpleMiddleware
    {
        _services.Add(new ServiceDescriptor(typeof(ISimpleMiddleware), factory, lifetime));
        return this;
    }

    /// <summary>
    /// Convenience method to add the built-in TypeVerificationMiddleware.
    /// </summary>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder AddTypeVerificationMiddleware(Action<TypeVerificationOptions>? configure = null)
    {
        if (configure != null)
        {
            _services.Configure(configure);
        }
        
        return AddGlobalMiddleware<TypeVerificationMiddleware>();
    }

    /// <summary>
    /// Convenience method to add the built-in TddEnforcementMiddleware.
    /// </summary>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder AddTddEnforcementMiddleware(Action<TddEnforcementOptions>? configure = null)
    {
        if (configure != null)
        {
            _services.Configure(configure);
        }
        
        return AddGlobalMiddleware<TddEnforcementMiddleware>();
    }

    /// <summary>
    /// Convenience method to add the built-in LoggingSimpleMiddleware.
    /// </summary>
    /// <param name="logLevel">The log level for middleware operations (default: Information).</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder AddLoggingMiddleware(LogLevel logLevel = LogLevel.Information)
    {
        return AddGlobalMiddleware<LoggingSimpleMiddleware>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<LoggingSimpleMiddleware>>();
            return new LoggingSimpleMiddleware(logger, logLevel);
        });
    }

    /// <summary>
    /// Convenience method to add the built-in TokenCountingSimpleMiddleware.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder AddTokenCountingMiddleware()
    {
        return AddGlobalMiddleware<TokenCountingSimpleMiddleware>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<TokenCountingSimpleMiddleware>>();
            return new TokenCountingSimpleMiddleware(logger);
        });
    }

    #endregion

    /// <summary>
    /// Configures an auto-started service that runs alongside the MCP server.
    /// </summary>
    /// <param name="configure">Action to configure the service.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder UseAutoService(Action<ServiceConfiguration> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        var config = new ServiceConfiguration();
        configure(config);

        // Add ServiceManager if not already registered
        if (!_services.Any(sd => sd.ServiceType == typeof(IServiceManager)))
        {
            _services.AddSingleton<IServiceManager, ServiceManager>();
            _services.AddHostedService<ServiceManager>(provider => 
                (ServiceManager)provider.GetRequiredService<IServiceManager>());
        }

        // Add ServiceLifecycleHost for this specific service
        _services.AddHostedService(provider =>
        {
            var serviceManager = provider.GetRequiredService<IServiceManager>();
            var logger = provider.GetService<ILogger<ServiceLifecycleHost>>();
            return new ServiceLifecycleHost(serviceManager, config, logger);
        });

        return this;
    }

    /// <summary>
    /// Configures multiple auto-started services.
    /// </summary>
    /// <param name="configurations">The service configurations.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder UseAutoServices(params Action<ServiceConfiguration>[] configurations)
    {
        foreach (var configure in configurations)
        {
            UseAutoService(configure);
        }
        return this;
    }

    /// <summary>
    /// Builds the MCP server with the configured options.
    /// </summary>
    /// <returns>The configured MCP server ready to run.</returns>
    public McpServer Build()
    {
        // If no transport specified, use stdio by default
        if (_transport == null)
        {
            UseStdioTransport();
        }
        
        var serviceProvider = _services.BuildServiceProvider();
        return BuildWithProvider(serviceProvider);
    }
    
    private McpServer BuildWithProvider(IServiceProvider serviceProvider)
    {
        // Get registries
        var toolRegistry = serviceProvider.GetRequiredService<McpToolRegistry>();
        var resourceRegistry = serviceProvider.GetRequiredService<ResourceRegistry>();
        var promptRegistry = serviceProvider.GetRequiredService<IPromptRegistry>();
        
        // Configure tools
        if (_toolAssembly != null)
        {
            toolRegistry.DiscoverAndRegisterTools(_toolAssembly);
        }
        
        _toolConfiguration?.Invoke(toolRegistry);
        
        // Configure resources
        _resourceConfiguration?.Invoke(resourceRegistry);
        
        // Configure prompts
        if (_promptAssembly != null)
        {
            DiscoverAndRegisterPrompts(promptRegistry, _promptAssembly);
        }
        
        _promptConfiguration?.Invoke(promptRegistry);
        
        // Create server
        var serverInfo = new Implementation
        {
            Name = _configuration.ServerName,
            Version = _configuration.ServerVersion
        };
        
        var logger = serviceProvider.GetService<ILogger<McpServer>>();
        var transport = serviceProvider.GetRequiredService<IMcpTransport>();
        var server = new McpServer(transport, toolRegistry, resourceRegistry, promptRegistry, serverInfo, logger);
        
        return server;
    }

    /// <summary>
    /// Builds and runs the MCP server as a hosted service.
    /// </summary>
    /// <returns>A task that completes when the server stops.</returns>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // If no transport specified, use stdio by default
        if (_transport == null)
        {
            UseStdioTransport();
        }
        
        var serviceProvider = _services.BuildServiceProvider();
        var server = BuildWithProvider(serviceProvider);
        
        // Start all hosted services (including auto-services)
        var hostedServices = serviceProvider.GetServices<IHostedService>();
        foreach (var hostedService in hostedServices)
        {
            if (hostedService != server) // Don't start the server twice
            {
                await hostedService.StartAsync(cancellationToken);
            }
        }
        
        // Start the MCP server
        await server.StartAsync(cancellationToken);
        
        // Keep running until cancellation
        var tcs = new TaskCompletionSource<bool>();
        using (cancellationToken.Register(() => tcs.TrySetResult(true)))
        {
            await tcs.Task;
        }
        
        // Stop the MCP server
        await server.StopAsync(CancellationToken.None);
        
        // Stop all hosted services
        foreach (var hostedService in hostedServices)
        {
            if (hostedService != server)
            {
                await hostedService.StopAsync(CancellationToken.None);
            }
        }
        
        // Dispose async if supported, otherwise dispose synchronously
        if (serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            serviceProvider.Dispose();
        }
    }

    /// <summary>
    /// Creates a hosted service for use with IHost.
    /// </summary>
    /// <returns>A hosted service that can be registered with IHost.</returns>
    public IHostedService BuildHostedService()
    {
        return Build();
    }

    private void ConfigureDefaultServices()
    {
        // Add memory cache services
        _services.AddMemoryCache(options =>
        {
            // Set size limit for the cache (100 MB default)
            options.SizeLimit = 100 * 1024 * 1024;
            options.CompactionPercentage = 0.05; // Compact 5% when size limit is reached
            options.ExpirationScanFrequency = TimeSpan.FromMinutes(1);
        });
        
        // Add resource cache service as singleton
        _services.Configure<ResourceCacheOptions>(options =>
        {
            options.DefaultExpiration = TimeSpan.FromMinutes(5);
            options.SlidingExpiration = TimeSpan.FromMinutes(2);
            options.MaxSizeBytes = 100 * 1024 * 1024; // 100 MB
            options.EnableStatistics = true;
        });
#pragma warning disable CS0618 // Type or member is obsolete - backward compatibility
        _services.AddSingleton<IResourceCache, InMemoryResourceCache>();
#pragma warning restore CS0618 // Type or member is obsolete
        
        // Add framework services
        _services.AddSingleton<McpToolRegistry>();
        _services.AddSingleton<ResourceRegistry>();
        _services.AddSingleton<IPromptRegistry, PromptRegistry>();
        
        // Add default logging
        _services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddConsole(options =>
            {
                // MCP protocol requires stderr for logging
                options.LogToStandardErrorThreshold = LogLevel.Trace;
            });
        });
    }

    private void DiscoverAndRegisterPrompts(IPromptRegistry registry, Assembly assembly)
    {
        var promptTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IPrompt).IsAssignableFrom(t))
            .ToList();

        foreach (var promptType in promptTypes)
        {
            try
            {
                // Create instance using DI
                var prompt = (IPrompt)ActivatorUtilities.CreateInstance(_services.BuildServiceProvider(), promptType);
                registry.RegisterPrompt(prompt);
            }
            catch (Exception ex)
            {
                var logger = _services.BuildServiceProvider().GetService<ILogger<McpServerBuilder>>();
                logger?.LogWarning(ex, "Failed to register prompt type {PromptType}", promptType.Name);
            }
        }
    }

    /// <summary>
    /// Internal configuration class.
    /// </summary>
    private class ServerConfiguration
    {
        public string ServerName { get; set; } = "COA MCP Server";
        public string ServerVersion { get; set; } = "1.0.0";
    }
}

/// <summary>
/// Extension methods for IHostBuilder integration.
/// </summary>
public static class McpServerBuilderExtensions
{
    /// <summary>
    /// Adds an MCP server to the host.
    /// </summary>
    /// <param name="hostBuilder">The host builder.</param>
    /// <param name="configure">Action to configure the MCP server.</param>
    /// <returns>The host builder for chaining.</returns>
    public static IHostBuilder UseMcpServer(this IHostBuilder hostBuilder, Action<McpServerBuilder> configure)
    {
        return hostBuilder.ConfigureServices((context, services) =>
        {
            var builder = new McpServerBuilder();
            
            // Copy existing services
            foreach (var service in services)
            {
                builder.Services.Add(service);
            }
            
            configure(builder);
            
            var hostedService = builder.BuildHostedService();
            services.AddSingleton(hostedService);
            services.AddHostedService(provider => provider.GetRequiredService<IHostedService>());
        });
    }
}