using System;

namespace COA.Mcp.Framework.Configuration;

/// <summary>
/// Configuration options for the tool management system that provides professional
/// guidance and workflow suggestions without manipulative language.
/// </summary>
public class ToolManagementConfiguration
{
    /// <summary>
    /// Gets or sets whether to use the default tool description provider.
    /// This enables context-aware tool descriptions based on available capabilities.
    /// </summary>
    /// <value>Default is true for professional tool guidance.</value>
    public bool UseDefaultDescriptionProvider { get; set; } = true;

    /// <summary>
    /// Gets or sets whether workflow suggestions are enabled.
    /// This provides professional guidance about optimal tool usage patterns.
    /// </summary>
    /// <value>Default is true to help users understand efficient workflows.</value>
    public bool EnableWorkflowSuggestions { get; set; } = true;

    /// <summary>
    /// Gets or sets whether tools can specify priority levels for scenario-based recommendations.
    /// This allows gentle guidance toward more efficient tools without blocking alternatives.
    /// </summary>
    /// <value>Default is true for intelligent tool prioritization.</value>
    public bool EnableToolPrioritySystem { get; set; } = true;

    /// <summary>
    /// Gets or sets the default priority level for tools that don't specify their own priority.
    /// Priority ranges from 1 (lowest) to 100 (highest), with 50 being neutral.
    /// </summary>
    /// <value>Default is 50 for balanced prioritization.</value>
    public int DefaultToolPriority { get; set; } = 50;

    /// <summary>
    /// Gets or sets whether to include alternative tool suggestions in descriptions.
    /// This provides educational context about different approaches without forbidding them.
    /// </summary>
    /// <value>Default is true for comprehensive guidance.</value>
    public bool IncludeAlternativeToolSuggestions { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of workflow suggestions to include in generated instructions.
    /// This prevents instruction bloat while providing valuable guidance.
    /// </summary>
    /// <value>Default is 5 for focused guidance without overwhelming users.</value>
    public int MaxWorkflowSuggestionsInInstructions { get; set; } = 5;

    /// <summary>
    /// Gets or sets whether to emphasize high-impact workflows in generated instructions.
    /// High-impact workflows provide the most significant efficiency improvements.
    /// </summary>
    /// <value>Default is true to prioritize the most beneficial guidance.</value>
    public bool EmphasizeHighImpactWorkflows { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include expected benefits in workflow descriptions.
    /// This provides measurable justification for workflow recommendations.
    /// </summary>
    /// <value>Default is true for evidence-based guidance.</value>
    public bool IncludeExpectedBenefits { get; set; } = true;
}