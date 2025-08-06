using NUnit.Framework;
using COA.Mcp.Framework.Models;
using System.Text.Json;
using FluentAssertions;

namespace COA.Mcp.Framework.Tests.Models;

[TestFixture]
public class ToolResultBaseTests
{
    // Test implementation of ToolResultBase for testing
    private class TestToolResult : ToolResultBase
    {
        public override string Operation => "test_operation";
        
        // Additional property for testing
        [System.Text.Json.Serialization.JsonPropertyName("testData")]
        public string? TestData { get; set; }
    }

    [Test]
    public void ToolResultBase_Should_SerializeCorrectly()
    {
        // Arrange
        var result = new TestToolResult
        {
            Success = true,
            Message = "Operation completed successfully",
            TestData = "Some test data",
            Insights = new List<string> { "Insight 1", "Insight 2" },
            Actions = new List<AIAction>
            {
                new AIAction
                {
                    Action = "next_action",
                    Description = "Perform the next action",
                    Priority = 75
                }
            },
            Meta = new ToolExecutionMetadata
            {
                Mode = "summary",
                Truncated = false,
                ExecutionTime = "125ms",
                Tokens = 1500
            },
            ResourceUri = "resource://test/123"
        };

        // Act
        var json = JsonSerializer.Serialize(result);
        var deserialized = JsonSerializer.Deserialize<TestToolResult>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Success.Should().BeTrue();
        deserialized.Operation.Should().Be("test_operation");
        deserialized.Message.Should().Be("Operation completed successfully");
        deserialized.TestData.Should().Be("Some test data");
        deserialized.Insights.Should().HaveCount(2);
        deserialized.Actions.Should().HaveCount(1);
        deserialized.Meta.Should().NotBeNull();
        deserialized.Meta!.ExecutionTime.Should().Be("125ms");
        deserialized.ResourceUri.Should().Be("resource://test/123");
    }

