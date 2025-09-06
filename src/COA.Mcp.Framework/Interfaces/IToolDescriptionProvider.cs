using System;

namespace COA.Mcp.Framework.Interfaces;

/// <summary>
/// Provides context-aware tool descriptions that can be customized based on available tools,
/// user patterns, or specific scenarios. This enables more targeted guidance without
/// manipulative language.
/// </summary>
public interface IToolDescriptionProvider
{
    /// <summary>
    /// Gets a context-enhanced description for the specified tool.
    /// This allows dynamic description modification based on available capabilities
    /// and usage context.
    /// </summary>
    /// <param name="toolName">The name of the tool to describe.</param>
    /// <param name="context">Optional context information for customizing the description.</param>
    /// <returns>
    /// Enhanced description or null to use the default tool description.
    /// Should focus on technical benefits and use cases rather than emotional language.
    /// </returns>
    string? GetEnhancedDescription(string toolName, ToolDescriptionContext? context = null);

    /// <summary>
    /// Registers a description override for a specific tool in a given context.
    /// This allows runtime customization of tool descriptions based on user needs.
    /// </summary>
    /// <param name="toolName">The tool name to override.</param>
    /// <param name="description">The enhanced description to use.</param>
    /// <param name="context">Optional context where this override applies.</param>
    void RegisterDescriptionOverride(string toolName, string description, string? context = null);

    /// <summary>
    /// Determines if a tool description should be enhanced based on current context
    /// and usage patterns. This allows selective enhancement without overwhelming users.
    /// </summary>
    /// <param name="toolName">The tool name to check.</param>
    /// <param name="context">The current context.</param>
    /// <returns>True if the description should be enhanced, false to use default.</returns>
    bool ShouldEnhanceDescription(string toolName, ToolDescriptionContext? context = null);
}

/// <summary>
/// Provides context information for tool description enhancement.
/// This allows descriptions to be tailored based on available capabilities
/// and current usage scenarios.
/// </summary>
public class ToolDescriptionContext
{
    /// <summary>
    /// Gets or sets the names of all available tools in the current session.
    /// This allows descriptions to reference complementary or alternative tools.
    /// </summary>
    public string[] AvailableTools { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the current scenario or workflow context.
    /// Examples: "code_generation", "debugging", "refactoring", "exploration"
    /// </summary>
    public string? Scenario { get; set; }

    /// <summary>
    /// Gets or sets the user's apparent expertise level based on tool usage patterns.
    /// This allows descriptions to be more or less detailed as appropriate.
    /// Values: "beginner", "intermediate", "expert"
    /// </summary>
    public string? ExpertiseLevel { get; set; }

    /// <summary>
    /// Gets or sets custom context variables for advanced description customization.
    /// This extensibility point allows for future enhancements without interface changes.
    /// </summary>
    public Dictionary<string, object> CustomContext { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets whether the user prefers concise or detailed descriptions.
    /// This personalization helps balance guidance with brevity.
    /// </summary>
    public bool PreferConciseDescriptions { get; set; } = false;
}