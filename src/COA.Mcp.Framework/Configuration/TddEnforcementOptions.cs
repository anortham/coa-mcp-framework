using System;
using System.Collections.Generic;

namespace COA.Mcp.Framework.Configuration;

/// <summary>
/// Configuration options for Test-Driven Development (TDD) enforcement middleware.
/// </summary>
public class TddEnforcementOptions
{
    /// <summary>
    /// Gets or sets whether TDD enforcement is enabled.
    /// Default: false (optional feature)
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the TDD enforcement mode.
    /// Default: Warning (allow but warn)
    /// </summary>
    public TddEnforcementMode Mode { get; set; } = TddEnforcementMode.Warning;

    /// <summary>
    /// Gets or sets whether to require failing tests before allowing implementation.
    /// This enforces the "Red" phase of Red-Green-Refactor.
    /// Default: true
    /// </summary>
    public bool RequireFailingTest { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to allow refactoring operations without requiring tests.
    /// Default: true (refactoring is allowed as part of the cycle)
    /// </summary>
    public bool AllowRefactoring { get; set; } = true;

    /// <summary>
    /// Gets or sets the test runners configuration by language or project type.
    /// Key: language/project identifier (e.g., "csharp", "typescript", "javascript")
    /// Value: test runner configuration
    /// </summary>
    public Dictionary<string, TestRunnerConfig> TestRunners { get; set; } = new()
    {
        ["csharp"] = new TestRunnerConfig 
        { 
            Command = "dotnet test", 
            TimeoutMs = 30000,
            WorkingDirectory = "."
        },
        ["typescript"] = new TestRunnerConfig 
        { 
            Command = "npm test", 
            TimeoutMs = 30000,
            WorkingDirectory = "."
        },
        ["javascript"] = new TestRunnerConfig 
        { 
            Command = "npm test", 
            TimeoutMs = 30000,
            WorkingDirectory = "."
        },
        ["python"] = new TestRunnerConfig 
        { 
            Command = "pytest", 
            TimeoutMs = 30000,
            WorkingDirectory = "."
        }
    };

    /// <summary>
    /// Gets or sets the timeout for test execution in milliseconds.
    /// Default: 30000ms (30 seconds)
    /// </summary>
    public int TestTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Gets or sets the file patterns to identify as test files.
    /// Used to skip TDD enforcement when editing test files themselves.
    /// </summary>
    public List<string> TestFilePatterns { get; set; } = new()
    {
        "*test*", "*Test*", "*tests*", "*Tests*", 
        "*spec*", "*Spec*", "*specs*", "*Specs*",
        "__tests__"
    };

    /// <summary>
    /// Gets or sets the patterns to identify generated code files.
    /// TDD enforcement is typically skipped for generated files.
    /// </summary>
    public List<string> GeneratedCodePatterns { get; set; } = new()
    {
        "*.generated.*", "*.g.*", "*.designer.*", 
        "*AssemblyInfo.cs", "*.min.js"
    };

    /// <summary>
    /// Gets or sets the file paths or patterns to exclude from TDD enforcement.
    /// Supports wildcards and regex patterns.
    /// </summary>
    public List<string> ExcludedPaths { get; set; } = new();

    /// <summary>
    /// Gets or sets how often to check test status in minutes.
    /// Default: 5 minutes
    /// </summary>
    public int TestStatusCheckIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets whether to cache test results to avoid repeated test runs.
    /// Default: true
    /// </summary>
    public bool CacheTestResults { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum age of cached test results in minutes.
    /// Default: 30 minutes
    /// </summary>
    public int TestResultCacheMaxAgeMinutes { get; set; } = 30;

    /// <summary>
    /// Gets or sets whether to automatically run tests when files change.
    /// Default: false (manual test runs only)
    /// </summary>
    public bool AutoRunTests { get; set; } = false;

    /// <summary>
    /// Gets or sets the workflow phases that are allowed without test failures.
    /// </summary>
    public List<TddPhase> AllowedPhasesWithoutTests { get; set; } = new()
    {
        TddPhase.Refactor
    };

    /// <summary>
    /// Gets or sets whether to log detailed TDD enforcement events.
    /// Default: false
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Gets or sets the patterns to identify new functionality that requires tests.
    /// These patterns help distinguish between new features and refactoring.
    /// </summary>
    public List<string> NewFunctionalityPatterns { get; set; } = new()
    {
        @"public\s+class\s+\w+",      // New public class
        @"public\s+\w+\s+\w+\s*\(",   // New public method
        @"public\s+\w+\s+\w+\s*\{",   // New public property
        @"public\s+interface\s+\w+",  // New public interface
        @"public\s+enum\s+\w+"        // New public enum
    };

    /// <summary>
    /// Gets or sets the patterns to identify refactoring operations.
    /// These operations are typically allowed without new tests.
    /// </summary>
    public List<string> RefactoringPatterns { get; set; } = new()
    {
        @"// Extract method",
        @"// Rename variable",
        @"// Move method",
        @"private\s+\w+\s+Extract\w+"  // Extract method pattern
    };

    /// <summary>
    /// Gets or sets the minimum code complexity threshold that requires tests.
    /// Simple one-line changes might not need TDD enforcement.
    /// Default: 10 (lines of meaningful code)
    /// </summary>
    public int MinimumComplexityThreshold { get; set; } = 10;

    /// <summary>
    /// Gets or sets whether to enforce TDD for all code changes or only new functionality.
    /// Default: false (only new functionality)
    /// </summary>
    public bool EnforceForAllChanges { get; set; } = false;

    /// <summary>
    /// Gets or sets custom commands to run before test execution.
    /// Useful for build steps or environment setup.
    /// </summary>
    public List<string> PreTestCommands { get; set; } = new();

    /// <summary>
    /// Gets or sets custom commands to run after test execution.
    /// Useful for cleanup or reporting.
    /// </summary>
    public List<string> PostTestCommands { get; set; } = new();

    /// <summary>
    /// Validates the configuration and sets defaults for missing values.
    /// </summary>
    public void Validate()
    {
        if (TestTimeoutMs <= 0)
        {
            TestTimeoutMs = 30000;
        }

        if (TestStatusCheckIntervalMinutes <= 0)
        {
            TestStatusCheckIntervalMinutes = 5;
        }

        if (TestResultCacheMaxAgeMinutes <= 0)
        {
            TestResultCacheMaxAgeMinutes = 30;
        }

        if (MinimumComplexityThreshold <= 0)
        {
            MinimumComplexityThreshold = 10;
        }

        // Ensure we have at least basic test runners configured
        if (!TestRunners.ContainsKey("csharp"))
        {
            TestRunners["csharp"] = new TestRunnerConfig 
            { 
                Command = "dotnet test", 
                TimeoutMs = TestTimeoutMs 
            };
        }

        // Validate each test runner config
        foreach (var runner in TestRunners.Values)
        {
            runner.Validate();
        }
    }
}

/// <summary>
/// Configuration for a specific test runner.
/// </summary>
public class TestRunnerConfig
{
    /// <summary>
    /// Gets or sets the command to execute tests.
    /// </summary>
    public string Command { get; set; } = "";

    /// <summary>
    /// Gets or sets the working directory for test execution.
    /// Default: "." (current directory)
    /// </summary>
    public string WorkingDirectory { get; set; } = ".";

    /// <summary>
    /// Gets or sets the timeout for test execution in milliseconds.
    /// Default: 30000ms (30 seconds)
    /// </summary>
    public int TimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Gets or sets additional environment variables for test execution.
    /// </summary>
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();

    /// <summary>
    /// Gets or sets command-line arguments to pass to the test runner.
    /// </summary>
    public List<string> Arguments { get; set; } = new();

    /// <summary>
    /// Gets or sets the expected exit code for successful test runs.
    /// Default: 0
    /// </summary>
    public int ExpectedSuccessExitCode { get; set; } = 0;

    /// <summary>
    /// Gets or sets patterns to identify failing tests in the output.
    /// </summary>
    public List<string> FailingTestPatterns { get; set; } = new()
    {
        @"FAILED", @"Failed:", @"ERROR", @"Error:", 
        @"\d+\s+failed", @"\d+\s+error"
    };

    /// <summary>
    /// Gets or sets patterns to identify passing tests in the output.
    /// </summary>
    public List<string> PassingTestPatterns { get; set; } = new()
    {
        @"PASSED", @"Passed:", @"OK", @"Success:", 
        @"\d+\s+passed", @"All tests passed"
    };

    /// <summary>
    /// Gets or sets whether this test runner supports incremental testing.
    /// Default: false
    /// </summary>
    public bool SupportsIncrementalTesting { get; set; } = false;

    /// <summary>
    /// Gets or sets the command arguments for incremental testing.
    /// Only used if SupportsIncrementalTesting is true.
    /// </summary>
    public List<string> IncrementalTestArguments { get; set; } = new();

    /// <summary>
    /// Validates the test runner configuration.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Command))
        {
            throw new ArgumentException("Test runner command cannot be empty");
        }

        if (TimeoutMs <= 0)
        {
            TimeoutMs = 30000;
        }

        if (string.IsNullOrWhiteSpace(WorkingDirectory))
        {
            WorkingDirectory = ".";
        }
    }

    /// <summary>
    /// Builds the complete command line including arguments.
    /// </summary>
    /// <returns>The complete command line string.</returns>
    public string BuildCommandLine()
    {
        if (!Arguments.Any())
        {
            return Command;
        }

        return $"{Command} {string.Join(" ", Arguments)}";
    }
}

/// <summary>
/// Enumeration of TDD enforcement modes.
/// </summary>
public enum TddEnforcementMode
{
    /// <summary>
    /// TDD enforcement is disabled.
    /// </summary>
    Disabled = 0,

    /// <summary>
    /// Show warnings for TDD violations but allow operations to continue.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Block operations that violate TDD principles.
    /// </summary>
    Strict = 2
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
    Refactor = 3
}