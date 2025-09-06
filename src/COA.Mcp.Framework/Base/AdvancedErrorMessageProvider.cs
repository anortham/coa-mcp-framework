using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using COA.Mcp.Framework.Configuration;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework.Services;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.Base;

/// <summary>
/// Advanced error message provider that integrates with the error recovery template system
/// to provide professional, educational error messages with recovery guidance.
/// </summary>
public class AdvancedErrorMessageProvider : ErrorMessageProvider
{
    private readonly ErrorRecoveryTemplateProcessor? _recoveryProcessor;
    private readonly ErrorRecoveryOptions _options;
    private readonly ILogger<AdvancedErrorMessageProvider>? _logger;
    private readonly IEnumerable<object>? _toolInstances;

    /// <summary>
    /// Initializes a new instance of the AdvancedErrorMessageProvider.
    /// </summary>
    /// <param name="recoveryProcessor">Optional error recovery template processor for enhanced guidance.</param>
    /// <param name="options">Configuration options for error recovery behavior.</param>
    /// <param name="toolInstances">Available tool instances for context-aware error messages.</param>
    /// <param name="logger">Optional logger for debugging error message generation.</param>
    public AdvancedErrorMessageProvider(
        ErrorRecoveryTemplateProcessor? recoveryProcessor = null,
        ErrorRecoveryOptions? options = null,
        IEnumerable<object>? toolInstances = null,
        ILogger<AdvancedErrorMessageProvider>? logger = null)
    {
        _recoveryProcessor = recoveryProcessor;
        _options = options ?? new ErrorRecoveryOptions();
        _toolInstances = toolInstances;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override string ValidationFailed(string paramName, string requirement)
    {
        var baseMessage = base.ValidationFailed(paramName, requirement);
        
        if (_recoveryProcessor == null || !_options.EnableRecoveryGuidance)
        {
            return baseMessage;
        }

        try
        {
            var context = CreateErrorContext();
            context.AdditionalContext["parameter_name"] = paramName;
            context.AdditionalContext["requirement"] = requirement;

            return _recoveryProcessor.ProcessError(
                "VALIDATION_ERROR", 
                GetCurrentToolName(),
                baseMessage, 
                context);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to enhance validation error message for parameter '{ParamName}'", paramName);
            return baseMessage;
        }
    }

    /// <inheritdoc/>
    public override string ToolExecutionFailed(string toolName, string details)
    {
        var baseMessage = base.ToolExecutionFailed(toolName, details);
        
        if (_recoveryProcessor == null || !_options.EnableRecoveryGuidance)
        {
            return baseMessage;
        }

        try
        {
            var errorType = DetermineErrorType(details);
            var context = CreateErrorContext();
            context.AdditionalContext["error_details"] = details;

            return _recoveryProcessor.ProcessError(
                errorType,
                toolName,
                baseMessage,
                context);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to enhance execution error message for tool '{ToolName}'", toolName);
            return baseMessage;
        }
    }

    /// <inheritdoc/>
    public override string ParameterRequired(string paramName)
    {
        var baseMessage = base.ParameterRequired(paramName);
        
        if (_recoveryProcessor == null || !_options.EnableRecoveryGuidance)
        {
            return baseMessage;
        }

        try
        {
            var context = CreateErrorContext();
            context.AdditionalContext["parameter_name"] = paramName;

            return _recoveryProcessor.ProcessError(
                "PARAMETER_REQUIRED",
                GetCurrentToolName(),
                baseMessage,
                context);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to enhance required parameter error message for '{ParamName}'", paramName);
            return baseMessage;
        }
    }

    /// <inheritdoc/>
    public override RecoveryInfo GetRecoveryInfo(string errorCode, string? context = null, Exception? exception = null)
    {
        var baseRecovery = base.GetRecoveryInfo(errorCode, context, exception);

        if (_recoveryProcessor == null || !_options.EnableRecoveryGuidance)
        {
            return baseRecovery;
        }

        try
        {
            // Enhance recovery info with template-based guidance
            var enhancedSteps = GenerateEnhancedRecoverySteps(errorCode, context, exception);
            
            if (enhancedSteps.Any())
            {
                baseRecovery.Steps = enhancedSteps.ToArray();
            }

            return baseRecovery;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to enhance recovery info for error code '{ErrorCode}'", errorCode);
            return baseRecovery;
        }
    }

    /// <inheritdoc/>
    public override List<SuggestedAction> GetSuggestedActions(string errorCode, string toolName)
    {
        var baseSuggestions = base.GetSuggestedActions(errorCode, toolName);

        if (!_options.SuggestAlternativeTools || _toolInstances == null)
        {
            return baseSuggestions;
        }

        try
        {
            var enhancedSuggestions = GenerateSuggestedActions(errorCode, toolName);
            baseSuggestions.AddRange(enhancedSuggestions);
            return baseSuggestions;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to generate enhanced suggested actions for error '{ErrorCode}' in tool '{ToolName}'", errorCode, toolName);
            return baseSuggestions;
        }
    }

    /// <summary>
    /// Creates a professional error message for type verification errors.
    /// </summary>
    /// <param name="typeName">The type that could not be verified.</param>
    /// <param name="suggestion">Suggested tool to use for verification.</param>
    /// <returns>Professional error message with recovery guidance.</returns>
    public string TypeVerificationError(string typeName, string suggestion)
    {
        var baseMessage = $"Type '{typeName}' could not be verified. Consider using {suggestion} for accurate type information.";
        
        if (_recoveryProcessor == null || !_options.EnableRecoveryGuidance)
        {
            return baseMessage;
        }

        try
        {
            var context = CreateErrorContext();
            context.AdditionalContext["type_name"] = typeName;
            context.AdditionalContext["suggested_tool"] = suggestion;

            return _recoveryProcessor.ProcessError(
                "TYPE_VERIFICATION_ERROR",
                GetCurrentToolName(),
                baseMessage,
                context);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to enhance type verification error message for type '{TypeName}'", typeName);
            return baseMessage;
        }
    }

    /// <summary>
    /// Creates a professional error message for file not found errors.
    /// </summary>
    /// <param name="filePath">The file path that was not found.</param>
    /// <returns>Professional error message with file discovery guidance.</returns>
    public string FileNotFoundError(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var baseMessage = $"File not found: {filePath}";
        
        if (_recoveryProcessor == null || !_options.EnableRecoveryGuidance)
        {
            return baseMessage;
        }

        try
        {
            var context = CreateErrorContext();
            context.AdditionalContext["file_path"] = filePath;
            context.AdditionalContext["filename"] = fileName;

            return _recoveryProcessor.ProcessError(
                "FILE_NOT_FOUND",
                GetCurrentToolName(),
                baseMessage,
                context);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to enhance file not found error message for '{FilePath}'", filePath);
            return baseMessage;
        }
    }

    /// <summary>
    /// Creates a professional error message for workspace indexing requirements.
    /// </summary>
    /// <returns>Professional error message with indexing guidance.</returns>
    public string WorkspaceNotIndexedError()
    {
        var baseMessage = "Workspace indexing required for optimal search performance. Run index_workspace first.";
        
        if (_recoveryProcessor == null || !_options.EnableRecoveryGuidance)
        {
            return baseMessage;
        }

        try
        {
            var context = CreateErrorContext();
            
            return _recoveryProcessor.ProcessError(
                "WORKSPACE_NOT_INDEXED",
                GetCurrentToolName(),
                baseMessage,
                context);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to enhance workspace not indexed error message");
            return baseMessage;
        }
    }

    private ErrorRecoveryContext CreateErrorContext()
    {
        var context = new ErrorRecoveryContext();

        if (_toolInstances != null)
        {
            // Extract available tools and markers from tool instances
            var toolNames = _toolInstances
                .Where(t => t is IMcpTool)
                .Cast<IMcpTool>()
                .Select(t => t.Name)
                .ToList();

            var markerNames = _toolInstances
                .SelectMany(t => t.GetType().GetInterfaces())
                .Where(i => typeof(IToolMarker).IsAssignableFrom(i) && i != typeof(IToolMarker))
                .Select(i => i.Name)
                .Distinct()
                .ToList();

            context.AvailableTools = toolNames;
            context.AvailableMarkers = markerNames;
        }

        return context;
    }

    private string DetermineErrorType(string details)
    {
        // Analyze error details to determine specific error type
        if (details.Contains("file not found", StringComparison.OrdinalIgnoreCase) ||
            details.Contains("path not found", StringComparison.OrdinalIgnoreCase))
        {
            return "FILE_NOT_FOUND";
        }

        if (details.Contains("access", StringComparison.OrdinalIgnoreCase) &&
            details.Contains("denied", StringComparison.OrdinalIgnoreCase))
        {
            return "ACCESS_DENIED";
        }

        if (details.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return "TIMEOUT";
        }

        if (details.Contains("index", StringComparison.OrdinalIgnoreCase) ||
            details.Contains("workspace", StringComparison.OrdinalIgnoreCase))
        {
            return "WORKSPACE_NOT_INDEXED";
        }

        return "TOOL_ERROR";
    }

    private string GetCurrentToolName()
    {
        // In a real implementation, this could be retrieved from the current execution context
        // For now, return a generic name
        return "current_tool";
    }

    private List<string> GenerateEnhancedRecoverySteps(string errorCode, string? context, Exception? exception)
    {
        var steps = new List<string>();

        if (_toolInstances == null)
        {
            return steps;
        }

        var availableTools = _toolInstances
            .Where(t => t is IMcpTool)
            .Cast<IMcpTool>()
            .ToList();

        switch (errorCode)
        {
            case "TYPE_VERIFICATION_ERROR":
                if (availableTools.Any(t => t.Name.Contains("goto_definition")))
                    steps.Add("Use goto_definition to verify exact method signatures and types");
                if (availableTools.Any(t => t.Name.Contains("symbol_search")))
                    steps.Add("Use symbol_search to find comprehensive type information");
                break;

            case "FILE_NOT_FOUND":
                if (availableTools.Any(t => t.Name.Contains("file_search")))
                    steps.Add("Use file_search to locate files by name or pattern");
                if (availableTools.Any(t => t.Name.Contains("directory_search")))
                    steps.Add("Use directory_search to explore project structure");
                break;

            case "WORKSPACE_NOT_INDEXED":
                if (availableTools.Any(t => t.Name.Contains("index_workspace")))
                    steps.Add("Run index_workspace to enable high-performance search capabilities");
                break;
        }

        return steps;
    }

    private List<SuggestedAction> GenerateSuggestedActions(string errorCode, string toolName)
    {
        var actions = new List<SuggestedAction>();

        if (_toolInstances == null)
        {
            return actions;
        }

        var availableTools = _toolInstances
            .Where(t => t is IMcpTool)
            .Cast<IMcpTool>()
            .ToList();

        // Generate tool-specific suggestions based on available alternatives
        switch (errorCode)
        {
            case "TYPE_VERIFICATION_ERROR":
                foreach (var tool in availableTools.Where(t => 
                    t.Name.Contains("definition") || t.Name.Contains("symbol") || t.Name.Contains("reference")))
                {
                    actions.Add(new SuggestedAction
                    {
                        Tool = tool.Name,
                        Description = $"Try using {tool.Name} for accurate type information",
                        Parameters = new Dictionary<string, object> { ["tool_name"] = tool.Name }
                    });
                }
                break;
        }

        return actions;
    }
}