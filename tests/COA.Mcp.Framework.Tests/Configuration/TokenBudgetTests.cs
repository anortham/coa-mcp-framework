using System;
using NUnit.Framework;
using COA.Mcp.Framework.Configuration;
using COA.Mcp.Framework.Interfaces;

namespace COA.Mcp.Framework.Tests.Configuration;

[TestFixture]
public class TokenBudgetTests
{
    [Test]
    public void TokenBudgetConfiguration_HasCorrectDefaults()
    {
        var config = new TokenBudgetConfiguration();
        
        Assert.That(config.MaxTokens, Is.EqualTo(10000));
        Assert.That(config.WarningThreshold, Is.EqualTo(8000));
        Assert.That(config.Strategy, Is.EqualTo(TokenLimitStrategy.Warn));
        Assert.That(config.IncludeSystemPrompts, Is.True);
        Assert.That(config.EstimationMultiplier, Is.EqualTo(1.2));
    }

    [Test]
    public void TokenBudgetRegistry_ReturnsToolSpecificBudget()
    {
        var registry = new TokenBudgetRegistry();
        registry.ForTool("TestTool")
            .MaxTokens(5000)
            .WarningThreshold(4000)
            .Apply();
        
        var budget = registry.GetBudget("TestTool", ToolCategory.General);
        
        Assert.That(budget.MaxTokens, Is.EqualTo(5000));
        Assert.That(budget.WarningThreshold, Is.EqualTo(4000));
    }

    [Test]
    public void TokenBudgetRegistry_ReturnsCategoryBudget_WhenNoToolSpecific()
    {
        var registry = new TokenBudgetRegistry();
        registry.ForCategory(ToolCategory.Analysis)
            .MaxTokens(20000)
            .WithStrategy(TokenLimitStrategy.Truncate)
            .Apply();
        
        var budget = registry.GetBudget("AnalysisTool", ToolCategory.Analysis);
        
        Assert.That(budget.MaxTokens, Is.EqualTo(20000));
        Assert.That(budget.Strategy, Is.EqualTo(TokenLimitStrategy.Truncate));
    }

    [Test]
    public void TokenBudgetRegistry_ReturnsDefaultBudget_WhenNoSpecificConfig()
    {
        var registry = new TokenBudgetRegistry();
        registry.Default()
            .MaxTokens(15000)
            .WarningThreshold(12000)
            .Apply();
        
        var budget = registry.GetBudget("UnknownTool", ToolCategory.General);
        
        Assert.That(budget.MaxTokens, Is.EqualTo(15000));
        Assert.That(budget.WarningThreshold, Is.EqualTo(12000));
    }

    [Test]
    public void TokenBudgetRegistry_PrioritizesToolOverCategory()
    {
        var registry = new TokenBudgetRegistry();
        
        registry.ForCategory(ToolCategory.General)
            .MaxTokens(10000)
            .Apply();
            
        registry.ForTool("SpecialTool")
            .MaxTokens(5000)
            .Apply();
        
        var budget = registry.GetBudget("SpecialTool", ToolCategory.General);
        
        Assert.That(budget.MaxTokens, Is.EqualTo(5000));
    }

    [Test]
    public void TokenBudgetBuilder_ConfiguresAllProperties()
    {
        var registry = new TokenBudgetRegistry();
        
        registry.ForTool("TestTool")
            .MaxTokens(7500)
            .WarningThreshold(6000)
            .WithStrategy(TokenLimitStrategy.Throw)
            .IncludeSystemPrompts(false)
            .EstimationMultiplier(1.5)
            .Apply();
        
        var budget = registry.GetBudget("TestTool", ToolCategory.General);
        
        Assert.That(budget.MaxTokens, Is.EqualTo(7500));
        Assert.That(budget.WarningThreshold, Is.EqualTo(6000));
        Assert.That(budget.Strategy, Is.EqualTo(TokenLimitStrategy.Throw));
        Assert.That(budget.IncludeSystemPrompts, Is.False);
        Assert.That(budget.EstimationMultiplier, Is.EqualTo(1.5));
    }

    [Test]
    public void TokenLimitStrategy_HasCorrectValues()
    {
        Assert.That(Enum.IsDefined(typeof(TokenLimitStrategy), TokenLimitStrategy.Warn));
        Assert.That(Enum.IsDefined(typeof(TokenLimitStrategy), TokenLimitStrategy.Throw));
        Assert.That(Enum.IsDefined(typeof(TokenLimitStrategy), TokenLimitStrategy.Truncate));
        Assert.That(Enum.IsDefined(typeof(TokenLimitStrategy), TokenLimitStrategy.Ignore));
    }
}