using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using COA.Mcp.Framework.Configuration;
using COA.Mcp.Framework.Exceptions;
using COA.Mcp.Framework.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace COA.Mcp.Framework.Pipeline.Middleware;

/// <summary>
/// Middleware that enforces Test-Driven Development (TDD) principles by requiring 
/// failing tests before allowing production code implementation. Implements the 
/// red-green-refactor cycle enforcement.
/// </summary>
public class TddEnforcementMiddleware : SimpleMiddlewareBase
{
    private readonly ITestStatusService _testStatusService;
    private readonly ILogger<TddEnforcementMiddleware> _logger;
    private readonly TddEnforcementOptions _options;
    
    private static readonly HashSet<string> CodeGenerationTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "Edit", "Write", "MultiEdit", "NotebookEdit"
    };

    private static readonly HashSet<string> TestFilePatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "test", "tests", "spec", "specs", "__tests__"
    };

    /// <summary>
    /// Initializes a new instance of the TddEnforcementMiddleware class.
    /// </summary>
    public TddEnforcementMiddleware(
        ITestStatusService testStatusService,
        ILogger<TddEnforcementMiddleware> logger,
        IOptions<TddEnforcementOptions> options)
    {
        _testStatusService = testStatusService ?? throw new ArgumentNullException(nameof(testStatusService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        
        Order = 10; // Run after type verification
        IsEnabled = _options.Enabled;
    }

    /// <inheritdoc/>
    public override async Task OnBeforeExecutionAsync(string toolName, object? parameters)
    {
        if (!IsEnabled || !CodeGenerationTools.Contains(toolName) || parameters == null)
        {
            return;
        }

        _logger.LogDebug("TddEnforcementMiddleware: Checking tool {ToolName}", toolName);

        try
        {
            var filePath = ExtractFilePathFromParameters(toolName, parameters);
            var code = ExtractCodeFromParameters(toolName, parameters);
            
            // Skip if no meaningful code or if editing test files
            if (string.IsNullOrWhiteSpace(code) || IsTestFile(filePath))
            {
                _logger.LogDebug("Skipping TDD check - test file or no code: {FilePath}", filePath);
                return;
            }

            // Skip if this is a refactoring operation (no new functionality)
            if (_options.AllowRefactoring && IsRefactoringOperation(code, filePath))
            {
                _logger.LogDebug("Skipping TDD check - detected refactoring operation: {FilePath}", filePath);
                return;
            }

            // Check if this is adding new functionality that requires tests
            if (IsNewFunctionality(code))
            {
                await EnforceTddWorkflow(toolName, filePath, code);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TddEnforcementMiddleware for tool {ToolName}", toolName);
            
            // In case of errors, fail open in warning mode, fail closed in strict mode
            if (_options.Mode == TddEnforcementMode.Strict)
            {
                throw new McpException($"TDD enforcement failed due to error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Enforces the TDD workflow by checking for failing tests before allowing implementation.
    /// </summary>
    private async Task EnforceTddWorkflow(string toolName, string filePath, string code)
    {
        var workspaceRoot = FindWorkspaceRoot(filePath);
        var testStatus = await _testStatusService.GetTestStatusAsync(workspaceRoot);
        
        _logger.LogDebug("TDD Check: Workspace={WorkspaceRoot}, HasFailingTests={HasFailingTests}, " +
                        "LastTestRun={LastTestRun}", workspaceRoot, testStatus.HasFailingTests, testStatus.LastTestRun);

        var violations = new List<string>();

        // Check for failing test requirement
        if (_options.RequireFailingTest && !testStatus.HasFailingTests)
        {
            // Look for related test files
            var relatedTestFiles = await FindRelatedTestFiles(filePath, workspaceRoot);
            
            if (!relatedTestFiles.Any())
            {
                violations.Add("No test files found for this code");
            }
            else if (!testStatus.RecentTestRuns.Any())
            {
                violations.Add("No recent test runs detected");
            }
            else
            {
                violations.Add("No failing tests found - write a failing test first (RED phase)");
            }
        }

        // Check test recency
        if (testStatus.LastTestRun.HasValue && 
            DateTime.UtcNow - testStatus.LastTestRun.Value > TimeSpan.FromMinutes(30))
        {
            violations.Add("Tests haven't been run recently - run tests first to verify current state");
        }

        if (violations.Any())
        {
            await HandleTddViolation(toolName, filePath, violations, testStatus, workspaceRoot);
        }
        else
        {
            _logger.LogDebug("TDD check passed for {ToolName} on {FilePath}", toolName, filePath);
        }
    }

    /// <summary>
    /// Handles TDD violations by logging and optionally throwing an exception.
    /// </summary>
    private async Task HandleTddViolation(
        string toolName,
        string filePath,
        IList<string> violations,
        TestStatus testStatus,
        string workspaceRoot)
    {
        var errorMessage = BuildTddViolationMessage(violations, testStatus, workspaceRoot);
        
        await _testStatusService.LogTddViolationAsync(toolName, filePath, violations.ToList());

        _logger.LogWarning("TDD violation for {ToolName} on {FilePath}: {Violations}",
            toolName, filePath, string.Join("; ", violations));

        if (_options.Mode == TddEnforcementMode.Strict)
        {
            throw new TddViolationException(errorMessage);
        }
        else if (_options.Mode == TddEnforcementMode.Warning)
        {
            _logger.LogWarning("TDD warning for {ToolName}: {Message}", toolName, errorMessage);
        }
    }

    /// <summary>
    /// Builds a detailed error message for TDD violations.
    /// </summary>
    private static string BuildTddViolationMessage(
        IList<string> violations,
        TestStatus testStatus,
        string workspaceRoot)
    {
        var message = new List<string>
        {
            "🚫 TDD VIOLATION: Implementation without proper test coverage",
            "",
            "Issues detected:"
        };

        foreach (var violation in violations)
        {
            message.Add($"  • {violation}");
        }

        message.Add("");
        message.Add("🔍 TDD Workflow (Red-Green-Refactor):");
        message.Add("1. RED: Write a failing test that describes the desired behavior");
        message.Add("2. GREEN: Write the minimal code to make the test pass");
        message.Add("3. REFACTOR: Improve the code while keeping tests green");
        message.Add("");
        message.Add("📋 Required actions:");

        if (!testStatus.HasFailingTests)
        {
            message.Add("1. Write a failing test first:");
            message.Add("   • Create or update test files");
            message.Add("   • Write tests that describe the expected behavior");
            message.Add("   • Verify tests fail before implementing");
        }

        message.Add("2. Run tests to verify current state:");
        
        var testRunner = GetTestRunnerCommand(workspaceRoot);
        if (!string.IsNullOrEmpty(testRunner))
        {
            message.Add($"   {testRunner}");
        }
        else
        {
            message.Add("   dotnet test  (for .NET projects)");
            message.Add("   npm test     (for Node.js projects)");
            message.Add("   pytest       (for Python projects)");
        }

        message.Add("");
        message.Add("3. After tests fail, implement the minimal code to pass");
        message.Add("");
        message.Add("💡 TIP: This ensures your code is tested and behaves as expected!");
        
        return string.Join("\n", message);
    }

    /// <summary>
    /// Determines the appropriate test runner command for the workspace.
    /// </summary>
    private static string GetTestRunnerCommand(string workspaceRoot)
    {
        if (File.Exists(Path.Combine(workspaceRoot, "package.json")))
        {
            return "npm test";
        }
        
        if (Directory.GetFiles(workspaceRoot, "*.sln", SearchOption.TopDirectoryOnly).Any() ||
            Directory.GetFiles(workspaceRoot, "*.csproj", SearchOption.AllDirectories).Any())
        {
            return "dotnet test";
        }

        if (File.Exists(Path.Combine(workspaceRoot, "pytest.ini")) ||
            File.Exists(Path.Combine(workspaceRoot, "pyproject.toml")))
        {
            return "pytest";
        }

        return "";
    }

    /// <summary>
    /// Extracts file path from tool parameters.
    /// </summary>
    private static string ExtractFilePathFromParameters(string toolName, object parameters)
    {
        if (parameters is JsonElement jsonElement)
        {
            return TryGetStringProperty(jsonElement, "file_path") ?? 
                   TryGetStringProperty(jsonElement, "filePath") ?? 
                   TryGetStringProperty(jsonElement, "notebook_path") ?? "";
        }

        return GetPropertyValue<string>(parameters, "file_path") ?? 
               GetPropertyValue<string>(parameters, "filePath") ?? 
               GetPropertyValue<string>(parameters, "notebook_path") ?? "";
    }

    /// <summary>
    /// Extracts code content from tool parameters.
    /// </summary>
    private static string ExtractCodeFromParameters(string toolName, object parameters)
    {
        if (parameters is JsonElement jsonElement)
        {
            return toolName switch
            {
                "Edit" => TryGetStringProperty(jsonElement, "new_string") ?? "",
                "Write" => TryGetStringProperty(jsonElement, "content") ?? "",
                "MultiEdit" => ExtractMultiEditCode(jsonElement),
                "NotebookEdit" => TryGetStringProperty(jsonElement, "new_source") ?? "",
                _ => ""
            };
        }

        return toolName switch
        {
            "Edit" => GetPropertyValue<string>(parameters, "new_string") ?? "",
            "Write" => GetPropertyValue<string>(parameters, "content") ?? "",
            "MultiEdit" => ExtractMultiEditCodeFromObject(parameters),
            "NotebookEdit" => GetPropertyValue<string>(parameters, "new_source") ?? "",
            _ => ""
        };
    }

    /// <summary>
    /// Extracts code from MultiEdit operations.
    /// </summary>
    private static string ExtractMultiEditCode(JsonElement jsonElement)
    {
        if (jsonElement.TryGetProperty("edits", out var editsElement) && editsElement.ValueKind == JsonValueKind.Array)
        {
            var codes = new List<string>();
            foreach (var edit in editsElement.EnumerateArray())
            {
                var newString = TryGetStringProperty(edit, "new_string");
                if (!string.IsNullOrEmpty(newString))
                {
                    codes.Add(newString);
                }
            }
            return string.Join("\n", codes);
        }
        return "";
    }

    /// <summary>
    /// Extracts code from MultiEdit object parameters.
    /// </summary>
    private static string ExtractMultiEditCodeFromObject(object parameters)
    {
        var edits = GetPropertyValue<object>(parameters, "edits");
        if (edits is IEnumerable<object> enumerable)
        {
            var codes = new List<string>();
            foreach (var edit in enumerable)
            {
                var newString = GetPropertyValue<string>(edit, "new_string");
                if (!string.IsNullOrEmpty(newString))
                {
                    codes.Add(newString);
                }
            }
            return string.Join("\n", codes);
        }
        return "";
    }

    /// <summary>
    /// Checks if the file path indicates a test file.
    /// </summary>
    private static bool IsTestFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        var fileName = Path.GetFileName(filePath).ToLowerInvariant();
        var directory = Path.GetDirectoryName(filePath)?.ToLowerInvariant() ?? "";

        return TestFilePatterns.Any(pattern => 
            fileName.Contains(pattern) || directory.Contains(pattern));
    }

    /// <summary>
    /// Checks if the operation appears to be refactoring rather than new functionality.
    /// </summary>
    private static bool IsRefactoringOperation(string code, string filePath)
    {
        // Simple heuristics to detect refactoring:
        // - Contains extract method patterns
        // - Contains variable renames
        // - Contains method moves
        // - No new public methods or classes

        var refactoringPatterns = new[]
        {
            @"// Extract method",
            @"// Rename variable",
            @"// Move method",
            @"private\s+\w+\s+Extract\w+", // Extract method pattern
        };

        return refactoringPatterns.Any(pattern => 
            Regex.IsMatch(code, pattern, RegexOptions.IgnoreCase));
    }

    /// <summary>
    /// Checks if the code represents new functionality that requires tests.
    /// </summary>
    private static bool IsNewFunctionality(string code)
    {
        // Patterns that indicate new functionality
        var newFunctionalityPatterns = new[]
        {
            @"public\s+class\s+\w+", // New public class
            @"public\s+\w+\s+\w+\s*\(", // New public method
            @"public\s+\w+\s+\w+\s*\{", // New public property
            @"public\s+interface\s+\w+", // New public interface
            @"public\s+enum\s+\w+", // New public enum
        };

        return newFunctionalityPatterns.Any(pattern => 
            Regex.IsMatch(code, pattern, RegexOptions.IgnoreCase));
    }

    /// <summary>
    /// Finds the workspace root directory.
    /// </summary>
    private static string FindWorkspaceRoot(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return Directory.GetCurrentDirectory();

        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        
        while (!string.IsNullOrEmpty(directory))
        {
            // Look for workspace indicators
            if (File.Exists(Path.Combine(directory, ".git")) ||
                Directory.Exists(Path.Combine(directory, ".git")) ||
                File.Exists(Path.Combine(directory, "*.sln")) ||
                File.Exists(Path.Combine(directory, "package.json")) ||
                File.Exists(Path.Combine(directory, "pyproject.toml")))
            {
                return directory;
            }

            var parent = Directory.GetParent(directory);
            if (parent == null) break;
            directory = parent.FullName;
        }

        return directory ?? Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// Finds test files related to the given source file.
    /// </summary>
    private static Task<List<string>> FindRelatedTestFiles(string sourceFilePath, string workspaceRoot)
    {
        if (string.IsNullOrEmpty(sourceFilePath))
            return Task.FromResult(new List<string>());

        var sourceFileName = Path.GetFileNameWithoutExtension(sourceFilePath);
        var testFiles = new List<string>();

        try
        {
            var searchPatterns = new[]
            {
                $"{sourceFileName}Test.*",
                $"{sourceFileName}Tests.*",
                $"{sourceFileName}Spec.*",
                $"{sourceFileName}.test.*",
                $"{sourceFileName}.spec.*"
            };

            foreach (var pattern in searchPatterns)
            {
                var files = Directory.GetFiles(workspaceRoot, pattern, SearchOption.AllDirectories);
                testFiles.AddRange(files);
            }
        }
        catch (Exception)
        {
            // Ignore file system errors
        }

        return Task.FromResult(testFiles);
    }

    /// <summary>
    /// Safely gets a string property from JsonElement.
    /// </summary>
    private static string? TryGetStringProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String 
            ? prop.GetString() 
            : null;
    }

    /// <summary>
    /// Gets a property value using reflection.
    /// </summary>
    private static T? GetPropertyValue<T>(object obj, string propertyName)
    {
        var property = obj.GetType().GetProperty(propertyName);
        return property != null ? (T?)property.GetValue(obj) : default(T);
    }
}

/// <summary>
/// Exception thrown when TDD principles are violated.
/// </summary>
public class TddViolationException : McpException
{
    public TddViolationException(string message) : base(message, "TDD_VIOLATION") { }
    public TddViolationException(string message, Exception innerException) : base(message, "TDD_VIOLATION", innerException) { }
}