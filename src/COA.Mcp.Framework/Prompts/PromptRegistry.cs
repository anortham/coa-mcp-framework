using COA.Mcp.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.Prompts;

/// <summary>
/// Central registry for managing MCP prompt templates and handling prompt requests.
/// Coordinates between different prompt templates to offer a unified prompt API.
/// </summary>
public class PromptRegistry : IPromptRegistry
{
    private readonly ILogger<PromptRegistry> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, IPrompt> _prompts = new();
    private readonly Dictionary<string, Type> _promptTypes = new();

    public PromptRegistry(ILogger<PromptRegistry> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public async Task<List<Prompt>> ListPromptsAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // Satisfy async requirement
        
        var prompts = new List<Prompt>();

        // Add registered instances
        foreach (var prompt in _prompts.Values)
        {
            try
            {
                prompts.Add(new Prompt
                {
                    Name = prompt.Name,
                    Description = prompt.Description,
                    Arguments = prompt.Arguments
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating prompt definition for {PromptName}", prompt.Name);
                // Continue with other prompts
            }
        }

        // Add registered types
        foreach (var kvp in _promptTypes)
        {
            if (_prompts.ContainsKey(kvp.Key))
            {
                continue; // Already added as instance
            }

            try
            {
                var prompt = (IPrompt)_serviceProvider.GetRequiredService(kvp.Value);
                prompts.Add(new Prompt
                {
                    Name = prompt.Name,
                    Description = prompt.Description,
                    Arguments = prompt.Arguments
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating prompt definition for type {PromptType}", kvp.Value.Name);
                // Continue with other prompts
            }
        }

        _logger.LogDebug("Listed {Count} prompts from {InstanceCount} instances and {TypeCount} types", 
            prompts.Count, _prompts.Count, _promptTypes.Count);

        return prompts;
    }

    /// <inheritdoc />
    public async Task<GetPromptResult> GetPromptAsync(string name, Dictionary<string, object>? arguments = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Prompt name cannot be null or empty", nameof(name));
        }

        // Try to get registered instance first
        if (_prompts.TryGetValue(name, out var prompt))
        {
            return await RenderPromptAsync(prompt, name, arguments, cancellationToken);
        }

        // Try to get registered type
        if (_promptTypes.TryGetValue(name, out var promptType))
        {
            prompt = (IPrompt)_serviceProvider.GetRequiredService(promptType);
            return await RenderPromptAsync(prompt, name, arguments, cancellationToken);
        }

        _logger.LogWarning("Prompt not found: {PromptName}", name);
        throw new InvalidOperationException($"Prompt '{name}' not found");
    }

    private async Task<GetPromptResult> RenderPromptAsync(IPrompt prompt, string name, Dictionary<string, object>? arguments, CancellationToken cancellationToken)
    {
        try
        {
            // Validate arguments
            var validation = prompt.ValidateArguments(arguments);
            if (!validation.IsValid)
            {
                var errorMessage = $"Invalid arguments for prompt '{name}': {string.Join(", ", validation.Errors)}";
                _logger.LogWarning(errorMessage);
                throw new ArgumentException(errorMessage);
            }

            // Log warnings if any
            foreach (var warning in validation.Warnings)
            {
                _logger.LogWarning("Prompt {PromptName} argument warning: {Warning}", name, warning);
            }

            // Render the prompt
            var result = await prompt.RenderAsync(arguments, cancellationToken);
            
            _logger.LogDebug("Successfully rendered prompt {PromptName} with {MessageCount} messages", 
                name, result.Messages.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering prompt {PromptName}", name);
            throw;
        }
    }

    /// <inheritdoc />
    public void RegisterPrompt(IPrompt prompt)
    {
        if (prompt == null)
        {
            throw new ArgumentNullException(nameof(prompt));
        }

        if (string.IsNullOrWhiteSpace(prompt.Name))
        {
            throw new ArgumentException("Prompt name cannot be null or empty", nameof(prompt));
        }

        // Check for duplicate names
        if (_prompts.ContainsKey(prompt.Name) || _promptTypes.ContainsKey(prompt.Name))
        {
            _logger.LogWarning("Replacing existing prompt: {PromptName}", prompt.Name);
        }

        _prompts[prompt.Name] = prompt;
        _promptTypes.Remove(prompt.Name); // Remove from types if it was there
        
        _logger.LogInformation("Registered prompt: {PromptName} - {Description}", 
            prompt.Name, prompt.Description);
    }

    /// <inheritdoc />
    public void RegisterPromptType<TPrompt>() where TPrompt : IPrompt
    {
        var promptType = typeof(TPrompt);
        
        // Create a temporary instance to get the name
        var tempPrompt = (IPrompt)ActivatorUtilities.CreateInstance(_serviceProvider, promptType);
        var name = tempPrompt.Name;

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException($"Prompt type {promptType.Name} has null or empty name");
        }

        // Check for duplicate names
        if (_prompts.ContainsKey(name) || _promptTypes.ContainsKey(name))
        {
            _logger.LogWarning("Replacing existing prompt: {PromptName}", name);
        }

        _promptTypes[name] = promptType;
        _prompts.Remove(name); // Remove from instances if it was there

        _logger.LogInformation("Registered prompt type: {PromptType} with name {PromptName}", 
            promptType.Name, name);
    }

    /// <inheritdoc />
    public IEnumerable<string> GetPromptNames()
    {
        return _prompts.Keys.Concat(_promptTypes.Keys).Distinct();
    }

    /// <inheritdoc />
    public bool HasPrompt(string name)
    {
        return _prompts.ContainsKey(name) || _promptTypes.ContainsKey(name);
    }
}