using COA.Mcp.Protocol;

namespace COA.Mcp.Framework.Prompts;

/// <summary>
/// Interface for MCP prompt templates that generate interactive workflows.
/// Prompts guide users through complex operations by providing templated message sequences.
/// </summary>
public interface IPrompt
{
    /// <summary>
    /// Gets the unique name of this prompt.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the human-readable description of what this prompt accomplishes.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the list of arguments that can be provided to customize this prompt.
    /// </summary>
    List<PromptArgument> Arguments { get; }

    /// <summary>
    /// Renders the prompt with the provided arguments, generating the final messages.
    /// </summary>
    /// <param name="arguments">Arguments to customize the prompt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rendered prompt result with messages.</returns>
    Task<GetPromptResult> RenderAsync(Dictionary<string, object>? arguments = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that the provided arguments are correct for this prompt.
    /// </summary>
    /// <param name="arguments">Arguments to validate.</param>
    /// <returns>Validation result with any errors.</returns>
    PromptValidationResult ValidateArguments(Dictionary<string, object>? arguments = null);
}

/// <summary>
/// Represents the result of prompt argument validation.
/// </summary>
public class PromptValidationResult
{
    /// <summary>
    /// Gets or sets whether the validation passed.
    /// </summary>
    public bool IsValid { get; set; }
    
    /// <summary>
    /// Gets or sets the list of validation errors.
    /// </summary>
    public List<string> Errors { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the list of validation warnings.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static PromptValidationResult Success() => new() { IsValid = true };
    
    /// <summary>
    /// Creates a failed validation result with errors.
    /// </summary>
    public static PromptValidationResult Failure(params string[] errors) => new() 
    { 
        IsValid = false, 
        Errors = errors.ToList() 
    };
}