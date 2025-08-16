using System;
using System.Collections.Generic;
using COA.Mcp.Visualization.Validation;
using FluentAssertions;
using NUnit.Framework;

namespace COA.Mcp.Visualization.Tests.Validation;

[TestFixture]
public class VisualizationValidatorTests
{
    [Test]
    public void Validate_ValidDescriptor_ShouldReturnSuccess()
    {
        // Arrange
        var descriptor = new VisualizationDescriptor
        {
            Type = StandardVisualizationTypes.SearchResults,
            Version = "1.0",
            Data = new { query = "test", results = new[] { "item1", "item2" } }
        };

        // Act
        var result = VisualizationValidator.Validate(descriptor);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Test]
    public void Validate_NullDescriptor_ShouldReturnFailure()
    {
        // Act
        var result = VisualizationValidator.Validate(null!);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Visualization descriptor cannot be null");
    }

    [Test]
    public void Validate_EmptyType_ShouldReturnFailure()
    {
        // Arrange
        var descriptor = new VisualizationDescriptor
        {
            Type = "",
            Data = new { test = "data" }
        };

        // Act
        var result = VisualizationValidator.Validate(descriptor);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Visualization type is required and cannot be empty");
    }

    [Test]
    public void Validate_NullData_ShouldReturnFailure()
    {
        // Arrange
        var descriptor = new VisualizationDescriptor
        {
            Type = "test-type",
            Data = null!
        };

        // Act
        var result = VisualizationValidator.Validate(descriptor);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Visualization data is required and cannot be null");
    }

    [TestCase("invalid_type_with_underscores")]
    [TestCase("Invalid-Type-With-Capitals")]
    [TestCase("-starts-with-hyphen")]
    [TestCase("ends-with-hyphen-")]
    [TestCase("double--hyphens")]
    [TestCase("has spaces")]
    public void Validate_InvalidTypeFormat_ShouldReturnFailure(string invalidType)
    {
        // Arrange
        var descriptor = new VisualizationDescriptor
        {
            Type = invalidType,
            Data = new { test = "data" }
        };

        // Act
        var result = VisualizationValidator.Validate(descriptor);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Invalid visualization type"));
    }

    [TestCase("search-results")]
    [TestCase("data-grid")]
    [TestCase("hierarchy")]
    [TestCase("timeline")]
    [TestCase("custom-type")]
    [TestCase("type-with-numbers-123")]
    public void Validate_ValidTypeFormat_ShouldPass(string validType)
    {
        // Arrange
        var descriptor = new VisualizationDescriptor
        {
            Type = validType,
            Data = new { test = "data" }
        };

        // Act
        var result = VisualizationValidator.Validate(descriptor);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
    }

    [TestCase("1.0")]
    [TestCase("2.1")]
    [TestCase("1.0.0")]
    [TestCase("10.5.2")]
    public void Validate_ValidVersionFormat_ShouldPass(string validVersion)
    {
        // Arrange
        var descriptor = new VisualizationDescriptor
        {
            Type = "test-type",
            Version = validVersion,
            Data = new { test = "data" }
        };

        // Act
        var result = VisualizationValidator.Validate(descriptor);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
    }

    [TestCase("1")]
    [TestCase("v1.0")]
    [TestCase("1.0.0.0")]
    [TestCase("1.a")]
    public void Validate_InvalidVersionFormat_ShouldReturnFailure(string invalidVersion)
    {
        // Arrange
        var descriptor = new VisualizationDescriptor
        {
            Type = "test-type",
            Version = invalidVersion,
            Data = new { test = "data" }
        };

        // Act
        var result = VisualizationValidator.Validate(descriptor);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Invalid version format"));
    }

    [Test]
    public void Validate_EmptyVersion_ShouldPass()
    {
        // Arrange
        var descriptor = new VisualizationDescriptor
        {
            Type = "test-type",
            Version = "", // Empty string should be treated as not specified
            Data = new { test = "data" }
        };

        // Act
        var result = VisualizationValidator.Validate(descriptor);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
    }

    [Test]
    public void ValidateHint_ValidHint_ShouldReturnSuccess()
    {
        // Arrange
        var hint = new VisualizationHint
        {
            PreferredView = "grid",
            FallbackFormat = "json",
            Interactive = true,
            MaxConcurrentTabs = 3
        };

        // Act
        var result = VisualizationValidator.ValidateHint(hint);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Test]
    public void ValidateHint_NullHint_ShouldReturnFailure()
    {
        // Act
        var result = VisualizationValidator.ValidateHint(null!);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Visualization hint cannot be null");
    }

