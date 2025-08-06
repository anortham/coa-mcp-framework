using System;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework.Models;

namespace COA.Mcp.Client.Interfaces
{
    /// <summary>
    /// Strongly-typed interface for MCP client operations.
    /// </summary>
    /// <typeparam name="TParams">Type of the tool parameters.</typeparam>
    /// <typeparam name="TResult">Type of the tool result.</typeparam>
    public interface ITypedMcpClient<TParams, TResult> : IMcpClient
        where TParams : class
        where TResult : ToolResultBase, new()
    {
        /// <summary>
        /// Calls a tool with strongly-typed parameters and result.
        /// </summary>
        Task<TResult> CallToolAsync(string toolName, TParams parameters, CancellationToken cancellationToken = default);

        /// <summary>
        /// Calls a tool with strongly-typed parameters and result, with automatic retry on failure.
        /// </summary>
        Task<TResult> CallToolWithRetryAsync(string toolName, TParams parameters, CancellationToken cancellationToken = default);

        /// <summary>
        /// Calls multiple tools in parallel with the same parameter type.
        /// </summary>
        Task<Dictionary<string, TResult>> CallToolsBatchAsync(Dictionary<string, TParams> toolCalls, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Factory interface for creating typed MCP clients.
    /// </summary>
    public interface ITypedMcpClientFactory
    {
        /// <summary>
        /// Creates a typed MCP client for specific parameter and result types.
        /// </summary>
        ITypedMcpClient<TParams, TResult> CreateTypedClient<TParams, TResult>()
            where TParams : class
            where TResult : ToolResultBase, new();

        /// <summary>
        /// Creates a typed MCP client with custom configuration.
        /// </summary>
        ITypedMcpClient<TParams, TResult> CreateTypedClient<TParams, TResult>(Action<Configuration.McpClientOptions> configure)
            where TParams : class
            where TResult : ToolResultBase, new();
    }
}