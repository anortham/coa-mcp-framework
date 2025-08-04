using System.Linq;
using System.Reflection;
using COA.Mcp.Framework.Registration;
using FluentAssertions;
using NUnit.Framework;

namespace COA.Mcp.Framework.Tests;

[TestFixture]
public class ToolDiscoveryTests
{
    private ToolDiscoveryService _discoveryService;

    [SetUp]
    public void Setup()
    {
        _discoveryService = new ToolDiscoveryService();
    }

    [Test]
    public void DiscoverTools_WithValidAssembly_FindsTools()
    {
        // Arrange
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var tools = _discoveryService.DiscoverTools(assembly).ToList();

        // Assert
        tools.Should().NotBeEmpty();
        tools.Should().Contain(t => t.Name == "greet");
    }

    [Test]
    public void DiscoverTools_WithExampleTool_ExtractsCorrectMetadata()
    {
        // Arrange
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var tools = _discoveryService.DiscoverTools(assembly).ToList();
        var greetTool = tools.FirstOrDefault(t => t.Name == "greet");

        // Assert
        greetTool.Should().NotBeNull();
        greetTool!.Description.Should().Contain("Generates a friendly greeting");
        greetTool.DeclaringType.Should().Be(typeof(ExampleTool));
        greetTool.ParameterType.Should().Be(typeof(GreetParams));
        greetTool.Parameters.Should().NotBeNull();
        greetTool.Parameters!.Properties.Should().ContainKey("Name");
        greetTool.Parameters.Required.Should().Contain("Name");
    }

    [Test]
    public void ValidateToolType_WithValidType_ReturnsSuccess()
    {
        // Arrange
        var type = typeof(ExampleTool);

        // Act
        var result = _discoveryService.ValidateToolType(type);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Test]
    public void ValidateToolMethod_WithValidMethod_ReturnsSuccess()
    {
        // Arrange
        var method = typeof(ExampleTool).GetMethod("GreetAsync");

        // Act
        var result = _discoveryService.ValidateToolMethod(method!);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}