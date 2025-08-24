using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace COA.Mcp.Framework.Testing;

/// <summary>
/// Service for managing test status and execution in support of TDD enforcement.
/// </summary>
public interface ITestStatusService
{
    /// <summary>
    /// Gets the current test status for the given workspace.
    /// </summary>
    /// <param name="workspaceRoot">The root directory of the workspace.</param>
    /// <returns>The current test status.</returns>
    Task<TestStatus> GetTestStatusAsync(string workspaceRoot);

    /// <summary>
    /// Runs tests for the specified workspace and returns the results.
    /// </summary>
    /// <param name="workspaceRoot">The root directory of the workspace.</param>
    /// <param name="incremental">Whether to run only tests related to recent changes.</param>
    /// <returns>The test execution results.</returns>
    Task<TestExecutionResult> RunTestsAsync(string workspaceRoot, bool incremental = false);

    /// <summary>
    /// Checks if there are failing tests that could be used for TDD red phase.
    /// </summary>
    /// <param name="workspaceRoot">The root directory of the workspace.</param>
    /// <returns>True if there are failing tests available.</returns>
    Task<bool> HasFailingTestsAsync(string workspaceRoot);

    /// <summary>
    /// Gets the list of recently failing tests.
    /// </summary>
    /// <param name="workspaceRoot">The root directory of the workspace.</param>
    /// <returns>Collection of failing test information.</returns>
    Task<IEnumerable<FailingTest>> GetFailingTestsAsync(string workspaceRoot);

    /// <summary>
    /// Logs a TDD violation event for monitoring and metrics.
    /// </summary>
    /// <param name="toolName">The name of the tool that caused the violation.</param>
    /// <param name="filePath">The file path being modified.</param>
    /// <param name="violations">The specific TDD violations detected.</param>
    Task LogTddViolationAsync(string toolName, string filePath, IList<string> violations);

    /// <summary>
    /// Clears cached test results to force fresh test execution.
    /// </summary>
    /// <param name="workspaceRoot">The root directory of the workspace.</param>
    Task ClearTestCacheAsync(string workspaceRoot);

    /// <summary>
    /// Watches for file changes and automatically invalidates test cache when needed.
    /// </summary>
    /// <param name="workspaceRoot">The root directory of the workspace.</param>
    /// <param name="enable">Whether to enable or disable file watching.</param>
    Task SetFileWatchingAsync(string workspaceRoot, bool enable);

    /// <summary>
    /// Gets test execution history for analysis and reporting.
    /// </summary>
    /// <param name="workspaceRoot">The root directory of the workspace.</param>
    /// <param name="limit">Maximum number of historical entries to return.</param>
    /// <returns>Historical test execution data.</returns>
    Task<IEnumerable<TestExecutionHistory>> GetTestHistoryAsync(string workspaceRoot, int limit = 10);
}

/// <summary>
/// Represents the current status of tests in a workspace.
/// </summary>
public class TestStatus
{
    /// <summary>
    /// Gets or sets whether there are currently failing tests.
    /// </summary>
    public bool HasFailingTests { get; set; }

    /// <summary>
    /// Gets or sets the number of failing tests.
    /// </summary>
    public int FailingTestCount { get; set; }

    /// <summary>
    /// Gets or sets the number of passing tests.
    /// </summary>
    public int PassingTestCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of tests.
    /// </summary>
    public int TotalTestCount { get; set; }

    /// <summary>
    /// Gets or sets when tests were last run.
    /// </summary>
    public DateTime? LastTestRun { get; set; }

    /// <summary>
    /// Gets or sets the duration of the last test run.
    /// </summary>
    public TimeSpan? LastTestDuration { get; set; }

    /// <summary>
    /// Gets or sets recent test run results.
    /// </summary>
    public List<TestExecutionResult> RecentTestRuns { get; set; } = new();

    /// <summary>
    /// Gets or sets the current TDD phase based on test results.
    /// </summary>
    public TddPhase CurrentPhase { get; set; } = TddPhase.Red;

