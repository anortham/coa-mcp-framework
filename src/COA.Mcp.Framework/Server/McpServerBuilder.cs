using System;
using System.IO;
using System.Reflection;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Registration;
using COA.Mcp.Framework.Resources;
using COA.Mcp.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
    private Assembly? _toolAssembly;

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
    /// Configures custom input/output streams.
    /// </summary>
    /// <param name="input">The input stream to read from.</param>
    /// <param name="output">The output stream to write to.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder WithStreams(TextReader input, TextWriter output)
    {
        _configuration.Input = input;
        _configuration.Output = output;
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
    /// Builds the MCP server with the configured options.
    /// </summary>
    /// <returns>The configured MCP server ready to run.</returns>
    public McpServer Build()
    {
        var serviceProvider = _services.BuildServiceProvider();
        
        // Get registries
        var toolRegistry = serviceProvider.GetRequiredService<McpToolRegistry>();
        var resourceRegistry = serviceProvider.GetRequiredService<ResourceRegistry>();
        
        // Configure tools
        if (_toolAssembly != null)
        {
            toolRegistry.DiscoverAndRegisterTools(_toolAssembly);
        }
        
        _toolConfiguration?.Invoke(toolRegistry);
        
        // Configure resources
        _resourceConfiguration?.Invoke(resourceRegistry);
        
        // Create server
        var serverInfo = new Implementation
        {
            Name = _configuration.ServerName,
            Version = _configuration.ServerVersion
        };
        
        var logger = serviceProvider.GetService<ILogger<McpServer>>();
        var server = new McpServer(toolRegistry, resourceRegistry, serverInfo, logger);
        
        // Set custom streams if provided
        if (_configuration.Input != null && _configuration.Output != null)
        {
            server.SetStreams(_configuration.Input, _configuration.Output);
        }
        
        return server;
    }

    /// <summary>
    /// Builds and runs the MCP server as a hosted service.
    /// </summary>
    /// <returns>A task that completes when the server stops.</returns>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var server = Build();
        await server.StartAsync(cancellationToken);
        
        // Keep running until cancellation
        var tcs = new TaskCompletionSource<bool>();
        using (cancellationToken.Register(() => tcs.TrySetResult(true)))
        {
            await tcs.Task;
        }
        
        await server.StopAsync(CancellationToken.None);
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
        // Add framework services
        _services.AddSingleton<McpToolRegistry>();
        _services.AddSingleton<ResourceRegistry>();
        
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

    /// <summary>
    /// Internal configuration class.
    /// </summary>
    private class ServerConfiguration
    {
        public string ServerName { get; set; } = "COA MCP Server";
        public string ServerVersion { get; set; } = "1.0.0";
        public TextReader? Input { get; set; }
        public TextWriter? Output { get; set; }
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