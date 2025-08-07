using COA.Mcp.Framework.Prompts;
using COA.Mcp.Protocol;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace COA.Mcp.Framework.Tests.Prompts;

[TestFixture]
public class PromptRegistryTests
{
    private class TestPrompt : PromptBase
    {
        public override string Name => "test-prompt";
        public override string Description => "Test prompt";
        
        public override List<PromptArgument> Arguments => new()
        {
            new PromptArgument { Name = "arg1", Required = true }
        };

        public override Task<GetPromptResult> RenderAsync(Dictionary<string, object>? arguments = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new GetPromptResult
            {
                Description = Description,
                Messages = new List<PromptMessage>
                {
                    CreateSystemMessage("Test message")
                }
            });
        }
    }

    private class AnotherTestPrompt : PromptBase
    {
        public override string Name => "another-prompt";
        public override string Description => "Another test prompt";
        public override List<PromptArgument> Arguments => new();

        public override Task<GetPromptResult> RenderAsync(Dictionary<string, object>? arguments = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new GetPromptResult
            {
                Messages = new List<PromptMessage>()
            });
        }
    }

    private class ServiceDependentPrompt : PromptBase
    {
        private readonly ILogger<ServiceDependentPrompt> _logger;

        public ServiceDependentPrompt(ILogger<ServiceDependentPrompt> logger)
        {
            _logger = logger;
        }

        public override string Name => "service-prompt";
        public override string Description => "Prompt with service dependency";
        public override List<PromptArgument> Arguments => new();

        public override Task<GetPromptResult> RenderAsync(Dictionary<string, object>? arguments = null, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Rendering prompt");
            return Task.FromResult(new GetPromptResult { Messages = new List<PromptMessage>() });
        }
    }

    private IPromptRegistry CreateRegistry()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IPromptRegistry, PromptRegistry>();
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IPromptRegistry>();
    }

    [Test]
    public void RegisterPrompt_WithValidPrompt_ShouldRegister()
    {
        // Arrange
        var registry = CreateRegistry();
        var prompt = new TestPrompt();

        // Act
        registry.RegisterPrompt(prompt);

        // Assert
        registry.HasPrompt("test-prompt").Should().BeTrue();
        registry.GetPromptNames().Should().Contain("test-prompt");
    }

    [Test]
    public void RegisterPrompt_WithNullPrompt_ShouldThrow()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act & Assert
        var act = () => registry.RegisterPrompt(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void RegisterPrompt_DuplicateName_ShouldReplace()
    {
        // Arrange
        var registry = CreateRegistry();
        var prompt1 = new TestPrompt();
        var prompt2 = new TestPrompt();

        // Act
        registry.RegisterPrompt(prompt1);
        registry.RegisterPrompt(prompt2); // Should log warning but not throw

        // Assert
        registry.HasPrompt("test-prompt").Should().BeTrue();
    }

    [Test]
    public void RegisterPromptType_WithValidType_ShouldRegister()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IPromptRegistry, PromptRegistry>();
        services.AddTransient<ServiceDependentPrompt>();
        var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IPromptRegistry>();

        // Act
        registry.RegisterPromptType<ServiceDependentPrompt>();

        // Assert
        registry.HasPrompt("service-prompt").Should().BeTrue();
    }

    [Test]
    public async Task ListPromptsAsync_ShouldReturnAllPrompts()
    {
        // Arrange
        var registry = CreateRegistry();
        registry.RegisterPrompt(new TestPrompt());
        registry.RegisterPrompt(new AnotherTestPrompt());

        // Act
        var prompts = await registry.ListPromptsAsync();

        // Assert
        prompts.Should().HaveCount(2);
        prompts.Should().Contain(p => p.Name == "test-prompt");
        prompts.Should().Contain(p => p.Name == "another-prompt");
        prompts.First(p => p.Name == "test-prompt").Arguments.Should().HaveCount(1);
    }

    [Test]
    public async Task GetPromptAsync_WithValidName_ShouldReturnRenderedPrompt()
    {
        // Arrange
        var registry = CreateRegistry();
        registry.RegisterPrompt(new TestPrompt());
        var arguments = new Dictionary<string, object> { ["arg1"] = "value" };

        // Act
        var result = await registry.GetPromptAsync("test-prompt", arguments);

        // Assert
        result.Should().NotBeNull();
        result.Description.Should().Be("Test prompt");
        result.Messages.Should().HaveCount(1);
        result.Messages[0].Role.Should().Be("system");
    }

    [Test]
    public async Task GetPromptAsync_WithInvalidName_ShouldThrow()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act & Assert
        var act = async () => await registry.GetPromptAsync("non-existent", null);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*non-existent*not found*");
    }

    [Test]
    public async Task GetPromptAsync_WithInvalidArguments_ShouldThrow()
    {
        // Arrange
        var registry = CreateRegistry();
        registry.RegisterPrompt(new TestPrompt());
        var arguments = new Dictionary<string, object>(); // Missing required arg1

        // Act & Assert
        var act = async () => await registry.GetPromptAsync("test-prompt", arguments);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid arguments*arg1*");
    }

    [Test]
    public async Task GetPromptAsync_WithWarnings_ShouldLogButNotThrow()
    {
        // Arrange
        var registry = CreateRegistry();
        registry.RegisterPrompt(new TestPrompt());
        var arguments = new Dictionary<string, object>
        {
            ["arg1"] = "value",
            ["unknown"] = "extra" // Unknown argument should generate warning
        };

        // Act
        var result = await registry.GetPromptAsync("test-prompt", arguments);

        // Assert
        result.Should().NotBeNull();
        // The warning should be logged but not throw
    }

    [Test]
    public void HasPrompt_WithRegisteredPrompt_ShouldReturnTrue()
    {
        // Arrange
        var registry = CreateRegistry();
        registry.RegisterPrompt(new TestPrompt());

        // Act & Assert
        registry.HasPrompt("test-prompt").Should().BeTrue();
    }

    [Test]
    public void HasPrompt_WithUnregisteredPrompt_ShouldReturnFalse()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act & Assert
        registry.HasPrompt("non-existent").Should().BeFalse();
    }

    [Test]
    public void GetPromptNames_ShouldReturnAllNames()
    {
        // Arrange
        var registry = CreateRegistry();
        registry.RegisterPrompt(new TestPrompt());
        registry.RegisterPrompt(new AnotherTestPrompt());

        // Act
        var names = registry.GetPromptNames().ToList();

        // Assert
        names.Should().HaveCount(2);
        names.Should().Contain("test-prompt");
        names.Should().Contain("another-prompt");
    }

    [Test]
    public void GetPromptNames_WithNoPrompts_ShouldReturnEmpty()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var names = registry.GetPromptNames();

        // Assert
        names.Should().BeEmpty();
    }

    [Test]
    public async Task GetPromptAsync_FromType_ShouldCreateInstanceWithDI()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IPromptRegistry, PromptRegistry>();
        services.AddTransient<ServiceDependentPrompt>();
        var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IPromptRegistry>();
        
        registry.RegisterPromptType<ServiceDependentPrompt>();

        // Act
        var result = await registry.GetPromptAsync("service-prompt", null);

        // Assert
        result.Should().NotBeNull();
        result.Messages.Should().NotBeNull();
    }
}