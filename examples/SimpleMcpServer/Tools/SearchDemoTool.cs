using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using Microsoft.Extensions.Logging;

namespace SimpleMcpServer.Tools;

/// <summary>
/// Parameters for the search demo tool
/// </summary>
public class SearchDemoParams
{
    /// <summary>
    /// The search query to execute
    /// </summary>
    [Required]
    public required string Query { get; set; }

    /// <summary>
    /// Maximum number of results to return
    /// </summary>
    [Range(1, 100)]
    public int MaxResults { get; set; } = 10;

    /// <summary>
    /// File type filter (optional)
    /// </summary>
    public string? FileType { get; set; }
}

/// <summary>
/// Result from the search demo tool
/// </summary>
public class SearchDemoResult : ToolResultBase
{
    /// <summary>
    /// The search query that was executed
    /// </summary>
    public required string Query { get; set; }

    /// <summary>
    /// The search results
    /// </summary>
    public required List<SearchResultItem> Results { get; set; }

    /// <summary>
    /// Total number of results found
    /// </summary>
    public int TotalResults { get; set; }

    /// <summary>
    /// Time taken to execute the search
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <inheritdoc/>
    public override string Operation => "search-demo";
}

/// <summary>
/// Individual search result item
/// </summary>
public class SearchResultItem
{
    public required string FilePath { get; set; }
    public int Line { get; set; }
    public int Column { get; set; }
    public required string Snippet { get; set; }
    public double Score { get; set; }
    public string? Context { get; set; }
}

/// <summary>
/// Demo tool that demonstrates code search functionality with mock results
/// </summary>
public class SearchDemoTool : McpToolBase<SearchDemoParams, SearchDemoResult>
{
    private static readonly List<SearchResultItem> _mockResults = new()
    {
        new SearchResultItem
        {
            FilePath = "/src/Controllers/UserController.cs",
            Line = 25,
            Column = 12,
            Snippet = "public async Task<IActionResult> GetUser(int id)",
            Score = 0.95,
            Context = "Controller method for retrieving user by ID"
        },
        new SearchResultItem
        {
            FilePath = "/src/Models/User.cs",
            Line = 15,
            Column = 20,
            Snippet = "public class User : BaseEntity",
            Score = 0.88,
            Context = "User entity model class"
        },
        new SearchResultItem
        {
            FilePath = "/src/Services/UserService.cs",
            Line = 42,
            Column = 16,
            Snippet = "var user = await _repository.GetUserAsync(id);",
            Score = 0.82,
            Context = "Service layer user retrieval"
        },
        new SearchResultItem
        {
            FilePath = "/src/Repositories/UserRepository.cs",
            Line = 33,
            Column = 8,
            Snippet = "public async Task<User> GetUserAsync(int id)",
            Score = 0.79,
            Context = "Repository method for database user lookup"
        },
        new SearchResultItem
        {
            FilePath = "/tests/UserControllerTests.cs",
            Line = 67,
            Column = 5,
            Snippet = "var result = await controller.GetUser(testUserId);",
            Score = 0.75,
            Context = "Unit test for user controller"
        },
        new SearchResultItem
        {
            FilePath = "/src/DTOs/UserDto.cs",
            Line = 8,
            Column = 18,
            Snippet = "public class UserDto",
            Score = 0.72,
            Context = "Data transfer object for user"
        },
        new SearchResultItem
        {
            FilePath = "/src/Extensions/UserExtensions.cs",
            Line = 12,
            Column = 28,
            Snippet = "public static UserDto ToDto(this User user)",
            Score = 0.68,
            Context = "Extension method for user conversion"
        },
        new SearchResultItem
        {
            FilePath = "/src/Validators/UserValidator.cs",
            Line = 19,
            Column = 16,
            Snippet = "RuleFor(user => user.Email).NotEmpty();",
            Score = 0.65,
            Context = "User validation rules"
        }
    };

    public SearchDemoTool(ILogger<SearchDemoTool>? logger = null) : base(null, logger)
    {
    }

    public override string Name => "search_demo";
    public override string Description => "Demonstrates visualization capabilities by simulating a code search with rich results display";

    private SearchDemoResult? _lastResult;

    protected override async Task<SearchDemoResult> ExecuteInternalAsync(
        SearchDemoParams parameters, 
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        // Simulate search delay
        await Task.Delay(Random.Shared.Next(100, 500), cancellationToken);

        // Filter results based on query and file type
        var filteredResults = _mockResults
            .Where(r => r.Snippet.Contains(parameters.Query, StringComparison.OrdinalIgnoreCase) ||
                       r.FilePath.Contains(parameters.Query, StringComparison.OrdinalIgnoreCase) ||
                       (r.Context?.Contains(parameters.Query, StringComparison.OrdinalIgnoreCase) ?? false))
            .Where(r => string.IsNullOrEmpty(parameters.FileType) || 
                       r.FilePath.EndsWith($".{parameters.FileType}", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.Score)
            .Take(parameters.MaxResults)
            .ToList();

        var executionTime = DateTime.UtcNow - startTime;

        var result = new SearchDemoResult
        {
            Query = parameters.Query,
            Results = filteredResults,
            TotalResults = filteredResults.Count,
            ExecutionTime = executionTime,
            Success = true,
            Message = $"Found {filteredResults.Count} results for query '{parameters.Query}'"
        };

        _lastResult = result; // Store for visualization
        return result;
    }

}