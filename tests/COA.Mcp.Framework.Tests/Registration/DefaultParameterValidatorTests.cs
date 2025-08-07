using System;
using System.ComponentModel.DataAnnotations;
using COA.Mcp.Framework.Attributes;
using COA.Mcp.Framework.Registration;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using RequiredAttribute = COA.Mcp.Framework.Attributes.RequiredAttribute;
using RangeAttribute = COA.Mcp.Framework.Attributes.RangeAttribute;
using StringLengthAttribute = COA.Mcp.Framework.Attributes.StringLengthAttribute;

namespace COA.Mcp.Framework.Tests.Registration
{
    [TestFixture]
    public class DefaultParameterValidatorTests
    {
        private DefaultParameterValidator _validator;
        private Mock<ILogger<DefaultParameterValidator>> _loggerMock;

        [SetUp]
        public void SetUp()
        {
            _loggerMock = new Mock<ILogger<DefaultParameterValidator>>();
            _validator = new DefaultParameterValidator(_loggerMock.Object);
        }

        #region Validate Method Tests

        [Test]
        public void Validate_WithNullParameterType_ThrowsArgumentNullException()
        {
            // Act & Assert
            Action act = () => _validator.Validate(new object(), null);
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("parameterType");
        }

        [Test]
        public void Validate_WithNullParameters_AndNoRequiredProperties_ReturnsSuccess()
        {
            // Act
            var result = _validator.Validate(null, typeof(OptionalClass));

            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }

        [Test]
        public void Validate_WithNullParameters_AndRequiredProperties_ReturnsFailure()
        {
            // Act
            var result = _validator.Validate(null, typeof(RequiredClass));

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().NotBeEmpty();
            result.Errors.Should().Contain(e => e.ParameterName == "RequiredProperty");
        }

        [Test]
        public void Validate_WithTypeMismatch_ReturnsFailure()
        {
            // Arrange
            var wrongTypeParam = "not a RequiredClass";

            // Act
            var result = _validator.Validate(wrongTypeParam, typeof(RequiredClass));

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle();
            result.Errors[0].ErrorMessage.Should().Contain("Expected parameter type");
        }

        [Test]
        public void Validate_WithValidObject_ReturnsSuccess()
        {
            // Arrange
            var validObject = new RequiredClass { RequiredProperty = "value" };

            // Act
            var result = _validator.Validate(validObject, typeof(RequiredClass));

            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }

        [Test]
        public void Validate_WithInvalidObject_ReturnsFailure()
        {
            // Arrange
            var invalidObject = new ValidationClass 
            { 
                RequiredField = null,
                RangeValue = 150 // Out of range
            };

            // Act
            var result = _validator.Validate(invalidObject, typeof(ValidationClass));

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().NotBeEmpty();
        }

        [Test]
        public void Validate_WithNonNullableValueType_RequiresValue()
        {
            // Act
            var result = _validator.Validate(null, typeof(ValueTypeClass));

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.ParameterName == "IntValue");
        }

        [Test]
        public void Validate_WithNullableValueType_DoesNotRequireValue()
        {
            // Act
            var result = _validator.Validate(null, typeof(NullableValueTypeClass));

            // Assert
            result.IsValid.Should().BeTrue();
        }

        #endregion

        #region ValidateParameter Method Tests

        [Test]
        public void ValidateParameter_WithNoAttributes_ReturnsSuccess()
        {
            // Act
            var result = _validator.ValidateParameter("value", "param", Array.Empty<ParameterValidationAttribute>());

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Test]
        public void ValidateParameter_WithValidValue_ReturnsSuccess()
        {
            // Arrange
            var attributes = new[] { new RequiredAttribute() };

            // Act
            var result = _validator.ValidateParameter("value", "param", attributes);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Test]
        public void ValidateParameter_WithInvalidValue_ReturnsFailure()
        {
            // Arrange
            var attributes = new[] { new RequiredAttribute() };

            // Act
            var result = _validator.ValidateParameter(null, "param", attributes);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle();
            result.Errors[0].ParameterName.Should().Be("param");
        }

