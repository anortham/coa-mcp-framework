using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Configuration;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Pipeline;
using COA.Mcp.Framework.Pipeline.Middleware;
using COA.Mcp.Framework.Pipeline.SimpleMiddleware;
using COA.Mcp.Framework.Prompts;
using COA.Mcp.Framework.Registration;
using COA.Mcp.Framework.Resources;
using COA.Mcp.Framework.Server.Services;
using COA.Mcp.Framework.Services;
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
    private Action<McpToolRegistry, IServiceProvider>? _toolConfigurationWithServices;
    private Action<ResourceRegistry, IServiceProvider>? _resourceConfigurationWithServices;
    private Action<IPromptRegistry, IServiceProvider>? _promptConfigurationWithServices;
    private Assembly? _toolAssembly;
    private Assembly? _promptAssembly;
    private IMcpTransport? _transport;
    private bool _transportConfigured;
    private ILogger<McpServerBuilder>? _logger;
    private List<ToolComparison>? _toolComparisons;
    private WorkflowEnforcement _workflowEnforcement = WorkflowEnforcement.Recommend;

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
    /// Sets instructions that help Claude understand how to use the server's tools effectively.
    /// Instructions become part of Claude's context during MCP interactions.
    /// </summary>
    /// <param name="instructions">The behavioral guidance instructions.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder WithInstructions(string? instructions)
    {
        _configuration.Instructions = instructions;
        return this;
    }

    /// <summary>
    /// Configures tool management services including priority systems and workflow suggestions.
    /// This enables professional tool guidance without manipulative language.
    /// </summary>
    /// <param name="configure">Action to configure tool management options.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder ConfigureToolManagement(Action<ToolManagementConfiguration>? configure = null)
    {
        var config = new ToolManagementConfiguration();
        configure?.Invoke(config);

        // Register tool description provider
        if (config.UseDefaultDescriptionProvider)
        {
            _services.AddSingleton<IToolDescriptionProvider, DefaultToolDescriptionProvider>();
        }

        // Register workflow suggestion manager
        if (config.EnableWorkflowSuggestions)
        {
            _services.AddSingleton<WorkflowSuggestionManager>();
        }

        return this;
    }

    /// <summary>
    /// Enables automatic instruction generation based on available tools and their priorities.
    /// This creates context-aware behavioral guidance without manual instruction writing.
    /// </summary>
    /// <param name="configure">Optional action to configure instruction generation.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder WithGeneratedInstructions(Action<InstructionGenerationOptions>? configure = null)
    {
        var options = new InstructionGenerationOptions();
        configure?.Invoke(options);

        _configuration.UseGeneratedInstructions = true;
        _configuration.InstructionGenerationOptions = options;

        // Ensure tool management is configured for instruction generation
        ConfigureToolManagement(config =>
        {
            config.UseDefaultDescriptionProvider = true;
            config.EnableWorkflowSuggestions = true;
        });

        return this;
    }

    /// <summary>
    /// Enables template-based instruction generation with sophisticated conditional logic
    /// based on available tool capabilities. This is the most advanced instruction generation mode.
    /// </summary>
    /// <param name="configure">Optional action to configure template instruction options.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder WithTemplateInstructions(Action<TemplateInstructionOptions>? configure = null)
    {
        var options = new TemplateInstructionOptions();
        configure?.Invoke(options);

        _configuration.UseTemplateInstructions = true;
        _configuration.TemplateInstructionOptions = options;

        // Register template processing services
        _services.AddSingleton<InstructionTemplateProcessor>();
        _services.AddSingleton<InstructionTemplateManager>();

        // Ensure tool management is configured for template generation
        ConfigureToolManagement(config =>
        {
            config.UseDefaultDescriptionProvider = true;
            config.EnableWorkflowSuggestions = true;
            config.EnableToolPrioritySystem = true;
        });

        return this;
    }

    /// <summary>
    /// Configures advanced error recovery with template-based guidance and professional error messages.
    /// This system provides educational error messages that teach better tool usage patterns
    /// without emotional manipulation, following the professional approach from Phase 5 implementation.
    /// </summary>
    /// <param name="configure">Optional action to configure error recovery options.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder WithAdvancedErrorRecovery(Action<ErrorRecoveryOptions>? configure = null)
    {
        var options = new ErrorRecoveryOptions();
        configure?.Invoke(options);

        // Register error recovery configuration
        _services.Configure<ErrorRecoveryOptions>(opts =>
        {
            opts.EnableRecoveryGuidance = options.EnableRecoveryGuidance;
            opts.IncludeOriginalError = options.IncludeOriginalError;
            opts.RecoveryTone = options.RecoveryTone;
            opts.IncludePerformanceMetrics = options.IncludePerformanceMetrics;
            opts.IncludeWorkflowTips = options.IncludeWorkflowTips;
            opts.MaxErrorMessageLength = options.MaxErrorMessageLength;
            opts.CacheCompiledTemplates = options.CacheCompiledTemplates;
            opts.EnableDebugLogging = options.EnableDebugLogging;
            opts.ExternalTemplateDirectory = options.ExternalTemplateDirectory;
            opts.WatchExternalTemplates = options.WatchExternalTemplates;
            opts.Priority = options.Priority;
            opts.SuggestAlternativeTools = options.SuggestAlternativeTools;
            opts.EnableBehavioralConditioning = options.EnableBehavioralConditioning;
        });

        // Register core error recovery services
        _services.AddSingleton<ErrorRecoveryTemplateProcessor>();
        _services.AddTransient<AdvancedErrorMessageProvider>();

        // Ensure template processing services are available (dependency for error recovery)
        _services.AddSingleton<InstructionTemplateProcessor>();

        return this;
    }

    /// <summary>
    /// Sets instructions from a template file with variables.
    /// </summary>
    /// <param name="templatePath">Path to the .scriban template file.</param>
    /// <param name="variables">Variables for template processing.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder WithInstructionsFromTemplate(string templatePath, TemplateVariables variables)
    {
        if (File.Exists(templatePath))
        {
            var templateContent = File.ReadAllText(templatePath);
            var processor = new InstructionTemplateProcessor();
            var instructions = processor.ProcessTemplate(templateContent, variables);
            _configuration.Instructions = instructions;
        }
        return this;
    }

    /// <summary>
    /// Adds a tool comparison for behavioral guidance.
    /// </summary>
    /// <param name="task">The task description.</param>
    /// <param name="serverTool">Your server's tool for this task.</param>
    /// <param name="builtInTool">The built-in tool it replaces.</param>
    /// <param name="advantage">Why your tool is better.</param>
    /// <param name="performanceMetric">Specific performance data.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder WithToolComparison(string task, string serverTool, string builtInTool, 
        string advantage, string performanceMetric = "")
    {
        // Store comparisons for template processing
        _toolComparisons ??= new List<ToolComparison>();
        _toolComparisons.Add(new ToolComparison
        {
            Task = task,
            ServerTool = serverTool,
            BuiltInTool = builtInTool,
            Advantage = advantage,
            PerformanceMetric = performanceMetric
        });
        return this;
    }

    /// <summary>
    /// Sets the workflow enforcement level for behavioral guidance.
    /// </summary>
    /// <param name="level">How strongly to recommend tool usage.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder WithWorkflowEnforcement(WorkflowEnforcement level)
    {
        _workflowEnforcement = level;
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
        
        // Register as a factory to avoid calling BuildServiceProvider here
        _services.AddSingleton<IMcpTransport>(serviceProvider =>
        {
            var logger = serviceProvider.GetService<ILogger<StdioTransport>>();
            return new StdioTransport(options.Input, options.Output, logger);
        });
        
        _transportConfigured = true;
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
        
        // Register as a factory to avoid calling BuildServiceProvider here
        _services.AddSingleton<IMcpTransport>(serviceProvider =>
        {
            var logger = serviceProvider.GetService<ILogger<HttpTransport>>();
            return new HttpTransport(options, logger);
        });
        _services.AddSingleton(options);
        
        _transportConfigured = true;
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
        
        // Register as a factory to avoid calling BuildServiceProvider here
        _services.AddSingleton<IMcpTransport>(serviceProvider =>
        {
            var logger = serviceProvider.GetService<ILogger<WebSocketTransport>>();
            return new WebSocketTransport(options, logger);
        });
        _services.AddSingleton(options);
        
        _transportConfigured = true;
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
        _transportConfigured = true;
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
    /// Configures tools for the server with access to the service provider.
    /// </summary>
    /// <param name="configure">Action to configure the tool registry with service provider access.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder ConfigureTools(Action<McpToolRegistry, IServiceProvider> configure)
    {
        _toolConfigurationWithServices += configure;
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
    /// Configures resources for the server with access to the service provider.
    /// </summary>
    /// <param name="configure">Action to configure the resource registry with service provider access.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder ConfigureResources(Action<ResourceRegistry, IServiceProvider> configure)
    {
        _resourceConfigurationWithServices += configure;
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
    /// Configures prompts for the server with access to the service provider.
    /// </summary>
    /// <param name="configure">Action to configure the prompt registry with service provider access.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder ConfigurePrompts(Action<IPromptRegistry, IServiceProvider> configure)
    {
        _promptConfigurationWithServices += configure;
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
    /// Configures framework-wide options including logging behavior.
    /// </summary>
    /// <param name="configure">Action to configure framework options.</param>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder ConfigureFramework(Action<FrameworkOptions> configure)
    {
        _services.Configure(configure);
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
        if (!_transportConfigured)
        {
            UseStdioTransport();
        }
        
        var serviceProvider = _services.BuildServiceProvider();
        return BuildWithProvider(serviceProvider);
    }
    
    private McpServer BuildWithProvider(IServiceProvider serviceProvider)
    {
        // Initialize logger
        _logger = serviceProvider.GetService<ILogger<McpServerBuilder>>();
        
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
        _toolConfigurationWithServices?.Invoke(toolRegistry, serviceProvider);
        
        // Configure resources
        _resourceConfiguration?.Invoke(resourceRegistry);
        _resourceConfigurationWithServices?.Invoke(resourceRegistry, serviceProvider);
        
        // Configure prompts
        if (_promptAssembly != null)
        {
            DiscoverAndRegisterPrompts(promptRegistry, _promptAssembly);
        }
        
        _promptConfiguration?.Invoke(promptRegistry);
        _promptConfigurationWithServices?.Invoke(promptRegistry, serviceProvider);
        
        // Generate instructions if enabled
        var finalInstructions = _configuration.Instructions;
        
        // Template instructions take priority over basic generated instructions
        if (_configuration.UseTemplateInstructions && _configuration.TemplateInstructionOptions != null)
        {
            finalInstructions = GenerateTemplateInstructions(toolRegistry, serviceProvider);
        }
        else if (_configuration.UseGeneratedInstructions && _configuration.InstructionGenerationOptions != null)
        {
            finalInstructions = GenerateInstructions(toolRegistry, serviceProvider);
        }
        
        // Create server
        var serverInfo = new Implementation
        {
            Name = _configuration.ServerName,
            Version = _configuration.ServerVersion
        };
        
        var logger = serviceProvider.GetService<ILogger<McpServer>>();
        var transport = serviceProvider.GetRequiredService<IMcpTransport>();
        var server = new McpServer(transport, toolRegistry, resourceRegistry, promptRegistry, serverInfo, finalInstructions, logger);
        
        return server;
    }

    /// <summary>
    /// Builds and runs the MCP server as a hosted service.
    /// </summary>
    /// <returns>A task that completes when the server stops.</returns>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // If no transport specified, use stdio by default
        if (!_transportConfigured)
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
        
        // Add tool management services (only registered when explicitly enabled via ConfigureToolManagement)
        // These services are opt-in to avoid unused service registrations
        
        // Configure framework options if not already configured
        _services.Configure<FrameworkOptions>(options => { });
        
        // Add logging configuration only if not already configured
        ConfigureFrameworkLogging();
    }
    
    private void ConfigureFrameworkLogging()
    {
        var serviceProvider = _services.BuildServiceProvider();
        var frameworkOptions = serviceProvider.GetService<IOptions<FrameworkOptions>>()?.Value ?? new FrameworkOptions();
        
        // Check if logging is already configured by looking for existing loggers
        var existingLoggers = _services.Where(s => s.ServiceType == typeof(ILoggerProvider) || 
                                                   s.ServiceType == typeof(ILoggingBuilder)).Any();
        
        if (!frameworkOptions.EnableFrameworkLogging || 
            (!frameworkOptions.ConfigureLoggingIfNotConfigured && existingLoggers))
        {
            return;
        }
        
        _services.AddLogging(builder =>
        {
            // Set framework log level (default Warning to reduce verbosity)
            builder.SetMinimumLevel(frameworkOptions.FrameworkLogLevel);
            
            // Add console logging with stderr output for MCP protocol compatibility
            builder.AddConsole(options =>
            {
                options.LogToStandardErrorThreshold = LogLevel.Trace;
            });
            
            // Configure category-specific log levels for reduced framework verbosity
            builder.AddFilter("COA.Mcp.Framework.Pipeline.Middleware", 
                frameworkOptions.EnableDetailedMiddlewareLogging ? LogLevel.Debug : LogLevel.Warning);
            builder.AddFilter("COA.Mcp.Framework.Transport", 
                frameworkOptions.EnableDetailedTransportLogging ? LogLevel.Debug : LogLevel.Warning);
            builder.AddFilter("COA.Mcp.Framework.Base", 
                frameworkOptions.EnableDetailedToolLogging ? LogLevel.Information : LogLevel.Warning);
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
    /// Generates professional instructions based on available tools and their priorities.
    /// </summary>
    /// <param name="toolRegistry">The tool registry containing registered tools.</param>
    /// <param name="serviceProvider">The service provider for accessing tool management services.</param>
    /// <returns>Generated instruction text or null if generation failed.</returns>
    private string? GenerateInstructions(McpToolRegistry toolRegistry, IServiceProvider serviceProvider)
    {
        try
        {
            var options = _configuration.InstructionGenerationOptions;
            if (options == null || !options.EnableAutomaticGeneration)
                return _configuration.Instructions;

            var instructions = new List<string>();
            
            // Add title
            if (!string.IsNullOrEmpty(options.InstructionTitle))
            {
                instructions.Add(options.InstructionTitle);
                instructions.Add("");
            }

            // Add introduction
            if (options.IncludeIntroduction && !string.IsNullOrEmpty(options.IntroductionText))
            {
                instructions.Add(options.IntroductionText);
                instructions.Add("");
            }

            // Get available tools
            var availableTools = toolRegistry.GetProtocolTools().Select(t => t.Name).ToList();

            // Generate workflow suggestions if enabled
            if (options.IncludeWorkflowSuggestions)
            {
                var workflowManager = serviceProvider.GetService<WorkflowSuggestionManager>();
                if (workflowManager != null)
                {
                    var workflowInstructions = workflowManager.GenerateInstructionText(
                        availableTools, 
                        options.IncludeAlternativeTools);
                    
                    if (!string.IsNullOrEmpty(workflowInstructions))
                    {
                        instructions.Add(workflowInstructions);
                        instructions.Add("");
                    }
                }
            }

            // Add tool priority information if enabled
            if (options.IncludeToolPriorities)
            {
                var priorityInstructions = GenerateToolPriorityInstructions(availableTools, serviceProvider, options);
                if (!string.IsNullOrEmpty(priorityInstructions))
                {
                    instructions.Add(priorityInstructions);
                    instructions.Add("");
                }
            }

            // Add custom text if provided
            if (!string.IsNullOrEmpty(options.CustomAppendText))
            {
                instructions.Add(options.CustomAppendText);
                instructions.Add("");
            }

            var result = string.Join("\n", instructions).Trim();
            
            // Respect maximum length
            if (result.Length > options.MaxInstructionLength)
            {
                result = result.Substring(0, options.MaxInstructionLength - 3) + "...";
            }

            return result;
        }
        catch (Exception ex)
        {
            // Log error and fallback to manual instructions
            var logger = serviceProvider.GetService<ILogger<McpServerBuilder>>();
            logger?.LogWarning(ex, "Failed to generate automatic instructions, falling back to manual instructions");
            return _configuration.Instructions;
        }
    }

    /// <summary>
    /// Generates tool priority instructions based on available tools.
    /// </summary>
    private string GenerateToolPriorityInstructions(IEnumerable<string> availableTools, IServiceProvider serviceProvider, InstructionGenerationOptions options)
    {
        var instructions = new List<string>();
        
        // This would be extended to check actual tool priorities from IToolPriority implementations
        // For now, we provide a basic structure
        var toolList = availableTools.ToList();
        if (toolList.Any())
        {
            instructions.Add("## Available Tools");
            instructions.Add($"This server provides {toolList.Count} specialized tools designed for optimal performance:");
            
            foreach (var tool in toolList.Take(10)) // Limit to first 10 tools to prevent bloat
            {
                instructions.Add($"- **{tool}**: Professional tool for enhanced efficiency");
            }
            
            if (toolList.Count > 10)
            {
                instructions.Add($"- ...and {toolList.Count - 10} additional tools");
            }
        }

        return string.Join("\n", instructions);
    }

    /// <summary>
    /// Generates instructions using template-based processing with conditional logic
    /// based on available tool capabilities and markers.
    /// </summary>
    /// <param name="toolRegistry">The tool registry containing registered tools.</param>
    /// <param name="serviceProvider">The service provider for accessing template services.</param>
    /// <returns>Template-generated instruction text or fallback instructions.</returns>
    private string? GenerateTemplateInstructions(McpToolRegistry toolRegistry, IServiceProvider serviceProvider)
    {
        try
        {
            var options = _configuration.TemplateInstructionOptions;
            if (options == null || !options.EnableTemplateInstructions)
                return _configuration.Instructions;

            var templateManager = serviceProvider.GetService<InstructionTemplateManager>();
            if (templateManager == null)
            {
                _logger?.LogWarning("InstructionTemplateManager not available, falling back to basic instructions");
                return HandleTemplateFallback(options);
            }

            // Get available tools and tool instances for marker detection
            var availableTools = toolRegistry.GetProtocolTools().Select(t => t.Name).ToList();
            var toolInstances = options.EnableMarkerDetection ? GetToolInstances(toolRegistry) : null;

            // Load external templates if specified
            if (!string.IsNullOrEmpty(options.TemplateDirectory))
            {
                // Convert to absolute path if relative
                var templateDir = Path.IsPathRooted(options.TemplateDirectory) 
                    ? options.TemplateDirectory 
                    : Path.Combine(Directory.GetCurrentDirectory(), options.TemplateDirectory);
                
                // Only attempt to load if directory exists
                if (Directory.Exists(templateDir))
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await templateManager.LoadTemplatesFromDirectoryAsync(templateDir, options.PrecompileTemplates);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Failed to load templates from directory: {Directory}", templateDir);
                        }
                    });
                }
                else
                {
                    _logger?.LogDebug("Template directory not found, skipping external template loading: {Directory}", templateDir);
                }
            }

            // Use custom template if provided
            if (!string.IsNullOrEmpty(options.CustomTemplate))
            {
                var processor = serviceProvider.GetService<InstructionTemplateProcessor>();
                if (processor != null)
                {
                    var variables = TemplateVariables.FromTools(availableTools, toolInstances);
                    AddCustomVariables(variables, options, serviceProvider);
                    
                    var result = processor.ProcessTemplate(options.CustomTemplate, variables, "custom");
                    return ApplyInstructionLimits(result, options);
                }
            }

            // Use context-based template
            var customVars = new Dictionary<string, object>(options.CustomTemplateVariables);
            AddServerInfo(customVars);
            
            var instructions = templateManager.GenerateInstructions(
                options.TemplateContext,
                availableTools,
                toolInstances,
                customVars);

            if (string.IsNullOrWhiteSpace(instructions))
            {
                _logger?.LogWarning("Template generation returned empty result for context: {Context}", options.TemplateContext);
                return HandleTemplateFallback(options);
            }

            return ApplyInstructionLimits(instructions, options);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to generate template instructions");
            return HandleTemplateFallback(_configuration.TemplateInstructionOptions);
        }
    }

    /// <summary>
    /// Handles fallback behavior when template instruction generation fails.
    /// </summary>
    private string? HandleTemplateFallback(TemplateInstructionOptions? options)
    {
        return options?.FallbackMode switch
        {
            TemplateFallbackMode.BasicInstructions => _configuration.UseGeneratedInstructions 
                ? GenerateInstructions(null!, null!) // Will handle null gracefully
                : _configuration.Instructions,
            TemplateFallbackMode.EmptyInstructions => string.Empty,
            TemplateFallbackMode.ErrorMessage => "<!-- Template instruction generation failed. Please check configuration. -->",
            TemplateFallbackMode.ManualInstructions => _configuration.Instructions,
            _ => _configuration.Instructions
        };
    }

    /// <summary>
    /// Gets tool instances from the tool registry for marker detection.
    /// </summary>
    private IEnumerable<object>? GetToolInstances(McpToolRegistry toolRegistry)
    {
        try
        {
            // This would need to be implemented in McpToolRegistry to expose tool instances
            // For now, return null to disable marker detection
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get tool instances for marker detection");
            return null;
        }
    }

    /// <summary>
    /// Adds custom variables to template variables including workflow suggestions and priorities.
    /// </summary>
    private void AddCustomVariables(TemplateVariables variables, TemplateInstructionOptions options, IServiceProvider serviceProvider)
    {
        if (variables.CustomVariables == null)
        {
            variables.CustomVariables = new Dictionary<string, object>();
        }

        // Add workflow suggestions if enabled
        if (options.IncludeWorkflowSuggestions)
        {
            var workflowManager = serviceProvider.GetService<WorkflowSuggestionManager>();
            if (workflowManager != null)
            {
                variables.WorkflowSuggestions = workflowManager.GetAllWorkflows().ToArray();
            }
        }

        // Add server information
        AddServerInfo(variables.CustomVariables);

        // Add custom template variables
        foreach (var kvp in options.CustomTemplateVariables)
        {
            variables.CustomVariables[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// Adds server information to the template variables.
    /// </summary>
    private void AddServerInfo(Dictionary<string, object> variables)
    {
        variables["server_info"] = new
        {
            name = _configuration.ServerName,
            version = _configuration.ServerVersion
        };
    }

    /// <summary>
    /// Applies instruction length limits and metadata if configured.
    /// </summary>
    private string ApplyInstructionLimits(string instructions, TemplateInstructionOptions options)
    {
        if (string.IsNullOrEmpty(instructions))
            return instructions;

        var result = instructions;

        // Apply length limit
        if (result.Length > options.MaxInstructionLength)
        {
            result = result.Substring(0, options.MaxInstructionLength - 3) + "...";
        }

        // Add processing metadata if enabled
        if (options.IncludeProcessingMetadata)
        {
            result += $"\n\n<!-- Generated using template context: {options.TemplateContext} -->";
        }

        return result;
    }

    /// <summary>
    /// Internal configuration class.
    /// </summary>
    private class ServerConfiguration
    {
        public string ServerName { get; set; } = "COA MCP Server";
        public string ServerVersion { get; set; } = "1.0.0";
        public string? Instructions { get; set; }
        public bool UseGeneratedInstructions { get; set; } = false;
        public InstructionGenerationOptions? InstructionGenerationOptions { get; set; }
        public bool UseTemplateInstructions { get; set; } = false;
        public TemplateInstructionOptions? TemplateInstructionOptions { get; set; }
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