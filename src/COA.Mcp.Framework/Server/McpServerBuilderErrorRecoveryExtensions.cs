using System;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Configuration;
using COA.Mcp.Framework.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace COA.Mcp.Framework.Server;

/// <summary>
/// Extension methods for McpServerBuilder to configure advanced error recovery features.
/// </summary>
public static class McpServerBuilderErrorRecoveryExtensions
{
    /// <summary>
    /// Configures advanced error recovery with template-based guidance and professional error messages.
    /// This system provides educational error messages that teach better tool usage patterns
    /// without emotional manipulation, following the professional approach from Phase 5 implementation.
    /// </summary>
    /// <param name="builder">The MCP server builder.</param>
    /// <param name="configure">Optional action to configure error recovery options.</param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// var builder = new McpServerBuilder()
    ///     .WithServerInfo("Professional Server", "1.0.0")
    ///     .WithAdvancedErrorRecovery(options =>
    ///     {
    ///         options.RecoveryTone = RecoveryTone.Professional;
    ///         options.IncludePerformanceMetrics = true;
    ///         options.IncludeWorkflowTips = true;
    ///         options.EnableBehavioralConditioning = true;
    ///     });
    /// </code>
    /// </example>
    public static McpServerBuilder WithAdvancedErrorRecovery(
        this McpServerBuilder builder, 
        Action<ErrorRecoveryOptions>? configure = null)
    {
        var options = new ErrorRecoveryOptions();
        configure?.Invoke(options);

        // Register error recovery services
        builder.Services.Configure<ErrorRecoveryOptions>(opts =>
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

        // Register core error recovery services using factory pattern for IOptions support
        builder.Services.AddSingleton<ErrorRecoveryTemplateProcessor>(sp => 
            new ErrorRecoveryTemplateProcessor(
                sp.GetRequiredService<InstructionTemplateProcessor>(),
                sp.GetRequiredService<IOptions<ErrorRecoveryOptions>>(),
                sp.GetService<ILogger<ErrorRecoveryTemplateProcessor>>()));

        builder.Services.AddTransient<AdvancedErrorMessageProvider>(sp => 
            new AdvancedErrorMessageProvider(
                sp.GetService<ErrorRecoveryTemplateProcessor>(),
                sp.GetRequiredService<IOptions<ErrorRecoveryOptions>>(),
                null, // toolInstances - could be enhanced later
                sp.GetService<ILogger<AdvancedErrorMessageProvider>>()));

        // Ensure template processing services are available (dependency for error recovery)
        builder.Services.AddSingleton<InstructionTemplateProcessor>();

        return builder;
    }

    /// <summary>
    /// Configures error recovery for professional development environments.
    /// Enables all guidance features with metrics, workflow tips, and behavioral conditioning.
    /// </summary>
    /// <param name="builder">The MCP server builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static McpServerBuilder WithProfessionalErrorRecovery(this McpServerBuilder builder)
    {
        return builder.WithAdvancedErrorRecovery(options => options.ForProfessionalDevelopment());
    }

    /// <summary>
    /// Configures error recovery for educational environments.
    /// Emphasizes learning and explanation over conciseness, suitable for beginners.
    /// </summary>
    /// <param name="builder">The MCP server builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static McpServerBuilder WithEducationalErrorRecovery(this McpServerBuilder builder)
    {
        return builder.WithAdvancedErrorRecovery(options => options.ForEducationalEnvironment());
    }

    /// <summary>
    /// Configures error recovery for production environments.
    /// Provides minimal, concise guidance focused on immediate resolution steps.
    /// </summary>
    /// <param name="builder">The MCP server builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static McpServerBuilder WithProductionErrorRecovery(this McpServerBuilder builder)
    {
        return builder.WithAdvancedErrorRecovery(options => options.ForProductionEnvironment());
    }

    /// <summary>
    /// Configures error recovery with custom external templates.
    /// Loads error recovery templates from the specified directory.
    /// </summary>
    /// <param name="builder">The MCP server builder.</param>
    /// <param name="templateDirectory">Directory containing custom error recovery templates.</param>
    /// <param name="watchForChanges">Whether to watch for template file changes and reload them.</param>
    /// <param name="configure">Optional additional configuration.</param>
    /// <returns>The builder for chaining.</returns>
    public static McpServerBuilder WithCustomErrorRecoveryTemplates(
        this McpServerBuilder builder,
        string templateDirectory,
        bool watchForChanges = false,
        Action<ErrorRecoveryOptions>? configure = null)
    {
        return builder.WithAdvancedErrorRecovery(options =>
        {
            options.ExternalTemplateDirectory = templateDirectory;
            options.WatchExternalTemplates = watchForChanges;
            configure?.Invoke(options);
        });
    }

    /// <summary>
    /// Disables error recovery guidance, returning to standard framework error messages.
    /// Useful for debugging or when custom error handling is preferred.
    /// </summary>
    /// <param name="builder">The MCP server builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static McpServerBuilder WithoutErrorRecovery(this McpServerBuilder builder)
    {
        return builder.WithAdvancedErrorRecovery(options => options.DisableRecoveryGuidance());
    }

    /// <summary>
    /// Configures error recovery specifically for CodeSearch/CodeNav scenarios.
    /// Optimizes error guidance for type-aware development tools and code navigation.
    /// </summary>
    /// <param name="builder">The MCP server builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static McpServerBuilder WithCodeNavigationErrorRecovery(this McpServerBuilder builder)
    {
        return builder.WithAdvancedErrorRecovery(options =>
        {
            options.ForProfessionalDevelopment();
            options.SuggestAlternativeTools = true;
            options.EnableBehavioralConditioning = true;
            
            // Additional custom context for code navigation tools
            // This could be expanded with code navigation-specific error types
        });
    }

}