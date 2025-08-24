using System;
using System.Collections.Generic;
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

    private static object CreateEditParameters(string code, string filePath)
    {
        return new
        {
            file_path = filePath,
            new_string = code
        };
    }
}