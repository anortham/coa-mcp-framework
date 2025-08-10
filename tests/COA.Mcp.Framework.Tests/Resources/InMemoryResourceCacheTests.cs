using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Resources;
using COA.Mcp.Protocol;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace COA.Mcp.Framework.Tests.Resources;

[TestFixture]
public class InMemoryResourceCacheTests
{
    private InMemoryResourceCache _cache;
    private IMemoryCache _memoryCache;
    private Mock<ILogger<InMemoryResourceCache>> _loggerMock;
    private ResourceCacheOptions _options;

    [SetUp]
    public void SetUp()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _loggerMock = new Mock<ILogger<InMemoryResourceCache>>();
        _options = new ResourceCacheOptions
        {
            DefaultExpiration = TimeSpan.FromMinutes(5),
            SlidingExpiration = TimeSpan.FromMinutes(2),
            EnableStatistics = true
        };
        
        _cache = new InMemoryResourceCache(
            _memoryCache,
            _loggerMock.Object,
            Options.Create(_options));
    }

    [TearDown]
    public void TearDown()
    {
        _memoryCache?.Dispose();
    }

    [Test]
    public async Task GetAsync_Should_ReturnNull_WhenResourceNotInCache()
    {
        // Act
        var result = await _cache.GetAsync("test://resource");

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task SetAsync_And_GetAsync_Should_StoreAndRetrieveResource()
    {
        // Arrange
        var uri = "test://resource";
        var resource = new ReadResourceResult
        {
            Contents = new List<ResourceContent>
            {
                new ResourceContent
                {
                    Uri = uri,
                    Text = "Test content",
                    MimeType = "text/plain"
                }
            }
        };

        // Act
        await _cache.SetAsync(uri, resource);
        var retrieved = await _cache.GetAsync(uri);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved.Contents.Should().HaveCount(1);
        retrieved.Contents[0].Text.Should().Be("Test content");
    }

    [Test]
    public async Task ExistsAsync_Should_ReturnTrue_WhenResourceExists()
    {
        // Arrange
        var uri = "test://resource";
        var resource = new ReadResourceResult
        {
            Contents = new List<ResourceContent>()
        };
        await _cache.SetAsync(uri, resource);

        // Act
        var exists = await _cache.ExistsAsync(uri);

        // Assert
        exists.Should().BeTrue();
    }

    [Test]
    public async Task ExistsAsync_Should_ReturnFalse_WhenResourceDoesNotExist()
    {
        // Act
        var exists = await _cache.ExistsAsync("test://nonexistent");

        // Assert
        exists.Should().BeFalse();
    }

    [Test]
    public async Task RemoveAsync_Should_RemoveResourceFromCache()
    {
        // Arrange
        var uri = "test://resource";
        var resource = new ReadResourceResult
        {
            Contents = new List<ResourceContent>()
        };
        await _cache.SetAsync(uri, resource);

        // Act
        await _cache.RemoveAsync(uri);
        var exists = await _cache.ExistsAsync(uri);

        // Assert
        exists.Should().BeFalse();
    }

    [Test]
    public async Task GetStatisticsAsync_Should_TrackHitsAndMisses()
    {
        // Arrange
        var uri = "test://resource";
        var resource = new ReadResourceResult
        {
            Contents = new List<ResourceContent>()
        };
        await _cache.SetAsync(uri, resource);

        // Act
        await _cache.GetAsync(uri); // Hit
        await _cache.GetAsync(uri); // Hit
        await _cache.GetAsync("test://nonexistent"); // Miss

        var stats = await _cache.GetStatisticsAsync();

        // Assert
        stats.TotalHits.Should().Be(2);
        stats.TotalMisses.Should().Be(1);
        stats.HitRate.Should().BeApproximately(0.667, 0.001);
    }

    [Test]
    public async Task SetAsync_Should_UseDefaultExpiration_WhenNotSpecified()
    {
        // Arrange
        var uri = "test://resource";
        var resource = new ReadResourceResult
        {
            Contents = new List<ResourceContent>()
        };

        // Act
        await _cache.SetAsync(uri, resource);

        // Assert
        // The resource should exist immediately after setting
        var exists = await _cache.ExistsAsync(uri);
        exists.Should().BeTrue();
        
        // Verify logging indicates default expiration was used
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Cached resource: {uri} with expiration: {_options.DefaultExpiration}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Test]
    public async Task SetAsync_Should_UseCustomExpiration_WhenSpecified()
    {
        // Arrange
        var uri = "test://resource";
        var resource = new ReadResourceResult
        {
            Contents = new List<ResourceContent>()
        };
        var customExpiry = TimeSpan.FromSeconds(30);

        // Act
        await _cache.SetAsync(uri, resource, customExpiry);

        // Assert
        var exists = await _cache.ExistsAsync(uri);
        exists.Should().BeTrue();
        
        // Verify logging indicates custom expiration was used
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Cached resource: {uri} with expiration: {customExpiry}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Test]
    public async Task GetStatisticsAsync_Should_TrackHitsByScheme()
    {
        // Arrange
        var testResource = new ReadResourceResult { Contents = new List<ResourceContent>() };
        var httpResource = new ReadResourceResult { Contents = new List<ResourceContent>() };
        
        await _cache.SetAsync("test://resource1", testResource);
        await _cache.SetAsync("http://resource2", httpResource);

        // Act
        await _cache.GetAsync("test://resource1"); // test scheme hit
        await _cache.GetAsync("test://resource1"); // test scheme hit
        await _cache.GetAsync("http://resource2");  // http scheme hit

        var stats = await _cache.GetStatisticsAsync();

        // Assert
        stats.HitsByScheme.Should().ContainKey("test");
        stats.HitsByScheme["test"].Should().Be(2);
        stats.HitsByScheme.Should().ContainKey("http");
        stats.HitsByScheme["http"].Should().Be(1);
    }

    [Test]
    public void SetAsync_Should_ThrowException_WhenUriIsNullOrEmpty()
    {
        // Arrange
        var resource = new ReadResourceResult();

        // Act & Assert
        Func<Task> act1 = async () => await _cache.SetAsync(null, resource);
        Func<Task> act2 = async () => await _cache.SetAsync("", resource);
        Func<Task> act3 = async () => await _cache.SetAsync("  ", resource);

        act1.Should().ThrowAsync<ArgumentException>().WithParameterName("uri");
        act2.Should().ThrowAsync<ArgumentException>().WithParameterName("uri");
        act3.Should().ThrowAsync<ArgumentException>().WithParameterName("uri");
    }

    [Test]
    public void SetAsync_Should_ThrowException_WhenResultIsNull()
    {
        // Act & Assert
        Func<Task> act = async () => await _cache.SetAsync("test://resource", null);
        act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("result");
    }

    [Test]
    public async Task ClearAsync_Should_ResetStatistics()
    {
        // Arrange
        var uri = "test://resource";
        var resource = new ReadResourceResult { Contents = new List<ResourceContent>() };
        await _cache.SetAsync(uri, resource);
        await _cache.GetAsync(uri); // Create some statistics

        // Act
        await _cache.ClearAsync();
        var stats = await _cache.GetStatisticsAsync();

        // Assert
        stats.TotalHits.Should().Be(0);
        stats.TotalMisses.Should().Be(0);
        stats.TotalEvictions.Should().Be(0);
        stats.HitsByScheme.Should().BeEmpty();
    }
}