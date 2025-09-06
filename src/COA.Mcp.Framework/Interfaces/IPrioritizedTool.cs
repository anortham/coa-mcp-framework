using System;

namespace COA.Mcp.Framework.Interfaces;

/// <summary>
/// Marker interface for tools that specify their priority level.
/// Higher priority tools are emphasized in behavioral guidance.
/// </summary>
public interface IPrioritizedTool : IToolMarker
{
    /// <summary>
    /// Gets the tool's priority level (1-100, where 100 is highest).
    /// Used for generating tool usage recommendations in templates.
    /// </summary>
    int Priority { get; }
    
    /// <summary>
    /// Gets the scenarios where this tool should be prioritized.
    /// Examples: "type_verification", "code_exploration", "refactoring"
    /// </summary>
    string[] PreferredScenarios { get; }
}