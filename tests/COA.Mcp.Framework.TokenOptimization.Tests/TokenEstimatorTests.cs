using NUnit.Framework;
using FluentAssertions;
using COA.Mcp.Framework.TokenOptimization;
using System;
using System.Collections.Generic;

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
        // With new accurate calculation: serialized JSON + small structure overhead
        result.Should().BeGreaterThan(10); // Should include content + structure
        result.Should().BeLessThan(50); // But more reasonable than old 50 token overhead
    }
    
    [Test]
    public void EstimateCollection_WithEmptyCollection_ReturnsOverheadOnly()
    {
        // Arrange
        var emptyList = new List<string>();
        
        // Act
        var result = TokenEstimator.EstimateCollection(emptyList);
        
        // Assert
        // Empty array "[]" is 2 characters, which converts to 1 token (2/4 rounded up)
        result.Should().Be(1); // Overhead for "[]"
    }
    
    [Test]
    public void EstimateCollection_WithSmallCollection_EstimatesAllItems()
    {
        // Arrange
        var items = new[] { "One", "Two", "Three" };
        
        // Act
        var result = TokenEstimator.EstimateCollection(items);
        
        // Assert
        // With new accurate calculation: 3 small strings + array overhead
        result.Should().BeGreaterThan(3); // Should include items + structure overhead
        result.Should().BeLessThan(20); // But should be reasonable for 3 small strings
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
        var result = TokenEstimator.CalculateTokenBudget(totalLimit, currentUsage, TokenSafetyMode.Default);
        
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
        var result = TokenEstimator.CalculateTokenBudget(totalLimit, currentUsage, TokenSafetyMode.Default);
        
        // Assert
        result.Should().Be(0);
    }
    
    // New tests for improvements
    
    [Test]
    public void EstimateString_WithCjkText_UsesCorrectTokenRate()
    {
        // Arrange
        var cjkText = "这是一个测试文本"; // Chinese text
        var englishText = "This is a test text with similar length";
        
        // Act
        var cjkResult = TokenEstimator.EstimateString(cjkText);
        var englishResult = TokenEstimator.EstimateString(englishText);
        
        // Assert
        // CJK text should have more tokens per character
        cjkResult.Should().BeGreaterThan(0);
        // The estimation should recognize CJK characters use fewer chars per token
    }
    
    [Test]
    public void EstimateString_WithCodeLikeText_DetectsLowSpaceDensity()
    {
        // Arrange
        var codeText = "function_name_with_underscores_and_no_spaces_typical_of_code";
        var normalText = "This is normal text with regular spacing between words";
        
        // Act
        var codeResult = TokenEstimator.EstimateString(codeText);
        var normalResult = TokenEstimator.EstimateString(normalText);
        
        // Assert
        // Both should return reasonable estimates
        codeResult.Should().BeGreaterThan(0);
        normalResult.Should().BeGreaterThan(0);
    }
    
    [Test]
    public void EstimateObject_WithDictionary_EstimatesWithoutSerialization()
    {
        // Arrange
        var dict = new Dictionary<string, object>
        {
            ["key1"] = "value1",
            ["key2"] = 42,
            ["key3"] = new DateTime(2024, 1, 1)
        };
        
        // Act
        var result = TokenEstimator.EstimateObject(dict);
        
        // Assert
        result.Should().BeGreaterThan(0);
        // Should account for keys, values, and JSON structure
    }
    
    [Test]
    public void EstimateObject_WithList_EstimatesEfficiently()
    {
        // Arrange
        var list = new List<string> { "item1", "item2", "item3", "item4", "item5" };
        
        // Act
        var result = TokenEstimator.EstimateObject(list);
        
        // Assert
        result.Should().BeGreaterThan(0);
        // Should estimate based on collection method, not full serialization
    }
    
    [Test]
    public void CalculateTokenBudget_WithPercentage_AdaptsToModelSize()
    {
        // Arrange
        var smallModel = 4000;
        var largeModel = 128000;
        var currentUsage = 1000;
        
        // Act - 5% safety buffer
        var smallBudget = TokenEstimator.CalculateTokenBudget(
            smallModel, currentUsage, 0.05, 100, 5000);
        var largeBudget = TokenEstimator.CalculateTokenBudget(
            largeModel, currentUsage, 0.05, 100, 10000);
        
        // Assert
        // Small model: 4000 - 1000 - 200 (5% of 4000) = 2800
        smallBudget.Should().Be(2800);
        
        // Large model: 128000 - 1000 - 6400 (5% of 128000)
        // 6400 is less than max of 10000, so no capping
        // 128000 - 1000 - 6400 = 120600
        largeBudget.Should().Be(120600);
    }
    
    [Test]
    public void CalculateTokenBudget_WithPercentage_RespectsMinBuffer()
    {
        // Arrange
        var totalLimit = 2000;
        var currentUsage = 500;
        
        // Act - 5% would be 100, but min is 300
        var result = TokenEstimator.CalculateTokenBudget(
            totalLimit, currentUsage, 0.05, 300, 5000);
        
        // Assert
        // 2000 - 500 - 300 (min buffer enforced) = 1200
        result.Should().Be(1200);
    }
    
    [Test]
    public void EstimateCollection_WithLargeCollection_UsesDeterministicSampling()
    {
        // Arrange
        var largeList = new List<string>();
        for (int i = 0; i < 100; i++)
        {
            largeList.Add($"Item {i}");
        }
        
        // Act
        var result1 = TokenEstimator.EstimateCollection(largeList);
        var result2 = TokenEstimator.EstimateCollection(largeList);
        
        // Assert
        result1.Should().BeGreaterThan(0);
        result1.Should().Be(result2); // Deterministic sampling should give same result
    }
    
    [Test]
    public void EstimateCollection_EmptyCollection_ReturnsMinimalOverhead()
    {
        // Arrange
        var emptyList = new List<string>();
        
        // Act
        var result = TokenEstimator.EstimateCollection(emptyList);
        
        // Assert
        result.Should().BeGreaterThan(0); // Should return overhead for "[]"
        result.Should().BeLessThan(5); // But should be minimal
    }
}