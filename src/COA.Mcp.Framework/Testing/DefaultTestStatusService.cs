using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using COA.Mcp.Framework.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace COA.Mcp.Framework.Testing;

/// <summary>
/// Default implementation of ITestStatusService that manages test execution and status.
/// </summary>
public class DefaultTestStatusService : ITestStatusService
{
    private readonly ILogger<DefaultTestStatusService> _logger;
    private readonly TddEnforcementOptions _options;
    private readonly Dictionary<string, TestStatus> _cachedStatus = new();
    private readonly Dictionary<string, DateTime> _lastStatusCheck = new();

    /// <summary>
    /// Initializes a new instance of the DefaultTestStatusService class.
    /// </summary>
    public DefaultTestStatusService(
        ILogger<DefaultTestStatusService> logger,
        IOptions<TddEnforcementOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public async Task<TestStatus> GetTestStatusAsync(string workspaceRoot)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            return new TestStatus { ErrorMessage = "Invalid workspace root" };
        }

        var cacheKey = workspaceRoot.ToLowerInvariant();
        
        // Check if we have cached status that's still valid
        if (_cachedStatus.TryGetValue(cacheKey, out var cachedStatus) &&
            _lastStatusCheck.TryGetValue(cacheKey, out var lastCheck) &&
            DateTime.UtcNow - lastCheck < TimeSpan.FromMinutes(_options.TestStatusCheckIntervalMinutes))
        {
            _logger.LogDebug("Returning cached test status for workspace: {WorkspaceRoot}", workspaceRoot);
            return cachedStatus;
        }

        _logger.LogDebug("Refreshing test status for workspace: {WorkspaceRoot}", workspaceRoot);

