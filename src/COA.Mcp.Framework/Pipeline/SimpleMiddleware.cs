using System;
using System.Threading.Tasks;

namespace COA.Mcp.Framework.Pipeline;

/// <summary>
/// Simplified middleware interface for tool execution.
/// </summary>
public interface ISimpleMiddleware
{
    /// <summary>
    /// Gets the order in which this middleware should run (lower numbers run first).
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Gets whether this middleware is enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Called before tool execution.
    /// </summary>
    /// <param name="toolName">The name of the tool being executed.</param>
    /// <param name="parameters">The tool parameters.</param>
    /// <returns>A task representing the async operation.</returns>
    Task OnBeforeExecutionAsync(string toolName, object? parameters);

    /// <summary>
    /// Called after successful tool execution.
    /// </summary>
    /// <param name="toolName">The name of the tool that was executed.</param>
    /// <param name="parameters">The tool parameters.</param>
    /// <param name="result">The execution result.</param>
    /// <param name="elapsedMs">Execution time in milliseconds.</param>
    /// <returns>A task representing the async operation.</returns>
    Task OnAfterExecutionAsync(string toolName, object? parameters, object? result, long elapsedMs);

    /// <summary>
    /// Called when tool execution fails.
    /// </summary>
    /// <param name="toolName">The name of the tool that failed.</param>
    /// <param name="parameters">The tool parameters.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="elapsedMs">Execution time before failure in milliseconds.</param>
    /// <returns>A task representing the async operation.</returns>
    Task OnErrorAsync(string toolName, object? parameters, Exception exception, long elapsedMs);
}

/// <summary>
/// Base class for simple middleware with default implementations.
/// </summary>
public abstract class SimpleMiddlewareBase : ISimpleMiddleware
{
    /// <inheritdoc/>
    public virtual int Order { get; set; } = 0;

    /// <inheritdoc/>
    public virtual bool IsEnabled { get; set; } = true;

    /// <inheritdoc/>
    public virtual Task OnBeforeExecutionAsync(string toolName, object? parameters) => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task OnAfterExecutionAsync(string toolName, object? parameters, object? result, long elapsedMs) => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task OnErrorAsync(string toolName, object? parameters, Exception exception, long elapsedMs) => Task.CompletedTask;
}