    [Test]
    public void ToolResultBase_Should_HandleErrorResult()
    {
        // Arrange
        var result = new TestToolResult
        {
            Success = false,
            Message = "Operation failed",
            Error = new ErrorInfo
            {
                Code = BaseErrorCodes.TIMEOUT,
                Recovery = new RecoveryInfo
                {
                    Steps = new[] { "Retry with longer timeout" },
                    SuggestedActions = new List<SuggestedAction>
                    {
                        new SuggestedAction
                        {
                            Tool = "retry_with_timeout",
                            Description = "Retry the operation with a longer timeout",
                            Parameters = new { timeout = 60000 }
                        }
                    }
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(result);
        var deserialized = JsonSerializer.Deserialize<TestToolResult>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Success.Should().BeFalse();
        deserialized.Error.Should().NotBeNull();
        deserialized.Error!.Code.Should().Be(BaseErrorCodes.TIMEOUT);
        deserialized.Error.Recovery.Should().NotBeNull();
        deserialized.Error.Recovery!.Steps.Should().HaveCount(1);
        deserialized.Error.Recovery.SuggestedActions.Should().HaveCount(1);
    }

    [Test]
    public void ToolExecutionMetadata_Should_HaveCorrectDefaults()
    {
        // Arrange & Act
        var metadata = new ToolExecutionMetadata();

        // Assert
        metadata.Mode.Should().Be("full");
        metadata.Truncated.Should().BeFalse();
        metadata.Tokens.Should().BeNull();
        metadata.Cached.Should().BeNull();
        metadata.ExecutionTime.Should().BeNull();
    }

    [Test]
    public void ToolExecutionMetadata_Should_SerializeCorrectly()
    {
        // Arrange
        var metadata = new ToolExecutionMetadata
        {
            Mode = "summary",
            Truncated = true,
            Tokens = 2500,
            Cached = "hit",
            ExecutionTime = "45.2ms"
        };

        // Act
        var json = JsonSerializer.Serialize(metadata);

        // Assert
        json.Should().Contain("\"mode\":\"summary\"");
        json.Should().Contain("\"truncated\":true");
        json.Should().Contain("\"tokens\":2500");
        json.Should().Contain("\"cached\":\"hit\"");
        json.Should().Contain("\"executionTime\":\"45.2ms\"");
    }

    [Test]
    public void QueryInfo_Should_SerializeWithAllProperties()
    {
        // Arrange
        var query = new QueryInfo
        {
            Workspace = "C:\\Projects\\TestProject",
            FilePath = "src\\Program.cs",
            Position = new PositionInfo { Line = 42, Column = 15 },
            TargetSymbol = "TestMethod",
            GenerationType = "constructor",
            AdditionalParams = new Dictionary<string, object>
            {
                { "includePrivate", true },
                { "maxResults", 100 }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(query);
        var deserialized = JsonSerializer.Deserialize<QueryInfo>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Workspace.Should().Be("C:\\Projects\\TestProject");
        deserialized.FilePath.Should().Be("src\\Program.cs");
        deserialized.Position.Should().NotBeNull();
        deserialized.Position!.Line.Should().Be(42);
        deserialized.Position.Column.Should().Be(15);
        deserialized.TargetSymbol.Should().Be("TestMethod");
        deserialized.GenerationType.Should().Be("constructor");
        deserialized.AdditionalParams.Should().HaveCount(2);
    }

    [Test]
    public void SummaryInfo_Should_IncludeSymbolSummary()
    {
        // Arrange
        var summary = new SummaryInfo
        {
            TotalFound = 25,
            Returned = 10,
            ExecutionTime = "123ms",
            SymbolInfo = new SymbolSummary
            {
                Name = "GetUserById",
                Kind = "Method",
                ContainingType = "UserService",
                Namespace = "MyApp.Services"
            }
        };

        // Act
        var json = JsonSerializer.Serialize(summary);
        var deserialized = JsonSerializer.Deserialize<SummaryInfo>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.TotalFound.Should().Be(25);
        deserialized.Returned.Should().Be(10);
        deserialized.ExecutionTime.Should().Be("123ms");
        deserialized.SymbolInfo.Should().NotBeNull();
        deserialized.SymbolInfo!.Name.Should().Be("GetUserById");
        deserialized.SymbolInfo.Kind.Should().Be("Method");
        deserialized.SymbolInfo.ContainingType.Should().Be("UserService");
        deserialized.SymbolInfo.Namespace.Should().Be("MyApp.Services");
    }

    [Test]
    public void ResultsSummary_Should_IndicateMoreResults()
    {
        // Arrange
        var summary = new ResultsSummary
        {
            Included = 50,
            Total = 150,
            HasMore = true
        };

        // Act
        var json = JsonSerializer.Serialize(summary);
        var deserialized = JsonSerializer.Deserialize<ResultsSummary>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Included.Should().Be(50);
        deserialized.Total.Should().Be(150);
        deserialized.HasMore.Should().BeTrue();
    }

    [Test]
    public void AIAction_Should_HaveCorrectDefaults()
    {
        // Arrange & Act
        var action = new AIAction();

        // Assert
        action.Action.Should().BeEmpty();
        action.Description.Should().BeEmpty();
        action.Priority.Should().Be(50);
        action.Rationale.Should().BeNull();
        action.Category.Should().BeNull();
        action.Parameters.Should().BeNull();
    }

    [Test]
    public void AIAction_ToolProperty_Should_AliasAction()
    {
        // Arrange
        var action = new AIAction();

        // Act
        action.Tool = "test_tool";

        // Assert
        action.Action.Should().Be("test_tool");
        action.Tool.Should().Be("test_tool");

        // Act again
        action.Action = "another_tool";

        // Assert
        action.Tool.Should().Be("another_tool");
        action.Action.Should().Be("another_tool");
    }

    [Test]
    public void AIAction_Should_SerializeWithAllProperties()
    {
        // Arrange
        var action = new AIAction
        {
            Action = "analyze_code",
            Description = "Analyze the code for issues",
            Rationale = "Code complexity is high",
            Category = "analysis",
            Priority = 85,
            Parameters = new Dictionary<string, object>
            {
                { "depth", "deep" },
                { "includeMetrics", true }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(action);
        var deserialized = JsonSerializer.Deserialize<AIAction>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Action.Should().Be("analyze_code");
        deserialized.Description.Should().Be("Analyze the code for issues");
        deserialized.Rationale.Should().Be("Code complexity is high");
        deserialized.Category.Should().Be("analysis");
        deserialized.Priority.Should().Be(85);
        deserialized.Parameters.Should().HaveCount(2);
        
        // Tool property should not be serialized
        json.Should().NotContain("\"tool\"");
    }

    [Test]
    public void PositionInfo_Should_StoreLineAndColumn()
    {
        // Arrange
        var position = new PositionInfo
        {
            Line = 100,
            Column = 25
        };

        // Act
        var json = JsonSerializer.Serialize(position);
        var deserialized = JsonSerializer.Deserialize<PositionInfo>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Line.Should().Be(100);
        deserialized.Column.Should().Be(25);
    }
}