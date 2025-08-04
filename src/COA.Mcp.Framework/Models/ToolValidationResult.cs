using System.Collections.Generic;
using System.Linq;

namespace COA.Mcp.Framework;

/// <summary>
/// Result of tool validation.
/// </summary>
public class ToolValidationResult
{
    /// <summary>
    /// Gets whether the validation passed.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public List<string> Errors { get; init; } = new();

    /// <summary>
    /// Gets the validation warnings.
    /// </summary>
    public List<string> Warnings { get; init; } = new();

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ToolValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// Creates a validation result with warnings but no errors.
    /// </summary>
    public static ToolValidationResult SuccessWithWarnings(params string[] warnings) => new()
    {
        IsValid = true,
        Warnings = warnings.ToList()
    };

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static ToolValidationResult Failure(params string[] errors) => new()
    {
        IsValid = false,
        Errors = errors.ToList()
    };

    /// <summary>
    /// Creates a failed validation result with warnings.
    /// </summary>
    public static ToolValidationResult FailureWithWarnings(string[] errors, string[] warnings) => new()
    {
        IsValid = false,
        Errors = errors.ToList(),
        Warnings = warnings.ToList()
    };

    /// <summary>
    /// Combines multiple validation results.
    /// </summary>
    public static ToolValidationResult Combine(params ToolValidationResult[] results)
    {
        var combined = new ToolValidationResult
        {
            IsValid = results.All(r => r.IsValid),
            Errors = results.SelectMany(r => r.Errors).ToList(),
            Warnings = results.SelectMany(r => r.Warnings).ToList()
        };
        
        return combined;
    }

    /// <summary>
    /// Gets a formatted error message.
    /// </summary>
    public string GetFormattedMessage()
    {
        var messages = new List<string>();
        
        if (Errors.Any())
        {
            messages.Add($"Errors: {string.Join("; ", Errors)}");
        }
        
        if (Warnings.Any())
        {
            messages.Add($"Warnings: {string.Join("; ", Warnings)}");
        }
        
        return string.Join(" | ", messages);
    }
}