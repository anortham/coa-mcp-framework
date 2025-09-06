using System;

namespace COA.Mcp.Framework.Interfaces;

/// <summary>
/// Defines priority and scenario-based guidance for MCP tools.
/// This interface allows tools to specify when they should be preferred and provides
/// context-aware recommendations without enforcing strict workflows.
/// </summary>
public interface IToolPriority
{
    /// <summary>
    /// Gets the priority level for this tool.
    /// Higher values indicate higher priority when multiple tools could handle the same task.
    /// </summary>
    /// <value>
    /// Priority values: 1-100 (1=lowest priority, 100=highest priority)
    /// Default priority should be 50 for most tools.
    /// </value>
    int Priority { get; }

    /// <summary>
    /// Gets the scenario where this tool is most effective.
    /// This helps Claude understand when to prefer this tool over alternatives.
    /// </summary>
    /// <value>
    /// Examples: "type_verification", "bulk_search", "navigation", "code_generation"
    /// Can be null if the tool doesn't have a specific preferred scenario.
    /// </value>
    string? PreferredForScenario { get; }

    /// <summary>
    /// Gets an array of tool names that this tool can serve as an alternative to.
    /// This creates gentle guidance without blocking access to other tools.
    /// </summary>
    /// <value>
    /// Array of tool names, or null/empty if no alternatives are suggested.
    /// Example: ["file_search", "text_search"] for a more efficient symbol search tool.
    /// </value>
    string[]? AlternativeTo { get; }

    /// <summary>
    /// Gets a brief explanation of why this tool should be preferred in certain situations.
    /// This provides educational context without being manipulative.
    /// </summary>
    /// <value>
    /// Professional explanation focusing on efficiency and accuracy benefits.
    /// Should be 1-2 sentences maximum.
    /// </value>
    string? Rationale { get; }
}