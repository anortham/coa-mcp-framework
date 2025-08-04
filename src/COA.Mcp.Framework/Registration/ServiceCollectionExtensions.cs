using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace COA.Mcp.Framework.Registration;

/// <summary>
/// Extension methods for configuring MCP framework services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds MCP framework services to the service collection.
    /// </summary>
    public static IServiceCollection AddMcpFramework(
        this IServiceCollection services,
        Action<McpFrameworkOptions>? configure = null)
    {
        var options = new McpFrameworkOptions();
        configure?.Invoke(options);

        // Register core services
        services.TryAddSingleton<IToolRegistry, AttributeBasedToolRegistry>();
        services.TryAddSingleton<IToolDiscovery, ToolDiscoveryService>();
        services.TryAddSingleton<IParameterValidator, DefaultParameterValidator>();
        
        // Register the options
        services.AddSingleton(options);

        // Discover and register tools if assemblies are specified
        if (options.AssembliesToScan.Any())
        {
            services.AddSingleton<IHostedService, ToolDiscoveryHostedService>(provider =>
            {
                var registry = provider.GetRequiredService<IToolRegistry>();
                var discovery = provider.GetRequiredService<IToolDiscovery>();
                return new ToolDiscoveryHostedService(registry, discovery, options.AssembliesToScan.ToArray());
            });
        }

        // Register tool types for DI
        foreach (var assembly in options.AssembliesToScan)
        {
            RegisterToolTypes(services, assembly);
        }

        return services;
    }

    /// <summary>
    /// Adds a specific tool to the service collection.
    /// </summary>
    public static IServiceCollection AddMcpTool<TTool>(this IServiceCollection services)
        where TTool : class, ITool
    {
        services.TryAddScoped<TTool>();
        services.AddSingleton<IHostedService>(provider =>
        {
            var registry = provider.GetRequiredService<IToolRegistry>();
            var tool = ActivatorUtilities.CreateInstance<TTool>(provider);
            return new SingleToolRegistrationService(registry, tool);
        });

        return services;
    }

    /// <summary>
    /// Adds tool discovery for a specific assembly.
    /// </summary>
    public static IServiceCollection AddMcpToolsFromAssembly(
        this IServiceCollection services,
        Assembly assembly)
    {
        RegisterToolTypes(services, assembly);
        
        services.AddSingleton<IHostedService>(provider =>
        {
            var registry = provider.GetRequiredService<IToolRegistry>();
            var discovery = provider.GetRequiredService<IToolDiscovery>();
            return new ToolDiscoveryHostedService(registry, discovery, assembly);
        });

        return services;
    }

    private static void RegisterToolTypes(IServiceCollection services, Assembly assembly)
    {
        var toolTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract &&
                       t.GetCustomAttribute<Attributes.McpServerToolTypeAttribute>() != null)
            .ToList();

        foreach (var toolType in toolTypes)
        {
            // Register as scoped to support per-request state
            services.TryAddScoped(toolType);
            
            // If it implements ITool, also register by interface
            if (typeof(ITool).IsAssignableFrom(toolType))
            {
                services.TryAddScoped(typeof(ITool), toolType);
            }
        }
    }

    // Helper hosted service for tool discovery
    private class ToolDiscoveryHostedService : IHostedService
    {
        private readonly IToolRegistry _registry;
        private readonly IToolDiscovery _discovery;
        private readonly Assembly[] _assemblies;

        public ToolDiscoveryHostedService(
            IToolRegistry registry,
            IToolDiscovery discovery,
            params Assembly[] assemblies)
        {
            _registry = registry;
            _discovery = discovery;
            _assemblies = assemblies;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var metadata = _discovery.DiscoverTools(_assemblies);
            
            foreach (var tool in metadata)
            {
                _registry.RegisterTool(tool);
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    // Helper hosted service for single tool registration
    private class SingleToolRegistrationService : IHostedService
    {
        private readonly IToolRegistry _registry;
        private readonly ITool _tool;

        public SingleToolRegistrationService(IToolRegistry registry, ITool tool)
        {
            _registry = registry;
            _tool = tool;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _registry.RegisterTool(_tool);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

/// <summary>
/// Options for configuring the MCP framework.
/// </summary>
public class McpFrameworkOptions
{
    /// <summary>
    /// Gets the assemblies to scan for tools.
    /// </summary>
    public List<Assembly> AssembliesToScan { get; } = new();

    /// <summary>
    /// Gets or sets whether to enable automatic tool validation.
    /// </summary>
    public bool EnableValidation { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to throw on validation errors.
    /// </summary>
    public bool ThrowOnValidationErrors { get; set; } = false;

    /// <summary>
    /// Gets or sets the default tool timeout in milliseconds.
    /// </summary>
    public int DefaultTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Adds an assembly to scan for tools.
    /// </summary>
    public McpFrameworkOptions DiscoverToolsFromAssembly(Assembly assembly)
    {
        if (!AssembliesToScan.Contains(assembly))
        {
            AssembliesToScan.Add(assembly);
        }
        return this;
    }

    /// <summary>
    /// Adds the calling assembly to scan for tools.
    /// </summary>
    public McpFrameworkOptions DiscoverToolsFromCurrentAssembly()
    {
        return DiscoverToolsFromAssembly(Assembly.GetCallingAssembly());
    }
}