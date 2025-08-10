using System;

namespace COA.Mcp.Framework.Configuration;

/// <summary>
/// Configuration for token budget management in MCP tools.
/// </summary>
public class TokenBudgetConfiguration
{
    /// <summary>
    /// Gets or sets the maximum number of tokens allowed for this tool.
    /// Default is 10000.
    /// </summary>
    public int MaxTokens { get; set; } = 10000;

    /// <summary>
    /// Gets or sets the token count at which to emit a warning.
    /// Default is 80% of MaxTokens.
    /// </summary>
    public int WarningThreshold { get; set; } = 8000;

    /// <summary>
    /// Gets or sets the strategy to use when token limits are exceeded.
    /// </summary>
    public TokenLimitStrategy Strategy { get; set; } = TokenLimitStrategy.Warn;

    /// <summary>
    /// Gets or sets whether to include system prompts in token counting.
    /// </summary>
    public bool IncludeSystemPrompts { get; set; } = true;

    /// <summary>
    /// Gets or sets a multiplier for token estimation accuracy.
    /// Use values > 1.0 for conservative estimates.
    /// </summary>
    public double EstimationMultiplier { get; set; } = 1.2;
}

/// <summary>
/// Strategies for handling token limit violations.
/// </summary>
public enum TokenLimitStrategy
{
    /// <summary>
    /// Log a warning and continue execution.
    /// </summary>
    Warn,

    /// <summary>
    /// Throw an exception to prevent execution.
    /// </summary>
    Throw,

    /// <summary>
    /// Truncate output to stay within limits.
    /// </summary>
    Truncate,

    /// <summary>
    /// Ignore token limits (not recommended for production).
    /// </summary>
    Ignore
}