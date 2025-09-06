using System;

namespace COA.Mcp.Framework.Configuration;

/// <summary>
/// Represents a professional workflow suggestion that guides users toward more efficient
/// tool usage patterns without being manipulative or restrictive.
/// </summary>
public class WorkflowSuggestion
{
    /// <summary>
    /// Gets or sets the scenario or task type this workflow applies to.
    /// </summary>
    /// <value>
    /// Examples: "code_generation", "type_verification", "debugging", "refactoring"
    /// Should be specific enough to be actionable but general enough to be reusable.
    /// </value>
    public string Scenario { get; set; } = null!;

    /// <summary>
    /// Gets or sets the recommended sequence of tools for this scenario.
    /// Tools are listed in suggested order of execution for optimal results.
    /// </summary>
    /// <value>
    /// Array of tool names in recommended execution order.
    /// Example: ["index_workspace", "symbol_search", "goto_definition", "text_search"]
    /// </value>
    public string[] RecommendedToolOrder { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the professional rationale explaining why this workflow is recommended.
    /// This should focus on technical benefits and efficiency gains.
    /// </summary>
    /// <value>
    /// Professional explanation that educates users about the benefits of this approach.
    /// Should be 2-3 sentences maximum and avoid emotional manipulation.
    /// Examples focus on: accuracy, performance, reduced iterations, better results.
    /// </value>
    public string Rationale { get; set; } = null!;

    /// <summary>
    /// Gets or sets the expected efficiency improvement when following this workflow.
    /// This provides measurable benefits to justify the recommendation.
    /// </summary>
    /// <value>
    /// Examples: "30% fewer tokens", "50% fewer errors", "3x faster results"
    /// Should be realistic and measurable. Null if no specific metric is available.
    /// </value>
    public string? ExpectedBenefit { get; set; }

    /// <summary>
    /// Gets or sets alternative tools that could be used but are less optimal.
    /// This acknowledges other approaches without forbidding them.
    /// </summary>
    /// <value>
    /// Array of tool names that could handle the scenario but with lower efficiency.
    /// Used for educational comparison, not restriction.
    /// </value>
    public string[]? AlternativeTools { get; set; }

    /// <summary>
    /// Gets or sets the minimum number of tools from the recommended order that should be used
    /// to achieve meaningful benefits from this workflow.
    /// </summary>
    /// <value>
    /// Integer indicating how many tools from RecommendedToolOrder provide the core benefit.
    /// Default is 1 (using any recommended tool is better than none).
    /// </value>
    public int MinimumToolsForBenefit { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether this workflow suggestion should be prominently featured
    /// in instructions or tool descriptions.
    /// </summary>
    /// <value>
    /// True for high-impact workflows that provide significant benefits.
    /// False for specialized or context-specific workflows.
    /// </value>
    public bool IsHighImpact { get; set; } = false;

    /// <summary>
    /// Creates a new workflow suggestion with the specified scenario and tool order.
    /// </summary>
    /// <param name="scenario">The scenario this workflow applies to.</param>
    /// <param name="recommendedTools">The recommended tool execution order.</param>
    /// <param name="rationale">The professional rationale for this workflow.</param>
    public WorkflowSuggestion(string scenario, string[] recommendedTools, string rationale)
    {
        Scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
        RecommendedToolOrder = recommendedTools ?? throw new ArgumentNullException(nameof(recommendedTools));
        Rationale = rationale ?? throw new ArgumentNullException(nameof(rationale));
    }

    /// <summary>
    /// Parameterless constructor for serialization and configuration binding.
    /// </summary>
    public WorkflowSuggestion()
    {
    }
}