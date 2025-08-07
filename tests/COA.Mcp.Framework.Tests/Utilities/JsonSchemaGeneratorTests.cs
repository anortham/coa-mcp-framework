using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using COA.Mcp.Framework.Utilities;
using FluentAssertions;
using NUnit.Framework;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;
using RangeAttribute = System.ComponentModel.DataAnnotations.RangeAttribute;

namespace COA.Mcp.Framework.Tests.Utilities
{
    [TestFixture]
    public class JsonSchemaGeneratorTests
    {
        #region Basic Type Tests

        [Test]
        public void GenerateSchema_ForSimpleClass_ReturnsObjectSchema()
        {
            // Act
            var schema = JsonSchemaGenerator.GenerateSchema<SimpleClass>();

            // Assert
            schema.Should().ContainKey("type");
            schema["type"].Should().Be("object");
            schema.Should().ContainKey("additionalProperties");
            schema["additionalProperties"].Should().Be(false);
        }

        [Test]
        public void GenerateSchema_ForStringProperty_ReturnsStringType()
        {
            // Act
            var schema = JsonSchemaGenerator.GenerateSchema<SimpleClass>();

            // Assert
            var properties = schema["properties"] as Dictionary<string, object>;
            properties.Should().ContainKey("name");
            var nameSchema = properties["name"] as Dictionary<string, object>;
            nameSchema["type"].Should().Be("string");
        }

        [Test]
        public void GenerateSchema_ForIntProperty_ReturnsIntegerType()
        {
            // Act
            var schema = JsonSchemaGenerator.GenerateSchema<SimpleClass>();

            // Assert
            var properties = schema["properties"] as Dictionary<string, object>;
            properties.Should().ContainKey("age");
            var ageSchema = properties["age"] as Dictionary<string, object>;
            ageSchema["type"].Should().Be("integer");
        }

        [Test]
        public void GenerateSchema_ForBoolProperty_ReturnsBooleanType()
        {
            // Act
            var schema = JsonSchemaGenerator.GenerateSchema<SimpleClass>();

            // Assert
            var properties = schema["properties"] as Dictionary<string, object>;
            properties.Should().ContainKey("isActive");
            var isActiveSchema = properties["isActive"] as Dictionary<string, object>;
            isActiveSchema["type"].Should().Be("boolean");
        }

        [Test]
        public void GenerateSchema_ForDoubleProperty_ReturnsNumberType()
        {
            // Act
            var schema = JsonSchemaGenerator.GenerateSchema<NumericClass>();

            // Assert
            var properties = schema["properties"] as Dictionary<string, object>;
            properties.Should().ContainKey("price");
            var priceSchema = properties["price"] as Dictionary<string, object>;
            priceSchema["type"].Should().Be("number");
        }

        #endregion

        #region Nullable Type Tests

        [Test]
        public void GenerateSchema_ForNullableInt_ReturnsIntegerType()
        {
            // Act
            var schema = JsonSchemaGenerator.GenerateSchema<NullableClass>();

            // Assert
            var properties = schema["properties"] as Dictionary<string, object>;
            properties.Should().ContainKey("nullableInt");
            var nullableSchema = properties["nullableInt"] as Dictionary<string, object>;
            nullableSchema["type"].Should().Be("integer");
        }

        #endregion

        #region Enum Tests

        [Test]
        public void GenerateSchema_ForEnum_ReturnsEnumValues()
        {
            // Act
            var schema = JsonSchemaGenerator.GenerateSchema<EnumClass>();

            // Assert
            var properties = schema["properties"] as Dictionary<string, object>;
            properties.Should().ContainKey("status");
            var statusSchema = properties["status"] as Dictionary<string, object>;
            statusSchema["type"].Should().Be("string");
            statusSchema["enum"].Should().BeEquivalentTo(new[] { "Active", "Inactive", "Pending" });
        }

        #endregion

        #region Array/Collection Tests

        [Test]
        public void GenerateSchema_ForArray_ReturnsArraySchema()
        {
            // Act
            var schema = JsonSchemaGenerator.GenerateSchema<ArrayClass>();

            // Assert
            var properties = schema["properties"] as Dictionary<string, object>;
            properties.Should().ContainKey("tags");
            var tagsSchema = properties["tags"] as Dictionary<string, object>;
            tagsSchema["type"].Should().Be("array");
            tagsSchema.Should().ContainKey("items");
            var itemsSchema = tagsSchema["items"] as Dictionary<string, object>;
            itemsSchema["type"].Should().Be("string");
        }

