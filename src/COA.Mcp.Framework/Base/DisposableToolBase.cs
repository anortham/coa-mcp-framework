using System;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework.Interfaces;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.Base;

/// <summary>
/// Base class for MCP tools that manage disposable resources.
/// Extends McpToolBase with IAsyncDisposable pattern for proper resource cleanup.
/// </summary>
/// <typeparam name="TParams">The type of the tool's input parameters.</typeparam>
/// <typeparam name="TResult">The type of the tool's result.</typeparam>
public abstract class DisposableToolBase<TParams, TResult> : McpToolBase<TParams, TResult>, IDisposableTool<TParams, TResult>
    where TParams : class
{
    private readonly SemaphoreSlim _disposalLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the DisposableToolBase class.
    /// </summary>
    /// <param name="logger">Optional logger for the tool.</param>
    protected DisposableToolBase(ILogger? logger = null) : base(logger)
    {
    }

    /// <inheritdoc/>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Executes the tool with automatic disposal tracking.
    /// </summary>
    public override async Task<TResult> ExecuteAsync(TParams parameters, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, GetType().FullName ?? nameof(DisposableToolBase<TParams, TResult>));
        
        try
        {
            return await base.ExecuteAsync(parameters, cancellationToken);
        }
        catch
        {
            // If execution fails, we might want to dispose immediately
            // This is configurable based on tool requirements
            if (DisposeOnFailure)
            {
                await DisposeAsync();
            }
            throw;
        }
    }

    /// <summary>
    /// Gets whether the tool should dispose resources on execution failure.
    /// Override to change behavior (default: false).
    /// </summary>
    protected virtual bool DisposeOnFailure => false;

    /// <summary>
    /// Disposes the tool asynchronously.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsync(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the tool's resources.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        await _disposalLock.WaitAsync();
        try
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // Dispose managed resources
                await DisposeManagedResourcesAsync();
            }

            // Dispose unmanaged resources (if any)
            await DisposeUnmanagedResourcesAsync();

            _disposed = true;
        }
        finally
        {
            _disposalLock.Release();
        }
    }

    /// <summary>
    /// Override to dispose managed resources like database connections, HttpClient, etc.
    /// Called when disposing = true.
    /// </summary>
    protected virtual ValueTask DisposeManagedResourcesAsync()
    {
        // Override in derived classes to dispose managed resources
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Override to dispose unmanaged resources.
    /// Called regardless of disposing flag.
    /// </summary>
    protected virtual ValueTask DisposeUnmanagedResourcesAsync()
    {
        // Override in derived classes if you have unmanaged resources
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Ensures the object has not been disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if the object has been disposed.</exception>
    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, GetType().FullName ?? nameof(DisposableToolBase<TParams, TResult>));
    }

    /// <summary>
    /// Finalizer for cleanup of unmanaged resources.
    /// </summary>
    ~DisposableToolBase()
    {
        // Do not change this code. Put cleanup code in 'DisposeAsync(bool disposing)' method
        DisposeAsync(false).AsTask().GetAwaiter().GetResult();
    }
}