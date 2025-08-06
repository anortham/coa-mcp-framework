using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework.Attributes;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Exceptions;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Schema;
using COA.Mcp.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.Registration;

/// <summary>
/// Unified registry for MCP tools with type-safe registration and protocol integration.
/// This is the single registry that handles both framework tool management and MCP protocol serving.
/// </summary>
public class McpToolRegistry
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
        var toolTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.GetInterfaces().Any(i => 
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMcpTool<,>)))
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
            // Execute the tool
            var result = await registeredTool.ToolInstance.ExecuteAsync(arguments, cancellationToken);
            
            // Convert result to CallToolResult
            return CreateSuccessResult(result);
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
        var removed = _tools.TryRemove(toolName, out _);
        if (removed)
        {
            _logger?.LogInformation("Unregistered tool '{ToolName}'", toolName);
        }
        return removed;
    }

    /// <summary>
    /// Clears all registered tools.
    /// </summary>
    public void Clear()
    {
        _tools.Clear();
        _logger?.LogInformation("Cleared all registered tools");
    }

    private CallToolResult CreateSuccessResult(object? result)
    {
        string content;
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
            // Serialize complex objects as JSON
            content = JsonSerializer.Serialize(result, _jsonOptions);
            contentType = "application/json";
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
}