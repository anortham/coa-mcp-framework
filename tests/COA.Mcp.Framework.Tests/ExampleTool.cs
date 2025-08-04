using System.Threading.Tasks;
using COA.Mcp.Framework.Attributes;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Interfaces;

namespace COA.Mcp.Framework.Tests;

[McpServerToolType(Description = "Example tools for testing", Category = "Testing")]
public class ExampleTool : McpToolBase
{
    public override string ToolName => "example_tool";
    public override string Description => "An example tool for testing the framework";
    public override ToolCategory Category => ToolCategory.General;

    [McpServerTool("greet")]
    [COA.Mcp.Framework.Attributes.Description(@"Generates a friendly greeting.
    Returns: A personalized greeting message.
    Prerequisites: None.
    Use cases: Testing the framework, greeting users.")]
    public async Task<object> GreetAsync(GreetParams parameters)
    {
        var name = ValidateRequired(parameters.Name, nameof(parameters.Name));
        var age = ValidateRange(parameters.Age ?? 18, 0, 150, nameof(parameters.Age));

        await Task.Delay(10); // Simulate async work

        return CreateIntelligentResponse(
            data: new
            {
                Greeting = $"Hello, {name}! Welcome to the MCP Framework!",
                Age = age,
                IsAdult = age >= 18
            },
            insights: new[]
            {
                "Personalized greetings improve user engagement",
                $"{name} is {(age >= 18 ? "an adult" : "a minor")}"
            },
            actions: new[]
            {
                new { Tool = "get_weather", Description = "Check the weather for the user" },
                new { Tool = "get_news", Description = "Get personalized news" }
            }
        );
    }

    public override Task<object> ExecuteAsync(object parameters)
    {
        if (parameters is GreetParams greetParams)
        {
            return GreetAsync(greetParams);
        }

        throw new System.ArgumentException("Invalid parameters type");
    }
}

public class GreetParams
{
    [COA.Mcp.Framework.Attributes.Description("Name of the person to greet")]
    [Required]
    [StringLength(50)]
    public string? Name { get; set; }

    [COA.Mcp.Framework.Attributes.Description("Age of the person (optional)")]
    [COA.Mcp.Framework.Attributes.Range(0, 150)]
    public int? Age { get; set; }
}