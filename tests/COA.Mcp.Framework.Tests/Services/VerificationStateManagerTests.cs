using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using COA.Mcp.Framework.Configuration;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace COA.Mcp.Framework.Tests.Services;

[TestFixture]
public class VerificationStateManagerTests
{
    private Mock<ILogger<VerificationStateManager>> _mockLogger;
    private TypeVerificationOptions _options;
    private VerificationStateManager _manager;
    private string _tempDirectory;

    [SetUp]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<VerificationStateManager>>();
        _options = new TypeVerificationOptions
        {
            Enabled = true,
            CacheExpirationHours = 24,
            AutoVerifyOnHover = true,
            RequireMemberVerification = true,
            MaxCacheSize = 1000
        };

        var optionsWrapper = Options.Create(_options);
        _manager = new VerificationStateManager(_mockLogger.Object, optionsWrapper);
        
        // Create a temporary directory for test files
        _tempDirectory = Path.Combine(Path.GetTempPath(), "VerificationStateManagerTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        _manager?.Dispose();
        
        // Clean up temporary directory
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [Test]
    public async Task IsTypeVerifiedAsync_WithUnknownType_ShouldReturnFalse()
    {
        // Act
        var result = await _manager.IsTypeVerifiedAsync("UnknownType");

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task IsTypeVerifiedAsync_WithNullOrEmptyTypeName_ShouldReturnFalse()
    {
        // Act & Assert
        Assert.That(await _manager.IsTypeVerifiedAsync(null), Is.False);
        Assert.That(await _manager.IsTypeVerifiedAsync(""), Is.False);
        Assert.That(await _manager.IsTypeVerifiedAsync("   "), Is.False);
    }

    [Test]
    public async Task MarkTypeVerifiedAsync_ShouldCacheType()
    {
        // Arrange
        var typeInfo = CreateTestTypeInfo("User", "Models/User.cs");

        // Act
        await _manager.MarkTypeVerifiedAsync("User", typeInfo);

        // Assert
        var isVerified = await _manager.IsTypeVerifiedAsync("User");
        Assert.That(isVerified, Is.True);
    }

    [Test]
    public async Task MarkTypeVerifiedAsync_WithNullOrEmptyTypeName_ShouldNotCache()
    {
        // Arrange
        var typeInfo = CreateTestTypeInfo("User", "Models/User.cs");

        // Act
        await _manager.MarkTypeVerifiedAsync("", typeInfo);
        await _manager.MarkTypeVerifiedAsync(null, typeInfo);

        // Assert
        var stats = await _manager.GetCacheStatisticsAsync();
        Assert.That(stats.TotalTypes, Is.EqualTo(0));
    }

    [Test]
    public async Task HasVerifiedMemberAsync_WithVerifiedTypeAndMember_ShouldReturnTrue()
    {
        // Arrange
        var typeInfo = CreateTestTypeInfo("User", "Models/User.cs");
        typeInfo.Members["Name"] = new MemberInfo
        {
            Name = "Name",
            MemberType = MemberType.Property,
            DataType = "string",
            IsPublic = true
        };

        await _manager.MarkTypeVerifiedAsync("User", typeInfo);

        // Act
        var result = await _manager.HasVerifiedMemberAsync("User", "Name");

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task HasVerifiedMemberAsync_WithUnverifiedMember_ShouldReturnFalse()
    {
        // Arrange
        var typeInfo = CreateTestTypeInfo("User", "Models/User.cs");
        await _manager.MarkTypeVerifiedAsync("User", typeInfo);

        // Act
        var result = await _manager.HasVerifiedMemberAsync("User", "NonExistentMember");

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task HasVerifiedMemberAsync_WithUnverifiedType_ShouldReturnFalse()
    {
        // Act
        var result = await _manager.HasVerifiedMemberAsync("UnknownType", "SomeMember");

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task GetAvailableMembersAsync_WithVerifiedType_ShouldReturnMembers()
    {
        // Arrange
        var typeInfo = CreateTestTypeInfo("User", "Models/User.cs");
        typeInfo.Members["Name"] = new MemberInfo { Name = "Name" };
        typeInfo.Members["Email"] = new MemberInfo { Name = "Email" };
        typeInfo.Members["Save"] = new MemberInfo { Name = "Save" };

        await _manager.MarkTypeVerifiedAsync("User", typeInfo);

        // Act
        var members = await _manager.GetAvailableMembersAsync("User");

        // Assert
        Assert.That(members, Is.Not.Null);
        Assert.That(members.Count(), Is.EqualTo(3));
        Assert.That(members, Contains.Item("Name"));
        Assert.That(members, Contains.Item("Email"));
        Assert.That(members, Contains.Item("Save"));
    }

    [Test]
    public async Task GetAvailableMembersAsync_WithUnverifiedType_ShouldReturnNull()
    {
        // Act
        var members = await _manager.GetAvailableMembersAsync("UnknownType");

        // Assert
        Assert.That(members, Is.Null);
    }

    [Test]
    public async Task GetVerificationStatusAsync_WithVerifiedType_ShouldReturnStatus()
    {
        // Arrange
        var typeInfo = CreateTestTypeInfo("User", "Models/User.cs");
        await _manager.MarkTypeVerifiedAsync("User", typeInfo);

        // Act
        var status = await _manager.GetVerificationStatusAsync("User");

        // Assert
        Assert.That(status, Is.Not.Null);
        Assert.That(status.TypeName, Is.EqualTo("User"));
        Assert.That(status.FilePath, Is.EqualTo("Models/User.cs"));
        Assert.That(status.VerificationMethod, Is.EqualTo(VerificationMethod.ExplicitVerification));
    }

    [Test]
    public async Task GetVerificationStatusAsync_WithUnverifiedType_ShouldReturnNull()
    {
        // Act
        var status = await _manager.GetVerificationStatusAsync("UnknownType");

        // Assert
        Assert.That(status, Is.Null);
    }

    [Test]
    public async Task ClearCacheAsync_WithoutPattern_ShouldClearAllEntries()
    {
        // Arrange
        var typeInfo1 = CreateTestTypeInfo("User", "Models/User.cs");
        var typeInfo2 = CreateTestTypeInfo("Customer", "Models/Customer.cs");
        await _manager.MarkTypeVerifiedAsync("User", typeInfo1);
        await _manager.MarkTypeVerifiedAsync("Customer", typeInfo2);

        // Act
        await _manager.ClearCacheAsync();

        // Assert
        var isUserVerified = await _manager.IsTypeVerifiedAsync("User");
        var isCustomerVerified = await _manager.IsTypeVerifiedAsync("Customer");
        Assert.That(isUserVerified, Is.False);
        Assert.That(isCustomerVerified, Is.False);
    }

    [Test]
    public async Task ClearCacheAsync_WithPattern_ShouldClearMatchingEntries()
    {
        // Arrange
        var userInfo = CreateTestTypeInfo("User", "Models/User.cs");
        var customerInfo = CreateTestTypeInfo("Customer", "Models/Customer.cs");
        var productInfo = CreateTestTypeInfo("Product", "Models/Product.cs");
        
        await _manager.MarkTypeVerifiedAsync("User", userInfo);
        await _manager.MarkTypeVerifiedAsync("Customer", customerInfo);
        await _manager.MarkTypeVerifiedAsync("Product", productInfo);

        // Act
        await _manager.ClearCacheAsync("*er"); // Should match User and Customer

        // Assert
        var isUserVerified = await _manager.IsTypeVerifiedAsync("User");
        var isCustomerVerified = await _manager.IsTypeVerifiedAsync("Customer");
        var isProductVerified = await _manager.IsTypeVerifiedAsync("Product");
        
        Assert.That(isUserVerified, Is.False);
        Assert.That(isCustomerVerified, Is.False);
        Assert.That(isProductVerified, Is.True); // Should remain
    }

    [Test]
    public async Task GetCacheStatisticsAsync_ShouldReturnAccurateStats()
    {
        // Arrange
        var typeInfo = CreateTestTypeInfo("User", "Models/User.cs");
        await _manager.MarkTypeVerifiedAsync("User", typeInfo);

        // Trigger cache hits and misses
        await _manager.IsTypeVerifiedAsync("User"); // Hit
        await _manager.IsTypeVerifiedAsync("User"); // Hit
        await _manager.IsTypeVerifiedAsync("UnknownType"); // Miss
        await _manager.IsTypeVerifiedAsync("AnotherUnknown"); // Miss

        // Act
        var stats = await _manager.GetCacheStatisticsAsync();

        // Assert
        Assert.That(stats.TotalTypes, Is.EqualTo(1));
        Assert.That(stats.CacheHits, Is.EqualTo(2));
        Assert.That(stats.CacheMisses, Is.EqualTo(2));
        Assert.That(stats.HitRate, Is.EqualTo(50.0));
        Assert.That(stats.MemoryUsageBytes, Is.GreaterThan(0));
    }

    [Test]
    public async Task LogVerificationSuccessAsync_ShouldUpdateAccessTimes()
    {
        // Arrange
        var typeInfo = CreateTestTypeInfo("User", "Models/User.cs");
        await _manager.MarkTypeVerifiedAsync("User", typeInfo);
        var originalStatus = await _manager.GetVerificationStatusAsync("User");
        var originalAccessCount = originalStatus.AccessCount;
        var originalLastAccessedAt = originalStatus.LastAccessedAt;

        // Act
        await Task.Delay(10); // Ensure time difference
        await _manager.LogVerificationSuccessAsync("TestTool", "test.cs", new[] { "User" });

        // Assert
        var updatedStatus = await _manager.GetVerificationStatusAsync("User");
        Assert.That(updatedStatus.AccessCount, Is.GreaterThan(originalAccessCount));
        Assert.That(updatedStatus.LastAccessedAt, Is.GreaterThan(originalLastAccessedAt));
    }

    [Test]
    public async Task LogVerificationFailureAsync_ShouldLogWarning()
    {
        // Act
        await _manager.LogVerificationFailureAsync("TestTool", "test.cs", 
            new[] { "UnknownType1", "UnknownType2" }, 
            new[] { "MemberIssue1", "MemberIssue2" });

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task GetUnverifiedTypesAsync_ShouldExtractAndFilterTypes()
    {
        // Arrange
        var code = "User user = new User(); Customer customer = new Customer(); string name = \"test\";";
        var filePath = "test.cs";

        var userInfo = CreateTestTypeInfo("User", "Models/User.cs");
        await _manager.MarkTypeVerifiedAsync("User", userInfo); // User is verified

        // Act
        var unverifiedTypes = await _manager.GetUnverifiedTypesAsync(code, filePath);

        // Assert
        Assert.That(unverifiedTypes.Count, Is.EqualTo(1));
        Assert.That(unverifiedTypes.First().TypeName, Is.EqualTo("Customer"));
    }

    [Test]
    public async Task BulkVerifyTypesAsync_ShouldReturnVerificationStatus()
    {
        // Arrange
        var userInfo = CreateTestTypeInfo("User", "Models/User.cs");
        await _manager.MarkTypeVerifiedAsync("User", userInfo);

        var typeNames = new[] { "User", "Customer", "Product" };

        // Act
        var results = await _manager.BulkVerifyTypesAsync(typeNames, "/workspace");

        // Assert
        Assert.That(results.Count, Is.EqualTo(3));
        Assert.That(results["User"], Is.True);
        Assert.That(results["Customer"], Is.False);
        Assert.That(results["Product"], Is.False);
    }

    [Test]
    public async Task WarmCacheAsync_WithValidFile_ShouldProcessSuccessfully()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "test.cs");
        var content = "public class User { public string Name { get; set; } }";
        await File.WriteAllTextAsync(testFile, content);

        // Act - Should not throw
        await _manager.WarmCacheAsync(testFile);

        // Assert - Check that it logged the intent
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Test]
    public async Task WarmCacheAsync_WithInvalidFile_ShouldHandleGracefully()
    {
        // Act - Should not throw
        await _manager.WarmCacheAsync("nonexistent.cs");

        // Assert - Should complete without error
        Assert.Pass("Method completed without throwing exception");
    }

    [Test]
    public async Task InvalidateCacheForFileAsync_ShouldRemoveFileTypes()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "User.cs");
        var typeInfo = CreateTestTypeInfo("User", filePath);
        await _manager.MarkTypeVerifiedAsync("User", typeInfo);

        // Verify type is cached
        var isVerifiedBefore = await _manager.IsTypeVerifiedAsync("User");
        Assert.That(isVerifiedBefore, Is.True);

        // Act
        await _manager.InvalidateCacheForFileAsync(filePath);

        // Assert
        var isVerifiedAfter = await _manager.IsTypeVerifiedAsync("User");
        Assert.That(isVerifiedAfter, Is.False);
    }

    [Test]
    public async Task MarkTypeVerifiedAsync_WithFileModificationTime_ShouldSetCorrectTime()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "User.cs");
        await File.WriteAllTextAsync(filePath, "public class User { }");
        
        var typeInfo = CreateTestTypeInfo("User", filePath);

        // Act
        await _manager.MarkTypeVerifiedAsync("User", typeInfo);

        // Assert
        var status = await _manager.GetVerificationStatusAsync("User");
        Assert.That(status.FileModificationTime, Is.GreaterThan(0));
    }

