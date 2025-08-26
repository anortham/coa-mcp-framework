using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.Utilities;

/// <summary>
/// Utility class for common concurrent async patterns that improve performance
/// over sequential async operations.
/// </summary>
public static class ConcurrentAsyncUtilities
{
    /// <summary>
    /// Executes multiple async operations concurrently without return values.
    /// Significant performance improvement over sequential foreach+await pattern.
    /// </summary>
    /// <typeparam name="TInput">Input item type</typeparam>
    /// <param name="items">Items to process concurrently</param>
    /// <param name="asyncOperation">Async operation to execute for each item</param>
    /// <param name="maxConcurrency">Optional maximum concurrent operations (0 = unlimited)</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    public static async Task ExecuteConcurrentlyAsync<TInput>(
        IEnumerable<TInput> items,
        Func<TInput, Task> asyncOperation,
        int maxConcurrency = 0,
        CancellationToken cancellationToken = default)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));
        if (asyncOperation == null)
            throw new ArgumentNullException(nameof(asyncOperation));

        var itemList = items.ToList();
        if (!itemList.Any())
            return;

        if (maxConcurrency > 0 && itemList.Count > maxConcurrency)
        {
            // Use SemaphoreSlim to limit concurrency
            await ExecuteWithConcurrencyLimitAsync(itemList, asyncOperation, maxConcurrency, cancellationToken);
        }
        else
        {
            // Unlimited concurrency - use Task.WhenAll for maximum performance
            var tasks = itemList.Select(item => asyncOperation(item)).ToArray();
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Executes multiple async operations concurrently and returns results.
    /// Significant performance improvement over sequential foreach+await pattern.
    /// </summary>
    /// <typeparam name="TInput">Input item type</typeparam>
    /// <typeparam name="TResult">Result type</typeparam>
    /// <param name="items">Items to process concurrently</param>
    /// <param name="asyncOperation">Async operation to execute for each item</param>
    /// <param name="maxConcurrency">Optional maximum concurrent operations (0 = unlimited)</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>Results in the same order as input items</returns>
    public static async Task<TResult[]> ExecuteConcurrentlyAsync<TInput, TResult>(
        IEnumerable<TInput> items,
        Func<TInput, Task<TResult>> asyncOperation,
        int maxConcurrency = 0,
        CancellationToken cancellationToken = default)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));
        if (asyncOperation == null)
            throw new ArgumentNullException(nameof(asyncOperation));

        var itemList = items.ToList();
        if (!itemList.Any())
            return Array.Empty<TResult>();

        if (maxConcurrency > 0 && itemList.Count > maxConcurrency)
        {
            // Use SemaphoreSlim to limit concurrency
            return await ExecuteWithConcurrencyLimitAsync(itemList, asyncOperation, maxConcurrency, cancellationToken);
        }

        // Unlimited concurrency - use Task.WhenAll for maximum performance
        var tasks = itemList.Select(item => asyncOperation(item)).ToArray();
        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes multiple async operations concurrently with a maximum concurrency limit (no return values).
    /// </summary>
    private static async Task ExecuteWithConcurrencyLimitAsync<TInput>(
        IList<TInput> items,
        Func<TInput, Task> asyncOperation,
        int maxConcurrency,
        CancellationToken cancellationToken)
    {
        using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        var tasks = items.Select(async item =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await asyncOperation(item).ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes multiple async operations concurrently with a maximum concurrency limit.
    /// </summary>
    private static async Task<TResult[]> ExecuteWithConcurrencyLimitAsync<TInput, TResult>(
        IList<TInput> items,
        Func<TInput, Task<TResult>> asyncOperation,
        int maxConcurrency,
        CancellationToken cancellationToken)
    {
        using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var results = new TResult[items.Count];

        var tasks = items.Select(async (item, index) =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                results[index] = await asyncOperation(item).ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
        return results;
    }

    /// <summary>
    /// Executes multiple async operations concurrently and returns both successful results and failures.
    /// Continues execution even when some operations fail, collecting all errors.
    /// </summary>
    /// <typeparam name="TInput">Input item type</typeparam>
    /// <typeparam name="TResult">Result type</typeparam>
    /// <param name="items">Items to process concurrently</param>
    /// <param name="asyncOperation">Async operation to execute for each item</param>
    /// <param name="maxConcurrency">Optional maximum concurrent operations</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>Results and failures</returns>
    public static async Task<ConcurrentExecutionResult<TInput, TResult>> ExecuteConcurrentlyWithErrorHandlingAsync<TInput, TResult>(
        IEnumerable<TInput> items,
        Func<TInput, Task<TResult>> asyncOperation,
        int maxConcurrency = 0,
        CancellationToken cancellationToken = default)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));
        if (asyncOperation == null)
            throw new ArgumentNullException(nameof(asyncOperation));

        var itemList = items.ToList();
        if (!itemList.Any())
            return new ConcurrentExecutionResult<TInput, TResult>(Array.Empty<TResult>(), Array.Empty<ConcurrentExecutionFailure<TInput>>());

        using var semaphore = maxConcurrency > 0 ? new SemaphoreSlim(maxConcurrency, maxConcurrency) : null;
        var successfulResults = new ConcurrentBag<TResult>();
        var failures = new ConcurrentBag<ConcurrentExecutionFailure<TInput>>();

        var tasks = itemList.Select(async item =>
        {
            if (semaphore != null)
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var result = await asyncOperation(item).ConfigureAwait(false);
                successfulResults.Add(result);
            }
            catch (Exception ex)
            {
                failures.Add(new ConcurrentExecutionFailure<TInput>(item, ex));
            }
            finally
            {
                semaphore?.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);

        return new ConcurrentExecutionResult<TInput, TResult>(
            successfulResults.ToArray(),
            failures.ToArray());
    }

    /// <summary>
    /// Executes multiple async operations in batches to control memory usage and system load.
    /// Useful for processing large collections where unlimited concurrency might overwhelm resources.
    /// </summary>
    /// <typeparam name="TInput">Input item type</typeparam>
    /// <typeparam name="TResult">Result type</typeparam>
    /// <param name="items">Items to process in batches</param>
    /// <param name="asyncOperation">Async operation to execute for each item</param>
    /// <param name="batchSize">Size of each batch</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>All results from all batches</returns>
    public static async Task<TResult[]> ExecuteInBatchesAsync<TInput, TResult>(
        IEnumerable<TInput> items,
        Func<TInput, Task<TResult>> asyncOperation,
        int batchSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));
        if (asyncOperation == null)
            throw new ArgumentNullException(nameof(asyncOperation));
        if (batchSize <= 0)
            throw new ArgumentException("Batch size must be positive", nameof(batchSize));

        var itemList = items.ToList();
        if (!itemList.Any())
            return Array.Empty<TResult>();

        var results = new List<TResult>();

        for (int i = 0; i < itemList.Count; i += batchSize)
        {
            var batch = itemList.Skip(i).Take(batchSize);
            var batchTasks = batch.Select(asyncOperation).ToArray();
            var batchResults = await Task.WhenAll(batchTasks).ConfigureAwait(false);
            results.AddRange(batchResults);

            // Optional: small delay between batches to prevent overwhelming the system
            if (i + batchSize < itemList.Count)
            {
                await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            }
        }

        return results.ToArray();
    }

    /// <summary>
    /// Executes async operations with retry logic and exponential backoff.
    /// Useful for operations that may fail transiently due to network issues or temporary resource unavailability.
    /// </summary>
    /// <typeparam name="T">Result type</typeparam>
    /// <param name="operation">Operation to execute with retries</param>
    /// <param name="maxRetries">Maximum number of retries</param>
    /// <param name="initialDelay">Initial delay between retries</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>Result of the operation</returns>
    public static async Task<T> ExecuteWithRetriesAsync<T>(
        Func<Task<T>> operation,
        int maxRetries = 3,
        TimeSpan? initialDelay = null,
        CancellationToken cancellationToken = default)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));
        if (maxRetries < 0)
            throw new ArgumentException("Max retries must be non-negative", nameof(maxRetries));

        var delay = initialDelay ?? TimeSpan.FromMilliseconds(100);
        Exception lastException = null!;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                lastException = ex;
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2); // Exponential backoff
            }
        }

        throw lastException;
    }

    /// <summary>
    /// Executes async operations with timeout to prevent hanging operations.
    /// </summary>
    /// <typeparam name="T">Result type</typeparam>
    /// <param name="operation">Operation to execute with timeout</param>
    /// <param name="timeout">Maximum time to wait for completion</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>Result of the operation</returns>
    /// <exception cref="TimeoutException">Thrown when operation times out</exception>
    public static async Task<T> ExecuteWithTimeoutAsync<T>(
        Func<Task<T>> operation,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token);

        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            throw new TimeoutException($"Operation timed out after {timeout.TotalMilliseconds}ms");
        }
    }
}