        [Test]
        public void GenerateSchema_ForList_ReturnsArraySchema()
        {
            // Act
            var schema = JsonSchemaGenerator.GenerateSchema<ArrayClass>();

            // Assert
            var properties = schema["properties"] as Dictionary<string, object>;
            properties.Should().ContainKey("numbers");
            var numbersSchema = properties["numbers"] as Dictionary<string, object>;
            numbersSchema["type"].Should().Be("array");
            numbersSchema.Should().ContainKey("items");
            var itemsSchema = numbersSchema["items"] as Dictionary<string, object>;
            itemsSchema["type"].Should().Be("integer");
        }

        #endregion

        #region Validation Attribute Tests

        [Test]
        public void GenerateSchema_WithRequiredAttribute_AddsToRequiredList()
        {
            // Act
            var schema = JsonSchemaGenerator.GenerateSchema<ValidationClass>();

            // Assert
            schema.Should().ContainKey("required");
            var required = schema["required"] as List<string>;
            required.Should().Contain("requiredField");
        }

        [Test]
        public void GenerateSchema_WithRangeAttribute_AddsMinMaxConstraints()
        {
            // Act
            var schema = JsonSchemaGenerator.GenerateSchema<ValidationClass>();

            // Assert
            var properties = schema["properties"] as Dictionary<string, object>;
            var rangeSchema = properties["rangeValue"] as Dictionary<string, object>;
            rangeSchema["minimum"].Should().Be(1.0);
            rangeSchema["maximum"].Should().Be(100.0);
        }

        [Test]
        public void GenerateSchema_WithStringLengthAttribute_AddsLengthConstraints()
        {
            // Act
            var schema = JsonSchemaGenerator.GenerateSchema<ValidationClass>();

            // Assert
            var properties = schema["properties"] as Dictionary<string, object>;
            var lengthSchema = properties["lengthField"] as Dictionary<string, object>;
            lengthSchema["minLength"].Should().Be(2);
            lengthSchema["maxLength"].Should().Be(50);
        }

        [Test]
        public void GenerateSchema_WithMinLengthAttribute_AddsMinLengthConstraint()
        {
            // Act
            var schema = JsonSchemaGenerator.GenerateSchema<ValidationClass>();

            // Assert
            var properties = schema["properties"] as Dictionary<string, object>;
            var minSchema = properties["minLengthField"] as Dictionary<string, object>;
            minSchema["minLength"].Should().Be(5);
        }

        [Test]
        public void GenerateSchema_WithMaxLengthAttribute_AddsMaxLengthConstraint()
        {
            // Act
            var schema = JsonSchemaGenerator.GenerateSchema<ValidationClass>();

            // Assert
            var properties = schema["properties"] as Dictionary<string, object>;
            var maxSchema = properties["maxLengthField"] as Dictionary<string, object>;
            maxSchema["maxLength"].Should().Be(10);
        }

        [Test]
        public void GenerateSchema_WithRegularExpressionAttribute_AddsPattern()
        {
            // Act
            var schema = JsonSchemaGenerator.GenerateSchema<ValidationClass>();

            // Assert
            var properties = schema["properties"] as Dictionary<string, object>;
            var regexSchema = properties["regexField"] as Dictionary<string, object>;
            regexSchema["pattern"].Should().Be(@"^\d{3}-\d{3}-\d{4}$");
        }

        #endregion

        #region Description Attribute Tests

        [Test]
        public void GenerateSchema_WithDescriptionAttribute_AddsDescription()
        {
            // Act
            var schema = JsonSchemaGenerator.GenerateSchema<DescriptionClass>();

            // Assert
            var properties = schema["properties"] as Dictionary<string, object>;
            var descSchema = properties["describedField"] as Dictionary<string, object>;
            descSchema["description"].Should().Be("This is a described field");
        }

        #endregion

        #region JsonProperty Tests

        [Test]
        public void GenerateSchema_WithJsonPropertyName_UsesCustomName()
        {
            // Act
            var schema = JsonSchemaGenerator.GenerateSchema<JsonPropertyClass>();

            // Assert
            var properties = schema["properties"] as Dictionary<string, object>;
            properties.Should().ContainKey("custom_name");
            properties.Should().NotContainKey("originalName");
        }

        [Test]
        public void GenerateSchema_WithJsonIgnore_SkipsProperty()
        {
            // Act
            var schema = JsonSchemaGenerator.GenerateSchema<JsonPropertyClass>();

            // Assert
            var properties = schema["properties"] as Dictionary<string, object>;
            properties.Should().NotContainKey("ignoredField");
        }

