using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework.Attributes;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Exceptions;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework.Schema;
using COA.Mcp.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.Registration;

/// <summary>
/// Unified registry for MCP tools with type-safe registration and protocol integration.
/// This is the single registry that handles both framework tool management and MCP protocol serving.
/// Implements IAsyncDisposable to properly clean up registered tools.
/// </summary>
public class McpToolRegistry : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, RegisteredTool> _tools;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<McpToolRegistry>? _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the McpToolRegistry class.
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency injection.</param>
    /// <param name="logger">Optional logger.</param>
    public McpToolRegistry(IServiceProvider serviceProvider, ILogger<McpToolRegistry>? logger = null)
    {
        _tools = new ConcurrentDictionary<string, RegisteredTool>(StringComparer.OrdinalIgnoreCase);
        _serviceProvider = serviceProvider;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Registers a strongly-typed tool with compile-time type checking.
    /// </summary>
    /// <typeparam name="TParams">The type of the tool's parameters.</typeparam>
    /// <typeparam name="TResult">The type of the tool's result.</typeparam>
    /// <param name="tool">The tool instance to register.</param>
    public void RegisterTool<TParams, TResult>(IMcpTool<TParams, TResult> tool)
        where TParams : class
    {
        if (tool == null)
            throw new ArgumentNullException(nameof(tool));

        var registeredTool = new RegisteredTool
        {
            Name = tool.Name,
            Description = tool.Description,
            Category = tool.Category,
            ToolInstance = (IMcpTool)tool,
            ParameterType = typeof(TParams),
            ResultType = typeof(TResult),
            InputSchema = tool.GetInputSchema()
        };

        if (!_tools.TryAdd(tool.Name, registeredTool))
        {
            throw new ToolRegistrationException($"Tool '{tool.Name}' is already registered");
        }

        _logger?.LogInformation("Registered tool '{ToolName}' with types {ParamType} -> {ResultType}", 
            tool.Name, typeof(TParams).Name, typeof(TResult).Name);
    }

    /// <summary>
    /// Registers a tool using the non-generic interface (for runtime registration).
    /// </summary>
    /// <param name="tool">The tool instance to register.</param>
    public void RegisterTool(IMcpTool tool)
    {
        if (tool == null)
            throw new ArgumentNullException(nameof(tool));

        var registeredTool = new RegisteredTool
        {
            Name = tool.Name,
            Description = tool.Description,
            Category = tool.Category,
            ToolInstance = (IMcpTool)tool,
            ParameterType = tool.ParameterType,
            ResultType = tool.ResultType,
            InputSchema = tool.GetInputSchema()
        };

        if (!_tools.TryAdd(tool.Name, registeredTool))
        {
            throw new ToolRegistrationException($"Tool '{tool.Name}' is already registered");
        }

        _logger?.LogInformation("Registered tool '{ToolName}' with types {ParamType} -> {ResultType}", 
            tool.Name, tool.ParameterType.Name, tool.ResultType.Name);
    }

    /// <summary>
    /// Discovers and registers tools from the specified assembly using attributes.
    /// </summary>
    /// <param name="assembly">The assembly to scan for tools.</param>
    public void DiscoverAndRegisterTools(Assembly assembly)
    {
        // Skip test assemblies to avoid registering test helper classes
        if (IsTestAssembly(assembly))
        {
            _logger?.LogDebug("Skipping tool discovery in test assembly {AssemblyName}", assembly.GetName().Name);
            return;
        }

        var toolTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.GetInterfaces().Any(i => 
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMcpTool<,>)))
            .Where(t => !IsTestClass(t)) // Skip test helper classes
            .Where(t => HasParameterlessConstructor(t) || _serviceProvider.GetService(t) != null) // Only classes that can be instantiated
            .ToList();

        foreach (var toolType in toolTypes)
        {
            try
            {
                // Try to create instance from DI container first
                var toolInstance = _serviceProvider.GetService(toolType) 
                    ?? Activator.CreateInstance(toolType);

                if (toolInstance is IMcpTool tool)
                {
                    RegisterTool(tool);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to register tool type {ToolType}", toolType.Name);
            }
        }

        // Also discover attribute-based tools (legacy support)
        DiscoverAttributeBasedTools(assembly);
    }

    /// <summary>
    /// Discovers tools using the McpServerTool attribute (legacy support).
    /// </summary>
    private void DiscoverAttributeBasedTools(Assembly assembly)
    {
        var typesWithTools = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
            .ToList();

        foreach (var type in typesWithTools)
        {
            var instance = _serviceProvider.GetService(type) ?? Activator.CreateInstance(type);
            
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null)
                .ToList();

            foreach (var method in methods)
            {
                var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>()!;
                var descAttr = method.GetCustomAttribute<DescriptionAttribute>();
                
                // Create a wrapper for attribute-based tools
                var wrapper = new AttributeToolWrapper(
                    toolAttr.Name ?? method.Name,
                    descAttr?.Description ?? "No description provided",
                    instance!,
                    method);
                    
                RegisterTool(wrapper);
            }
        }
    }

    /// <summary>
    /// Gets the list of tools in MCP protocol format for tools/list response.
    /// </summary>
    /// <returns>List of Protocol.Tool objects.</returns>
    public List<Protocol.Tool> GetProtocolTools()
    {
        return _tools.Values.Select(t => new Protocol.Tool
        {
            Name = t.Name,
            Description = t.Description,
            InputSchema = t.InputSchema.ToJsonElement()
        }).ToList();
    }

    /// <summary>
    /// Calls a tool by name with the provided arguments (for MCP protocol handling).
    /// </summary>
    /// <param name="toolName">The name of the tool to call.</param>
    /// <param name="arguments">The arguments as a JsonElement.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The CallToolResult for the MCP protocol response.</returns>
    public async Task<CallToolResult> CallToolAsync(
        string toolName, 
        JsonElement? arguments, 
        CancellationToken cancellationToken = default)
    {
        if (!_tools.TryGetValue(toolName, out var registeredTool))
        {
            return new CallToolResult
            {
                IsError = true,
                Content = new List<ToolContent>
                {
                    new ToolContent
                    {
                        Type = "text",
                        Text = $"Tool '{toolName}' not found"
                    }
                }
            };
        }

        try
        {
            // Extract responseMode from arguments if present
            string? responseMode = null;
            if (arguments.HasValue && arguments.Value.TryGetProperty("responseMode", out var responseModeElement))
            {
                responseMode = responseModeElement.GetString();
            }
            
            // Execute the tool
            var result = await registeredTool.ToolInstance.ExecuteAsync(arguments, cancellationToken);
            
            // Convert result to CallToolResult with responseMode awareness
            return CreateSuccessResult(result, responseMode);
        }
        catch (ValidationException ex)
        {
            return CreateErrorResult("VALIDATION_ERROR", ex.Message);
        }
        catch (ToolExecutionException ex)
        {
            return CreateErrorResult("EXECUTION_ERROR", ex.Message);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error executing tool '{ToolName}'", toolName);
            return CreateErrorResult("INTERNAL_ERROR", $"An unexpected error occurred: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets a tool by name.
    /// </summary>
    public IMcpTool? GetTool(string toolName)
    {
        return _tools.TryGetValue(toolName, out var tool) ? tool.ToolInstance : null;
    }

    /// <summary>
    /// Gets all registered tools.
    /// </summary>
    public IEnumerable<IMcpTool> GetAllTools()
    {
        return _tools.Values.Select(t => t.ToolInstance);
    }

    /// <summary>
    /// Checks if a tool is registered.
    /// </summary>
    public bool IsToolRegistered(string toolName)
    {
        return _tools.ContainsKey(toolName);
    }

    /// <summary>
    /// Unregisters a tool.
    /// </summary>
    public bool UnregisterTool(string toolName)
    {
        var removed = _tools.TryRemove(toolName, out var registration);
        if (removed)
        {
            // Dispose if the tool implements IAsyncDisposable
            if (registration?.ToolInstance is IAsyncDisposable disposableTool)
            {
                try
                {
                    disposableTool.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    _logger?.LogDebug("Disposed tool '{ToolName}'", toolName);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error disposing tool '{ToolName}'", toolName);
                }
            }
            
            _logger?.LogInformation("Unregistered tool '{ToolName}'", toolName);
        }
        return removed;
    }

    /// <summary>
    /// Clears all registered tools.
    /// </summary>
    public void Clear()
    {
        // Dispose all disposable tools before clearing
        foreach (var kvp in _tools)
        {
            if (kvp.Value.ToolInstance is IAsyncDisposable disposableTool)
            {
                try
                {
                    disposableTool.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    _logger?.LogDebug("Disposed tool '{ToolName}' during clear", kvp.Key);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error disposing tool '{ToolName}' during clear", kvp.Key);
                }
            }
        }
        
        _tools.Clear();
        _logger?.LogInformation("Cleared all registered tools");
    }

    private CallToolResult CreateSuccessResult(object? result, string? requestedResponseMode = null)
    {
        string content;
        // Claude MCP only accepts "text" type, not "application/json"
        // Even JSON content should be sent as type "text"
        string contentType = "text";

        if (result == null)
        {
            content = "Tool executed successfully";
        }
        else if (result is string stringResult)
        {
            content = stringResult;
        }
        else
        {
            // Serialize complex objects as JSON but still use "text" type
            content = JsonSerializer.Serialize(result, _jsonOptions);
            
            // Token limits optimized for Claude
            const int OPTIMAL_TOKENS = 3000;    // Best performance, instant responses
            const int TARGET_TOKENS = 5000;     // Good balance, still fast
            // const int WARNING_TOKENS = 10000;   // Getting slow, should switch to summary (currently unused)
            const int CRITICAL_TOKENS = 20000;  // Must use resources or truncate
            const int HARD_LIMIT = 24000;       // 1K safety buffer before Claude's 25K limit
            const int CHARS_PER_TOKEN = 4;
            
            int estimatedTokens = content.Length / CHARS_PER_TOKEN;
            
            // Determine actual response mode based on size and request
            var actualResponseMode = DetermineResponseMode(requestedResponseMode, estimatedTokens, result);
            
            // Handle based on token size and response mode
            if (estimatedTokens > HARD_LIMIT)
            {
                // Always error if exceeding hard limit
                _logger?.LogError("Tool result exceeds hard token limit. Estimated: {Tokens}, Max: {MaxTokens}, RequestedMode: {Mode}", 
                    estimatedTokens, HARD_LIMIT, requestedResponseMode);
                
                return CreateTokenLimitError(estimatedTokens, HARD_LIMIT, result);
            }
            else if (estimatedTokens > CRITICAL_TOKENS)
            {
                if (actualResponseMode == "full")
                {
                    // User explicitly requested full mode but it's too large
                    _logger?.LogError("Full mode requested but result exceeds safe limit. Tokens: {Tokens}, Limit: {Limit}", 
                        estimatedTokens, CRITICAL_TOKENS);
                    
                    return CreateTokenLimitError(estimatedTokens, CRITICAL_TOKENS, result);
                }
                else
                {
                    // Auto-switch to summary
                    _logger?.LogWarning("Auto-switching to summary mode due to size. Tokens: {Tokens}", estimatedTokens);
                    content = CreateSummaryResponse(result, estimatedTokens);
                }
            }
            else if (estimatedTokens > TARGET_TOKENS && actualResponseMode == "auto")
            {
                // Suggest summary mode but still return full if not too large
                _logger?.LogInformation("Response size ({Tokens} tokens) exceeds target ({Target}). Consider using responseMode='summary'", 
                    estimatedTokens, TARGET_TOKENS);
                
                // Optionally add metadata hint if result is ToolResultBase
                if (result is ToolResultBase toolResult && toolResult.Meta != null)
                {
                    toolResult.Meta.Mode = "full";
                    toolResult.Meta.Tokens = estimatedTokens;
                    toolResult.Insights ??= new List<string>();
                    toolResult.Insights.Add($"Response contains {estimatedTokens} tokens. Use responseMode='summary' for faster responses.");
                    
                    // Re-serialize with the metadata
                    content = JsonSerializer.Serialize(result, _jsonOptions);
                }
            }
            else if (estimatedTokens <= OPTIMAL_TOKENS)
            {
                _logger?.LogDebug("Response size optimal at {Tokens} tokens", estimatedTokens);
            }
        }

        return new CallToolResult
        {
            IsError = false,
            Content = new List<ToolContent>
            {
                new ToolContent
                {
                    Type = contentType,
                    Text = content
                }
            }
        };
    }

    private string DetermineResponseMode(string? requestedMode, int estimatedTokens, object? result)
    {
        // If explicitly requested, use that (unless it would exceed limits)
        if (!string.IsNullOrEmpty(requestedMode))
        {
            return requestedMode.ToLowerInvariant();
        }
        
        // Check if result has its own mode preference
        if (result is ToolResultBase toolResult && toolResult.Meta?.Mode != null)
        {
            return toolResult.Meta.Mode.ToLowerInvariant();
        }
        
        // Default to auto
        return "auto";
    }

    private CallToolResult CreateTokenLimitError(int estimatedTokens, int limit, object? result)
    {
        var errorMessage = new StringBuilder();
        errorMessage.AppendLine($"Response too large: {estimatedTokens} tokens exceeds limit of {limit} tokens.");
        errorMessage.AppendLine();
        errorMessage.AppendLine("Available options:");
        errorMessage.AppendLine("1. Use responseMode='summary' to get an overview");
        errorMessage.AppendLine("2. Add maxResults parameter to limit data (e.g., maxResults=50)");
        errorMessage.AppendLine("3. Use pagination with offset/limit parameters");
        errorMessage.AppendLine("4. Apply filters to reduce the result set");
        
        // If result has a resourceUri, include it
        if (result is ToolResultBase toolResult && !string.IsNullOrEmpty(toolResult.ResourceUri))
        {
            errorMessage.AppendLine($"5. Access full results via resource: {toolResult.ResourceUri}");
        }
        
        return new CallToolResult
        {
            IsError = true,
            Content = new List<ToolContent>
            {
                new ToolContent
                {
                    Type = "text",
                    Text = errorMessage.ToString()
                }
            }
        };
    }

    private string CreateSummaryResponse(object? result, int originalTokens)
    {
        // If the result is already a ToolResultBase with summary support
        if (result is ToolResultBase toolResult)
        {
            var summary = new
            {
                success = toolResult.Success,
                operation = toolResult.Operation,
                message = toolResult.Message ?? "Response truncated due to size",
                summary = toolResult.Meta?.Mode == "summary" ? result : new
                {
                    message = "Full response too large. Key information preserved.",
                    originalTokens = originalTokens
                },
                insights = toolResult.Insights,
                actions = toolResult.Actions,
                resourceUri = toolResult.ResourceUri,
                meta = new
                {
                    mode = "summary",
                    truncated = true,
                    originalTokens = originalTokens,
                    message = "Use responseMode='full' to attempt full response, or access via resourceUri"
                }
            };
            
            return JsonSerializer.Serialize(summary, _jsonOptions);
        }
        
        // Generic summary for non-ToolResultBase results
        var genericSummary = new
        {
            message = "Response automatically summarized due to size",
            summary = new
            {
                dataType = result?.GetType().Name ?? "Unknown",
                originalTokens = originalTokens,
                truncated = true
            },
            meta = new
            {
                mode = "summary",
                message = "Original response too large. Use pagination or filtering parameters."
            }
        };
        
        return JsonSerializer.Serialize(genericSummary, _jsonOptions);
    }

    private CallToolResult CreateErrorResult(string errorCode, string message)
    {
        return new CallToolResult
        {
            IsError = true,
            Content = new List<ToolContent>
            {
                new ToolContent
                {
                    Type = "text",
                    Text = $"[{errorCode}] {message}"
                }
            }
        };
    }

    /// <summary>
    /// Internal class to track registered tool metadata.
    /// </summary>
    private class RegisteredTool
    {
        public required string Name { get; init; }
        public required string Description { get; init; }
        public required ToolCategory Category { get; init; }
        public required IMcpTool ToolInstance { get; init; }
        public required Type ParameterType { get; init; }
        public required Type ResultType { get; init; }
        public required IJsonSchema InputSchema { get; init; }
    }

    /// <summary>
    /// Wrapper for attribute-based tools to implement IMcpTool interface.
    /// </summary>
    private class AttributeToolWrapper : IMcpTool
    {
        private readonly object _instance;
        private readonly MethodInfo _method;
        private readonly Type _parameterType;
        private readonly Type _resultType;

        public AttributeToolWrapper(string name, string description, object instance, MethodInfo method)
        {
            Name = name;
            Description = description;
            _instance = instance;
            _method = method;
            
            // Determine parameter type
            var parameters = method.GetParameters();
            _parameterType = parameters.Length > 0 ? parameters[0].ParameterType : typeof(EmptyParameters);
            
            // Determine result type
            _resultType = method.ReturnType;
            if (_resultType.IsGenericType && _resultType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                _resultType = _resultType.GetGenericArguments()[0];
            }
            else if (_resultType == typeof(Task))
            {
                _resultType = typeof(void);
            }
        }

        public string Name { get; }
        public string Description { get; }
        public ToolCategory Category => ToolCategory.General;
        public Type ParameterType => _parameterType;
        public Type ResultType => _resultType;

        public IJsonSchema GetInputSchema()
        {
            // Create a non-generic JsonSchema wrapper for runtime types
            var schemaDict = Utilities.JsonSchemaGenerator.GenerateSchema(_parameterType);
            return new RuntimeJsonSchema(schemaDict);
        }

        public async Task<object?> ExecuteAsync(object? parameters, CancellationToken cancellationToken)
        {
            var methodParams = _method.GetParameters();
            object?[] args;

            if (methodParams.Length == 0)
            {
                args = Array.Empty<object>();
            }
            else if (methodParams.Length == 1)
            {
                args = new[] { parameters };
            }
            else if (methodParams.Length == 2 && methodParams[1].ParameterType == typeof(CancellationToken))
            {
                args = new[] { parameters, cancellationToken };
            }
            else
            {
                throw new InvalidOperationException($"Tool method '{Name}' has unsupported parameter signature");
            }

            var result = _method.Invoke(_instance, args);
            
            if (result is Task task)
            {
                await task;
                
                if (task.GetType().IsGenericType)
                {
                    var resultProperty = task.GetType().GetProperty("Result");
                    return resultProperty?.GetValue(task);
                }
                
                return null;
            }
            
            return result;
        }
    }
    
    #region IAsyncDisposable Implementation
    
    private bool _disposed;
    
    /// <summary>
    /// Disposes all registered tools that implement IAsyncDisposable.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        
        _logger?.LogInformation("Disposing McpToolRegistry and all registered tools");
        
        // Dispose all disposable tools
        var disposalTasks = new List<Task>();
        
        foreach (var kvp in _tools)
        {
            if (kvp.Value.ToolInstance is IAsyncDisposable disposableTool)
            {
                disposalTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await disposableTool.DisposeAsync();
                        _logger?.LogDebug("Disposed tool '{ToolName}'", kvp.Key);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error disposing tool '{ToolName}'", kvp.Key);
                    }
                }));
            }
        }
        
        // Wait for all disposals to complete
        if (disposalTasks.Count > 0)
        {
            await Task.WhenAll(disposalTasks);
            _logger?.LogInformation("Disposed {Count} disposable tools", disposalTasks.Count);
        }
        
        // Clear the registry
        _tools.Clear();
        _disposed = true;
        
        GC.SuppressFinalize(this);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Determines if an assembly is a test assembly by checking common test indicators.
    /// </summary>
    private static bool IsTestAssembly(Assembly assembly)
    {
        var name = assembly.GetName().Name ?? string.Empty;
        return name.Contains(".Tests") || 
               name.Contains(".Test") || 
               name.EndsWith("Tests") || 
               name.EndsWith("Test") ||
               assembly.GetReferencedAssemblies().Any(a => 
                   a.Name?.Contains("nunit") == true || 
                   a.Name?.Contains("xunit") == true || 
                   a.Name?.Contains("mstest") == true);
    }

    /// <summary>
    /// Determines if a type is a test class by checking naming conventions and attributes.
    /// First checks if the type is a legitimate MCP tool, then applies filtering.
    /// </summary>
    private static bool IsTestClass(Type type)
    {
        // If it's a legitimate tool, don't filter it out regardless of name
        if (type.GetInterfaces().Any(i => 
            i == typeof(IMcpTool) || 
            (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMcpTool<,>))))
        {
            return false; // It's a tool, not a test class
        }
        
        // For non-tool classes, apply the existing filtering
        var name = type.Name;
        return name.Contains("Test") || 
               name.Contains("Mock") || 
               name.Contains("Fake") || 
               name.Contains("Stub") ||
               type.IsNested || // Nested classes in test files are usually test helpers
               type.GetCustomAttributes().Any(a => 
                   a.GetType().Name.Contains("Test") ||
                   a.GetType().Name.Contains("Mock"));
    }

    /// <summary>
    /// Checks if a type has a parameterless constructor.
    /// </summary>
    private static bool HasParameterlessConstructor(Type type)
    {
        return type.GetConstructor(Type.EmptyTypes) != null;
    }

    #endregion
}