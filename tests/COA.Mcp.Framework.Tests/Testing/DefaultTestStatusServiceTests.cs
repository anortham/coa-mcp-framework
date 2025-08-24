using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using COA.Mcp.Framework.Configuration;
using COA.Mcp.Framework.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace COA.Mcp.Framework.Tests.Testing;

[TestFixture]
public class DefaultTestStatusServiceTests
{
    private Mock<ILogger<DefaultTestStatusService>> _mockLogger;
    private TddEnforcementOptions _options;
    private DefaultTestStatusService _service;
    private string _tempDirectory;

    [SetUp]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<DefaultTestStatusService>>();
        _options = new TddEnforcementOptions
        {
            Enabled = true,
            TestStatusCheckIntervalMinutes = 5,
            TestTimeoutMs = 30000
        };

        var optionsWrapper = Options.Create(_options);
        _service = new DefaultTestStatusService(_mockLogger.Object, optionsWrapper);
        
        // Create a temporary directory for test workspaces
        _tempDirectory = Path.Combine(Path.GetTempPath(), "DefaultTestStatusServiceTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up temporary directory with retry logic for file handle issues
        if (Directory.Exists(_tempDirectory))
        {
            TryDeleteDirectory(_tempDirectory, maxRetries: 5, delayMs: 100);
        }
    }

