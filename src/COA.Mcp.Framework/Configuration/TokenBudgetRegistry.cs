using System;
using System.Collections.Generic;
using COA.Mcp.Framework.Interfaces;

namespace COA.Mcp.Framework.Configuration;

/// <summary>
/// Registry for configuring token budgets for tools.
/// </summary>
public class TokenBudgetRegistry
{
    private readonly Dictionary<string, TokenBudgetConfiguration> _toolBudgets = new();
    private readonly Dictionary<ToolCategory, TokenBudgetConfiguration> _categoryBudgets = new();
    private TokenBudgetConfiguration _defaultBudget = new();

    /// <summary>
    /// Configures token budget for a specific tool type.
    /// </summary>
    /// <typeparam name="TTool">The tool type to configure.</typeparam>
    /// <returns>A builder for fluent configuration.</returns>
    public TokenBudgetBuilder ForTool<TTool>() where TTool : IMcpTool
    {
        var toolName = typeof(TTool).Name;
        return new TokenBudgetBuilder(this, toolName, null);
    }

    /// <summary>
    /// Configures token budget for a specific tool by name.
    /// </summary>
    /// <param name="toolName">The name of the tool to configure.</param>
    /// <returns>A builder for fluent configuration.</returns>
    public TokenBudgetBuilder ForTool(string toolName)
    {
        return new TokenBudgetBuilder(this, toolName, null);
    }

    /// <summary>
    /// Configures token budget for all tools in a category.
    /// </summary>
    /// <param name="category">The tool category to configure.</param>
    /// <returns>A builder for fluent configuration.</returns>
    public TokenBudgetBuilder ForCategory(ToolCategory category)
    {
        return new TokenBudgetBuilder(this, null, category);
    }

    /// <summary>
    /// Configures the default token budget for all tools.
    /// </summary>
    /// <returns>A builder for fluent configuration.</returns>
    public TokenBudgetBuilder Default()
    {
        return new TokenBudgetBuilder(this, null, null);
    }

    /// <summary>
    /// Gets the token budget configuration for a specific tool.
    /// </summary>
    /// <param name="toolName">The name of the tool.</param>
    /// <param name="category">The category of the tool.</param>
    /// <returns>The token budget configuration.</returns>
    public TokenBudgetConfiguration GetBudget(string toolName, ToolCategory category)
    {
        // Priority: Tool-specific > Category > Default
        if (_toolBudgets.TryGetValue(toolName, out var toolBudget))
            return toolBudget;

        if (_categoryBudgets.TryGetValue(category, out var categoryBudget))
            return categoryBudget;

        return _defaultBudget;
    }

    /// <summary>
    /// Internal method to set a tool-specific budget.
    /// </summary>
    internal void SetToolBudget(string toolName, TokenBudgetConfiguration config)
    {
        _toolBudgets[toolName] = config;
    }

    /// <summary>
    /// Internal method to set a category budget.
    /// </summary>
    internal void SetCategoryBudget(ToolCategory category, TokenBudgetConfiguration config)
    {
        _categoryBudgets[category] = config;
    }

    /// <summary>
    /// Internal method to set the default budget.
    /// </summary>
    internal void SetDefaultBudget(TokenBudgetConfiguration config)
    {
        _defaultBudget = config;
    }
}

/// <summary>
/// Builder for fluent token budget configuration.
/// </summary>
public class TokenBudgetBuilder
{
    private readonly TokenBudgetRegistry _registry;
    private readonly string? _toolName;
    private readonly ToolCategory? _category;
    private readonly TokenBudgetConfiguration _config = new();

    internal TokenBudgetBuilder(TokenBudgetRegistry registry, string? toolName, ToolCategory? category)
    {
        _registry = registry;
        _toolName = toolName;
        _category = category;
    }

    /// <summary>
    /// Sets the maximum number of tokens.
    /// </summary>
    /// <param name="maxTokens">The maximum token count.</param>
    /// <returns>The builder for chaining.</returns>
    public TokenBudgetBuilder MaxTokens(int maxTokens)
    {
        _config.MaxTokens = maxTokens;
        return this;
    }

    /// <summary>
    /// Sets the warning threshold.
    /// </summary>
    /// <param name="threshold">The warning threshold.</param>
    /// <returns>The builder for chaining.</returns>
    public TokenBudgetBuilder WarningThreshold(int threshold)
    {
        _config.WarningThreshold = threshold;
        return this;
    }

    /// <summary>
    /// Sets the token limit strategy.
    /// </summary>
    /// <param name="strategy">The strategy to use.</param>
    /// <returns>The builder for chaining.</returns>
    public TokenBudgetBuilder WithStrategy(TokenLimitStrategy strategy)
    {
        _config.Strategy = strategy;
        return this;
    }

    /// <summary>
    /// Sets whether to include system prompts in token counting.
    /// </summary>
    /// <param name="include">Whether to include system prompts.</param>
    /// <returns>The builder for chaining.</returns>
    public TokenBudgetBuilder IncludeSystemPrompts(bool include)
    {
        _config.IncludeSystemPrompts = include;
        return this;
    }

    /// <summary>
    /// Sets the estimation multiplier for conservative estimates.
    /// </summary>
    /// <param name="multiplier">The multiplier value.</param>
    /// <returns>The builder for chaining.</returns>
    public TokenBudgetBuilder EstimationMultiplier(double multiplier)
    {
        _config.EstimationMultiplier = multiplier;
        return this;
    }

    /// <summary>
    /// Applies the configuration to the registry.
    /// </summary>
    public void Apply()
    {
        if (_toolName != null)
            _registry.SetToolBudget(_toolName, _config);
        else if (_category.HasValue)
            _registry.SetCategoryBudget(_category.Value, _config);
        else
            _registry.SetDefaultBudget(_config);
    }
}