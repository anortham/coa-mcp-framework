using System;
using System.Linq;
using System.Reflection;
using COA.Mcp.Framework.Configuration;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Pipeline.Middleware;
using COA.Mcp.Framework.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        services.TryAddSingleton<McpToolRegistry>();
        
        // Register the options
        services.AddSingleton(options);

        // Discover and register tools from assemblies
        if (options.AssembliesToScan.Any())
        {
            services.AddSingleton(provider =>
            {
                var registry = provider.GetRequiredService<McpToolRegistry>();
                
                // Discover tools from all specified assemblies
                foreach (var assembly in options.AssembliesToScan)
                {
                    DiscoverAndRegisterTools(provider, registry, assembly);
                }
                
                return registry;
            });
        }

        // Register tool types for DI
        foreach (var assembly in options.AssembliesToScan)
        {
            RegisterToolTypes(services, assembly);
        }

        return services;
    }

    // Removed experimental enforcement registrations (TypeVerification/TDD)

    /// <summary>
    /// Adds a specific tool to the service collection.
    /// </summary>
    public static IServiceCollection AddMcpTool<TTool>(this IServiceCollection services)
        where TTool : class, IMcpTool
    {
        services.TryAddScoped<TTool>();
        
        // Register with the tool registry when the service is resolved
        services.Configure<McpToolRegistrationOptions>(opts =>
        {
            opts.ToolTypes.Add(typeof(TTool));
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
        
        services.Configure<McpToolRegistrationOptions>(opts =>
        {
            opts.AssembliesToScan.Add(assembly);
        });

        return services;
    }

    private static void RegisterToolTypes(IServiceCollection services, Assembly assembly)
    {
        // Find all types that implement IMcpTool
        var toolTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract &&
                       typeof(IMcpTool).IsAssignableFrom(t))
            .ToList();

        foreach (var toolType in toolTypes)
        {
            // Register as scoped to support per-request state
            services.TryAddScoped(toolType);
            
            // Also register by IMcpTool interface
            services.TryAddScoped(typeof(IMcpTool), toolType);
        }
    }

    private static void DiscoverAndRegisterTools(
        IServiceProvider provider,
        McpToolRegistry registry,
        Assembly assembly)
    {
        var toolTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract &&
                       typeof(IMcpTool).IsAssignableFrom(t))
            .ToList();

        foreach (var toolType in toolTypes)
        {
            try
            {
                var tool = (IMcpTool)ActivatorUtilities.CreateInstance(provider, toolType);
                registry.RegisterTool(tool);
            }
            catch (Exception ex)
            {
                // Log error but don't fail startup
                Console.Error.WriteLine($"Failed to register tool {toolType.Name}: {ex.Message}");
            }
        }
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
    /// Gets or sets the token optimization level.
    /// </summary>
    public TokenOptimizationLevel TokenOptimization { get; set; } = TokenOptimizationLevel.Balanced;

    /// <summary>
    /// Gets or sets whether to use AI-optimized responses.
    /// </summary>
    public bool UseAIOptimizedResponses { get; set; } = true;

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
    /// Sets the token optimization level.
    /// </summary>
    public McpFrameworkOptions UseTokenOptimization(TokenOptimizationLevel level)
    {
        TokenOptimization = level;
        return this;
    }
}

/// <summary>
/// Token optimization levels.
/// </summary>
public enum TokenOptimizationLevel
{
    /// <summary>
    /// No token optimization.
    /// </summary>
    None,

    /// <summary>
    /// Conservative optimization - preserves most content.
    /// </summary>
    Conservative,

    /// <summary>
    /// Balanced optimization - good trade-off between content and tokens.
    /// </summary>
    Balanced,

    /// <summary>
    /// Aggressive optimization - minimizes token usage.
    /// </summary>
    Aggressive
}

/// <summary>
/// Options for tool registration.
/// </summary>
internal class McpToolRegistrationOptions
{
    public List<Type> ToolTypes { get; } = new();
    public List<Assembly> AssembliesToScan { get; } = new();
}