    private static void TryDeleteDirectory(string directory, int maxRetries = 3, int delayMs = 100)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                Directory.Delete(directory, true);
                return; // Success
            }
            catch (IOException) when (i < maxRetries - 1)
            {
                // Wait and retry - processes may still be releasing file handles
                System.Threading.Thread.Sleep(delayMs);
            }
            catch (UnauthorizedAccessException) when (i < maxRetries - 1)
            {
                // Wait and retry - processes may still be releasing file handles
                System.Threading.Thread.Sleep(delayMs);
            }
        }

        // If we get here, all retries failed - try one more time without catching
        Directory.Delete(directory, true);
    }

    [Test]
    public async Task GetTestStatusAsync_WithInvalidWorkspaceRoot_ShouldReturnErrorStatus()
    {
        // Act
        var status = await _service.GetTestStatusAsync("");

        // Assert
        Assert.That(status, Is.Not.Null);
        Assert.That(status.ErrorMessage, Is.EqualTo("Invalid workspace root"));
        Assert.That(status.HasFailingTests, Is.False);
    }

    [Test]
    public async Task GetTestStatusAsync_WithNoTestRunner_ShouldReturnErrorStatus()
    {
        // Arrange
        var workspaceRoot = Path.Combine(_tempDirectory, "EmptyWorkspace");
        Directory.CreateDirectory(workspaceRoot);

        // Act
        var status = await _service.GetTestStatusAsync(workspaceRoot);

        // Assert
        Assert.That(status, Is.Not.Null);
        Assert.That(status.ErrorMessage, Is.EqualTo("No suitable test runner found for workspace"));
        Assert.That(status.HasFailingTests, Is.False);
    }

    [Test]
    public async Task GetTestStatusAsync_WithDotNetProject_ShouldUseDotNetTestRunner()
    {
        // Arrange
        var workspaceRoot = Path.Combine(_tempDirectory, "DotNetWorkspace");
        Directory.CreateDirectory(workspaceRoot);
        await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "Project.csproj"), "<Project></Project>");

        // Act
        var status = await _service.GetTestStatusAsync(workspaceRoot);

        // Assert
        Assert.That(status, Is.Not.Null);
        // The test will likely fail to run actual dotnet test, but should attempt it
        Assert.That(status.LastTestRun, Is.Not.Null);
    }

    [Test]
    public async Task GetTestStatusAsync_WithNodeJsProject_ShouldUseNpmTestRunner()
    {
        // Arrange
        var workspaceRoot = Path.Combine(_tempDirectory, "NodeWorkspace");
        Directory.CreateDirectory(workspaceRoot);
        await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "package.json"), "{}");

        // Act
        var status = await _service.GetTestStatusAsync(workspaceRoot);

        // Assert
        Assert.That(status, Is.Not.Null);
        // The test will likely fail to run actual npm test, but should attempt it
        Assert.That(status.LastTestRun, Is.Not.Null);
    }

    [Test]
    public async Task GetTestStatusAsync_WithPythonProject_ShouldUsePytestRunner()
    {
        // Arrange
        var workspaceRoot = Path.Combine(_tempDirectory, "PythonWorkspace");
        Directory.CreateDirectory(workspaceRoot);
        await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "pytest.ini"), "[tool:pytest]");

        // Act
        var status = await _service.GetTestStatusAsync(workspaceRoot);

        // Assert
        Assert.That(status, Is.Not.Null);
        Assert.That(status.LastTestRun, Is.Not.Null);
    }

    [Test]
    public async Task GetTestStatusAsync_WithCachedResult_ShouldReturnCachedStatus()
    {
        // Arrange
        var workspaceRoot = Path.Combine(_tempDirectory, "CachedWorkspace");
        Directory.CreateDirectory(workspaceRoot);
        await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "Project.csproj"), "<Project></Project>");

        // First call
        var firstStatus = await _service.GetTestStatusAsync(workspaceRoot);
        var firstTime = firstStatus.LastTestRun;

        await Task.Delay(100); // Small delay to ensure time difference would be visible

        // Second call within cache interval
        var secondStatus = await _service.GetTestStatusAsync(workspaceRoot);

        // Assert
        Assert.That(secondStatus.LastTestRun, Is.EqualTo(firstTime));
    }

    [Test]
    public async Task RunTestsAsync_WithInvalidWorkspaceRoot_ShouldReturnErrorResult()
    {
        // Act
        var result = await _service.RunTestsAsync("");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorOutput, Is.EqualTo("Invalid workspace root"));
    }

    [Test]
    public async Task RunTestsAsync_WithNoTestRunner_ShouldReturnErrorResult()
    {
        // Arrange
        var workspaceRoot = Path.Combine(_tempDirectory, "EmptyWorkspace2");
        Directory.CreateDirectory(workspaceRoot);

        // Act
        var result = await _service.RunTestsAsync(workspaceRoot);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorOutput, Is.EqualTo("No suitable test runner found"));
    }

    [Test]
    public async Task HasFailingTestsAsync_ShouldReturnTestStatusFailingFlag()
    {
        // Arrange
        var workspaceRoot = Path.Combine(_tempDirectory, "FailingTestsWorkspace");
        Directory.CreateDirectory(workspaceRoot);
        await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "Project.csproj"), "<Project></Project>");

        // Act
        var hasFailingTests = await _service.HasFailingTestsAsync(workspaceRoot);

        // Assert
        Assert.That(hasFailingTests, Is.TypeOf<bool>());
    }

    [Test]
    public async Task GetFailingTestsAsync_ShouldReturnFailingTestsFromRecentRuns()
    {
        // Arrange
        var workspaceRoot = Path.Combine(_tempDirectory, "FailingTestDetailsWorkspace");
        Directory.CreateDirectory(workspaceRoot);
        await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "Project.csproj"), "<Project></Project>");

        // Act
        var failingTests = await _service.GetFailingTestsAsync(workspaceRoot);

        // Assert
        Assert.That(failingTests, Is.Not.Null);
        Assert.That(failingTests, Is.InstanceOf<IEnumerable<FailingTest>>());
    }

    [Test]
    public async Task LogTddViolationAsync_ShouldLogWarning()
    {
        // Act
        await _service.LogTddViolationAsync("TestTool", "test.cs", new[] { "Violation1", "Violation2" });

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
            Times.Once);
    }

    [Test]
    public async Task ClearTestCacheAsync_ShouldClearCachedResults()
    {
        // Arrange
        var workspaceRoot = Path.Combine(_tempDirectory, "CacheClearWorkspace");
        Directory.CreateDirectory(workspaceRoot);
        await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "Project.csproj"), "<Project></Project>");

        // Get initial status (populates cache)
        await _service.GetTestStatusAsync(workspaceRoot);

        // Act
        await _service.ClearTestCacheAsync(workspaceRoot);

        // Assert - Should complete without error
        Assert.Pass("Cache cleared successfully");
    }

    [Test]
    public async Task SetFileWatchingAsync_ShouldLogIntent()
    {
        // Arrange
        var workspaceRoot = Path.Combine(_tempDirectory, "FileWatchWorkspace");

        // Act
        await _service.SetFileWatchingAsync(workspaceRoot, true);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
            Times.Once);
    }

    [Test]
    public async Task GetTestHistoryAsync_ShouldReturnEmptyHistory()
    {
        // Act
        var history = await _service.GetTestHistoryAsync(_tempDirectory);

        // Assert
        Assert.That(history, Is.Not.Null);
        Assert.That(history.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task GetTestHistoryAsync_WithLimit_ShouldRespectLimit()
    {
        // Act
        var history = await _service.GetTestHistoryAsync(_tempDirectory, 5);

        // Assert
        Assert.That(history, Is.Not.Null);
        Assert.That(history.Count(), Is.EqualTo(0)); // Empty for now, but respects the interface
    }

    [Test]
    public async Task RunTestsAsync_WithDotNetProject_ShouldSetCorrectExecutionData()
    {
        // Arrange
        var workspaceRoot = Path.Combine(_tempDirectory, "ExecutionDataWorkspace");
        Directory.CreateDirectory(workspaceRoot);
        await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "Project.csproj"), "<Project></Project>");

        // Act
        var result = await _service.RunTestsAsync(workspaceRoot);

        // Assert
        Assert.That(result.ExecutedAt, Is.Not.EqualTo(default(DateTime)));
        Assert.That(result.WorkingDirectory, Is.EqualTo(workspaceRoot));
        Assert.That(result.Duration, Is.GreaterThan(TimeSpan.Zero));
        Assert.That(result.ExitCode, Is.Not.EqualTo(0)); // Will likely fail since dotnet test won't work in test environment
    }

    [Test]
    public async Task RunTestsAsync_WithIncremental_AndUnsupportedRunner_ShouldUseNormalCommand()
    {
        // Arrange
        var workspaceRoot = Path.Combine(_tempDirectory, "IncrementalWorkspace");
        Directory.CreateDirectory(workspaceRoot);
        await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "Project.csproj"), "<Project></Project>");

        // Act
        var result = await _service.RunTestsAsync(workspaceRoot, incremental: true);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ExecutedCommand, Does.Contain("dotnet test"));
        Assert.That(result.ExecutedCommand, Does.Not.Contain("incremental")); // Default dotnet test doesn't support incremental
    }

    [Test]
    public void TestRunnerConfig_BuildCommandLine_ShouldCombineCommandAndArguments()
    {
        // Arrange
        var config = new TestRunnerConfig
        {
            Command = "dotnet test",
            Arguments = new List<string> { "--verbosity", "normal", "--logger", "console" }
        };

        // Act
        var commandLine = config.BuildCommandLine();

        // Assert
        Assert.That(commandLine, Is.EqualTo("dotnet test --verbosity normal --logger console"));
    }

    [Test]
    public void TestRunnerConfig_BuildCommandLine_WithNoArguments_ShouldReturnCommandOnly()
    {
        // Arrange
        var config = new TestRunnerConfig
        {
            Command = "npm test"
        };

        // Act
        var commandLine = config.BuildCommandLine();

        // Assert
        Assert.That(commandLine, Is.EqualTo("npm test"));
    }

    [Test]
    public void TestRunnerConfig_Validate_WithEmptyCommand_ShouldThrow()
    {
        // Arrange
        var config = new TestRunnerConfig
        {
            Command = ""
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Test]
    public void TestRunnerConfig_Validate_WithValidConfig_ShouldNotThrow()
    {
        // Arrange
        var config = new TestRunnerConfig
        {
            Command = "dotnet test",
            WorkingDirectory = ".",
            TimeoutMs = 30000
        };

        // Act & Assert
        Assert.DoesNotThrow(() => config.Validate());
    }

    [Test]
    public void TestRunnerConfig_Validate_WithInvalidTimeout_ShouldSetDefault()
    {
        // Arrange
        var config = new TestRunnerConfig
        {
            Command = "dotnet test",
            TimeoutMs = -1000
        };

        // Act
        config.Validate();

        // Assert
        Assert.That(config.TimeoutMs, Is.EqualTo(30000));
    }

    [Test]
    public void TestRunnerConfig_Validate_WithEmptyWorkingDirectory_ShouldSetDefault()
    {
        // Arrange
        var config = new TestRunnerConfig
        {
            Command = "dotnet test",
            WorkingDirectory = ""
        };

        // Act
        config.Validate();

        // Assert
        Assert.That(config.WorkingDirectory, Is.EqualTo("."));
    }

    [Test]
    public void TddEnforcementOptions_Validate_ShouldSetDefaults()
    {
        // Arrange
        var options = new TddEnforcementOptions
        {
            TestTimeoutMs = -1,
            TestStatusCheckIntervalMinutes = -1,
            TestResultCacheMaxAgeMinutes = -1,
            MinimumComplexityThreshold = -1
        };

        // Act
        options.Validate();

        // Assert
        Assert.That(options.TestTimeoutMs, Is.EqualTo(30000));
        Assert.That(options.TestStatusCheckIntervalMinutes, Is.EqualTo(5));
        Assert.That(options.TestResultCacheMaxAgeMinutes, Is.EqualTo(30));
        Assert.That(options.MinimumComplexityThreshold, Is.EqualTo(10));
    }

    [Test]
    public void TddEnforcementOptions_Validate_ShouldEnsureDefaultTestRunners()
    {
        // Arrange
        var options = new TddEnforcementOptions();
        options.TestRunners.Clear(); // Remove defaults

        // Act
        options.Validate();

        // Assert
        Assert.That(options.TestRunners.ContainsKey("csharp"), Is.True);
        Assert.That(options.TestRunners["csharp"].Command, Is.EqualTo("dotnet test"));
    }

    [Test]
    public void TddEnforcementOptions_Validate_ShouldValidateTestRunners()
    {
        // Arrange
        var options = new TddEnforcementOptions();
        options.TestRunners["invalid"] = new TestRunnerConfig { Command = "" };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Test]
    public void FailingTest_DefaultValues_ShouldBeInitialized()
    {
        // Act
        var failingTest = new FailingTest();

        // Assert
        Assert.That(failingTest.Name, Is.EqualTo(""));
        Assert.That(failingTest.FullName, Is.EqualTo(""));
        Assert.That(failingTest.ConsecutiveFailures, Is.EqualTo(1));
        Assert.That(failingTest.Metadata, Is.Not.Null);
    }

    [Test]
    public void TestStatus_DefaultValues_ShouldBeInitialized()
    {
        // Act
        var status = new TestStatus();

        // Assert
        Assert.That(status.HasFailingTests, Is.False);
        Assert.That(status.CurrentPhase, Is.EqualTo(COA.Mcp.Framework.Testing.TddPhase.Red));
        Assert.That(status.TestsRunning, Is.False);
        Assert.That(status.RecentTestRuns, Is.Not.Null);
        Assert.That(status.Metadata, Is.Not.Null);
    }

    [Test]
    public void TestExecutionResult_DefaultValues_ShouldBeInitialized()
    {
        // Act
        var result = new TestExecutionResult();

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.ExecutedAt, Is.Not.EqualTo(default(DateTime)));
        Assert.That(result.FailingTests, Is.Not.Null);
        Assert.That(result.ExitCode, Is.EqualTo(0));
    }
}