    /// <summary>
    /// Gets or sets whether tests are currently running.
    /// </summary>
    public bool TestsRunning { get; set; }

    /// <summary>
    /// Gets or sets any error messages from test execution.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets additional metadata about the test environment.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Represents the result of a test execution.
/// </summary>
public class TestExecutionResult
{
    /// <summary>
    /// Gets or sets whether the test execution was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when tests were executed.
    /// </summary>
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the duration of test execution.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the total number of tests run.
    /// </summary>
    public int TotalTests { get; set; }

    /// <summary>
    /// Gets or sets the number of passing tests.
    /// </summary>
    public int PassedTests { get; set; }

    /// <summary>
    /// Gets or sets the number of failing tests.
    /// </summary>
    public int FailedTests { get; set; }

    /// <summary>
    /// Gets or sets the number of skipped tests.
    /// </summary>
    public int SkippedTests { get; set; }

    /// <summary>
    /// Gets or sets detailed information about failing tests.
    /// </summary>
    public List<FailingTest> FailingTests { get; set; } = new();

    /// <summary>
    /// Gets or sets the raw output from the test runner.
    /// </summary>
    public string? RawOutput { get; set; }

    /// <summary>
    /// Gets or sets any error messages from test execution.
    /// </summary>
    public string? ErrorOutput { get; set; }

    /// <summary>
    /// Gets or sets the exit code from the test runner.
    /// </summary>
    public int ExitCode { get; set; }

    /// <summary>
    /// Gets or sets the command that was executed.
    /// </summary>
    public string? ExecutedCommand { get; set; }

    /// <summary>
    /// Gets or sets the working directory where tests were run.
    /// </summary>
    public string? WorkingDirectory { get; set; }
}

/// <summary>
/// Represents a failing test with detailed information.
/// </summary>
public class FailingTest
{
    /// <summary>
    /// Gets or sets the name of the failing test.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the full name including namespace/class.
    /// </summary>
    public string FullName { get; set; } = "";

    /// <summary>
    /// Gets or sets the test class or fixture name.
    /// </summary>
    public string? ClassName { get; set; }

    /// <summary>
    /// Gets or sets the failure message.
    /// </summary>
    public string? FailureMessage { get; set; }

    /// <summary>
    /// Gets or sets the stack trace of the failure.
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// Gets or sets the file path where the test is defined.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Gets or sets the line number where the test failure occurred.
    /// </summary>
    public int? LineNumber { get; set; }

    /// <summary>
    /// Gets or sets the duration of the test execution.
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// Gets or sets when this test started failing.
    /// </summary>
    public DateTime? FirstFailedAt { get; set; }

    /// <summary>
    /// Gets or sets how many times this test has failed consecutively.
    /// </summary>
    public int ConsecutiveFailures { get; set; } = 1;

    /// <summary>
    /// Gets or sets additional metadata about the test.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Represents historical test execution data.
/// </summary>
public class TestExecutionHistory
{
    /// <summary>
    /// Gets or sets the timestamp of the test execution.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the test execution result.
    /// </summary>
    public TestExecutionResult Result { get; set; } = new();

    /// <summary>
    /// Gets or sets the trigger that caused the test execution.
    /// </summary>
    public string? Trigger { get; set; }

    /// <summary>
    /// Gets or sets the files that were changed before this test run.
    /// </summary>
    public List<string> ChangedFiles { get; set; } = new();

    /// <summary>
    /// Gets or sets the commit hash or version if available.
    /// </summary>
    public string? Version { get; set; }
}

/// <summary>
/// Enumeration of TDD workflow phases.
/// </summary>
public enum TddPhase
{
    /// <summary>
    /// Red phase: Writing failing tests.
    /// </summary>
    Red = 1,

    /// <summary>
    /// Green phase: Writing minimal code to pass tests.
    /// </summary>
    Green = 2,

    /// <summary>
    /// Refactor phase: Improving code while keeping tests green.
    /// </summary>
    Refactor = 3,

    /// <summary>
    /// Unknown phase: Cannot determine current state.
    /// </summary>
    Unknown = 0
}