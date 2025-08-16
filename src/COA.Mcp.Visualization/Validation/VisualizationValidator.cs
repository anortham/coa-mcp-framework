using System;
using System.Collections.Generic;
using System.Linq;

namespace COA.Mcp.Visualization.Validation;

/// <summary>
/// Validator for visualization descriptors and related objects
/// </summary>
public static class VisualizationValidator
{
    /// <summary>
    /// Validates a visualization descriptor
    /// </summary>
    /// <param name="descriptor">The descriptor to validate</param>
    /// <returns>Validation result with any errors found</returns>
    public static ValidationResult Validate(VisualizationDescriptor descriptor)
    {
        var errors = new List<string>();

        if (descriptor == null)
        {
            errors.Add("Visualization descriptor cannot be null");
            return new ValidationResult(false, errors);
        }

        // Validate required fields
        if (string.IsNullOrWhiteSpace(descriptor.Type))
        {
            errors.Add("Visualization type is required and cannot be empty");
        }

        if (descriptor.Data == null)
        {
            errors.Add("Visualization data is required and cannot be null");
        }

        // Validate type format
        if (!string.IsNullOrWhiteSpace(descriptor.Type))
        {
            if (!IsValidVisualizationType(descriptor.Type))
            {
                errors.Add($"Invalid visualization type '{descriptor.Type}'. Type should be lowercase with hyphens (e.g., 'search-results')");
            }
        }

        // Validate version format
        if (!string.IsNullOrEmpty(descriptor.Version) && !IsValidVersionFormat(descriptor.Version))
        {
            errors.Add($"Invalid version format '{descriptor.Version}'. Expected format: 'x.y' or 'x.y.z'");
        }

        // Validate hint if present
        if (descriptor.Hint != null)
        {
            var hintValidation = ValidateHint(descriptor.Hint);
            if (!hintValidation.IsValid)
            {
                errors.AddRange(hintValidation.Errors);
            }
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// Validates a visualization hint
    /// </summary>
    /// <param name="hint">The hint to validate</param>
    /// <returns>Validation result with any errors found</returns>
    public static ValidationResult ValidateHint(VisualizationHint hint)
    {
        var errors = new List<string>();

        if (hint == null)
        {
            errors.Add("Visualization hint cannot be null");
            return new ValidationResult(false, errors);
        }

        // Validate preferred view if specified
        if (!string.IsNullOrWhiteSpace(hint.PreferredView))
        {
            if (!IsValidViewType(hint.PreferredView))
            {
                errors.Add($"Invalid preferred view '{hint.PreferredView}'. Valid values: grid, tree, chart, markdown, timeline, progress, auto");
            }
        }

        // Validate fallback format
        if (!string.IsNullOrWhiteSpace(hint.FallbackFormat))
        {
            if (!IsValidFallbackFormat(hint.FallbackFormat))
            {
                errors.Add($"Invalid fallback format '{hint.FallbackFormat}'. Valid values: json, csv, markdown, text");
            }
        }

        // Validate max concurrent tabs
        if (hint.MaxConcurrentTabs < 1 || hint.MaxConcurrentTabs > 20)
        {
            errors.Add($"MaxConcurrentTabs must be between 1 and 20, got {hint.MaxConcurrentTabs}");
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// Validates type-specific data structure
    /// </summary>
    /// <param name="type">The visualization type</param>
    /// <param name="data">The data to validate</param>
    /// <returns>Validation result with any errors found</returns>
    public static ValidationResult ValidateTypeSpecificData(string type, object? data)
    {
        var errors = new List<string>();

        if (data == null)
        {
            errors.Add("Data cannot be null");
            return new ValidationResult(false, errors);
        }

        try
        {
            // Validate based on standard types
            switch (type.ToLowerInvariant())
            {
                case StandardVisualizationTypes.SearchResults:
                    ValidateSearchResultsData(data, errors);
                    break;

                case StandardVisualizationTypes.DataGrid:
                    ValidateDataGridData(data, errors);
                    break;

                case StandardVisualizationTypes.Hierarchy:
                    ValidateHierarchyData(data, errors);
                    break;

                case StandardVisualizationTypes.Timeline:
                    ValidateTimelineData(data, errors);
                    break;

                case StandardVisualizationTypes.Progress:
                    ValidateProgressData(data, errors);
                    break;

                case StandardVisualizationTypes.Diagnostic:
                    ValidateDiagnosticData(data, errors);
                    break;

                // For unknown types, just ensure data is not null (already checked above)
                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Error validating data structure: {ex.Message}");
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    private static bool IsValidVisualizationType(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return false;

        // Should be lowercase with hyphens, no spaces
        return type.All(c => char.IsLower(c) || c == '-' || char.IsDigit(c)) &&
               !type.StartsWith('-') &&
               !type.EndsWith('-') &&
               !type.Contains("--");
    }

    private static bool IsValidVersionFormat(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return false;

        var parts = version.Split('.');
        return parts.Length >= 2 && parts.Length <= 3 &&
               parts.All(part => int.TryParse(part, out _));
    }

    private static bool IsValidViewType(string viewType)
    {
        var validViews = new[] { "grid", "tree", "chart", "markdown", "timeline", "progress", "auto" };
        return validViews.Contains(viewType?.ToLowerInvariant());
    }

    private static bool IsValidFallbackFormat(string fallbackFormat)
    {
        var validFormats = new[] { "json", "csv", "markdown", "text" };
        return validFormats.Contains(fallbackFormat?.ToLowerInvariant());
    }

    private static void ValidateSearchResultsData(object data, List<string> errors)
    {
        // Basic validation - in a real implementation, you'd use reflection or JSON deserialization
        // to validate the structure more thoroughly
        if (data.ToString()?.Contains("query") != true)
        {
            errors.Add("Search results data should contain a 'query' field");
        }
    }

    private static void ValidateDataGridData(object data, List<string> errors)
    {
        // Basic validation for data grid structure
        if (data.ToString()?.Contains("columns") != true)
        {
            errors.Add("Data grid data should contain a 'columns' field");
        }
    }

    private static void ValidateHierarchyData(object data, List<string> errors)
    {
        // Basic validation for hierarchy structure
        if (data.ToString()?.Contains("root") != true)
        {
            errors.Add("Hierarchy data should contain a 'root' field");
        }
    }

    private static void ValidateTimelineData(object data, List<string> errors)
    {
        // Basic validation for timeline structure
        if (data.ToString()?.Contains("events") != true)
        {
            errors.Add("Timeline data should contain an 'events' field");
        }
    }

    private static void ValidateProgressData(object data, List<string> errors)
    {
        // Basic validation for progress structure
        var dataStr = data.ToString() ?? "";
        if (!dataStr.Contains("current") || !dataStr.Contains("total"))
        {
            errors.Add("Progress data should contain 'current' and 'total' fields");
        }
    }

    private static void ValidateDiagnosticData(object data, List<string> errors)
    {
        // Basic validation for diagnostic structure
        if (data.ToString()?.Contains("diagnostics") != true)
        {
            errors.Add("Diagnostic data should contain a 'diagnostics' field");
        }
    }
}

/// <summary>
/// Result of a validation operation
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Whether the validation passed
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// List of validation errors (empty if valid)
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Creates a new validation result
    /// </summary>
    /// <param name="isValid">Whether validation passed</param>
    /// <param name="errors">List of errors</param>
    public ValidationResult(bool isValid, IEnumerable<string> errors)
    {
        IsValid = isValid;
        Errors = errors.ToList().AsReadOnly();
    }

    /// <summary>
    /// Creates a successful validation result
    /// </summary>
    /// <returns>Valid result with no errors</returns>
    public static ValidationResult Success() => new(true, Array.Empty<string>());

    /// <summary>
    /// Creates a failed validation result with a single error
    /// </summary>
    /// <param name="error">The error message</param>
    /// <returns>Invalid result with the error</returns>
    public static ValidationResult Failure(string error) => new(false, new[] { error });

    /// <summary>
    /// Creates a failed validation result with multiple errors
    /// </summary>
    /// <param name="errors">The error messages</param>
    /// <returns>Invalid result with the errors</returns>
    public static ValidationResult Failure(IEnumerable<string> errors) => new(false, errors);
}