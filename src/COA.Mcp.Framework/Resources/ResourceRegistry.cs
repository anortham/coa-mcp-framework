using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Protocol;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.Resources;

/// <summary>
/// Central registry for managing resource providers and routing resource requests.
/// Coordinates between different resource providers to offer a unified resource API.
/// </summary>
public class ResourceRegistry : IResourceRegistry
{
    private readonly ILogger<ResourceRegistry> _logger;
#pragma warning disable CS0618 // Type or member is obsolete - backward compatibility
    private readonly IResourceCache? _cache;
    private readonly List<IResourceProvider> _providers = new();

    public ResourceRegistry(ILogger<ResourceRegistry> logger, IResourceCache? cache = null)
#pragma warning restore CS0618 // Type or member is obsolete
    {
        _logger = logger;
        _cache = cache;
        
        if (_cache == null)
        {
            _logger.LogWarning("No IResourceCache provided. Resource caching is disabled. " +
                              "This may cause issues with scoped providers. " +
                              "Consider registering IResourceCache in your DI container.");
        }
    }

    /// <inheritdoc />
    public async Task<List<Resource>> ListResourcesAsync(CancellationToken cancellationToken = default)
    {
        var allResources = new List<Resource>();

        foreach (var provider in _providers)
        {
            try
            {
                var resources = await provider.ListResourcesAsync(cancellationToken);
                allResources.AddRange(resources);
                _logger.LogDebug("Provider {ProviderName} contributed {Count} resources", 
                    provider.Name, resources.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing resources from provider {ProviderName}", provider.Name);
                // Continue with other providers
            }
        }

        _logger.LogInformation("Listed {TotalCount} resources from {ProviderCount} providers", 
            allResources.Count, _providers.Count);

        return allResources;
    }

    /// <inheritdoc />
    public async Task<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            throw new ArgumentException("URI cannot be null or empty", nameof(uri));
        }

        _logger.LogDebug("Attempting to read resource: {Uri}", uri);
        
        // Check cache first if available
        if (_cache != null)
        {
            try
            {
                var cachedResult = await _cache.GetAsync(uri);
                if (cachedResult != null)
                {
                    _logger.LogDebug("Returning cached resource: {Uri}", uri);
                    return cachedResult;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve resource from cache: {Uri}. Continuing without cache.", uri);
                // Continue without cache
            }
        }
        
        _logger.LogDebug("Available providers: {Count}", _providers.Count);
        
        foreach (var provider in _providers)
        {
            _logger.LogDebug("Checking provider {ProviderName} (scheme: {Scheme}) for URI: {Uri}", 
                provider.Name, provider.Scheme, uri);
                
            try
            {
                var canHandle = provider.CanHandle(uri);
                _logger.LogDebug("Provider {ProviderName} CanHandle({Uri}) = {CanHandle}", 
                    provider.Name, uri, canHandle);
                    
                if (canHandle)
                {
                    _logger.LogDebug("Provider {ProviderName} can handle URI, attempting to read", provider.Name);
                    var result = await provider.ReadResourceAsync(uri, cancellationToken);
                    if (result != null)
                    {
                        _logger.LogDebug("Provider {ProviderName} successfully read resource {Uri}", 
                            provider.Name, uri);
                        
                        // Cache the result if cache is available
                        if (_cache != null)
                        {
                            try
                            {
                                await _cache.SetAsync(uri, result);
                                _logger.LogDebug("Cached resource: {Uri}", uri);
                            }
                            catch (Exception cacheEx)
                            {
                                _logger.LogWarning(cacheEx, "Failed to cache resource: {Uri}. Continuing without caching.", uri);
                                // Continue without caching - not a critical error
                            }
                        }
                        
                        return result;
                    }
                    else
                    {
                        _logger.LogWarning("Provider {ProviderName} returned null for resource {Uri}", 
                            provider.Name, uri);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading resource {Uri} from provider {ProviderName}", 
                    uri, provider.Name);
                // Continue with other providers
            }
        }

        _logger.LogWarning("No provider could handle resource URI: {Uri}. Total providers checked: {Count}", 
            uri, _providers.Count);
        throw new InvalidOperationException($"No provider found for resource URI: {uri}");
    }

    /// <inheritdoc />
    public void RegisterProvider(IResourceProvider provider)
    {
        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        // Check for duplicate schemes
        var existingProvider = _providers.FirstOrDefault(p => p.Scheme == provider.Scheme);
        if (existingProvider != null)
        {
            _logger.LogWarning("Replacing existing provider for scheme '{Scheme}'. " +
                             "Old: {OldProvider}, New: {NewProvider}", 
                             provider.Scheme, existingProvider.Name, provider.Name);
            _providers.Remove(existingProvider);
        }

        _providers.Add(provider);
        _logger.LogInformation("Registered resource provider: {ProviderName} (scheme: {Scheme})", 
            provider.Name, provider.Scheme);
    }

    /// <inheritdoc />
    public IEnumerable<IResourceProvider> GetProviders()
    {
        return _providers.AsReadOnly();
    }

    /// <summary>
    /// Gets a single resource content by URI (simplified method for framework).
    /// </summary>
    public async Task<ResourceContent> GetResourceAsync(string uri)
    {
        var result = await ReadResourceAsync(uri);
        return result.Contents.FirstOrDefault() ?? new ResourceContent
        {
            Uri = uri,
            Text = "Resource not found",
            MimeType = "text/plain"
        };
    }
}