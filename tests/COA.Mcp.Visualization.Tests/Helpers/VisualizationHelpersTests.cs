using System;
using System.Collections.Generic;
using System.Linq;
using COA.Mcp.Visualization.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace COA.Mcp.Visualization.Tests.Helpers;

[TestFixture]
public class VisualizationHelpersTests
{
    [Test]
    public void CreateSearchResults_WithBasicData_ShouldCreateValidDescriptor()
    {
        // Arrange
        var query = "test search";
        var results = new[]
        {
            new SearchResult { FilePath = "/test1.cs", Line = 10, Snippet = "code1" },
            new SearchResult { FilePath = "/test2.cs", Line = 20, Snippet = "code2" }
        };

        // Act
        var descriptor = VisualizationHelpers.CreateSearchResults(query, results);

        // Assert
        descriptor.Should().NotBeNull();
        descriptor.Type.Should().Be(StandardVisualizationTypes.SearchResults);
        descriptor.Hint.Should().NotBeNull();
        descriptor.Hint!.PreferredView.Should().Be("grid");
        descriptor.Hint.FallbackFormat.Should().Be("json");
    }

    [Test]
    public void CreateSearchResults_WithConfiguration_ShouldApplyCustomSettings()
    {
        // Arrange
        var query = "configured search";
        var results = new[] { new SearchResult { FilePath = "/test.cs", Line = 1 } };

        // Act
        var descriptor = VisualizationHelpers.CreateSearchResults(query, results, builder =>
        {
            builder.WithPriority(VisualizationPriority.High)
                   .WithInteractive(false)
                   .WithMetadata("source", "test");
        });

        // Assert
        descriptor.Should().NotBeNull();
        descriptor.Hint.Should().NotBeNull();
        descriptor.Hint!.Priority.Should().Be(VisualizationPriority.High);
        descriptor.Hint.Interactive.Should().BeFalse();
        descriptor.Metadata.Should().NotBeNull();
        descriptor.Metadata!["source"].Should().Be("test");
    }

    [Test]
    public void CreateDataGrid_WithColumnsAndRows_ShouldCreateValidDescriptor()
    {
        // Arrange
        var columns = new[]
        {
            new GridColumn { Name = "name", DisplayName = "Name", Type = "string" },
            new GridColumn { Name = "age", DisplayName = "Age", Type = "number" }
        };
        var rows = new[]
        {
            new Dictionary<string, object> { ["name"] = "John", ["age"] = 30 },
            new Dictionary<string, object> { ["name"] = "Jane", ["age"] = 25 }
        };

        // Act
        var descriptor = VisualizationHelpers.CreateDataGrid(columns, rows);

        // Assert
        descriptor.Should().NotBeNull();
        descriptor.Type.Should().Be(StandardVisualizationTypes.DataGrid);
        descriptor.Hint.Should().NotBeNull();
        descriptor.Hint!.PreferredView.Should().Be("grid");
        descriptor.Hint.FallbackFormat.Should().Be("csv");
    }

    [Test]
    public void CreateHierarchy_WithRootNode_ShouldCreateValidDescriptor()
    {
        // Arrange
        var root = new HierarchyNode
        {
            Name = "Root",
            Type = "folder",
            Children = new List<HierarchyNode>
            {
                new() { Name = "Child1", Type = "file" },
                new() { Name = "Child2", Type = "file" }
            }
        };

        // Act
        var descriptor = VisualizationHelpers.CreateHierarchy(root);

        // Assert
        descriptor.Should().NotBeNull();
        descriptor.Type.Should().Be(StandardVisualizationTypes.Hierarchy);
        descriptor.Hint.Should().NotBeNull();
        descriptor.Hint!.PreferredView.Should().Be("tree");
        descriptor.Hint.FallbackFormat.Should().Be("json");
    }

    [Test]
    public void CreateMetrics_WithMetricsObject_ShouldCreateValidDescriptor()
    {
        // Arrange
        var metrics = new
        {
            performance = new { cpu = 75.5, memory = 60.2 },
            quality = new { coverage = 85.0, complexity = 3.2 }
        };

        // Act
        var descriptor = VisualizationHelpers.CreateMetrics(metrics);

        // Assert
        descriptor.Should().NotBeNull();
        descriptor.Type.Should().Be(StandardVisualizationTypes.Metrics);
        descriptor.Hint.Should().NotBeNull();
        descriptor.Hint!.PreferredView.Should().Be("chart");
        descriptor.Hint.FallbackFormat.Should().Be("json");
    }

    [Test]
    public void CreateTimeline_WithEvents_ShouldCreateValidDescriptor()
    {
        // Arrange
        var events = new[]
        {
            new TimelineEvent 
            { 
                Timestamp = DateTime.Now.AddHours(-2), 
                Title = "Start", 
                Description = "Process started" 
            },
            new TimelineEvent 
            { 
                Timestamp = DateTime.Now.AddHours(-1), 
                Title = "Middle", 
                Description = "Process running" 
            },
            new TimelineEvent 
            { 
                Timestamp = DateTime.Now, 
                Title = "End", 
                Description = "Process completed" 
            }
        };

        // Act
        var descriptor = VisualizationHelpers.CreateTimeline(events);

        // Assert
        descriptor.Should().NotBeNull();
        descriptor.Type.Should().Be(StandardVisualizationTypes.Timeline);
        descriptor.Hint.Should().NotBeNull();
        descriptor.Hint!.PreferredView.Should().Be("timeline");
        descriptor.Hint.FallbackFormat.Should().Be("json");
    }

