using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.RegularExpressions;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Models;

namespace SimpleMcpServer.Tools;

/// <summary>
/// A tool for manipulating strings in various ways.
/// </summary>
public class StringManipulationTool : McpToolBase<StringManipulationParameters, StringManipulationResult>
{
    public override string Name => "string_manipulation";
    public override string Description => "Performs various string manipulation operations";
    public override ToolCategory Category => ToolCategory.Utility;

    protected override async Task<StringManipulationResult> ExecuteInternalAsync(
        StringManipulationParameters parameters, 
        CancellationToken cancellationToken)
    {
        // Validate inputs
        var text = ValidateRequired(parameters.Text, nameof(parameters.Text));
        var operation = ValidateRequired(parameters.Operation, nameof(parameters.Operation));

        string result;
        var stats = new Dictionary<string, object>();

        try
        {
            switch (operation.ToLower())
            {
                case "reverse":
                    result = new string(text.Reverse().ToArray());
                    break;

                case "uppercase":
                case "upper":
                    result = text.ToUpper();
                    break;

                case "lowercase":
                case "lower":
                    result = text.ToLower();
                    break;

                case "capitalize":
                    result = CapitalizeWords(text);
                    break;

                case "count_words":
                    var wordCount = CountWords(text);
                    result = text; // Return original text
                    stats["word_count"] = wordCount;
                    stats["character_count"] = text.Length;
                    stats["line_count"] = text.Split('\n').Length;
                    break;

                case "remove_spaces":
                    result = Regex.Replace(text, @"\s+", "");
                    stats["removed_characters"] = text.Length - result.Length;
                    break;

                case "trim":
                    result = text.Trim();
                    stats["removed_characters"] = text.Length - result.Length;
                    break;

                case "replace":
                    var find = ValidateRequired(parameters.Find, "Find");
                    var replaceWith = parameters.ReplaceWith ?? string.Empty;
                    result = text.Replace(find, replaceWith);
                    stats["replacements_made"] = (text.Length - result.Length) / Math.Max(1, find.Length - replaceWith.Length);
                    break;

                case "extract_numbers":
                    var numbers = Regex.Matches(text, @"-?\d+\.?\d*")
                        .Select(m => m.Value)
                        .ToList();
                    result = string.Join(", ", numbers);
                    stats["numbers_found"] = numbers.Count;
                    break;

                case "extract_emails":
                    var emails = Regex.Matches(text, @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}")
                        .Select(m => m.Value)
                        .ToList();
                    result = string.Join(", ", emails);
                    stats["emails_found"] = emails.Count;
                    break;

                case "base64_encode":
                    var bytes = Encoding.UTF8.GetBytes(text);
                    result = Convert.ToBase64String(bytes);
                    break;

                case "base64_decode":
                    try
                    {
                        var decodedBytes = Convert.FromBase64String(text);
                        result = Encoding.UTF8.GetString(decodedBytes);
                    }
                    catch
                    {
                        return new StringManipulationResult
                        {
                            Success = false,
                            Error = new ErrorInfo
                            {
                                Code = "INVALID_BASE64",
                                Message = "Invalid base64 string"
                            }
                        };
                    }
                    break;

                default:
                    return new StringManipulationResult
                    {
                        Success = false,
                        Error = new ErrorInfo
                        {
                            Code = "UNKNOWN_OPERATION",
                            Message = $"Unknown operation: {operation}"
                        }
                    };
            }

            await Task.CompletedTask; // Simulate async work

            return new StringManipulationResult
            {
                Success = true,
                Original = text,
                Result = result,
                OperationPerformed = operation,
                Statistics = stats.Count > 0 ? stats : null
            };
        }
        catch (Exception ex)
        {
            return new StringManipulationResult
            {
                Success = false,
                Error = new ErrorInfo
                {
                    Code = "PROCESSING_ERROR",
                    Message = $"Error performing {operation}: {ex.Message}"
                }
            };
        }
    }

    private string CapitalizeWords(string text)
    {
        return Regex.Replace(text, @"\b\w", m => m.Value.ToUpper());
    }

    private int CountWords(string text)
    {
        return Regex.Matches(text, @"\b\w+\b").Count;
    }
}

public class StringManipulationParameters
{
    [Required]
    [Description("The text to manipulate")]
    public string Text { get; set; } = string.Empty;

    [Required]
    [Description("The operation to perform")]
    public string Operation { get; set; } = string.Empty;

    [Description("Text to find (for replace operation)")]
    public string? Find { get; set; }

    [Description("Text to replace with (for replace operation)")]
    public string? ReplaceWith { get; set; }
}

public class StringManipulationResult : ToolResultBase
{
    public override string Operation => "string_manipulation";
    public string? Original { get; set; }
    public string? Result { get; set; }
    public string? OperationPerformed { get; set; }
    public Dictionary<string, object>? Statistics { get; set; }
}