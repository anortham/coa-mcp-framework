using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using COA.Mcp.Framework.Configuration;
using COA.Mcp.Framework.Exceptions;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework.Pipeline.Middleware;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace COA.Mcp.Framework.Tests.Pipeline.Middleware;

[TestFixture]
public class TypeVerificationMiddlewareTests
{
    private Mock<IVerificationStateManager> _mockVerificationStateManager;
    private Mock<ILogger<TypeVerificationMiddleware>> _mockLogger;
    private TypeVerificationOptions _options;
    private TypeVerificationMiddleware _middleware;

    [SetUp]
    public void Setup()
    {
        _mockVerificationStateManager = new Mock<IVerificationStateManager>();
        _mockLogger = new Mock<ILogger<TypeVerificationMiddleware>>();
        _options = new TypeVerificationOptions
        {
            Enabled = true,
            Mode = TypeVerificationMode.Strict,
            RequireMemberVerification = true
        };

        var optionsWrapper = Options.Create(_options);
        _middleware = new TypeVerificationMiddleware(
            _mockVerificationStateManager.Object,
            _mockLogger.Object,
            optionsWrapper);
    }

    [Test]
    public async Task OnBeforeExecutionAsync_WithDisabledMiddleware_ShouldNotPerformVerification()
    {
        // Arrange
        _options.Enabled = false;
        var middleware = new TypeVerificationMiddleware(
            _mockVerificationStateManager.Object,
            _mockLogger.Object,
            Options.Create(_options));

        var parameters = CreateEditParameters("User user = new User();", "test.cs");

        // Act
        await middleware.OnBeforeExecutionAsync("Edit", parameters);

        // Assert
        _mockVerificationStateManager.Verify(x => x.IsTypeVerifiedAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task OnBeforeExecutionAsync_WithNonEditTool_ShouldNotPerformVerification()
    {
        // Arrange
        var parameters = new { someParameter = "value" };

        // Act
        await _middleware.OnBeforeExecutionAsync("Read", parameters);

        // Assert
        _mockVerificationStateManager.Verify(x => x.IsTypeVerifiedAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task OnBeforeExecutionAsync_WithCSharpCode_ShouldExtractTypes()
    {
        // Arrange
        var code = "public User GetUser() { return new User(); }";
        var parameters = CreateEditParameters(code, "test.cs");

        _mockVerificationStateManager
            .Setup(x => x.IsTypeVerifiedAsync("User"))
            .ReturnsAsync(true);

        // Act
        await _middleware.OnBeforeExecutionAsync("Edit", parameters);

        // Assert
        _mockVerificationStateManager.Verify(x => x.IsTypeVerifiedAsync("User"), Times.AtLeastOnce);
    }

    [Test]
    public async Task OnBeforeExecutionAsync_WithTypeScriptCode_ShouldExtractTypes()
    {
        // Arrange
        var code = "interface User { name: string; } const user: User = new User();";
        var parameters = CreateEditParameters(code, "test.ts");

        _mockVerificationStateManager
            .Setup(x => x.IsTypeVerifiedAsync("User"))
            .ReturnsAsync(true);

        // Act
        await _middleware.OnBeforeExecutionAsync("Edit", parameters);

        // Assert
        _mockVerificationStateManager.Verify(x => x.IsTypeVerifiedAsync("User"), Times.AtLeastOnce);
    }

    [Test]
    public async Task OnBeforeExecutionAsync_WithWhitelistedType_ShouldNotRequireVerification()
    {
        // Arrange
        var code = "string name = \"test\"; int count = 5;";
        var parameters = CreateEditParameters(code, "test.cs");

        // Act
        await _middleware.OnBeforeExecutionAsync("Edit", parameters);

        // Assert
        _mockVerificationStateManager.Verify(x => x.IsTypeVerifiedAsync("string"), Times.Never);
        _mockVerificationStateManager.Verify(x => x.IsTypeVerifiedAsync("int"), Times.Never);
    }

    [Test]
    public async Task OnBeforeExecutionAsync_WithUnverifiedType_InStrictMode_ShouldThrowException()
    {
        // Arrange
        var code = "CustomType instance = new CustomType();";
        var parameters = CreateEditParameters(code, "test.cs");

        _mockVerificationStateManager
            .Setup(x => x.IsTypeVerifiedAsync("CustomType"))
            .ReturnsAsync(false);

        // Act & Assert
        try
        {
            await _middleware.OnBeforeExecutionAsync("Edit", parameters);
            Assert.Fail("Expected TypeVerificationException to be thrown");
        }
        catch (McpException ex)
        {
            Assert.That(ex.Message, Does.Contain("Unverified types detected"));
            Assert.That(ex.Message, Does.Contain("CustomType"));
        }
    }

    [Test]
    public async Task OnBeforeExecutionAsync_WithUnverifiedType_InWarningMode_ShouldLogWarning()
    {
        // Arrange
        _options.Mode = TypeVerificationMode.Warning;
        var middleware = new TypeVerificationMiddleware(
            _mockVerificationStateManager.Object,
            _mockLogger.Object,
            Options.Create(_options));

        var code = "CustomType instance = new CustomType();";
        var parameters = CreateEditParameters(code, "test.cs");

        _mockVerificationStateManager
            .Setup(x => x.IsTypeVerifiedAsync("CustomType"))
            .ReturnsAsync(false);

        // Act
        await _middleware.OnBeforeExecutionAsync("Edit", parameters);

        // Assert - Should not throw, but should log warning
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
            Times.AtLeastOnce);
    }

    [Test]
    public async Task OnBeforeExecutionAsync_WithMemberAccess_ShouldVerifyMember()
    {
        // Arrange
        var code = "User.Name = \"test\"; User.Save();";
        var parameters = CreateEditParameters(code, "test.cs");

        _mockVerificationStateManager
            .Setup(x => x.IsTypeVerifiedAsync("User"))
            .ReturnsAsync(true);
        _mockVerificationStateManager
            .Setup(x => x.HasVerifiedMemberAsync("User", "Name"))
            .ReturnsAsync(true);
        _mockVerificationStateManager
            .Setup(x => x.HasVerifiedMemberAsync("User", "Save"))
            .ReturnsAsync(true);

        // Act
        await _middleware.OnBeforeExecutionAsync("Edit", parameters);

        // Assert
        _mockVerificationStateManager.Verify(x => x.HasVerifiedMemberAsync("User", "Name"), Times.Once);
        _mockVerificationStateManager.Verify(x => x.HasVerifiedMemberAsync("User", "Save"), Times.Once);
    }

    [Test]
    public async Task OnBeforeExecutionAsync_WithUnverifiedMember_ShouldProvideAvailableMembers()
    {
        // Arrange
        var code = "User.NonExistentProperty = \"test\";";
        var parameters = CreateEditParameters(code, "test.cs");

        _mockVerificationStateManager
            .Setup(x => x.IsTypeVerifiedAsync("User"))
            .ReturnsAsync(true);
        _mockVerificationStateManager
            .Setup(x => x.HasVerifiedMemberAsync("User", "NonExistentProperty"))
            .ReturnsAsync(false);
        _mockVerificationStateManager
            .Setup(x => x.GetAvailableMembersAsync("User"))
            .ReturnsAsync(new[] { "Name", "Email", "Save", "Delete" });

        // Act & Assert
        try
        {
            await _middleware.OnBeforeExecutionAsync("Edit", parameters);
            Assert.Fail("Expected TypeVerificationException to be thrown");
        }
        catch (McpException ex)
        {
            Assert.That(ex.Message, Does.Contain("Unknown type members detected"));
            Assert.That(ex.Message, Does.Contain("User.NonExistentProperty does not exist"));
            Assert.That(ex.Message, Does.Contain("Available members: Name, Email, Save, Delete"));
        }
    }

    [Test]
    public async Task OnBeforeExecutionAsync_WithMultiEditTool_ShouldExtractFromAllEdits()
    {
        // Arrange
        var parameters = new
        {
            file_path = "test.cs",
            edits = new[]
            {
                new { old_string = "old1", new_string = "User user1 = new User();" },
                new { old_string = "old2", new_string = "Customer customer = new Customer();" }
            }
        };

        _mockVerificationStateManager
            .Setup(x => x.IsTypeVerifiedAsync("User"))
            .ReturnsAsync(true);
        _mockVerificationStateManager
            .Setup(x => x.IsTypeVerifiedAsync("Customer"))
            .ReturnsAsync(true);

        // Act
        await _middleware.OnBeforeExecutionAsync("MultiEdit", parameters);

        // Assert
        _mockVerificationStateManager.Verify(x => x.IsTypeVerifiedAsync("User"), Times.AtLeastOnce);
        _mockVerificationStateManager.Verify(x => x.IsTypeVerifiedAsync("Customer"), Times.AtLeastOnce);
    }

    [Test]
    public async Task OnBeforeExecutionAsync_WithWriteTool_ShouldExtractFromContent()
    {
        // Arrange
        var parameters = new
        {
            file_path = "test.cs",
            content = "public class NewClass : BaseClass { public string Property { get; set; } }"
        };

        _mockVerificationStateManager
            .Setup(x => x.IsTypeVerifiedAsync("NewClass"))
            .ReturnsAsync(true);
        _mockVerificationStateManager
            .Setup(x => x.IsTypeVerifiedAsync("BaseClass"))
            .ReturnsAsync(true);

        // Act
        await _middleware.OnBeforeExecutionAsync("Write", parameters);

        // Assert
        _mockVerificationStateManager.Verify(x => x.IsTypeVerifiedAsync("NewClass"), Times.AtLeastOnce);
        _mockVerificationStateManager.Verify(x => x.IsTypeVerifiedAsync("BaseClass"), Times.AtLeastOnce);
    }

    [Test]
    public async Task OnBeforeExecutionAsync_WithGenericTypes_ShouldExtractGenericTypeNames()
    {
        // Arrange
        var code = "MyList<User> users = new MyList<User>(); MyDictionary<string, Customer> customers = new MyDictionary<string, Customer>();";
        var parameters = CreateEditParameters(code, "test.cs");

        _mockVerificationStateManager
            .Setup(x => x.IsTypeVerifiedAsync("MyList"))
            .ReturnsAsync(true);
        _mockVerificationStateManager
            .Setup(x => x.IsTypeVerifiedAsync("User"))
            .ReturnsAsync(true);
        _mockVerificationStateManager
            .Setup(x => x.IsTypeVerifiedAsync("MyDictionary"))
            .ReturnsAsync(true);
        _mockVerificationStateManager
            .Setup(x => x.IsTypeVerifiedAsync("Customer"))
            .ReturnsAsync(true);

        // Act
        await _middleware.OnBeforeExecutionAsync("Edit", parameters);

        // Assert
        _mockVerificationStateManager.Verify(x => x.IsTypeVerifiedAsync("MyList"), Times.AtLeastOnce);
        _mockVerificationStateManager.Verify(x => x.IsTypeVerifiedAsync("User"), Times.AtLeastOnce);
        _mockVerificationStateManager.Verify(x => x.IsTypeVerifiedAsync("MyDictionary"), Times.AtLeastOnce);
        _mockVerificationStateManager.Verify(x => x.IsTypeVerifiedAsync("Customer"), Times.AtLeastOnce);
    }

    [Test]
    public async Task OnBeforeExecutionAsync_WithSuccessfulVerification_ShouldLogSuccess()
    {
        // Arrange
        var code = "User user = new User();";
        var parameters = CreateEditParameters(code, "test.cs");

        _mockVerificationStateManager
            .Setup(x => x.IsTypeVerifiedAsync("User"))
            .ReturnsAsync(true);

        // Act
        await _middleware.OnBeforeExecutionAsync("Edit", parameters);

        // Assert
        _mockVerificationStateManager.Verify(
            x => x.LogVerificationSuccessAsync("Edit", "test.cs", It.IsAny<IList<string>>()),
            Times.Once);
    }

    [Test]
    public async Task OnBeforeExecutionAsync_WithVerificationFailure_ShouldLogFailure()
    {
        // Arrange
        _options.Mode = TypeVerificationMode.Warning; // To avoid exception
        var middleware = new TypeVerificationMiddleware(
            _mockVerificationStateManager.Object,
            _mockLogger.Object,
            Options.Create(_options));

        var code = "UnknownType instance = new UnknownType();";
        var parameters = CreateEditParameters(code, "test.cs");

        _mockVerificationStateManager
            .Setup(x => x.IsTypeVerifiedAsync("UnknownType"))
            .ReturnsAsync(false);

        // Act
        await _middleware.OnBeforeExecutionAsync("Edit", parameters);

        // Assert
        _mockVerificationStateManager.Verify(
            x => x.LogVerificationFailureAsync("Edit", "test.cs", It.IsAny<IList<string>>(), It.IsAny<IList<string>>()),
            Times.Once);
    }

    [Test]
    public async Task OnBeforeExecutionAsync_WithJsonElementParameters_ShouldParseCorrectly()
    {
        // Arrange
        var jsonString = JsonSerializer.Serialize(new
        {
            file_path = "test.cs",
            new_string = "User user = new User();"
        });
        var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonString);

        _mockVerificationStateManager
            .Setup(x => x.IsTypeVerifiedAsync("User"))
            .ReturnsAsync(true);

        // Act
        await _middleware.OnBeforeExecutionAsync("Edit", jsonElement);

        // Assert
        _mockVerificationStateManager.Verify(x => x.IsTypeVerifiedAsync("User"), Times.AtLeastOnce);
    }

    [Test]
    public async Task OnBeforeExecutionAsync_WithNoCodeContent_ShouldReturnEarly()
    {
        // Arrange
        var parameters = new { file_path = "test.cs", new_string = "" };

        // Act
        await _middleware.OnBeforeExecutionAsync("Edit", parameters);

        // Assert
        _mockVerificationStateManager.Verify(x => x.IsTypeVerifiedAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task OnBeforeExecutionAsync_WithCustomWhitelistedTypes_ShouldSkipVerification()
    {
        // Arrange
        _options.WhitelistedTypes = new HashSet<string> { "CustomFrameworkType" };
        var middleware = new TypeVerificationMiddleware(
            _mockVerificationStateManager.Object,
            _mockLogger.Object,
            Options.Create(_options));

        var code = "CustomFrameworkType instance = new CustomFrameworkType();";
        var parameters = CreateEditParameters(code, "test.cs");

        // Act
        await _middleware.OnBeforeExecutionAsync("Edit", parameters);

        // Assert
        _mockVerificationStateManager.Verify(x => x.IsTypeVerifiedAsync("CustomFrameworkType"), Times.Never);
    }

    /// <summary>
    /// BUG REPRODUCTION: Tests sequential async performance killer in TypeVerificationMiddleware.
    /// This test SHOULD FAIL due to sequential await calls in foreach loops (lines 112-144).
    /// Expected behavior: Concurrent verification of multiple types for performance.
    /// Actual behavior: Each type verification blocks the next, causing O(n) sequential delays.
    /// </summary>
    [Test]
    public async Task OnBeforeExecutionAsync_WithManyTypes_ShouldFailDueToSequentialAsyncPerformance()
    {
        // Arrange - Create code with many custom types to trigger sequential verification bottleneck
        var typeCount = 20;
        var typeNames = Enumerable.Range(0, typeCount).Select(i => $"CustomType{i}").ToList();
        var code = string.Join(" ", typeNames.Select(t => $"{t} {t.ToLower()} = new {t}();"));
        var parameters = CreateEditParameters(code, "test.cs");
        
        // Setup mock to simulate realistic verification delays
        var verificationDelayMs = 100; // Simulate realistic async operation delay
        foreach (var typeName in typeNames)
        {
            _mockVerificationStateManager
                .Setup(x => x.IsTypeVerifiedAsync(typeName))
                .Returns(async () =>
                {
                    await Task.Delay(verificationDelayMs); // Simulate network/disk I/O delay
                    return true;
                });
        }
        
        // Act - Measure execution time to prove sequential bottleneck
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await _middleware.OnBeforeExecutionAsync("Edit", parameters);
        stopwatch.Stop();
        
        // Assert - This SHOULD PASS with concurrent execution but WILL FAIL with sequential
        var actualTimeMs = stopwatch.ElapsedMilliseconds;
        var expectedSequentialTimeMs = typeCount * verificationDelayMs; // 20 * 100ms = 2000ms
        var expectedConcurrentTimeMs = verificationDelayMs + 50; // ~150ms with proper concurrency
        
        // BUG: Sequential foreach loops cause actual time to be close to sequential time
        Assert.That(actualTimeMs, Is.LessThan(expectedConcurrentTimeMs * 2), // Allow some tolerance
            $"CRITICAL PERFORMANCE BUG: TypeVerificationMiddleware took {actualTimeMs}ms " +
            $"but should take ~{expectedConcurrentTimeMs}ms with concurrent execution. " +
            $"Root cause: Lines 112-144 use sequential 'foreach (var typeRef in extractedTypes) await _verificationStateManager.IsTypeVerifiedAsync(...)' " +
            $"Expected: Task.WhenAll() or Parallel.ForEachAsync() for concurrent verification. " +
            $"Impact: {typeCount} types verified sequentially = {expectedSequentialTimeMs}ms instead of {expectedConcurrentTimeMs}ms");
            
        // Verify all types were actually checked (correctness preserved)
        foreach (var typeName in typeNames)
        {
            _mockVerificationStateManager.Verify(x => x.IsTypeVerifiedAsync(typeName), Times.Once);
        }
    }

    /// <summary>
    /// BUG REPRODUCTION: Tests member verification also suffers from sequential performance.
    /// Lines 130-142 also use sequential await in foreach for member verification.
    /// </summary>
    [Test]
    public async Task OnBeforeExecutionAsync_WithManyMemberAccess_ShouldFailDueToSequentialMemberVerification()
    {
        // Arrange - Create code with many member accesses to trigger member verification bottleneck
        var memberCount = 15;
        var memberAccesses = Enumerable.Range(0, memberCount).Select(i => $"User.Property{i}").ToList();
        var code = string.Join("; ", memberAccesses.Select(m => $"{m} = \"test\"")) + ";";
        var parameters = CreateEditParameters(code, "test.cs");
        
        // Setup User type as verified
        _mockVerificationStateManager
            .Setup(x => x.IsTypeVerifiedAsync("User"))
            .ReturnsAsync(true);
            
        // Setup member verification with delays
        var memberDelayMs = 50;
        for (int i = 0; i < memberCount; i++)
        {
            var memberName = $"Property{i}";
            _mockVerificationStateManager
                .Setup(x => x.HasVerifiedMemberAsync("User", memberName))
                .Returns(async () =>
                {
                    await Task.Delay(memberDelayMs); // Simulate member lookup delay
                    return true;
                });
        }
        
        // Act - Measure member verification performance
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await _middleware.OnBeforeExecutionAsync("Edit", parameters);
        stopwatch.Stop();
        
        // Assert - Member verification should be concurrent but isn't
        var actualTimeMs = stopwatch.ElapsedMilliseconds;
        var expectedSequentialTimeMs = memberCount * memberDelayMs; // 15 * 50ms = 750ms
        var expectedConcurrentTimeMs = memberDelayMs + 25; // ~75ms with concurrency
        
        // BUG: Lines 130-142 sequential member verification
        Assert.That(actualTimeMs, Is.LessThan(expectedConcurrentTimeMs * 2),
            $"CRITICAL PERFORMANCE BUG: Member verification took {actualTimeMs}ms " +
            $"but should take ~{expectedConcurrentTimeMs}ms with concurrent execution. " +
            $"Root cause: Lines 130-142 use sequential 'await _verificationStateManager.HasVerifiedMemberAsync()' in foreach loop. " +
            $"Expected: Concurrent member verification using Task.WhenAll(). " +
            $"Impact: Each member verification blocks the next causing O(n) delays!");
    }

    private static object CreateEditParameters(string code, string filePath)
    {
        return new
        {
            file_path = filePath,
            new_string = code
        };
    }
}