    [Test]
    public void CreateDiagnostics_WithDiagnosticItems_ShouldCreateValidDescriptor()
    {
        // Arrange
        var diagnostics = new[]
        {
            new DiagnosticItem 
            { 
                Severity = "error", 
                Message = "Compilation error", 
                FilePath = "/test.cs", 
                Line = 10 
            },
            new DiagnosticItem 
            { 
                Severity = "warning", 
                Message = "Unused variable", 
                FilePath = "/test.cs", 
                Line = 15 
            },
            new DiagnosticItem 
            { 
                Severity = "info", 
                Message = "Code suggestion", 
                FilePath = "/test.cs", 
                Line = 20 
            }
        };

        // Act
        var descriptor = VisualizationHelpers.CreateDiagnostics(diagnostics);

        // Assert
        descriptor.Should().NotBeNull();
        descriptor.Type.Should().Be(StandardVisualizationTypes.Diagnostic);
        descriptor.Hint.Should().NotBeNull();
        descriptor.Hint!.PreferredView.Should().Be("grid");
        descriptor.Hint.FallbackFormat.Should().Be("json");
        descriptor.Hint.Priority.Should().Be(VisualizationPriority.High);

        // Verify diagnostic counts in data
        descriptor.Data.Should().NotBeNull();
    }

    [Test]
    public void CreateProgress_WithProgressData_ShouldCreateValidDescriptor()
    {
        // Arrange
        var current = 75;
        var total = 100;
        var message = "Processing files...";

        // Act
        var descriptor = VisualizationHelpers.CreateProgress(current, total, message);

        // Assert
        descriptor.Should().NotBeNull();
        descriptor.Type.Should().Be(StandardVisualizationTypes.Progress);
        descriptor.Hint.Should().NotBeNull();
        descriptor.Hint!.PreferredView.Should().Be("progress");
        descriptor.Hint.FallbackFormat.Should().Be("text");
        descriptor.Hint.Priority.Should().Be(VisualizationPriority.High);
    }

    [Test]
    public void CreateProgress_WithZeroTotal_ShouldHandleGracefully()
    {
        // Arrange
        var current = 5;
        var total = 0;
        var message = "Initializing...";

        // Act
        var descriptor = VisualizationHelpers.CreateProgress(current, total, message);

        // Assert
        descriptor.Should().NotBeNull();
        descriptor.Type.Should().Be(StandardVisualizationTypes.Progress);
        // Should not throw division by zero exception
    }

    [Test]
    public void SearchResult_Properties_ShouldBeSettable()
    {
        // Arrange & Act
        var result = new SearchResult
        {
            FilePath = "/path/to/file.cs",
            Line = 42,
            Column = 10,
            Snippet = "public class TestClass",
            Score = 0.95,
            Metadata = new Dictionary<string, object> { ["type"] = "class" }
        };

        // Assert
        result.FilePath.Should().Be("/path/to/file.cs");
        result.Line.Should().Be(42);
        result.Column.Should().Be(10);
        result.Snippet.Should().Be("public class TestClass");
        result.Score.Should().Be(0.95);
        result.Metadata.Should().NotBeNull();
        result.Metadata!["type"].Should().Be("class");
    }

    [Test]
    public void GridColumn_Properties_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var column = new GridColumn();

        // Assert
        column.Name.Should().BeEmpty();
        column.DisplayName.Should().BeEmpty();
        column.Type.Should().Be("string");
        column.Sortable.Should().BeTrue();
        column.Filterable.Should().BeTrue();
        column.Width.Should().BeNull();
    }

    [Test]
    public void HierarchyNode_Properties_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var node = new HierarchyNode();

        // Assert
        node.Name.Should().BeEmpty();
        node.Type.Should().BeEmpty();
        node.Children.Should().NotBeNull();
        node.Children.Should().BeEmpty();
        node.Metadata.Should().BeNull();
    }

    [Test]
    public void TimelineEvent_Properties_ShouldBeSettable()
    {
        // Arrange
        var timestamp = DateTime.Now;
        var metadata = new Dictionary<string, object> { ["category"] = "build" };

        // Act
        var timelineEvent = new TimelineEvent
        {
            Timestamp = timestamp,
            Title = "Build Started",
            Description = "Compilation began",
            Type = "start",
            Metadata = metadata
        };

        // Assert
        timelineEvent.Timestamp.Should().Be(timestamp);
        timelineEvent.Title.Should().Be("Build Started");
        timelineEvent.Description.Should().Be("Compilation began");
        timelineEvent.Type.Should().Be("start");
        timelineEvent.Metadata.Should().BeSameAs(metadata);
    }

    [Test]
    public void DiagnosticItem_Properties_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var diagnostic = new DiagnosticItem();

        // Assert
        diagnostic.Severity.Should().Be("info");
        diagnostic.Message.Should().BeEmpty();
        diagnostic.FilePath.Should().BeNull();
        diagnostic.Line.Should().BeNull();
        diagnostic.Column.Should().BeNull();
        diagnostic.Code.Should().BeNull();
        diagnostic.Source.Should().BeNull();
    }
}