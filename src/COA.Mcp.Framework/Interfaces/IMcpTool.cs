using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework.Schema;

namespace COA.Mcp.Framework.Interfaces;

/// <summary>
/// Strongly-typed interface for MCP tools with compile-time parameter and result validation.
/// </summary>
/// <typeparam name="TParams">The type of the tool's input parameters.</typeparam>
/// <typeparam name="TResult">The type of the tool's result.</typeparam>
public interface IMcpTool<TParams, TResult>
    where TParams : class
{
    /// <summary>
    /// Gets the unique name of the tool.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the human-readable description of what the tool does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the category this tool belongs to.
    /// </summary>
    ToolCategory Category { get; }

    /// <summary>
    /// Executes the tool with the provided parameters.
    /// </summary>
    /// <param name="parameters">The strongly-typed parameters for the tool.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The strongly-typed result of the tool execution.</returns>
    Task<TResult> ExecuteAsync(TParams parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the strongly-typed JSON schema for the tool's input parameters.
    /// </summary>
    /// <returns>A typed JSON schema for the parameters.</returns>
    JsonSchema<TParams> GetInputSchema();
}


/// <summary>
/// Non-generic base interface for runtime tool discovery and invocation.
/// This is used internally by the framework for registration and protocol handling.
/// </summary>
public interface IMcpTool
{
    /// <summary>
    /// Gets the unique name of the tool.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the human-readable description of what the tool does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the category this tool belongs to.
    /// </summary>
    ToolCategory Category { get; }

    /// <summary>
    /// Gets the type of the parameters this tool expects.
    /// </summary>
    Type ParameterType { get; }

    /// <summary>
    /// Gets the type of the result this tool returns.
    /// </summary>
    Type ResultType { get; }

    /// <summary>
    /// Gets the JSON schema for the tool's parameters.
    /// </summary>
    IJsonSchema GetInputSchema();

    /// <summary>
    /// Executes the tool with untyped parameters.
    /// This is used internally for protocol handling.
    /// </summary>
    /// <param name="parameters">The parameters as an object (will be deserialized to ParameterType).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The result as an object.</returns>
    Task<object?> ExecuteAsync(object? parameters, CancellationToken cancellationToken = default);
}