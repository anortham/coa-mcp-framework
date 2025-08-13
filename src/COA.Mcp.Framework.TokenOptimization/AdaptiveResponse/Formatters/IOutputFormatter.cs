using System.Data;
using COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Models;

namespace COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Formatters;

/// <summary>
/// Interface for formatting output content for specific IDE environments.
/// </summary>
public interface IOutputFormatter
{
    /// <summary>
    /// Formats a summary header for the IDE environment.
    /// </summary>
    /// <param name="summary">The summary text.</param>
    /// <param name="data">The associated data for context.</param>
    /// <returns>Formatted summary text.</returns>
    string FormatSummary(string summary, object? data = null);
    
    /// <summary>
    /// Formats file references for the IDE environment.
    /// </summary>
    /// <param name="references">The file references to format.</param>
    /// <returns>Formatted file reference text.</returns>
    string FormatFileReferences(IEnumerable<FileReference> references);
    
    /// <summary>
    /// Formats action items for the IDE environment.
    /// </summary>
    /// <param name="actions">The action items to format.</param>
    /// <returns>Formatted action text.</returns>
    string FormatActions(IEnumerable<ActionItem> actions);
    
    /// <summary>
    /// Formats tabular data for the IDE environment.
    /// </summary>
    /// <param name="data">The data table to format.</param>
    /// <returns>Formatted table text.</returns>
    string FormatTable(DataTable data);
    
    /// <summary>
    /// Formats a list of items for the IDE environment.
    /// </summary>
    /// <param name="items">The items to format.</param>
    /// <param name="title">Optional title for the list.</param>
    /// <returns>Formatted list text.</returns>
    string FormatList(IEnumerable<object> items, string? title = null);
    
    /// <summary>
    /// Formats error information for the IDE environment.
    /// </summary>
    /// <param name="error">The error information.</param>
    /// <returns>Formatted error text.</returns>
    string FormatError(COA.Mcp.Framework.Models.ErrorInfo error);
}

/// <summary>
/// Interface for creating resources from large data sets.
/// </summary>
public interface IResourceFormatter
{
    /// <summary>
    /// Formats data as a resource (HTML, CSV, JSON, etc.).
    /// </summary>
    /// <typeparam name="T">The type of data to format.</typeparam>
    /// <param name="data">The data to format.</param>
    /// <returns>The formatted content as a string.</returns>
    Task<string> FormatResourceAsync<T>(T data);
    
    /// <summary>
    /// Gets the MIME type for the formatted resource.
    /// </summary>
    string GetMimeType();
    
    /// <summary>
    /// Gets the file extension for the formatted resource.
    /// </summary>
    string GetFileExtension();
}

/// <summary>
/// Factory interface for creating output formatters.
/// </summary>
public interface IOutputFormatterFactory
{
    /// <summary>
    /// Creates an inline formatter for the specified IDE environment.
    /// </summary>
    /// <param name="environment">The IDE environment.</param>
    /// <returns>An appropriate output formatter.</returns>
    IOutputFormatter CreateInlineFormatter(IDEEnvironment environment);
    
    /// <summary>
    /// Creates a resource formatter for the specified format and environment.
    /// </summary>
    /// <param name="format">The desired format (html, csv, json, etc.).</param>
    /// <param name="environment">The IDE environment.</param>
    /// <returns>An appropriate resource formatter.</returns>
    IResourceFormatter CreateResourceFormatter(string format, IDEEnvironment environment);
}

/// <summary>
/// Interface for providing resources to the MCP framework.
/// </summary>
public interface IResourceProvider
{
    /// <summary>
    /// Stores content as a resource and returns a URI.
    /// </summary>
    /// <param name="path">The resource path/key.</param>
    /// <param name="content">The content to store.</param>
    /// <param name="mimeType">Optional MIME type.</param>
    /// <returns>The resource URI.</returns>
    Task<string> StoreAsync(string path, string content, string? mimeType = null);
    
    /// <summary>
    /// Retrieves content from a stored resource.
    /// </summary>
    /// <param name="uri">The resource URI.</param>
    /// <returns>The stored content.</returns>
    Task<string?> RetrieveAsync(string uri);
    
    /// <summary>
    /// Checks if a resource exists.
    /// </summary>
    /// <param name="uri">The resource URI.</param>
    /// <returns>True if the resource exists.</returns>
    Task<bool> ExistsAsync(string uri);
}

/// <summary>
/// Default implementation of IResourceProvider that stores in memory.
/// </summary>
public class DefaultResourceProvider : IResourceProvider
{
    private readonly Dictionary<string, (string content, string? mimeType, DateTime created)> _storage = new();
    private readonly object _lock = new();
    
    public Task<string> StoreAsync(string path, string content, string? mimeType = null)
    {
        lock (_lock)
        {
            var uri = $"mcp://{path}";
            _storage[uri] = (content, mimeType, DateTime.UtcNow);
            return Task.FromResult(uri);
        }
    }
    
    public Task<string?> RetrieveAsync(string uri)
    {
        lock (_lock)
        {
            return Task.FromResult(_storage.TryGetValue(uri, out var stored) ? stored.content : null);
        }
    }
    
    public Task<bool> ExistsAsync(string uri)
    {
        lock (_lock)
        {
            return Task.FromResult(_storage.ContainsKey(uri));
        }
    }
}