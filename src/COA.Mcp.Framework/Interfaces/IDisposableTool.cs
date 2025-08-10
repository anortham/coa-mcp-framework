using System;
using System.Threading.Tasks;

namespace COA.Mcp.Framework.Interfaces;

/// <summary>
/// Interface for MCP tools that manage disposable resources like database connections,
/// file handles, or network connections that need cleanup after use.
/// </summary>
public interface IDisposableTool : IMcpTool, IAsyncDisposable
{
    /// <summary>
    /// Gets whether the tool has been disposed.
    /// </summary>
    bool IsDisposed { get; }
}

/// <summary>
/// Generic interface for disposable MCP tools with strongly-typed parameters and results.
/// </summary>
/// <typeparam name="TParams">The type of the tool's input parameters.</typeparam>
/// <typeparam name="TResult">The type of the tool's result.</typeparam>
public interface IDisposableTool<TParams, TResult> : IMcpTool<TParams, TResult>, IDisposableTool
    where TParams : class
{
}