using System;
using System.Collections.Generic;

namespace COA.Mcp.Framework.Configuration;

/// <summary>
/// Configuration options for template-based instruction generation.
/// This enables sophisticated conditional instructions based on tool capabilities
/// without hardcoded assumptions about what tools are available.
/// </summary>
public class TemplateInstructionOptions
{
    /// <summary>
    /// Gets or sets whether template-based instruction generation is enabled.
    /// When enabled, instructions are generated using Scriban templates with conditional logic.
    /// </summary>
    /// <value>Default is false to maintain backward compatibility.</value>
    public bool EnableTemplateInstructions { get; set; } = false;

    /// <summary>
    /// Gets or sets the template context to use for instruction generation.
    /// This determines which built-in template is used for generating instructions.
    /// </summary>
    /// <value>
    /// Available built-in contexts: "general", "codesearch", "database"
    /// Default is "general" for universal compatibility.
    /// </value>
    public string TemplateContext { get; set; } = "general";

    /// <summary>
    /// Gets or sets custom template text to use instead of built-in templates.
    /// When specified, this overrides the TemplateContext setting.
    /// </summary>
    /// <value>Scriban template text or null to use built-in templates.</value>
    public string? CustomTemplate { get; set; }

    /// <summary>
    /// Gets or sets the directory path containing external template files.
    /// Template files should have .scriban extension and be named after their context.
    /// </summary>
    /// <value>Directory path or null to use only built-in templates.</value>
    public string? TemplateDirectory { get; set; }

    /// <summary>
    /// Gets or sets whether templates should be pre-compiled for performance.
    /// Pre-compilation improves rendering speed but uses more memory.
    /// </summary>
    /// <value>Default is true for better performance in production.</value>
    public bool PrecompileTemplates { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable automatic tool marker detection.
    /// This analyzes tool instances to determine their capabilities for template conditionals.
    /// </summary>
    /// <value>Default is true to enable capability-based conditional instructions.</value>
    public bool EnableMarkerDetection { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include tool priority information in template variables.
    /// This enables priority-based recommendations in generated instructions.
    /// </summary>
    /// <value>Default is true to enable intelligent tool prioritization.</value>
    public bool IncludeToolPriorities { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include workflow suggestions from WorkflowSuggestionManager.
    /// This provides structured workflow guidance in template-generated instructions.
    /// </summary>
    /// <value>Default is true to include professional workflow guidance.</value>
    public bool IncludeWorkflowSuggestions { get; set; } = true;

    /// <summary>
    /// Gets or sets custom template variables to make available in all templates.
    /// This allows server-specific customization of template rendering.
    /// </summary>
    /// <value>Dictionary of variable names to values, or empty for no custom variables.</value>
    public Dictionary<string, object> CustomTemplateVariables { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the maximum length of generated instructions in characters.
    /// This prevents template-generated instructions from becoming too verbose.
    /// </summary>
    /// <value>Default is 3000 characters for comprehensive but focused guidance.</value>
    public int MaxInstructionLength { get; set; } = 3000;

    /// <summary>
    /// Gets or sets whether to cache compiled templates in memory.
    /// Template caching improves performance but uses memory for each compiled template.
    /// </summary>
    /// <value>Default is true for better performance with repeated template usage.</value>
    public bool EnableTemplateCache { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to watch template directory for file changes.
    /// When enabled, external template files are reloaded when modified.
    /// </summary>
    /// <value>Default is false to avoid file system overhead in production.</value>
    public bool WatchTemplateFiles { get; set; } = false;

    /// <summary>
    /// Gets or sets the fallback behavior when template processing fails.
    /// </summary>
    /// <value>Default is to fallback to basic instruction generation.</value>
    public TemplateFallbackMode FallbackMode { get; set; } = TemplateFallbackMode.BasicInstructions;

    /// <summary>
    /// Gets or sets whether to include template processing metadata in generated instructions.
    /// This is useful for debugging template issues but should be disabled in production.
    /// </summary>
    /// <value>Default is false to keep instructions clean.</value>
    public bool IncludeProcessingMetadata { get; set; } = false;
}

/// <summary>
/// Defines how the system should behave when template processing fails.
/// </summary>
public enum TemplateFallbackMode
{
    /// <summary>
    /// Generate basic instructions using the non-template system.
    /// This ensures users always receive some form of guidance.
    /// </summary>
    BasicInstructions,

    /// <summary>
    /// Return empty instructions when template processing fails.
    /// This avoids potentially incorrect fallback instructions.
    /// </summary>
    EmptyInstructions,

    /// <summary>
    /// Return an error message explaining that instruction generation failed.
    /// This is useful for debugging template issues.
    /// </summary>
    ErrorMessage,

    /// <summary>
    /// Use the manually configured instructions as fallback.
    /// This provides a reliable backup when templates fail.
    /// </summary>
    ManualInstructions
}