using Microsoft.CodeAnalysis;

namespace COA.Mcp.Framework.Migration.Analyzers;

/// <summary>
/// Interface for analyzing specific patterns in MCP projects
/// </summary>
public interface IPatternAnalyzer
{
    /// <summary>
    /// Analyzes a syntax tree for specific patterns that need migration
    /// </summary>
    Task<List<PatternMatch>> AnalyzeAsync(SyntaxNode root, string filePath);
}