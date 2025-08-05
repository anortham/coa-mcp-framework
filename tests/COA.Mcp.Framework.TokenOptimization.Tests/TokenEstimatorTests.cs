using NUnit.Framework;
using FluentAssertions;
using COA.Mcp.Framework.TokenOptimization;

namespace COA.Mcp.Framework.TokenOptimization.Tests;

[TestFixture]
public class TokenEstimatorTests
{
    [Test]
    public void EstimateString_WithNullString_ReturnsZero()
    {
        // Act
        var result = TokenEstimator.EstimateString(null);
        
        // Assert
        result.Should().Be(0);
    }
    
    [Test]
    public void EstimateString_WithEmptyString_ReturnsZero()
    {
        // Act
        var result = TokenEstimator.EstimateString(string.Empty);
        
        // Assert
        result.Should().Be(0);
    }
    
    [Test]
    public void EstimateString_WithSimpleText_ReturnsReasonableEstimate()
    {
        // Arrange
        var text = "The quick brown fox jumps over the lazy dog.";
        
        // Act
        var result = TokenEstimator.EstimateString(text);
        
        // Assert
        result.Should().BeGreaterThan(5).And.BeLessThan(20);
    }
    
    [Test]
    public void EstimateString_WithWhitespace_NormalizesAndEstimates()
    {
        // Arrange
        var textWithSpaces = "Hello    World   !";
        var normalizedText = "Hello World !";
        
        // Act
        var resultWithSpaces = TokenEstimator.EstimateString(textWithSpaces);
        var resultNormalized = TokenEstimator.EstimateString(normalizedText);
        
        // Assert
        resultWithSpaces.Should().Be(resultNormalized);
    }
    
    [Test]
    public void EstimateObject_WithNull_ReturnsZero()
    {
        // Act
        var result = TokenEstimator.EstimateObject(null);
        
        // Assert
        result.Should().Be(0);
    }
    
    [Test]
    public void EstimateObject_WithPrimitiveTypes_EstimatesCorrectly()
    {
        // Arrange
        var testCases = new object[]
        {
            42,
            3.14159,
            true,
            DateTime.UtcNow,
            Guid.NewGuid()
        };
        
        // Act & Assert
        foreach (var testCase in testCases)
        {
            var result = TokenEstimator.EstimateObject(testCase);
            result.Should().BeGreaterThan(0, $"because {testCase} should have token count");
        }
    }
    
    [Test]
    public void EstimateObject_WithComplexObject_IncludesStructureOverhead()
    {
        // Arrange
        var complexObject = new
        {
            Name = "Test",
            Value = 123,
            Items = new[] { "A", "B", "C" }
        };
        
        // Act
        var result = TokenEstimator.EstimateObject(complexObject);
        
        // Assert
        result.Should().BeGreaterThan(50); // Should include JSON structure overhead
    }
    
    [Test]
    public void EstimateCollection_WithEmptyCollection_ReturnsOverheadOnly()
    {
        // Arrange
        var emptyList = new List<string>();
        
        // Act
        var result = TokenEstimator.EstimateCollection(emptyList);
        
        // Assert
        result.Should().Be(50); // JSON structure overhead only
    }
    
    [Test]
    public void EstimateCollection_WithSmallCollection_EstimatesAllItems()
    {
        // Arrange
        var items = new[] { "One", "Two", "Three" };
        
        // Act
        var result = TokenEstimator.EstimateCollection(items);
        
        // Assert
        result.Should().BeGreaterThan(50); // Should be more than just overhead
    }
    
    [Test]
    public void EstimateCollection_WithLargeCollection_UsesSampling()
    {
        // Arrange
        var largeCollection = Enumerable.Range(1, 100).Select(i => $"Item {i}").ToList();
        
        // Act
        var startTime = DateTime.UtcNow;
        var result = TokenEstimator.EstimateCollection(largeCollection);
        var duration = DateTime.UtcNow - startTime;
        
        // Assert
        result.Should().BeGreaterThan(100); // Should estimate reasonable token count
        duration.TotalMilliseconds.Should().BeLessThan(100); // Should be fast due to sampling
    }
    
    [Test]
    public void ApplyProgressiveReduction_WithinLimit_ReturnsAllItems()
    {
        // Arrange
        var items = new[] { "A", "B", "C" };
        var tokenLimit = 1000; // High limit
        
        // Act
        var result = TokenEstimator.ApplyProgressiveReduction(
            items,
            item => TokenEstimator.EstimateString(item),
            tokenLimit);
        
        // Assert
        result.Should().HaveCount(3);
        result.Should().BeEquivalentTo(items);
    }
    
    [Test]
    public void ApplyProgressiveReduction_ExceedsLimit_ReducesItems()
    {
        // Arrange
        var items = Enumerable.Range(1, 100).Select(i => $"Long item text {i}").ToList();
        var tokenLimit = 100; // Low limit
        
        // Act
        var result = TokenEstimator.ApplyProgressiveReduction(
            items,
            item => TokenEstimator.EstimateString(item),
            tokenLimit);
        
        // Assert
        result.Should().HaveCountLessThan(100);
        result.Should().HaveCountGreaterThanOrEqualTo(1);
    }
    
    [Test]
    public void CalculateTokenBudget_WithDefaultSafety_ReturnsCorrectBudget()
    {
        // Arrange
        var totalLimit = 200000;
        var currentUsage = 50000;
        
        // Act
        var result = TokenEstimator.CalculateTokenBudget(totalLimit, currentUsage);
        
        // Assert
        result.Should().Be(140000); // 200000 - 50000 - 10000 (default safety)
    }
    
    [Test]
    public void CalculateTokenBudget_WithConservativeSafety_ReturnsReducedBudget()
    {
        // Arrange
        var totalLimit = 200000;
        var currentUsage = 50000;
        
        // Act
        var result = TokenEstimator.CalculateTokenBudget(
            totalLimit, 
            currentUsage, 
            TokenSafetyMode.Conservative);
        
        // Assert
        result.Should().Be(145000); // 200000 - 50000 - 5000 (conservative safety)
    }
    
    [Test]
    public void CalculateTokenBudget_ExceedsLimit_ReturnsZero()
    {
        // Arrange
        var totalLimit = 10000;
        var currentUsage = 15000; // Already exceeded
        
        // Act
        var result = TokenEstimator.CalculateTokenBudget(totalLimit, currentUsage);
        
        // Assert
        result.Should().Be(0);
    }
}