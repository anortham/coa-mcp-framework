using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using COA.Mcp.Framework.Configuration;
using COA.Mcp.Framework.Exceptions;
using COA.Mcp.Framework.Pipeline.Middleware;
using COA.Mcp.Framework.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace COA.Mcp.Framework.Tests.Pipeline.Middleware;

[TestFixture]
public class TddEnforcementMiddlewareTests
{
    private Mock<ITestStatusService> _mockTestStatusService;
    private Mock<ILogger<TddEnforcementMiddleware>> _mockLogger;
    private TddEnforcementOptions _options;
    private TddEnforcementMiddleware _middleware;

    [SetUp]
    public void Setup()
    {
        _mockTestStatusService = new Mock<ITestStatusService>();
        _mockLogger = new Mock<ILogger<TddEnforcementMiddleware>>();
        _options = new TddEnforcementOptions
        {
            Enabled = true,
            Mode = TddEnforcementMode.Strict,
            RequireFailingTest = true,
            AllowRefactoring = true
        };

        var optionsWrapper = Options.Create(_options);
        _middleware = new TddEnforcementMiddleware(
            _mockTestStatusService.Object,
            _mockLogger.Object,
            optionsWrapper);
    }

    [Test]
    public async Task OnBeforeExecutionAsync_WithDisabledMiddleware_ShouldNotPerformChecks()
    {
        // Arrange
        _options.Enabled = false;
        var middleware = new TddEnforcementMiddleware(
            _mockTestStatusService.Object,
            _mockLogger.Object,
            Options.Create(_options));

        var parameters = CreateEditParameters("public class NewClass { }", "src/NewClass.cs");

        // Act
        await middleware.OnBeforeExecutionAsync("Edit", parameters);

        // Assert
        _mockTestStatusService.Verify(x => x.GetTestStatusAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task OnBeforeExecutionAsync_WithNonCodeGenerationTool_ShouldNotPerformChecks()
    {
        // Arrange
        var parameters = new { someParameter = "value" };

        // Act
        await _middleware.OnBeforeExecutionAsync("Read", parameters);

        // Assert
        _mockTestStatusService.Verify(x => x.GetTestStatusAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task OnBeforeExecutionAsync_WithTestFile_ShouldSkipTddCheck()
    {
        // Arrange
        var parameters = CreateEditParameters("public class UserTest { }", "tests/UserTest.cs");

        // Act
        await _middleware.OnBeforeExecutionAsync("Edit", parameters);

        // Assert
        _mockTestStatusService.Verify(x => x.GetTestStatusAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task OnBeforeExecutionAsync_WithNewPublicClass_ShouldEnforceTdd()
    {
        // Arrange
        var code = "public class UserService { public void SaveUser(User user) { } }";
        var parameters = CreateEditParameters(code, "src/UserService.cs");

        var testStatus = new TestStatus
        {
            HasFailingTests = false,
            LastTestRun = DateTime.UtcNow.AddMinutes(-5)
        };

        _mockTestStatusService
            .Setup(x => x.GetTestStatusAsync(It.IsAny<string>()))
            .ReturnsAsync(testStatus);

        // Act & Assert
        try
        {
            await _middleware.OnBeforeExecutionAsync("Edit", parameters);
            Assert.Fail("Expected TddViolationException to be thrown");
        }
        catch (McpException ex)
        {
            Assert.That(ex.Message, Does.Contain("TDD VIOLATION"));
            Assert.That(ex.Message, Does.Contain("No test files found"));
        }
    }

    [Test]
    public async Task OnBeforeExecutionAsync_WithFailingTests_ShouldAllowImplementation()
    {
        // Arrange
        var code = "public class UserService { public void SaveUser(User user) { } }";
        var parameters = CreateEditParameters(code, "src/UserService.cs");

        var testStatus = new TestStatus
        {
            HasFailingTests = true,
            FailingTestCount = 2,
            LastTestRun = DateTime.UtcNow.AddMinutes(-5)
        };

        _mockTestStatusService
            .Setup(x => x.GetTestStatusAsync(It.IsAny<string>()))
            .ReturnsAsync(testStatus);

        // Act - Should not throw
        await _middleware.OnBeforeExecutionAsync("Edit", parameters);

        // Assert
        _mockTestStatusService.Verify(x => x.GetTestStatusAsync(It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task OnBeforeExecutionAsync_InWarningMode_ShouldLogWarningInsteadOfThrowing()
    {
        // Arrange
        _options.Mode = TddEnforcementMode.Warning;
        var middleware = new TddEnforcementMiddleware(
            _mockTestStatusService.Object,
            _mockLogger.Object,
            Options.Create(_options));

        var code = "public class UserService { }";
        var parameters = CreateEditParameters(code, "src/UserService.cs");

        var testStatus = new TestStatus
        {
            HasFailingTests = false,
            LastTestRun = DateTime.UtcNow.AddMinutes(-5)
        };

        _mockTestStatusService
            .Setup(x => x.GetTestStatusAsync(It.IsAny<string>()))
            .ReturnsAsync(testStatus);

        // Act - Should not throw
        await middleware.OnBeforeExecutionAsync("Edit", parameters);

        // Assert - Should log warning
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
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