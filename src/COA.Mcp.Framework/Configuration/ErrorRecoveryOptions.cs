using System;

namespace COA.Mcp.Framework.Configuration;

/// <summary>
/// Configuration options for the advanced error recovery system.
/// Controls how error messages are enhanced with professional guidance.
/// </summary>
public class ErrorRecoveryOptions
{
    /// <summary>
    /// Gets or sets whether recovery guidance is enabled.
    /// When false, original error messages are returned unchanged.
    /// </summary>
    public bool EnableRecoveryGuidance { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include the original error message along with recovery guidance.
    /// When true, both messages are shown; when false, only the enhanced guidance is shown.
    /// </summary>
    public bool IncludeOriginalError { get; set; } = true;

    /// <summary>
    /// Gets or sets the tone for recovery guidance messages.
    /// </summary>
    public RecoveryTone RecoveryTone { get; set; } = RecoveryTone.Professional;

    /// <summary>
    /// Gets or sets whether to include performance metrics in recovery guidance.
    /// When true, templates can include specific performance benefits and statistics.
    /// </summary>
    public bool IncludePerformanceMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include workflow tips in recovery guidance.
    /// When true, templates can include professional development workflow suggestions.
    /// </summary>
    public bool IncludeWorkflowTips { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum length of enhanced error messages.
    /// Prevents overly verbose guidance that could overwhelm users.
    /// </summary>
    public int MaxErrorMessageLength { get; set; } = 2000;

    /// <summary>
    /// Gets or sets whether to cache compiled error templates for performance.
    /// </summary>
    public bool CacheCompiledTemplates { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to log error recovery template processing for debugging.
    /// </summary>
    public bool EnableDebugLogging { get; set; } = false;

    /// <summary>
    /// Gets or sets the directory path for loading external error recovery templates.
    /// When set, the system will attempt to load custom templates from this directory.
    /// </summary>
    public string? ExternalTemplateDirectory { get; set; }

    /// <summary>
    /// Gets or sets whether to watch external template files for changes and reload them.
    /// Only applicable when ExternalTemplateDirectory is set.
    /// </summary>
    public bool WatchExternalTemplates { get; set; } = false;

    /// <summary>
    /// Gets or sets the priority of recovery guidance.
    /// Higher priority guidance appears first when multiple errors occur.
    /// </summary>
    public ErrorRecoveryPriority Priority { get; set; } = ErrorRecoveryPriority.Normal;

    /// <summary>
    /// Gets or sets whether error recovery should attempt to suggest alternative tools.
    /// When true, templates can recommend different tools that might avoid the error.
    /// </summary>
    public bool SuggestAlternativeTools { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include behavioral conditioning language in error messages.
    /// This uses educational language to help users learn better tool usage patterns.
    /// </summary>
    public bool EnableBehavioralConditioning { get; set; } = true;
}

/// <summary>
/// Defines the tone and style for error recovery guidance messages.
/// </summary>
public enum RecoveryTone
{
    /// <summary>
    /// Professional, technical tone focused on efficiency and best practices.
    /// Emphasizes metrics, performance benefits, and workflow improvements.
    /// </summary>
    Professional = 0,

    /// <summary>
    /// Friendly, helpful tone that provides guidance in an encouraging manner.
    /// Balances technical accuracy with approachable language.
    /// </summary>
    Friendly = 1,

    /// <summary>
    /// Concise, direct tone that provides minimal but precise guidance.
    /// Focuses on immediate action steps without extensive explanation.
    /// </summary>
    Concise = 2,

    /// <summary>
    /// Educational tone that explains the reasoning behind recommendations.
    /// Includes context about why certain approaches are better than others.
    /// </summary>
    Educational = 3
}

/// <summary>
/// Defines the priority level for error recovery guidance.
/// </summary>
public enum ErrorRecoveryPriority
{
    /// <summary>
    /// Low priority - guidance appears after other error information.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Normal priority - standard positioning for recovery guidance.
    /// </summary>
    Normal = 1,

    /// <summary>
    /// High priority - guidance appears prominently in error messages.
    /// </summary>
    High = 2,

    /// <summary>
    /// Critical priority - guidance takes precedence over original error messages.
    /// </summary>
    Critical = 3
}

/// <summary>
/// Extension methods for ErrorRecoveryOptions to provide convenient configuration.
/// </summary>
public static class ErrorRecoveryOptionsExtensions
{
    /// <summary>
    /// Configures options for professional development environments.
    /// Enables all guidance features with metrics and workflow tips.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <returns>The configured options for method chaining.</returns>
    public static ErrorRecoveryOptions ForProfessionalDevelopment(this ErrorRecoveryOptions options)
    {
        options.EnableRecoveryGuidance = true;
        options.RecoveryTone = RecoveryTone.Professional;
        options.IncludePerformanceMetrics = true;
        options.IncludeWorkflowTips = true;
        options.EnableBehavioralConditioning = true;
        options.SuggestAlternativeTools = true;
        return options;
    }

    /// <summary>
    /// Configures options for beginners or educational environments.
    /// Emphasizes learning and explanation over conciseness.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <returns>The configured options for method chaining.</returns>
    public static ErrorRecoveryOptions ForEducationalEnvironment(this ErrorRecoveryOptions options)
    {
        options.EnableRecoveryGuidance = true;
        options.RecoveryTone = RecoveryTone.Educational;
        options.IncludePerformanceMetrics = false; // Less overwhelming for beginners
        options.IncludeWorkflowTips = true;
        options.EnableBehavioralConditioning = true;
        options.MaxErrorMessageLength = 3000; // Allow more detailed explanations
        return options;
    }

    /// <summary>
    /// Configures options for production environments where conciseness is preferred.
    /// Minimal guidance focused on immediate resolution steps.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <returns>The configured options for method chaining.</returns>
    public static ErrorRecoveryOptions ForProductionEnvironment(this ErrorRecoveryOptions options)
    {
        options.EnableRecoveryGuidance = true;
        options.RecoveryTone = RecoveryTone.Concise;
        options.IncludePerformanceMetrics = false;
        options.IncludeWorkflowTips = false;
        options.EnableBehavioralConditioning = false;
        options.MaxErrorMessageLength = 500;
        return options;
    }

    /// <summary>
    /// Disables all error recovery guidance, returning original error messages only.
    /// Useful for debugging or when custom error handling is preferred.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <returns>The configured options for method chaining.</returns>
    public static ErrorRecoveryOptions DisableRecoveryGuidance(this ErrorRecoveryOptions options)
    {
        options.EnableRecoveryGuidance = false;
        return options;
    }
}