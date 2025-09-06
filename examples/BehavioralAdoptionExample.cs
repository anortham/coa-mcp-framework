using System;
using System.Collections.Generic;
using COA.Mcp.Framework.Configuration;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Server;
using COA.Mcp.Framework.Services;

namespace BehavioralAdoptionExample;

/// <summary>
/// Example demonstrating the new behavioral adoption enhancements
/// </summary>
public class BehavioralAdoptionExample
{
    public static void Main(string[] args)
    {
        // 1. Create template variables with tool comparisons
        var templateVariables = new TemplateVariables
        {
            AvailableTools = new[] { "text_search", "symbol_search", "goto_definition" },
            BuiltInTools = new[] { "Read", "Grep", "Bash", "Search" },
            ToolComparisons = new Dictionary<string, ToolComparison>
            {
                ["Find code patterns"] = new()
                {
                    Task = "Find code patterns",
                    ServerTool = "text_search",
                    BuiltInTool = "grep",
                    Advantage = "Lucene-indexed with Tree-sitter parsing",
                    PerformanceMetric = "100x faster, searches millions of lines in <500ms"
                },
                ["Navigate to definition"] = new()
                {
                    Task = "Navigate to definition", 
                    ServerTool = "goto_definition",
                    BuiltInTool = "Read + manual search",
                    Advantage = "Direct jump with exact type signatures",
                    PerformanceMetric = "Instant navigation vs 30+ seconds manual search"
                }
            },
            EnforcementLevel = WorkflowEnforcement.Recommend
        };

        // 2. Build server with behavioral adoption features
        var builder = new McpServerBuilder()
            .WithServerInfo("Enhanced CodeSearch", "2.1.0")
            
            // Use template-based instructions
            .WithTemplateInstructions(options =>
            {
                options.ContextName = "codesearch";
                options.CustomVariables["ProjectType"] = "Multi-language codebase";
            })
            
            // Add tool comparisons for professional guidance
            .WithToolComparison(
                task: "Find code patterns",
                serverTool: "text_search", 
                builtInTool: "grep",
                advantage: "Lucene-indexed with Tree-sitter parsing",
                performanceMetric: "100x faster, searches millions of lines in <500ms"
            )
            
            .WithToolComparison(
                task: "Navigate to definition",
                serverTool: "goto_definition",
                builtInTool: "Read + manual search", 
                advantage: "Direct jump with exact type signatures",
                performanceMetric: "Instant navigation vs 30+ seconds manual search"
            )
            
            // Set enforcement level
            .WithWorkflowEnforcement(WorkflowEnforcement.Recommend)
            
            // Configure tool management
            .ConfigureToolManagement(config =>
            {
                config.EnableWorkflowSuggestions = true;
                config.EnableToolPriority = true;
                config.UseDefaultDescriptionProvider = true;
            });

        Console.WriteLine("✅ Behavioral Adoption Example Setup Complete!");
        Console.WriteLine();
        Console.WriteLine("New Features Demonstrated:");
        Console.WriteLine("- ToolComparison class for professional tool promotion");
        Console.WriteLine("- WorkflowEnforcement levels (Suggest/Recommend/StronglyUrge)"); 
        Console.WriteLine("- Enhanced TemplateVariables with built-in tool awareness");
        Console.WriteLine("- IPrioritizedTool interface for priority-based guidance");
        Console.WriteLine("- DefaultToolDescriptionProvider with TransformToImperative");
        Console.WriteLine("- McpServerBuilder convenience methods");
        Console.WriteLine();
        Console.WriteLine("Templates can now access:");
        Console.WriteLine("- {{builtin_tools}} - List of Claude's built-in tools");
        Console.WriteLine("- {{tool_comparisons}} - Professional comparisons");
        Console.WriteLine("- {{enforcement_level}} - Workflow enforcement level");
        Console.WriteLine("- {{#has_builtin}}{{/has_builtin}} - Built-in tool detection");
    }
}

/// <summary>
/// Example tool implementing the new IPrioritizedTool interface
/// </summary>
public class ExampleSearchTool : IPrioritizedTool
{
    public int Priority => 90; // High priority
    public string[] PreferredScenarios => new[] { "code_exploration", "type_verification" };
}

/// <summary>
/// Example of using TransformToImperative
/// </summary>
public class DescriptionTransformationExample
{
    public static void DemonstrateTransformation()
    {
        var passiveDescription = "Searches for text patterns in files";
        
        Console.WriteLine("Description Transformation Examples:");
        Console.WriteLine($"Original: {passiveDescription}");
        Console.WriteLine($"Priority 50: {DefaultToolDescriptionProvider.TransformToImperative(passiveDescription, 50)}");
        Console.WriteLine($"Priority 70: {DefaultToolDescriptionProvider.TransformToImperative(passiveDescription, 70)}");
        Console.WriteLine($"Priority 85: {DefaultToolDescriptionProvider.TransformToImperative(passiveDescription, 85)}");
        Console.WriteLine($"Priority 95: {DefaultToolDescriptionProvider.TransformToImperative(passiveDescription, 95)}");
    }
}