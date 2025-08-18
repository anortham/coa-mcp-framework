using NUnit.Framework;
using COA.Mcp.Framework.Resources;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Protocol;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;

namespace COA.Mcp.Framework.Tests.Resources;

[TestFixture]
public class ResourceRegistryWithCacheTests
{
    private ResourceRegistry _registry;
    private Mock<ILogger<ResourceRegistry>> _loggerMock;
#pragma warning disable CS0618 // Type or member is obsolete - testing backward compatibility
    private Mock<IResourceCache> _cacheMock;
    private Mock<IResourceProvider> _providerMock;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<ResourceRegistry>>();
        _cacheMock = new Mock<IResourceCache>();
#pragma warning restore CS0618 // Type or member is obsolete
        _providerMock = new Mock<IResourceProvider>();
        
        _registry = new ResourceRegistry(_loggerMock.Object, _cacheMock.Object);
    }

    [Test]
    public async Task ReadResourceAsync_Should_ReturnCachedResource_WhenCacheHit()
    {
        // Arrange
        var uri = "test://resource";
        var cachedResult = new ReadResourceResult
        {
            Contents = new List<ResourceContent>
            {
                new ResourceContent
                {
                    Uri = uri,
                    Text = "Cached content",
                    MimeType = "text/plain"
                }
            }
        };

        _cacheMock.Setup(c => c.GetAsync(uri))
            .ReturnsAsync(cachedResult);

        // Act
        var result = await _registry.ReadResourceAsync(uri);

        // Assert
        result.Should().Be(cachedResult);
        result.Contents[0].Text.Should().Be("Cached content");
        
        // Verify provider was never called
        _providerMock.Verify(p => p.ReadResourceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        
        // Verify cache was checked
        _cacheMock.Verify(c => c.GetAsync(uri), Times.Once);
        
        // Verify result was not cached again (since it came from cache)
        _cacheMock.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<ReadResourceResult>(), It.IsAny<TimeSpan?>()), Times.Never);
    }

    [Test]
    public async Task ReadResourceAsync_Should_CacheResult_WhenCacheMiss()
    {
        // Arrange
        var uri = "test://resource";
        var providerResult = new ReadResourceResult
        {
            Contents = new List<ResourceContent>
            {
                new ResourceContent
                {
                    Uri = uri,
                    Text = "Fresh content",
                    MimeType = "text/plain"
                }
            }
        };

        _cacheMock.Setup(c => c.GetAsync(uri))
            .ReturnsAsync((ReadResourceResult?)null); // Cache miss

        _providerMock.Setup(p => p.Scheme).Returns("test");
        _providerMock.Setup(p => p.Name).Returns("Test Provider");
        _providerMock.Setup(p => p.CanHandle(uri)).Returns(true);
        _providerMock.Setup(p => p.ReadResourceAsync(uri, It.IsAny<CancellationToken>()))
            .ReturnsAsync(providerResult);

        _registry.RegisterProvider(_providerMock.Object);

        // Act
        var result = await _registry.ReadResourceAsync(uri);

        // Assert
        result.Should().Be(providerResult);
        result.Contents[0].Text.Should().Be("Fresh content");
        
        // Verify cache was checked first
        _cacheMock.Verify(c => c.GetAsync(uri), Times.Once);
        
        // Verify provider was called
        _providerMock.Verify(p => p.ReadResourceAsync(uri, It.IsAny<CancellationToken>()), Times.Once);
        
        // Verify result was cached
        _cacheMock.Verify(c => c.SetAsync(uri, providerResult, It.IsAny<TimeSpan?>()), Times.Once);
    }

    [Test]
    public async Task ReadResourceAsync_Should_NotCacheNullResults()
    {
        // Arrange
        var uri = "test://resource";

        _cacheMock.Setup(c => c.GetAsync(uri))
            .ReturnsAsync((ReadResourceResult?)null); // Cache miss

        _providerMock.Setup(p => p.Scheme).Returns("test");
        _providerMock.Setup(p => p.Name).Returns("Test Provider");
        _providerMock.Setup(p => p.CanHandle(uri)).Returns(true);
        _providerMock.Setup(p => p.ReadResourceAsync(uri, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReadResourceResult?)null); // Provider returns null

        _registry.RegisterProvider(_providerMock.Object);

        // Setup a second provider that will handle the request
        var provider2Mock = new Mock<IResourceProvider>();
        provider2Mock.Setup(p => p.Scheme).Returns("test2");
        provider2Mock.Setup(p => p.Name).Returns("Test Provider 2");
        provider2Mock.Setup(p => p.CanHandle(uri)).Returns(true);
        provider2Mock.Setup(p => p.ReadResourceAsync(uri, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReadResourceResult { Contents = new List<ResourceContent>() });
        
        _registry.RegisterProvider(provider2Mock.Object);

        // Act
        var result = await _registry.ReadResourceAsync(uri);

        // Assert
        // Verify that null result from first provider was not cached
        _cacheMock.Verify(
            c => c.SetAsync(uri, null, It.IsAny<TimeSpan?>()), 
            Times.Never,
            "Null results should not be cached");
        
        // Verify that successful result from second provider was cached
        _cacheMock.Verify(
            c => c.SetAsync(uri, It.IsAny<ReadResourceResult>(), It.IsAny<TimeSpan?>()), 
            Times.Once);
    }

    [Test]
    public async Task ReadResourceAsync_Should_WorkWithoutCache()
    {
        // Arrange
        var registryNoCache = new ResourceRegistry(_loggerMock.Object, null);
        var uri = "test://resource";
        var providerResult = new ReadResourceResult
        {
            Contents = new List<ResourceContent>
            {
                new ResourceContent
                {
                    Uri = uri,
                    Text = "Content without cache",
                    MimeType = "text/plain"
                }
            }
        };

        _providerMock.Setup(p => p.Scheme).Returns("test");
        _providerMock.Setup(p => p.Name).Returns("Test Provider");
        _providerMock.Setup(p => p.CanHandle(uri)).Returns(true);
        _providerMock.Setup(p => p.ReadResourceAsync(uri, It.IsAny<CancellationToken>()))
            .ReturnsAsync(providerResult);

        registryNoCache.RegisterProvider(_providerMock.Object);

        // Act
        var result = await registryNoCache.ReadResourceAsync(uri);

        // Assert
        result.Should().Be(providerResult);
        
        // Verify provider was called
        _providerMock.Verify(p => p.ReadResourceAsync(uri, It.IsAny<CancellationToken>()), Times.Once);
        
        // Verify warning was logged about missing cache
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("No IResourceCache provided")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Test]
    public async Task ReadResourceAsync_Should_HandleCacheExceptions()
    {
        // Arrange
        var uri = "test://resource";
        var providerResult = new ReadResourceResult
        {
            Contents = new List<ResourceContent>
            {
                new ResourceContent
                {
                    Uri = uri,
                    Text = "Content despite cache error",
                    MimeType = "text/plain"
                }
            }
        };

        // Cache throws exception on get
        _cacheMock.Setup(c => c.GetAsync(uri))
            .ThrowsAsync(new Exception("Cache error"));

        _providerMock.Setup(p => p.Scheme).Returns("test");
        _providerMock.Setup(p => p.Name).Returns("Test Provider");
        _providerMock.Setup(p => p.CanHandle(uri)).Returns(true);
        _providerMock.Setup(p => p.ReadResourceAsync(uri, It.IsAny<CancellationToken>()))
            .ReturnsAsync(providerResult);

        _registry.RegisterProvider(_providerMock.Object);

        // Act & Assert
        // Should not throw - should fall back to provider
        var act = async () => await _registry.ReadResourceAsync(uri);
        await act.Should().NotThrowAsync();
    }
}