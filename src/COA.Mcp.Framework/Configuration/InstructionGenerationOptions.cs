using System;

namespace COA.Mcp.Framework.Configuration;

/// <summary>
/// Configuration options for automatic instruction generation based on available tools
/// and their capabilities. This enables dynamic behavioral guidance without manual writing.
/// </summary>
public class InstructionGenerationOptions
{
    /// <summary>
    /// Gets or sets whether to generate instructions automatically based on available tools.
    /// When enabled, instructions are built dynamically from tool priorities and workflows.
    /// </summary>
    /// <value>Default is true for automatic professional guidance.</value>
    public bool EnableAutomaticGeneration { get; set; } = true;

    /// <summary>
    /// Gets or sets the title prefix for generated instructions.
    /// This appears at the top of the generated instruction text.
    /// </summary>
    /// <value>Default provides professional server identification.</value>
    public string InstructionTitle { get; set; } = "## Professional Tool Server";

    /// <summary>
    /// Gets or sets whether to include a general introduction in generated instructions.
    /// The introduction explains the purpose and benefits of following the guidance.
    /// </summary>
    /// <value>Default is true for context setting.</value>
    public bool IncludeIntroduction { get; set; } = true;

    /// <summary>
    /// Gets or sets the introduction text for generated instructions.
    /// This provides context about why the guidance is beneficial.
    /// </summary>
    /// <value>Default focuses on efficiency and accuracy benefits.</value>
    public string IntroductionText { get; set; } = 
        "This server provides high-performance tools designed for optimal efficiency and accuracy. " +
        "Following these recommendations reduces debugging iterations and improves results.";

    /// <summary>
    /// Gets or sets whether to include workflow suggestions in generated instructions.
    /// This adds professional guidance about optimal tool usage patterns.
    /// </summary>
    /// <value>Default is true for comprehensive guidance.</value>
    public bool IncludeWorkflowSuggestions { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include tool priority information in generated instructions.
    /// This helps Claude understand which tools are most effective for specific scenarios.
    /// </summary>
    /// <value>Default is true for intelligent tool selection.</value>
    public bool IncludeToolPriorities { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include expected benefits in the generated instructions.
    /// This provides measurable justification for following the recommendations.
    /// </summary>
    /// <value>Default is true for evidence-based guidance.</value>
    public bool IncludeExpectedBenefits { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include alternative tool mentions in generated instructions.
    /// This acknowledges other approaches while explaining why the recommended tools are preferred.
    /// </summary>
    /// <value>Default is false to keep instructions focused, but can be enabled for comprehensive guidance.</value>
    public bool IncludeAlternativeTools { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum length of generated instructions in characters.
    /// This prevents instruction bloat while maintaining useful guidance.
    /// </summary>
    /// <value>Default is 2000 characters for concise but comprehensive guidance.</value>
    public int MaxInstructionLength { get; set; } = 2000;

    /// <summary>
    /// Gets or sets the tone for generated instructions.
    /// This controls the language style used in the generated text.
    /// </summary>
    /// <value>Default is "Professional" for technical, factual guidance.</value>
    public InstructionTone Tone { get; set; } = InstructionTone.Professional;

    /// <summary>
    /// Gets or sets whether to include performance metrics in generated instructions.
    /// This adds specific numbers about efficiency improvements when available.
    /// </summary>
    /// <value>Default is true for evidence-based recommendations.</value>
    public bool IncludePerformanceMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets custom text to append to generated instructions.
    /// This allows server-specific guidance to be added to the standard generation.
    /// </summary>
    /// <value>Default is empty for standard generation only.</value>
    public string? CustomAppendText { get; set; }

    /// <summary>
    /// Gets or sets the minimum tool count required for workflow inclusion.
    /// Workflows are only included if this many recommended tools are available.
    /// </summary>
    /// <value>Default is 2 to ensure meaningful workflow guidance.</value>
    public int MinimumToolsForWorkflowInclusion { get; set; } = 2;
}

/// <summary>
/// Defines the tone and language style for generated instructions.
/// </summary>
public enum InstructionTone
{
    /// <summary>
    /// Professional, technical tone focusing on efficiency and accuracy.
    /// Uses factual language and measurable benefits.
    /// </summary>
    Professional,

    /// <summary>
    /// Casual, friendly tone while maintaining technical accuracy.
    /// More conversational but still professional.
    /// </summary>
    Friendly,

    /// <summary>
    /// Concise, minimal tone with essential information only.
    /// Very brief recommendations without extensive explanation.
    /// </summary>
    Concise,

    /// <summary>
    /// Educational tone that explains the reasoning behind recommendations.
    /// Helps users understand not just what to do, but why.
    /// </summary>
    Educational
}