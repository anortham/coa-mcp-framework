using NUnit.Framework;
using COA.Mcp.Framework.Resources;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Protocol;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;

namespace COA.Mcp.Framework.Tests.Resources;

[TestFixture]
public class ResourceRegistryTests
{
    private ResourceRegistry _registry;
    private Mock<ILogger<ResourceRegistry>> _loggerMock;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<ResourceRegistry>>();
        _registry = new ResourceRegistry(_loggerMock.Object);
    }

    [Test]
    public void RegisterProvider_Should_AddProvider()
    {
        // Arrange
        var providerMock = new Mock<IResourceProvider>();
        providerMock.Setup(p => p.Scheme).Returns("test-scheme");
        providerMock.Setup(p => p.Name).Returns("Test Provider");
        var provider = providerMock.Object;

        // Act
        _registry.RegisterProvider(provider);
        var providers = _registry.GetProviders();

        // Assert
        providers.Should().ContainSingle();
        providers.First().Should().Be(provider);
    }

    [Test]
    public void RegisterProvider_Should_ReplaceDuplicateScheme()
    {
        // Arrange
        var provider1Mock = new Mock<IResourceProvider>();
        provider1Mock.Setup(p => p.Scheme).Returns("test-scheme");
        provider1Mock.Setup(p => p.Name).Returns("Provider 1");
        var provider1 = provider1Mock.Object;

        var provider2Mock = new Mock<IResourceProvider>();
        provider2Mock.Setup(p => p.Scheme).Returns("test-scheme");
        provider2Mock.Setup(p => p.Name).Returns("Provider 2");
        var provider2 = provider2Mock.Object;

        // Act
        _registry.RegisterProvider(provider1);
        _registry.RegisterProvider(provider2); // Should replace provider1

        // Assert
        var providers = _registry.GetProviders();
        providers.Should().ContainSingle();
        providers.First().Should().Be(provider2);
        
        // Verify warning was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Replacing existing provider")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task ListResourcesAsync_Should_AggregateFromAllProviders()
    {
        // Arrange
        var provider1Mock = new Mock<IResourceProvider>();
        provider1Mock.Setup(p => p.Scheme).Returns("scheme1");
        provider1Mock.Setup(p => p.ListResourcesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Resource>
            {
                new Resource { Uri = "scheme1://resource1", Name = "Resource 1" }
            });
        var provider1 = provider1Mock.Object;

        var provider2Mock = new Mock<IResourceProvider>();
        provider2Mock.Setup(p => p.Scheme).Returns("scheme2");
        provider2Mock.Setup(p => p.ListResourcesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Resource>
            {
                new Resource { Uri = "scheme2://resource2", Name = "Resource 2" }
            });
        var provider2 = provider2Mock.Object;

        _registry.RegisterProvider(provider1);
        _registry.RegisterProvider(provider2);

        // Act
        var resources = await _registry.ListResourcesAsync();

        // Assert
        resources.Should().HaveCount(2);
        resources.Should().Contain(r => r.Uri == "scheme1://resource1");
        resources.Should().Contain(r => r.Uri == "scheme2://resource2");
    }

    [Test]
    public async Task ReadResourceAsync_Should_DelegateToCorrectProvider()
    {
        // Arrange
        var provider1Mock = new Mock<IResourceProvider>();
        provider1Mock.Setup(p => p.Scheme).Returns("scheme1");
        provider1Mock.Setup(p => p.CanHandle("scheme1://resource1")).Returns(true);
        provider1Mock.Setup(p => p.CanHandle("scheme2://resource2")).Returns(false);
        provider1Mock.Setup(p => p.ReadResourceAsync("scheme1://resource1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReadResourceResult
            {
                Contents = new List<ResourceContent>
                {
                    new ResourceContent
                    {
                        Uri = "scheme1://resource1",
                        MimeType = "text/plain",
                        Text = "Content from provider 1"
                    }
                }
            });
        var provider1 = provider1Mock.Object;

        var provider2Mock = new Mock<IResourceProvider>();
        provider2Mock.Setup(p => p.Scheme).Returns("scheme2");
        provider2Mock.Setup(p => p.CanHandle("scheme1://resource1")).Returns(false);
        provider2Mock.Setup(p => p.CanHandle("scheme2://resource2")).Returns(true);
        var provider2 = provider2Mock.Object;

        _registry.RegisterProvider(provider1);
        _registry.RegisterProvider(provider2);

        // Act
        var result = await _registry.ReadResourceAsync("scheme1://resource1");

        // Assert
        result.Should().NotBeNull();
        result!.Contents.Should().HaveCount(1);
        result.Contents[0].Text.Should().Be("Content from provider 1");
        
        provider1Mock.Verify(p => p.ReadResourceAsync("scheme1://resource1", It.IsAny<CancellationToken>()), Times.Once);
        provider2Mock.Verify(p => p.ReadResourceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ReadResourceAsync_Should_ThrowException_WhenNoProviderCanHandle()
    {
        // Arrange
        var providerMock = new Mock<IResourceProvider>();
        providerMock.Setup(p => p.Scheme).Returns("scheme1");
        providerMock.Setup(p => p.CanHandle("unknown://resource")).Returns(false);
        var provider = providerMock.Object;
        
        _registry.RegisterProvider(provider);

        // Act
        Func<Task> act = async () => await _registry.ReadResourceAsync("unknown://resource");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("No provider found for resource URI: unknown://resource");
    }

    [Test]
    public async Task ListResourcesAsync_Should_HandleProviderExceptions()
    {
        // Arrange
        var failingProviderMock = new Mock<IResourceProvider>();
        failingProviderMock.Setup(p => p.Scheme).Returns("failing");
        failingProviderMock.Setup(p => p.ListResourcesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Provider error"));
        var failingProvider = failingProviderMock.Object;

        var workingProviderMock = new Mock<IResourceProvider>();
        workingProviderMock.Setup(p => p.Scheme).Returns("working");
        workingProviderMock.Setup(p => p.ListResourcesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Resource>
            {
                new Resource { Uri = "working://resource", Name = "Working Resource" }
            });
        var workingProvider = workingProviderMock.Object;

        _registry.RegisterProvider(failingProvider);
        _registry.RegisterProvider(workingProvider);

        // Act
        var resources = await _registry.ListResourcesAsync();

        // Assert
        resources.Should().HaveCount(1);
        resources[0].Uri.Should().Be("working://resource");
        
        // Verify error was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Error listing resources from provider")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public void GetProviders_Should_ReturnReadOnlyList()
    {
        // Arrange
        var providerMock = new Mock<IResourceProvider>();
        providerMock.Setup(p => p.Scheme).Returns("test");
        var provider = providerMock.Object;
        _registry.RegisterProvider(provider);

        // Act
        var providers = _registry.GetProviders();

        // Assert
        providers.Should().BeAssignableTo<IReadOnlyList<IResourceProvider>>();
        providers.Should().HaveCount(1);
    }

    [Test]
    public async Task ReadResourceAsync_Should_ThrowException_WhenProviderFails()
    {
        // Arrange
        var providerMock = new Mock<IResourceProvider>();
        providerMock.Setup(p => p.Scheme).Returns("error");
        providerMock.Setup(p => p.CanHandle("error://resource")).Returns(true);
        providerMock.Setup(p => p.ReadResourceAsync("error://resource", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Read error"));
        var provider = providerMock.Object;

        _registry.RegisterProvider(provider);

        // Act
        Func<Task> act = async () => await _registry.ReadResourceAsync("error://resource");

        // Assert
        // The registry logs the error but continues checking other providers
        // Since no provider successfully handles it, it throws InvalidOperationException
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("No provider found for resource URI: error://resource");
        
        // Verify error was logged for the failed provider
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Error reading resource")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}

// Test implementation of IResourceProvider
public class TestResourceProvider : IResourceProvider
{
    public string Scheme { get; set; } = "test";
    public string Name { get; set; } = "Test Provider";
    public string Description { get; set; } = "Test provider for unit tests";

    private readonly List<Resource> _resources = new();
    private readonly Dictionary<string, ReadResourceResult> _resourceContents = new();

    public void AddResource(Resource resource)
    {
        _resources.Add(resource);
    }

    public void SetResourceContent(string uri, ReadResourceResult content)
    {
        _resourceContents[uri] = content;
    }

    public Task<List<Resource>> ListResourcesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_resources.ToList());
    }

    public Task<ReadResourceResult?> ReadResourceAsync(string uri, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_resourceContents.TryGetValue(uri, out var content) ? content : null);
    }

    public bool CanHandle(string uri)
    {
        return uri.StartsWith($"{Scheme}://");
    }
}