        try
        {
            var testRunner = GetTestRunnerForWorkspace(workspaceRoot);
            if (testRunner == null)
            {
                var status = new TestStatus
                {
                    ErrorMessage = "No suitable test runner found for workspace",
                    LastTestRun = null,
                    HasFailingTests = false
                };

                _cachedStatus[cacheKey] = status;
                _lastStatusCheck[cacheKey] = DateTime.UtcNow;
                return status;
            }

            // Run tests to get current status
            var testResult = await RunTestsInternalAsync(workspaceRoot, testRunner);
            
            var newStatus = new TestStatus
            {
                HasFailingTests = testResult.FailedTests > 0,
                FailingTestCount = testResult.FailedTests,
                PassingTestCount = testResult.PassedTests,
                TotalTestCount = testResult.TotalTests,
                LastTestRun = testResult.ExecutedAt,
                LastTestDuration = testResult.Duration,
                CurrentPhase = DetermineCurrentPhase(testResult),
                RecentTestRuns = new List<TestExecutionResult> { testResult }
            };

            if (!testResult.Success)
            {
                newStatus.ErrorMessage = testResult.ErrorOutput;
            }

            _cachedStatus[cacheKey] = newStatus;
            _lastStatusCheck[cacheKey] = DateTime.UtcNow;

            return newStatus;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting test status for workspace: {WorkspaceRoot}", workspaceRoot);
            
            var errorStatus = new TestStatus
            {
                ErrorMessage = $"Failed to get test status: {ex.Message}",
                HasFailingTests = false
            };

            return errorStatus;
        }
    }

    /// <inheritdoc/>
    public async Task<TestExecutionResult> RunTestsAsync(string workspaceRoot, bool incremental = false)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            return new TestExecutionResult
            {
                Success = false,
                ErrorOutput = "Invalid workspace root"
            };
        }

        var testRunner = GetTestRunnerForWorkspace(workspaceRoot);
        if (testRunner == null)
        {
            return new TestExecutionResult
            {
                Success = false,
                ErrorOutput = "No suitable test runner found"
            };
        }

        return await RunTestsInternalAsync(workspaceRoot, testRunner, incremental);
    }

    /// <inheritdoc/>
    public async Task<bool> HasFailingTestsAsync(string workspaceRoot)
    {
        var status = await GetTestStatusAsync(workspaceRoot);
        return status.HasFailingTests;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<FailingTest>> GetFailingTestsAsync(string workspaceRoot)
    {
        var status = await GetTestStatusAsync(workspaceRoot);
        return status.RecentTestRuns.SelectMany(run => run.FailingTests);
    }

    /// <inheritdoc/>
    public async Task LogTddViolationAsync(string toolName, string filePath, IList<string> violations)
    {
        _logger.LogWarning("TDD Violation: Tool={ToolName}, File={FilePath}, Violations={Violations}",
            toolName, filePath, string.Join("; ", violations));

        // Could implement more sophisticated logging/metrics here
        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task ClearTestCacheAsync(string workspaceRoot)
    {
        var cacheKey = workspaceRoot.ToLowerInvariant();
        _cachedStatus.Remove(cacheKey);
        _lastStatusCheck.Remove(cacheKey);

        _logger.LogDebug("Cleared test cache for workspace: {WorkspaceRoot}", workspaceRoot);
        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task SetFileWatchingAsync(string workspaceRoot, bool enable)
    {
        // File watching implementation would go here
        // For now, just log the intent
        _logger.LogDebug("File watching {Action} for workspace: {WorkspaceRoot}", 
            enable ? "enabled" : "disabled", workspaceRoot);

        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<TestExecutionHistory>> GetTestHistoryAsync(string workspaceRoot, int limit = 10)
    {
        // This would typically read from persistent storage
        // For now, return empty history
        await Task.CompletedTask;
        return Enumerable.Empty<TestExecutionHistory>();
    }

    /// <summary>
    /// Determines the appropriate test runner for the given workspace.
    /// </summary>
    private TestRunnerConfig? GetTestRunnerForWorkspace(string workspaceRoot)
    {
        try
        {
            // Check for .NET projects
            if (Directory.GetFiles(workspaceRoot, "*.sln", SearchOption.TopDirectoryOnly).Any() ||
                Directory.GetFiles(workspaceRoot, "*.csproj", SearchOption.AllDirectories).Any())
            {
                return _options.TestRunners.TryGetValue("csharp", out var csharpRunner) 
                    ? csharpRunner 
                    : new TestRunnerConfig { Command = "dotnet test" };
            }

            // Check for Node.js projects
            if (File.Exists(Path.Combine(workspaceRoot, "package.json")))
            {
                return _options.TestRunners.TryGetValue("typescript", out var tsRunner) 
                    ? tsRunner 
                    : new TestRunnerConfig { Command = "npm test" };
            }

            // Check for Python projects
            if (File.Exists(Path.Combine(workspaceRoot, "pytest.ini")) ||
                File.Exists(Path.Combine(workspaceRoot, "pyproject.toml")) ||
                Directory.GetFiles(workspaceRoot, "test_*.py", SearchOption.AllDirectories).Any())
            {
                return _options.TestRunners.TryGetValue("python", out var pythonRunner)
                    ? pythonRunner
                    : new TestRunnerConfig { Command = "pytest" };
            }

            _logger.LogWarning("No recognized test framework found in workspace: {WorkspaceRoot}", workspaceRoot);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining test runner for workspace: {WorkspaceRoot}", workspaceRoot);
            return null;
        }
    }

    /// <summary>
    /// Runs tests using the specified test runner configuration.
    /// </summary>
    private async Task<TestExecutionResult> RunTestsInternalAsync(
        string workspaceRoot, 
        TestRunnerConfig testRunner, 
        bool incremental = false)
    {
        var result = new TestExecutionResult
        {
            ExecutedAt = DateTime.UtcNow,
            WorkingDirectory = workspaceRoot
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var workingDir = Path.IsPathRooted(testRunner.WorkingDirectory) 
                ? testRunner.WorkingDirectory 
                : Path.Combine(workspaceRoot, testRunner.WorkingDirectory);

            var command = incremental && testRunner.SupportsIncrementalTesting
                ? $"{testRunner.Command} {string.Join(" ", testRunner.IncrementalTestArguments)}"
                : testRunner.BuildCommandLine();

            result.ExecutedCommand = command;

            _logger.LogDebug("Running tests: {Command} in {WorkingDirectory}", command, workingDir);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = GetShellCommand(),
                    Arguments = GetShellArgs(command),
                    WorkingDirectory = workingDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            // Add environment variables
            foreach (var envVar in testRunner.EnvironmentVariables)
            {
                process.StartInfo.EnvironmentVariables[envVar.Key] = envVar.Value;
            }

            var outputLines = new List<string>();
            var errorLines = new List<string>();

            process.OutputDataReceived += (sender, e) => 
            {
                if (e.Data != null) outputLines.Add(e.Data);
            };

            process.ErrorDataReceived += (sender, e) => 
            {
                if (e.Data != null) errorLines.Add(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var completed = await WaitForProcessAsync(process, testRunner.TimeoutMs);
            stopwatch.Stop();

            result.Duration = stopwatch.Elapsed;
            result.RawOutput = string.Join("\n", outputLines);
            result.ErrorOutput = string.Join("\n", errorLines);
            result.ExitCode = completed ? process.ExitCode : -1;
            result.Success = completed && process.ExitCode == testRunner.ExpectedSuccessExitCode;

            // Parse test results from output
            ParseTestResults(result, testRunner);

            _logger.LogDebug("Test execution completed: Success={Success}, Duration={Duration}ms, " +
                            "Total={Total}, Passed={Passed}, Failed={Failed}",
                result.Success, result.Duration.TotalMilliseconds,
                result.TotalTests, result.PassedTests, result.FailedTests);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            result.Success = false;
            result.ErrorOutput = $"Test execution failed: {ex.Message}";

            _logger.LogError(ex, "Error running tests in workspace: {WorkspaceRoot}", workspaceRoot);
            return result;
        }
    }

    /// <summary>
    /// Waits for a process to complete with timeout.
    /// </summary>
    private static async Task<bool> WaitForProcessAsync(Process process, int timeoutMs)
    {
        var tcs = new TaskCompletionSource<bool>();
        
        process.Exited += (sender, e) => tcs.TrySetResult(true);
        process.EnableRaisingEvents = true;

        if (process.HasExited)
        {
            return true;
        }

        var timeoutTask = Task.Delay(timeoutMs);
        var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

        if (completedTask == timeoutTask)
        {
            try
            {
                process.Kill();
            }
            catch
            {
                // Ignore errors when killing process
            }
            return false;
        }

        return true;
    }

    /// <summary>
    /// Parses test results from process output.
    /// </summary>
    private static void ParseTestResults(TestExecutionResult result, TestRunnerConfig config)
    {
        if (string.IsNullOrEmpty(result.RawOutput))
            return;

        var output = result.RawOutput;

        // Parse failing tests
        foreach (var pattern in config.FailingTestPatterns)
        {
            var regex = new System.Text.RegularExpressions.Regex(pattern);
            var matches = regex.Matches(output);
            
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                // Extract test counts from patterns like "5 failed"
                if (int.TryParse(match.Groups[1].Value, out var count))
                {
                    result.FailedTests = Math.Max(result.FailedTests, count);
                }
            }
        }

        // Parse passing tests
        foreach (var pattern in config.PassingTestPatterns)
        {
            var regex = new System.Text.RegularExpressions.Regex(pattern);
            var matches = regex.Matches(output);
            
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (int.TryParse(match.Groups[1].Value, out var count))
                {
                    result.PassedTests = Math.Max(result.PassedTests, count);
                }
            }
        }

        result.TotalTests = result.PassedTests + result.FailedTests + result.SkippedTests;

        // If we couldn't parse specific counts, use simple heuristics
        if (result.TotalTests == 0)
        {
            if (output.Contains("FAILED") || output.Contains("Failed"))
            {
                result.FailedTests = 1; // At least one failure
            }
            if (output.Contains("PASSED") || output.Contains("OK"))
            {
                result.PassedTests = 1; // At least one success
            }
            result.TotalTests = result.PassedTests + result.FailedTests;
        }
    }

    /// <summary>
    /// Determines the current TDD phase based on test results.
    /// </summary>
    private static TddPhase DetermineCurrentPhase(TestExecutionResult result)
    {
        if (result.FailedTests > 0 && result.PassedTests == 0)
            return TddPhase.Red;
        
        if (result.PassedTests > 0 && result.FailedTests == 0)
            return TddPhase.Green;
        
        if (result.PassedTests > 0 && result.FailedTests == 0)
            return TddPhase.Refactor;
        
        return TddPhase.Unknown;
    }

    /// <summary>
    /// Gets the shell command for the current platform.
    /// </summary>
    private static string GetShellCommand()
    {
        return Environment.OSVersion.Platform == PlatformID.Win32NT ? "cmd.exe" : "/bin/bash";
    }

    /// <summary>
    /// Gets the shell arguments for the current platform.
    /// </summary>
    private static string GetShellArgs(string command)
    {
        return Environment.OSVersion.Platform == PlatformID.Win32NT 
            ? $"/c \"{command}\""
            : $"-c \"{command}\"";
    }
}