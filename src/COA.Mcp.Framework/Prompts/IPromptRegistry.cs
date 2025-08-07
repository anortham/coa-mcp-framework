using COA.Mcp.Protocol;

namespace COA.Mcp.Framework.Prompts;

/// <summary>
/// Service for managing and providing access to MCP prompts.
/// Prompts are interactive templates that guide users through complex operations.
/// </summary>
public interface IPromptRegistry
{
    /// <summary>
    /// Gets a list of all available prompts.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of available prompts.</returns>
    Task<List<Prompt>> ListPromptsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific prompt by name with arguments applied.
    /// </summary>
    /// <param name="name">The name of the prompt to retrieve.</param>
    /// <param name="arguments">Arguments to customize the prompt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rendered prompt with messages.</returns>
    Task<GetPromptResult> GetPromptAsync(string name, Dictionary<string, object>? arguments = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a prompt that can be invoked by clients.
    /// </summary>
    /// <param name="prompt">The prompt to register.</param>
    void RegisterPrompt(IPrompt prompt);

    /// <summary>
    /// Registers a prompt by type (for dependency injection).
    /// </summary>
    /// <typeparam name="TPrompt">The prompt type to register.</typeparam>
    void RegisterPromptType<TPrompt>() where TPrompt : IPrompt;

    /// <summary>
    /// Gets all registered prompt names.
    /// </summary>
    IEnumerable<string> GetPromptNames();

    /// <summary>
    /// Checks if a prompt is registered.
    /// </summary>
    /// <param name="name">The prompt name to check.</param>
    /// <returns>True if the prompt is registered.</returns>
    bool HasPrompt(string name);
}