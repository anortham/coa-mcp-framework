using NUnit.Framework;
using COA.Mcp.Framework.Models;
using System.Text.Json;
using FluentAssertions;

namespace COA.Mcp.Framework.Tests.Models;

[TestFixture]
public class ErrorModelsTests
{
    [Test]
    public void ErrorInfo_Should_SerializeCorrectly()
    {
        // Arrange
        var errorInfo = new ErrorInfo
        {
            Code = BaseErrorCodes.INVALID_PARAMETERS,
            Recovery = new RecoveryInfo
            {
                Steps = new[] 
                { 
                    "Check parameter format",
                    "Ensure all required fields are provided" 
                },
                SuggestedActions = new List<SuggestedAction>
                {
                    new SuggestedAction
                    {
                        Tool = "validate_parameters",
                        Description = "Validate the parameters before retrying",
                        Parameters = new { schema = "parameter_schema.json" }
                    }
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(errorInfo);
        var deserialized = JsonSerializer.Deserialize<ErrorInfo>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Code.Should().Be(BaseErrorCodes.INVALID_PARAMETERS);
        deserialized.Recovery.Should().NotBeNull();
        deserialized.Recovery!.Steps.Should().HaveCount(2);
        deserialized.Recovery.SuggestedActions.Should().HaveCount(1);
        deserialized.Recovery.SuggestedActions[0].Tool.Should().Be("validate_parameters");
    }

    [Test]
    public void RecoveryInfo_Should_InitializeWithEmptyCollections()
    {
        // Arrange & Act
        var recovery = new RecoveryInfo();

        // Assert
        recovery.Steps.Should().NotBeNull();
        recovery.Steps.Should().BeEmpty();
        recovery.SuggestedActions.Should().NotBeNull();
        recovery.SuggestedActions.Should().BeEmpty();
    }

    [Test]
    public void SuggestedAction_Should_AllowNullParameters()
    {
        // Arrange & Act
        var action = new SuggestedAction
        {
            Tool = "simple_tool",
            Description = "A simple action",
            Parameters = null
        };

        // Assert
        action.Parameters.Should().BeNull();
        var json = JsonSerializer.Serialize(action);
        json.Should().Contain("\"tool\":\"simple_tool\"");
        json.Should().Contain("\"description\":\"A simple action\"");
    }

    [TestCase(BaseErrorCodes.INTERNAL_ERROR, "INTERNAL_ERROR")]
    [TestCase(BaseErrorCodes.INVALID_PARAMETERS, "INVALID_PARAMETERS")]
    [TestCase(BaseErrorCodes.NOT_FOUND, "NOT_FOUND")]
    [TestCase(BaseErrorCodes.TIMEOUT, "TIMEOUT")]
    [TestCase(BaseErrorCodes.RESOURCE_LIMIT_EXCEEDED, "RESOURCE_LIMIT_EXCEEDED")]
    [TestCase(BaseErrorCodes.OPERATION_CANCELLED, "OPERATION_CANCELLED")]
    public void BaseErrorCodes_Should_HaveCorrectValues(string errorCode, string expectedValue)
    {
        // Assert
        errorCode.Should().Be(expectedValue);
    }

    [Test]
    public void ErrorInfo_Should_SerializeWithCorrectJsonPropertyNames()
    {
        // Arrange
        var errorInfo = new ErrorInfo
        {
            Code = "TEST_ERROR",
            Recovery = new RecoveryInfo
            {
                Steps = new[] { "Step 1" },
                SuggestedActions = new List<SuggestedAction>
                {
                    new SuggestedAction
                    {
                        Tool = "test_tool",
                        Description = "Test description",
                        Parameters = new { key = "value" }
                    }
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(errorInfo);

        // Assert
        json.Should().Contain("\"code\":\"TEST_ERROR\"");
        json.Should().Contain("\"recovery\":");
        json.Should().Contain("\"steps\":");
        json.Should().Contain("\"suggestedActions\":");
        json.Should().Contain("\"tool\":\"test_tool\"");
        json.Should().Contain("\"description\":\"Test description\"");
        json.Should().Contain("\"parameters\":");
    }

    [Test]
    public void SuggestedAction_Should_HandleComplexParameters()
    {
        // Arrange
        var complexParams = new
        {
            stringValue = "test",
            numberValue = 42,
            boolValue = true,
            arrayValue = new[] { 1, 2, 3 },
            nestedObject = new { key = "value" }
        };

        var action = new SuggestedAction
        {
            Tool = "complex_tool",
            Description = "Complex action",
            Parameters = complexParams
        };

        // Act
        var json = JsonSerializer.Serialize(action);
        var deserialized = JsonSerializer.Deserialize<SuggestedAction>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Tool.Should().Be("complex_tool");
        deserialized.Description.Should().Be("Complex action");
        deserialized.Parameters.Should().NotBeNull();
        
        var parametersJson = JsonSerializer.Serialize(deserialized.Parameters);
        parametersJson.Should().Contain("\"stringValue\":\"test\"");
        parametersJson.Should().Contain("\"numberValue\":42");
        parametersJson.Should().Contain("\"boolValue\":true");
    }
}