        [Test]
        public void GenerateSchema_PropertyNames_AreCamelCase()
        {
            // Act
            var schema = JsonSchemaGenerator.GenerateSchema<SimpleClass>();

            // Assert
            var properties = schema["properties"] as Dictionary<string, object>;
            properties.Should().ContainKey("name"); // lowercase 'n'
            properties.Should().ContainKey("age");  // lowercase 'a'
            properties.Should().NotContainKey("Name"); // not uppercase
        }

        #endregion

        #region Nested Object Tests

        [Test]
        public void GenerateSchema_WithNestedObject_GeneratesNestedSchema()
        {
            // Act
            var schema = JsonSchemaGenerator.GenerateSchema<NestedClass>();

            // Assert
            var properties = schema["properties"] as Dictionary<string, object>;
            properties.Should().ContainKey("nested");
            var nestedSchema = properties["nested"] as Dictionary<string, object>;
            nestedSchema["type"].Should().Be("object");
            nestedSchema.Should().ContainKey("properties");
            var nestedProps = nestedSchema["properties"] as Dictionary<string, object>;
            nestedProps.Should().ContainKey("name");
        }

        #endregion

        #region Value Type Required Tests

        [Test]
        public void GenerateSchema_NonNullableValueType_IsRequired()
        {
            // Act
            var schema = JsonSchemaGenerator.GenerateSchema<SimpleClass>();

            // Assert
            var required = schema["required"] as List<string>;
            required.Should().Contain("age"); // int is non-nullable value type
            required.Should().Contain("isActive"); // bool is non-nullable value type
        }

        [Test]
        public void GenerateSchema_NonNullableValueTypeWithDefaultValue_NotRequired()
        {
            // Act
            var schema = JsonSchemaGenerator.GenerateSchema<DefaultValueClass>();

            // Assert
            schema.Should().NotContainKey("required"); // Has default value, so not required
        }

        #endregion

        #region Generic Method Tests

        [Test]
        public void GenerateSchema_GenericMethod_ProducesCorrectSchema()
        {
            // Act
            var schema = JsonSchemaGenerator.GenerateSchema<SimpleClass>();

            // Assert
            schema.Should().NotBeNull();
            schema["type"].Should().Be("object");
        }

        [Test]
        public void GenerateSchema_NonGenericMethod_ProducesCorrectSchema()
        {
            // Act
            var schema = JsonSchemaGenerator.GenerateSchema(typeof(SimpleClass));

            // Assert
            schema.Should().NotBeNull();
            schema["type"].Should().Be("object");
        }

        #endregion

        #region Test Classes

        private class SimpleClass
        {
            public string Name { get; set; }
            public int Age { get; set; }
            public bool IsActive { get; set; }
        }

        private class NumericClass
        {
            public double Price { get; set; }
            public float Discount { get; set; }
            public decimal Total { get; set; }
        }

        private class NullableClass
        {
            public int? NullableInt { get; set; }
            public bool? NullableBool { get; set; }
        }

        private enum Status
        {
            Active,
            Inactive,
            Pending
        }

        private class EnumClass
        {
            public Status Status { get; set; }
        }

        private class ArrayClass
        {
            public string[] Tags { get; set; }
            public List<int> Numbers { get; set; }
        }

        private class ValidationClass
        {
            [Required]
            public string RequiredField { get; set; }

            [RangeAttribute(1, 100)]
            public int RangeValue { get; set; }

            [StringLength(50, MinimumLength = 2)]
            public string LengthField { get; set; }

            [MinLength(5)]
            public string MinLengthField { get; set; }

            [MaxLength(10)]
            public string MaxLengthField { get; set; }

            [RegularExpression(@"^\d{3}-\d{3}-\d{4}$")]
            public string RegexField { get; set; }
        }

        private class DescriptionClass
        {
            [DescriptionAttribute("This is a described field")]
            public string DescribedField { get; set; }
        }

        private class JsonPropertyClass
        {
            [JsonPropertyName("custom_name")]
            public string OriginalName { get; set; }

            [JsonIgnore]
            public string IgnoredField { get; set; }

            public string NormalField { get; set; }
        }

        private class NestedClass
        {
            public SimpleClass Nested { get; set; }
            public string TopLevel { get; set; }
        }

        private class DefaultValueClass
        {
            [DefaultValue(42)]
            public int ValueWithDefault { get; set; }
        }

        #endregion
    }
}