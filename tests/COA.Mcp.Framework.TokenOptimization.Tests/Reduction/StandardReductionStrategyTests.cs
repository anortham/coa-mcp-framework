using NUnit.Framework;
using FluentAssertions;
using COA.Mcp.Framework.TokenOptimization.Reduction;

namespace COA.Mcp.Framework.TokenOptimization.Tests.Reduction;

[TestFixture]
public class StandardReductionStrategyTests
{
    private StandardReductionStrategy _strategy;
    
    [SetUp]
    public void SetUp()
    {
        _strategy = new StandardReductionStrategy();
    }
    
    [Test]
    public void Name_ReturnsStandard()
    {
        _strategy.Name.Should().Be("standard");
    }
    
    [Test]
    public void Reduce_WithEmptyList_ReturnsEmptyResult()
    {
        // Arrange
        var items = new List<string>();
        
        // Act
        var result = _strategy.Reduce(items, s => 10, 1000);
        
        // Assert
        result.Items.Should().BeEmpty();
        result.OriginalCount.Should().Be(0);
        result.WasTruncated.Should().BeFalse();
    }
    
    [Test]
    public void Reduce_ItemsFitWithinLimit_ReturnsAllItems()
    {
        // Arrange
        var items = new List<string> { "A", "B", "C" };
        
        // Act
        var result = _strategy.Reduce(items, s => 10, 1000);
        
        // Assert
        result.Items.Should().HaveCount(3);
        result.Items.Should().BeEquivalentTo(items);
        result.WasTruncated.Should().BeFalse();
        result.Metadata["percentage_retained"].Should().Be(100);
    }
    
    [Test]
    public void Reduce_RequiresReduction_AppliesStandardSteps()
    {
        // Arrange
        var items = Enumerable.Range(1, 100).Select(i => $"Item{i}").ToList();
        var tokenLimit = 500; // Forces reduction
        
        // Act
        var result = _strategy.Reduce(items, s => 10, tokenLimit);
        
        // Assert
        result.Items.Should().HaveCountLessThan(100);
        result.WasTruncated.Should().BeTrue();
        result.ReductionPercentage.Should().BeGreaterThan(0);
        result.Metadata.Should().ContainKey("percentage_retained");
    }
    
    [Test]
    public void Reduce_WithCustomReductionSteps_UsesCustomSteps()
    {
        // Arrange
        var items = Enumerable.Range(1, 100).Select(i => $"Item{i}").ToList();
        var context = new ReductionContext
        {
            Metadata = { ["reduction_steps"] = new[] { 50, 25, 10 } }
        };
        
        // Act
        var result = _strategy.Reduce(items, s => 10, 600, context);
        
        // Assert
        // With more accurate token estimation, 50% now fits within the limit
        result.Items.Should().HaveCount(50); // 50% fits within token limit
        result.Metadata["percentage_retained"].Should().Be(50);
    }
    
    [Test]
    public void Reduce_NothingFits_ReturnsOneItem()
    {
        // Arrange
        var items = new List<string> { "VeryLongItem", "AnotherLongItem" };
        var tokenLimit = 5; // Very low limit
        
        // Act
        var result = _strategy.Reduce(items, s => 100, tokenLimit);
        
        // Assert
        result.Items.Should().HaveCount(1);
        result.Metadata["forced_single_item"].Should().Be(true);
    }
}