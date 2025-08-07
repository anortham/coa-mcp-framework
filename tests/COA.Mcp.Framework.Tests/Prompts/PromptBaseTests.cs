using COA.Mcp.Framework.Prompts;
using COA.Mcp.Protocol;
using FluentAssertions;
using NUnit.Framework;

namespace COA.Mcp.Framework.Tests.Prompts;

[TestFixture]
public class PromptBaseTests
{
    private class TestPrompt : PromptBase
    {
        public override string Name => "test-prompt";
        public override string Description => "Test prompt for unit tests";
        
        public override List<PromptArgument> Arguments => new()
        {
            new PromptArgument { Name = "required_arg", Description = "Required argument", Required = true },
            new PromptArgument { Name = "optional_arg", Description = "Optional argument", Required = false }
        };

        public override Task<GetPromptResult> RenderAsync(Dictionary<string, object>? arguments = null, CancellationToken cancellationToken = default)
        {
            var requiredValue = GetRequiredArgument<string>(arguments, "required_arg");
            var optionalValue = GetOptionalArgument<string>(arguments, "optional_arg", "default");
            
            return Task.FromResult(new GetPromptResult
            {
                Description = Description,
                Messages = new List<PromptMessage>
                {
                    CreateSystemMessage($"System: {requiredValue}"),
                    CreateUserMessage($"User: {optionalValue}"),
                    CreateAssistantMessage("Assistant: Ready")
                }
            });
        }
    }

    [Test]
    public void ValidateArguments_WithAllRequiredArguments_ShouldReturnValid()
    {
        // Arrange
        var prompt = new TestPrompt();
        var arguments = new Dictionary<string, object>
        {
            ["required_arg"] = "test value"
        };

        // Act
        var result = prompt.ValidateArguments(arguments);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Test]
    public void ValidateArguments_MissingRequiredArgument_ShouldReturnInvalid()
    {
        // Arrange
        var prompt = new TestPrompt();
        var arguments = new Dictionary<string, object>
        {
            ["optional_arg"] = "optional value"
        };

        // Act
        var result = prompt.ValidateArguments(arguments);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("required_arg"));
    }

    [Test]
    public void ValidateArguments_WithUnknownArgument_ShouldReturnWarning()
    {
        // Arrange
        var prompt = new TestPrompt();
        var arguments = new Dictionary<string, object>
        {
            ["required_arg"] = "test value",
            ["unknown_arg"] = "unknown value"
        };

        // Act
        var result = prompt.ValidateArguments(arguments);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle(w => w.Contains("unknown_arg"));
    }

    [Test]
    public void ValidateArguments_WithNullArguments_ShouldCheckRequired()
    {
        // Arrange
        var prompt = new TestPrompt();

        // Act
        var result = prompt.ValidateArguments(null);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("required_arg"));
    }

    [Test]
    public async Task RenderAsync_WithValidArguments_ShouldGenerateMessages()
    {
        // Arrange
        var prompt = new TestPrompt();
        var arguments = new Dictionary<string, object>
        {
            ["required_arg"] = "test",
            ["optional_arg"] = "custom"
        };

        // Act
        var result = await prompt.RenderAsync(arguments);

        // Assert
        result.Should().NotBeNull();
        result.Messages.Should().HaveCount(3);
        result.Messages[0].Role.Should().Be("system");
        result.Messages[0].Content.Text.Should().Contain("test");
        result.Messages[1].Role.Should().Be("user");
        result.Messages[1].Content.Text.Should().Contain("custom");
        result.Messages[2].Role.Should().Be("assistant");
    }

    // Note: CreateSystemMessage, CreateUserMessage, CreateAssistantMessage are protected
    // They are tested indirectly through RenderAsync tests above
    
    // Note: SubstituteVariables is protected and tested through prompt implementations
    
    // Note: GetRequiredArgument and GetOptionalArgument are protected
    // They are tested through the TestPrompt.RenderAsync implementation
}