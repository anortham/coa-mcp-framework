using COA.Mcp.Framework.Testing;
using COA.Mcp.Framework.Testing.Assertions;
using COA.Mcp.Framework.Testing.Builders;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using McpServerTemplate.Tools;

namespace McpServerTemplate.Tests;

[TestFixture]
public class HelloWorldToolTests : ToolTestBase<HelloWorldTool>
{
    protected override HelloWorldTool CreateTool()
    {
        return new HelloWorldTool(Logger);
    }

    [Test]
    public async Task HelloWorld_WithName_ReturnsPersonalizedGreeting()
    {
        // Arrange
        var parameters = new ToolParameterBuilder<HelloWorldParams>()
            .With(p => p.Name, "Alice")
            .Build();

        // Act
        var result = await Tool.ExecuteAsync(parameters);

        // Assert
        result.Should().BeSuccessful()
            .And.HaveProperty("Message", "Hello, Alice!");
    }

    [Test]
    public async Task HelloWorld_WithoutName_ReturnsDefaultGreeting()
    {
        // Arrange
        var parameters = new HelloWorldParams();

        // Act
        var result = await Tool.ExecuteAsync(parameters);

        // Assert
        result.Should().BeSuccessful()
            .And.HaveProperty("Message", "Hello, World!");
    }

    [Test]
    public async Task HelloWorld_WithTime_IncludesTimeInResponse()
    {
        // Arrange
        var parameters = new ToolParameterBuilder<HelloWorldParams>()
            .With(p => p.Name, "Bob")
            .With(p => p.IncludeTime, true)
            .Build();

        // Act
        var result = await Tool.ExecuteAsync(parameters);

        // Assert
        result.Should().BeSuccessful();
        
        var message = result.GetPropertyValue<string>("Message");
        message.Should().Contain("Bob")
            .And.Contain("UTC");
    }

    [Test]
    public async Task HelloWorld_Always_ReturnsInsightsAndActions()
    {
        // Arrange
        var parameters = new HelloWorldParams { Name = "Test" };

        // Act
        var result = await Tool.ExecuteAsync(parameters);

        // Assert
        result.Should().BeSuccessful()
            .And.HaveInsights()
            .And.HaveActions();

        var insights = result.GetPropertyValue<string[]>("Insights");
        insights.Should().HaveCount(3)
            .And.Contain(i => i.Contains("Greeted Test successfully"));

        var actions = result.GetPropertyValue<dynamic[]>("Actions");
        actions.Should().HaveCountGreaterThan(0);
    }

    [Test]
    public async Task HelloWorld_Always_IncludesExecutionMetadata()
    {
        // Arrange
        var parameters = new HelloWorldParams();

        // Act
        var result = await Tool.ExecuteAsync(parameters);

        // Assert
        result.Should().BeSuccessful()
            .And.HaveMetadata();

        var meta = result.GetPropertyValue<dynamic>("Meta");
        ((object)meta).Should().HaveProperty("ExecutionTime")
            .And.HaveProperty("TokensEstimated")
            .And.HaveProperty("ToolVersion", "1.0.0");
    }
}