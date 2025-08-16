using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;

namespace COA.Mcp.Visualization.Tests;

[TestFixture]
public class VisualizationDescriptorTests
{
    [Test]
    public void VisualizationDescriptor_WithRequiredProperties_ShouldBeValid()
    {
        // Arrange & Act
        var descriptor = new VisualizationDescriptor
        {
            Type = "test-type",
            Data = new { message = "test" }
        };

        // Assert
        descriptor.Type.Should().Be("test-type");
        descriptor.Version.Should().Be("1.0"); // default value
        descriptor.Data.Should().NotBeNull();
        descriptor.Hint.Should().BeNull();
        descriptor.Metadata.Should().BeNull();
    }

    [Test]
    public void VisualizationDescriptor_WithAllProperties_ShouldSerializeCorrectly()
    {
        // Arrange
        var hint = new VisualizationHint
        {
            PreferredView = "grid",
            FallbackFormat = "json",
            Interactive = true,
            Priority = VisualizationPriority.High
        };

        var metadata = new Dictionary<string, object>
        {
            ["source"] = "test",
            ["timestamp"] = "2023-01-01"
        };

        var descriptor = new VisualizationDescriptor
        {
            Type = StandardVisualizationTypes.SearchResults,
            Version = "2.0",
            Data = new { results = new[] { "item1", "item2" } },
            Hint = hint,
            Metadata = metadata
        };

        // Act
        var json = JsonSerializer.Serialize(descriptor);
        var deserialized = JsonSerializer.Deserialize<VisualizationDescriptor>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Type.Should().Be(StandardVisualizationTypes.SearchResults);
        deserialized.Version.Should().Be("2.0");
        deserialized.Data.Should().NotBeNull();
        deserialized.Hint.Should().NotBeNull();
        deserialized.Hint!.PreferredView.Should().Be("grid");
        deserialized.Hint.Priority.Should().Be(VisualizationPriority.High);
        deserialized.Metadata.Should().NotBeNull();
        deserialized.Metadata!.Should().ContainKey("source");
    }

    [Test]
    public void VisualizationDescriptor_JsonSerialization_ShouldUseCamelCase()
    {
        // Arrange
        var descriptor = new VisualizationDescriptor
        {
            Type = "test-type",
            Data = new { testProperty = "value" }
        };

        // Act
        var json = JsonSerializer.Serialize(descriptor);

        // Assert
        json.Should().Contain("\"type\":");
        json.Should().Contain("\"version\":");
        json.Should().Contain("\"data\":");
        json.Should().NotContain("\"Type\":");
        json.Should().NotContain("\"Version\":");
        json.Should().NotContain("\"Data\":");
    }

    [Test]
    public void VisualizationDescriptor_WithComplexData_ShouldSerializeAndDeserialize()
    {
        // Arrange
        var complexData = new
        {
            query = "test query",
            totalHits = 42,
            results = new[]
            {
                new { filePath = "/path/to/file1.cs", line = 10, snippet = "code snippet 1" },
                new { filePath = "/path/to/file2.cs", line = 25, snippet = "code snippet 2" }
            }
        };

        var descriptor = new VisualizationDescriptor
        {
            Type = StandardVisualizationTypes.SearchResults,
            Data = complexData
        };

        // Act
        var json = JsonSerializer.Serialize(descriptor);
        var deserialized = JsonSerializer.Deserialize<VisualizationDescriptor>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Type.Should().Be(StandardVisualizationTypes.SearchResults);
        deserialized.Data.Should().NotBeNull();
        
        // Verify the complex data structure is preserved
        var dataElement = (JsonElement)deserialized.Data;
        dataElement.GetProperty("query").GetString().Should().Be("test query");
        dataElement.GetProperty("totalHits").GetInt32().Should().Be(42);
        dataElement.GetProperty("results").GetArrayLength().Should().Be(2);
    }
}