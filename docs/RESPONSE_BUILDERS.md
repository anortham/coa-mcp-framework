# Typed Response Builders

Response builders help produce AI-friendly, token-aware results without brittle reflection.

## When to use

- You want to build a `ToolResult<T>` or a domain result with insights/actions/metadata.
- You want automatic sizing, summaries, or progressive reduction based on token budget.

## Quick Start

```csharp
using COA.Mcp.Framework.TokenOptimization.ResponseBuilders;
using COA.Mcp.Framework.Models;

public class SearchResponse
{
    public List<string> Files { get; set; } = new();
    public ResultsSummary Summary { get; set; } = new();
}

public class SearchResponseBuilder : BaseResponseBuilder<SearchResponse, ToolResult<SearchResponse>>
{
    public SearchResponseBuilder(ILogger<SearchResponseBuilder>? logger = null) : base(logger) {}

    public override Task<ToolResult<SearchResponse>> BuildResponseAsync(
        SearchResponse data, ResponseContext context)
    {
        var start = DateTime.UtcNow;
        var budget = CalculateTokenBudget(context);

        // Generate insights/actions as needed
        var insights = GenerateInsights(data, context.ResponseMode);
        var actions = GenerateActions(data, budget);

        var result = ToolResult<SearchResponse>.CreateSuccess(data, "Search completed");
        result.Insights = ReduceInsights(insights, budget);
        result.Actions = ReduceActions(actions, budget);
        result.Meta = CreateMetadata(start, wasTruncated: false);
        return Task.FromResult(result);
    }

    protected override List<string> GenerateInsights(SearchResponse data, string responseMode)
    {
        var insights = new List<string>();
        insights.Add($"Returned {data.Files.Count} files ({responseMode})");
        return insights;
    }

    protected override List<AIAction> GenerateActions(SearchResponse data, int tokenBudget)
    {
        return new List<AIAction>
        {
            new AIAction { Action = "open_file", Description = "Open the first file", Parameters = new { index = 0 } }
        };
    }
}
```

## Using in a Tool

```csharp
public class SearchTool : McpToolBase<SearchParams, ToolResult<SearchResponse>>
{
    public override string Name => "search";
    public override string Description => "Search files";

    protected override async Task<ToolResult<SearchResponse>> ExecuteInternalAsync(
        SearchParams parameters, CancellationToken cancellationToken)
    {
        var data = new SearchResponse
        {
            Files = new List<string> { "a.cs", "b.cs" },
            Summary = new ResultsSummary { Included = 2, Total = 2, HasMore = false }
        };

        var builder = new SearchResponseBuilder();
        return await BuildResponseAsync(builder, data, responseMode: "full");
    }
}
```

## Notes

- The builder is strongly typed (`BaseResponseBuilder<TInput, TResult>`), so you get compile-time safety.
- `ResponseContext` carries `ResponseMode` ("summary"/"full"), optional `TokenLimit`, and metadata you can extend.
- Use `ReduceInsights`/`ReduceActions` to fit within token budgets.