    [TestCase("invalid-view")]
    [TestCase("custom")]
    public void ValidateHint_InvalidPreferredView_ShouldReturnFailure(string invalidView)
    {
        // Arrange
        var hint = new VisualizationHint
        {
            PreferredView = invalidView
        };

        // Act
        var result = VisualizationValidator.ValidateHint(hint);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Invalid preferred view"));
    }

    [TestCase("grid")]
    [TestCase("tree")]
    [TestCase("chart")]
    [TestCase("markdown")]
    [TestCase("timeline")]
    [TestCase("progress")]
    [TestCase("auto")]
    [TestCase("GRID")] // Should pass due to case insensitive comparison
    public void ValidateHint_ValidPreferredView_ShouldPass(string validView)
    {
        // Arrange
        var hint = new VisualizationHint
        {
            PreferredView = validView
        };

        // Act
        var result = VisualizationValidator.ValidateHint(hint);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
    }

    [TestCase("xml")]
    [TestCase("PDF")]
    [TestCase("html")]
    public void ValidateHint_InvalidFallbackFormat_ShouldReturnFailure(string invalidFormat)
    {
        // Arrange
        var hint = new VisualizationHint
        {
            FallbackFormat = invalidFormat
        };

        // Act
        var result = VisualizationValidator.ValidateHint(hint);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Invalid fallback format"));
    }

    [TestCase("json")]
    [TestCase("csv")]
    [TestCase("markdown")]
    [TestCase("text")]
    public void ValidateHint_ValidFallbackFormat_ShouldPass(string validFormat)
    {
        // Arrange
        var hint = new VisualizationHint
        {
            FallbackFormat = validFormat
        };

        // Act
        var result = VisualizationValidator.ValidateHint(hint);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(21)]
    [TestCase(100)]
    public void ValidateHint_InvalidMaxConcurrentTabs_ShouldReturnFailure(int invalidTabs)
    {
        // Arrange
        var hint = new VisualizationHint
        {
            MaxConcurrentTabs = invalidTabs
        };

        // Act
        var result = VisualizationValidator.ValidateHint(hint);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("MaxConcurrentTabs must be between 1 and 20"));
    }

    [TestCase(1)]
    [TestCase(5)]
    [TestCase(10)]
    [TestCase(20)]
    public void ValidateHint_ValidMaxConcurrentTabs_ShouldPass(int validTabs)
    {
        // Arrange
        var hint = new VisualizationHint
        {
            MaxConcurrentTabs = validTabs
        };

        // Act
        var result = VisualizationValidator.ValidateHint(hint);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
    }

    [Test]
    public void ValidateTypeSpecificData_NullData_ShouldReturnFailure()
    {
        // Act
        var result = VisualizationValidator.ValidateTypeSpecificData("test-type", null);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Data cannot be null");
    }

    [Test]
    public void ValidateTypeSpecificData_SearchResults_ShouldValidateStructure()
    {
        // Arrange
        var validData = new { query = "test", results = new[] { "item1" } };
        var invalidData = new { results = new[] { "item1" } }; // missing query

        // Act
        var validResult = VisualizationValidator.ValidateTypeSpecificData(StandardVisualizationTypes.SearchResults, validData);
        var invalidResult = VisualizationValidator.ValidateTypeSpecificData(StandardVisualizationTypes.SearchResults, invalidData);

        // Assert
        validResult.IsValid.Should().BeTrue();
        invalidResult.IsValid.Should().BeFalse();
        invalidResult.Errors.Should().Contain(e => e.Contains("query"));
    }

    [Test]
    public void ValidationResult_Success_ShouldCreateValidResult()
    {
        // Act
        var result = ValidationResult.Success();

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Test]
    public void ValidationResult_Failure_ShouldCreateInvalidResult()
    {
        // Arrange
        var errorMessage = "Test error";

        // Act
        var result = ValidationResult.Failure(errorMessage);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(errorMessage);
    }

    [Test]
    public void ValidationResult_FailureWithMultipleErrors_ShouldCreateInvalidResult()
    {
        // Arrange
        var errors = new[] { "Error 1", "Error 2", "Error 3" };

        // Act
        var result = ValidationResult.Failure(errors);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(3);
        result.Errors.Should().Contain("Error 1");
        result.Errors.Should().Contain("Error 2");
        result.Errors.Should().Contain("Error 3");
    }
}