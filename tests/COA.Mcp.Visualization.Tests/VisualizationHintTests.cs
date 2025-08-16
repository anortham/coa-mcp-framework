using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;

namespace COA.Mcp.Visualization.Tests;

[TestFixture]
public class VisualizationHintTests
{
    [Test]
    public void VisualizationHint_WithDefaults_ShouldHaveExpectedValues()
    {
        // Arrange & Act
        var hint = new VisualizationHint();

        // Assert
        hint.PreferredView.Should().BeNull();
        hint.FallbackFormat.Should().Be("json");
        hint.Interactive.Should().BeTrue();
        hint.ConsolidateTabs.Should().BeTrue();
        hint.Priority.Should().Be(VisualizationPriority.Normal);
        hint.MaxConcurrentTabs.Should().Be(3);
        hint.Options.Should().BeNull();
    }

    [Test]
    public void VisualizationHint_WithCustomValues_ShouldRetainSettings()
    {
        // Arrange & Act
        var hint = new VisualizationHint
        {
            PreferredView = "tree",
            FallbackFormat = "csv",
            Interactive = false,
            ConsolidateTabs = false,
            Priority = VisualizationPriority.Critical,
            MaxConcurrentTabs = 1,
            Options = new Dictionary<string, object>
            {
                ["sortable"] = true,
                ["filterable"] = false
            }
        };

        // Assert
        hint.PreferredView.Should().Be("tree");
        hint.FallbackFormat.Should().Be("csv");
        hint.Interactive.Should().BeFalse();
        hint.ConsolidateTabs.Should().BeFalse();
        hint.Priority.Should().Be(VisualizationPriority.Critical);
        hint.MaxConcurrentTabs.Should().Be(1);
        hint.Options.Should().NotBeNull();
        hint.Options!["sortable"].Should().Be(true);
        hint.Options["filterable"].Should().Be(false);
    }

    [Test]
    public void VisualizationHint_JsonSerialization_ShouldUseCamelCase()
    {
        // Arrange
        var hint = new VisualizationHint
        {
            PreferredView = "grid",
            FallbackFormat = "json",
            Interactive = true,
            ConsolidateTabs = false,
            Priority = VisualizationPriority.High,
            MaxConcurrentTabs = 2
        };

        // Act
        var json = JsonSerializer.Serialize(hint);

        // Assert
        json.Should().Contain("\"preferredView\":");
        json.Should().Contain("\"fallbackFormat\":");
        json.Should().Contain("\"interactive\":");
        json.Should().Contain("\"consolidateTabs\":");
        json.Should().Contain("\"priority\":");
        json.Should().Contain("\"maxConcurrentTabs\":");
    }

    [Test]
    public void VisualizationHint_SerializeAndDeserialize_ShouldPreserveValues()
    {
        // Arrange
        var originalHint = new VisualizationHint
        {
            PreferredView = "chart",
            FallbackFormat = "markdown",
            Interactive = false,
            ConsolidateTabs = true,
            Priority = VisualizationPriority.Low,
            MaxConcurrentTabs = 5,
            Options = new Dictionary<string, object>
            {
                ["theme"] = "dark",
                ["animations"] = true,
                ["threshold"] = 100
            }
        };

        // Act
        var json = JsonSerializer.Serialize(originalHint);
        var deserializedHint = JsonSerializer.Deserialize<VisualizationHint>(json);

        // Assert
        deserializedHint.Should().NotBeNull();
        deserializedHint!.PreferredView.Should().Be("chart");
        deserializedHint.FallbackFormat.Should().Be("markdown");
        deserializedHint.Interactive.Should().BeFalse();
        deserializedHint.ConsolidateTabs.Should().BeTrue();
        deserializedHint.Priority.Should().Be(VisualizationPriority.Low);
        deserializedHint.MaxConcurrentTabs.Should().Be(5);
        deserializedHint.Options.Should().NotBeNull();
        deserializedHint.Options!.Should().ContainKey("theme");
        deserializedHint.Options.Should().ContainKey("animations");
        deserializedHint.Options.Should().ContainKey("threshold");
    }

    [TestCase(VisualizationPriority.OnRequest, "OnRequest")]
    [TestCase(VisualizationPriority.Low, "Low")]
    [TestCase(VisualizationPriority.Normal, "Normal")]
    [TestCase(VisualizationPriority.High, "High")]
    [TestCase(VisualizationPriority.Critical, "Critical")]
    public void VisualizationPriority_JsonSerialization_ShouldUseStringValues(
        VisualizationPriority priority, 
        string expectedString)
    {
        // Arrange
        var hint = new VisualizationHint { Priority = priority };

        // Act
        var json = JsonSerializer.Serialize(hint);

        // Assert
        json.Should().Contain($"\"{expectedString}\"");
    }

    [Test]
    public void VisualizationHint_WithNullOptions_ShouldSerializeCorrectly()
    {
        // Arrange
        var hint = new VisualizationHint
        {
            PreferredView = "grid",
            Options = null
        };

        // Act
        var json = JsonSerializer.Serialize(hint);
        var deserialized = JsonSerializer.Deserialize<VisualizationHint>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.PreferredView.Should().Be("grid");
        deserialized.Options.Should().BeNull();
    }
}