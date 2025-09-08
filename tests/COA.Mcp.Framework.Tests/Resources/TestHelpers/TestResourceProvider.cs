using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Protocol;

namespace COA.Mcp.Framework.Tests.Resources.TestHelpers;

/// <summary>
/// Simple resource provider for testing real resource behavior.
/// </summary>
public class TestResourceProvider : IResourceProvider
{
    private readonly Dictionary<string, ReadResourceResult> _resources = new();
    
    public string Scheme => "test";
    public string Name => "Test Resource Provider";
    public string Description => "Test resource provider for integration testing";
    
    /// <summary>
    /// Tracks how many times ReadResourceAsync was called for testing.
    /// </summary>
    public int ReadCallCount { get; private set; }
    
    public bool CanHandle(string uri) => uri.StartsWith("test://");

    public Task<ReadResourceResult?> ReadResourceAsync(string uri, CancellationToken cancellationToken = default)
    {
        ReadCallCount++;
        
        if (_resources.TryGetValue(uri, out var result))
        {
            return Task.FromResult<ReadResourceResult?>(result);
        }
        
        throw new FileNotFoundException($"Resource not found: {uri}");
    }

    public Task<List<Resource>> ListResourcesAsync(CancellationToken cancellationToken = default)
    {
        var resources = _resources.Keys.Select(uri => new Resource
        {
            Uri = uri,
            Name = uri.Replace("test://", ""),
            Description = $"Test resource: {uri}",
            MimeType = "text/plain"
        }).ToList();
        
        return Task.FromResult(resources);
    }

    /// <summary>
    /// Helper method for tests to add resources.
    /// </summary>
    public void AddResource(string uri, string content, string? mimeType = "text/plain")
    {
        _resources[uri] = new ReadResourceResult
        {
            Contents = new List<ResourceContent>
            {
                new ResourceContent
                {
                    Uri = uri,
                    Text = content,
                    MimeType = mimeType
                }
            }
        };
    }
    
    /// <summary>
    /// Helper method for tests to simulate slow operations.
    /// </summary>
    public async Task<ReadResourceResult> ReadResourceWithDelayAsync(string uri, TimeSpan delay, CancellationToken cancellationToken = default)
    {
        await Task.Delay(delay, cancellationToken);
        return await ReadResourceAsync(uri, cancellationToken);
    }
}