        [Test]
        public void ValidateParameter_WithMultipleFailingAttributes_ReturnsAllErrors()
        {
            // Arrange
            var attributes = new ParameterValidationAttribute[] 
            { 
                new RequiredAttribute(),
                new StringLengthAttribute(5, 10)
            };

            // Act - use a non-null value that fails both validations
            var result = _validator.ValidateParameter("abc", "param", attributes);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().HaveCount(1); // Only StringLength fails for "abc" (too short)
            
            // For a better test, use RangeAttribute with a value that's out of range
            var rangeAttributes = new ParameterValidationAttribute[]
            {
                new RangeAttribute(1, 10),
                new RangeAttribute(5, 15)
            };
            
            var rangeResult = _validator.ValidateParameter(20, "param", rangeAttributes);
            rangeResult.IsValid.Should().BeFalse();
            rangeResult.Errors.Should().HaveCount(2); // Both range checks fail for 20
        }

        [Test]
        public void ValidateParameter_WhenAttributeThrows_LogsAndReturnsError()
        {
            // Arrange
            var throwingAttribute = new ThrowingValidationAttribute();
            var attributes = new[] { throwingAttribute };

            // Act
            var result = _validator.ValidateParameter("value", "param", attributes);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle();
            result.Errors[0].ErrorMessage.Should().Contain("Validation error");
            
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error validating parameter")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion

        #region Standard Validation Attribute Conversion Tests

        [Test]
        public void Validate_WithStandardRequiredAttribute_ConvertsToFrameworkAttribute()
        {
            // Arrange
            var obj = new StandardValidationClass { RequiredField = null };

            // Act
            var result = _validator.Validate(obj, typeof(StandardValidationClass));

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.ParameterName == "RequiredField");
        }

        [Test]
        public void Validate_WithStandardRangeAttribute_ConvertsToFrameworkAttribute()
        {
            // Arrange
            var obj = new StandardValidationClass { RangeValue = 150 };

            // Act
            var result = _validator.Validate(obj, typeof(StandardValidationClass));

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.ParameterName == "RangeValue");
        }

        [Test]
        public void Validate_WithStandardStringLengthAttribute_ConvertsToFrameworkAttribute()
        {
            // Arrange
            var obj = new StandardValidationClass { LengthField = "x" }; // Too short

            // Act
            var result = _validator.Validate(obj, typeof(StandardValidationClass));

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.ParameterName == "LengthField");
        }

        [Test]
        public void Validate_WithCustomErrorMessage_PreservesMessage()
        {
            // Arrange
            var obj = new CustomMessageClass { CustomMessageField = null };

            // Act
            var result = _validator.Validate(obj, typeof(CustomMessageClass));

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => 
                e.ErrorMessage.Contains("Custom error message"));
        }

        #endregion

        #region Test Classes

        private class OptionalClass
        {
            public string? OptionalProperty { get; set; }
        }

        private class RequiredClass
        {
            [Required]
            public string RequiredProperty { get; set; }
        }

        private class ValidationClass
        {
            [Required]
            public string RequiredField { get; set; }

            [Range(1, 100)]
            public int RangeValue { get; set; }
        }

        private class ValueTypeClass
        {
            public int IntValue { get; set; }
            public bool BoolValue { get; set; }
        }

        private class NullableValueTypeClass
        {
            public int? NullableInt { get; set; }
            public bool? NullableBool { get; set; }
        }

        private class StandardValidationClass
        {
            [System.ComponentModel.DataAnnotations.Required]
            public string RequiredField { get; set; }

            [System.ComponentModel.DataAnnotations.Range(1, 100)]
            public int RangeValue { get; set; }

            [System.ComponentModel.DataAnnotations.StringLength(50, MinimumLength = 2)]
            public string LengthField { get; set; }
        }

        private class CustomMessageClass
        {
            [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Custom error message")]
            public string CustomMessageField { get; set; }
        }

        private class ThrowingValidationAttribute : ParameterValidationAttribute
        {
            public override bool IsValid(object? value, string parameterName)
            {
                throw new InvalidOperationException("Test exception");
            }

            public override string GetErrorMessage(string parameterName)
            {
                return "Should not reach here";
            }
        }

        #endregion
    }
}