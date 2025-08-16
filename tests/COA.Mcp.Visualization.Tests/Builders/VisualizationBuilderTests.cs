using System;
using System.Collections.Generic;
using System.Linq;
using COA.Mcp.Visualization.Builders;
using FluentAssertions;
using NUnit.Framework;

namespace COA.Mcp.Visualization.Tests.Builders;

[TestFixture]
public class VisualizationBuilderTests
{
    [Test]
    public void Create_ShouldReturnNewBuilderInstance()
    {
        // Act
        var builder = VisualizationBuilder.Create();

        // Assert
        builder.Should().NotBeNull();
        builder.Should().BeOfType<VisualizationBuilder>();
    }

    [Test]
    public void Build_WithoutType_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var builder = VisualizationBuilder.Create()
            .WithData(new { test = "data" });

        // Act & Assert
        var action = () => builder.Build();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Visualization type is required");
    }

    [Test]
    public void Build_WithMinimalConfiguration_ShouldCreateValidDescriptor()
    {
        // Arrange & Act
        var descriptor = VisualizationBuilder.Create()
            .WithType("test-type")
            .WithData(new { message = "test" })
            .Build();

        // Assert
        descriptor.Should().NotBeNull();
        descriptor.Type.Should().Be("test-type");
        descriptor.Version.Should().Be("1.0");
        descriptor.Data.Should().NotBeNull();
        descriptor.Hint.Should().BeNull();
        descriptor.Metadata.Should().BeNull();
    }

    [Test]
    public void Build_WithFullConfiguration_ShouldCreateCompleteDescriptor()
    {
        // Arrange
        var testData = new { items = new[] { 1, 2, 3 } };

        // Act
        var descriptor = VisualizationBuilder.Create()
            .WithType(StandardVisualizationTypes.DataGrid)
            .WithVersion("2.0")
            .WithData(testData)
            .WithPreferredView("grid")
            .WithFallback("csv")
            .WithInteractive(true)
            .WithPriority(VisualizationPriority.High)
            .WithMetadata("source", "test")
            .WithMetadata("timestamp", DateTime.Now)
            .Build();

        // Assert
        descriptor.Should().NotBeNull();
        descriptor.Type.Should().Be(StandardVisualizationTypes.DataGrid);
        descriptor.Version.Should().Be("2.0");
        descriptor.Data.Should().Be(testData);
        
        descriptor.Hint.Should().NotBeNull();
        descriptor.Hint!.PreferredView.Should().Be("grid");
        descriptor.Hint.FallbackFormat.Should().Be("csv");
        descriptor.Hint.Interactive.Should().BeTrue();
        descriptor.Hint.Priority.Should().Be(VisualizationPriority.High);
        
        descriptor.Metadata.Should().NotBeNull();
        descriptor.Metadata!.Should().ContainKey("source");
        descriptor.Metadata.Should().ContainKey("timestamp");
    }

    [Test]
    public void SearchResults_ShouldCreateCorrectDescriptor()
    {
        // Arrange
        var query = "test query";
        var results = new[]
        {
            new { filePath = "/test1.cs", line = 10 },
            new { filePath = "/test2.cs", line = 20 }
        };

        // Act
        var descriptor = VisualizationBuilder.SearchResults(query, results).Build();

        // Assert
        descriptor.Should().NotBeNull();
        descriptor.Type.Should().Be(StandardVisualizationTypes.SearchResults);
        descriptor.Hint.Should().NotBeNull();
        descriptor.Hint!.PreferredView.Should().Be("grid");
        descriptor.Hint.FallbackFormat.Should().Be("json");
        
        // Verify data structure
        descriptor.Data.Should().NotBeNull();
        var dataDict = descriptor.Data as Dictionary<string, object>;
        if (dataDict != null)
        {
            dataDict.Should().ContainKey("query");
            dataDict.Should().ContainKey("totalHits");
            dataDict.Should().ContainKey("results");
        }
    }

    [Test]
    public void DataGrid_ShouldCreateCorrectDescriptor()
    {
        // Arrange
        var columns = new[]
        {
            new { name = "col1", type = "string" },
            new { name = "col2", type = "number" }
        };
        var rows = new[]
        {
            new { col1 = "value1", col2 = 1 },
            new { col1 = "value2", col2 = 2 }
        };

        // Act
        var descriptor = VisualizationBuilder.DataGrid(columns, rows).Build();

        // Assert
        descriptor.Should().NotBeNull();
        descriptor.Type.Should().Be(StandardVisualizationTypes.DataGrid);
        descriptor.Hint.Should().NotBeNull();
        descriptor.Hint!.PreferredView.Should().Be("grid");
        descriptor.Hint.FallbackFormat.Should().Be("csv");
    }

    [Test]
    public void Hierarchy_ShouldCreateCorrectDescriptor()
    {
        // Arrange
        var root = new { name = "Root", children = new[] { new { name = "Child" } } };

        // Act
        var descriptor = VisualizationBuilder.Hierarchy(root).Build();

        // Assert
        descriptor.Should().NotBeNull();
        descriptor.Type.Should().Be(StandardVisualizationTypes.Hierarchy);
        descriptor.Hint.Should().NotBeNull();
        descriptor.Hint!.PreferredView.Should().Be("tree");
        descriptor.Hint.FallbackFormat.Should().Be("json");
    }

    [Test]
    public void Metrics_ShouldCreateCorrectDescriptor()
    {
        // Arrange
        var metrics = new { cpu = 75, memory = 60, disk = 80 };

        // Act
        var descriptor = VisualizationBuilder.Metrics(metrics).Build();

        // Assert
        descriptor.Should().NotBeNull();
        descriptor.Type.Should().Be(StandardVisualizationTypes.Metrics);
        descriptor.Hint.Should().NotBeNull();
        descriptor.Hint!.PreferredView.Should().Be("chart");
        descriptor.Hint.FallbackFormat.Should().Be("json");
    }

    [Test]
    public void Timeline_ShouldCreateCorrectDescriptor()
    {
        // Arrange
        var events = new[]
        {
            new { timestamp = DateTime.Now, title = "Event 1" },
            new { timestamp = DateTime.Now.AddHours(1), title = "Event 2" }
        };

        // Act
        var descriptor = VisualizationBuilder.Timeline(events).Build();

        // Assert
        descriptor.Should().NotBeNull();
        descriptor.Type.Should().Be(StandardVisualizationTypes.Timeline);
        descriptor.Hint.Should().NotBeNull();
        descriptor.Hint!.PreferredView.Should().Be("timeline");
        descriptor.Hint.FallbackFormat.Should().Be("json");
        
        // Verify data structure
        descriptor.Data.Should().NotBeNull();
        var dataDict = descriptor.Data as Dictionary<string, object>;
        if (dataDict != null)
        {
            dataDict.Should().ContainKey("events");
            dataDict.Should().ContainKey("totalEvents");
        }
    }

    [Test]
    public void WithHint_ShouldReplaceExistingHint()
    {
        // Arrange
        var initialHint = new VisualizationHint { PreferredView = "grid" };
        var newHint = new VisualizationHint { PreferredView = "tree", Priority = VisualizationPriority.High };

        // Act
        var descriptor = VisualizationBuilder.Create()
            .WithType("test")
            .WithData(new { })
            .WithHint(initialHint)
            .WithHint(newHint)
            .Build();

        // Assert
        descriptor.Hint.Should().Be(newHint);
        descriptor.Hint!.PreferredView.Should().Be("tree");
        descriptor.Hint.Priority.Should().Be(VisualizationPriority.High);
    }

    [Test]
    public void FluentMethods_ShouldCreateHintIfNotExists()
    {
        // Act
        var descriptor = VisualizationBuilder.Create()
            .WithType("test")
            .WithData(new { })
            .WithPreferredView("grid")
            .WithFallback("json")
            .WithInteractive(false)
            .WithPriority(VisualizationPriority.Critical)
            .Build();

        // Assert
        descriptor.Hint.Should().NotBeNull();
        descriptor.Hint!.PreferredView.Should().Be("grid");
        descriptor.Hint.FallbackFormat.Should().Be("json");
        descriptor.Hint.Interactive.Should().BeFalse();
        descriptor.Hint.Priority.Should().Be(VisualizationPriority.Critical);
    }

    [Test]
    public void WithMetadata_MultipleKeys_ShouldAddAllMetadata()
    {
        // Act
        var descriptor = VisualizationBuilder.Create()
            .WithType("test")
            .WithData(new { })
            .WithMetadata("key1", "value1")
            .WithMetadata("key2", 42)
            .WithMetadata("key3", true)
            .Build();

        // Assert
        descriptor.Metadata.Should().NotBeNull();
        descriptor.Metadata!.Should().HaveCount(3);
        descriptor.Metadata["key1"].Should().Be("value1");
        descriptor.Metadata["key2"].Should().Be(42);
        descriptor.Metadata["key3"].Should().Be(true);
    }

    [Test]
    public void WithMetadata_DuplicateKey_ShouldOverwriteValue()
    {
        // Act
        var descriptor = VisualizationBuilder.Create()
            .WithType("test")
            .WithData(new { })
            .WithMetadata("key", "original")
            .WithMetadata("key", "updated")
            .Build();

        // Assert
        descriptor.Metadata.Should().NotBeNull();
        descriptor.Metadata!.Should().HaveCount(1);
        descriptor.Metadata["key"].Should().Be("updated");
    }
}