    [Test]
    public async Task MarkTypeVerifiedAsync_WithExistingType_ShouldUpdateIfNewer()
    {
        // Arrange
        var typeInfo1 = CreateTestTypeInfo("User", "Models/User.cs");
        typeInfo1.Name = "User";
        await _manager.MarkTypeVerifiedAsync("User", typeInfo1);

        await Task.Delay(10); // Ensure time difference

        var typeInfo2 = CreateTestTypeInfo("User", "Models/User.cs");
        typeInfo2.Name = "User";
        typeInfo2.Namespace = "UpdatedNamespace";

        // Act
        await _manager.MarkTypeVerifiedAsync("User", typeInfo2);

        // Assert
        var status = await _manager.GetVerificationStatusAsync("User");
        Assert.That(status.Namespace, Is.EqualTo("UpdatedNamespace"));
    }

    [Test]
    public async Task IsTypeVerifiedAsync_WithExpiredType_ShouldReturnFalse()
    {
        // Arrange
        var typeInfo = CreateTestTypeInfo("User", "Models/User.cs");
        await _manager.MarkTypeVerifiedAsync("User", typeInfo);

        // Manually expire the type by setting ExpiresAt to a past time
        var status = await _manager.GetVerificationStatusAsync("User");
        status!.ExpiresAt = DateTime.UtcNow.AddMinutes(-1); // Set to past time

        // Act
        var isVerified = await _manager.IsTypeVerifiedAsync("User");

        // Assert
        Assert.That(isVerified, Is.False);
    }

    [Test]
    public async Task StartAsync_ShouldCompleteSuccessfully()
    {
        // Act & Assert - Should not throw
        await _manager.StartAsync(CancellationToken.None);
    }

    [Test]
    public async Task StopAsync_ShouldCompleteSuccessfully()
    {
        // Act & Assert - Should not throw
        await _manager.StopAsync(CancellationToken.None);
    }

    private static TypeInfo CreateTestTypeInfo(string name, string filePath)
    {
        return new TypeInfo
        {
            Name = name,
            FullName = $"TestNamespace.{name}",
            FilePath = filePath,
            LineNumber = 1,
            ColumnNumber = 1,
            Namespace = "TestNamespace",
            AssemblyName = "TestAssembly"
        };
    }
}