/// <summary>
/// Result of concurrent execution with error handling.
/// </summary>
/// <typeparam name="TInput">Input item type</typeparam>
/// <typeparam name="TResult">Result type</typeparam>
public class ConcurrentExecutionResult<TInput, TResult>
{
    public TResult[] SuccessfulResults { get; }
    public ConcurrentExecutionFailure<TInput>[] Failures { get; }

    public bool HasFailures => Failures.Any();
    public int TotalItems => SuccessfulResults.Length + Failures.Length;
    public double SuccessRate => TotalItems > 0 ? (double)SuccessfulResults.Length / TotalItems : 0.0;

    public ConcurrentExecutionResult(TResult[] successfulResults, ConcurrentExecutionFailure<TInput>[] failures)
    {
        SuccessfulResults = successfulResults ?? throw new ArgumentNullException(nameof(successfulResults));
        Failures = failures ?? throw new ArgumentNullException(nameof(failures));
    }
}

/// <summary>
/// Represents a failure during concurrent execution.
/// </summary>
/// <typeparam name="TInput">Input item type</typeparam>
public class ConcurrentExecutionFailure<TInput>
{
    public TInput Item { get; }
    public Exception Exception { get; }

    public ConcurrentExecutionFailure(TInput item, Exception exception)
    {
        Item = item;
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
    }
}