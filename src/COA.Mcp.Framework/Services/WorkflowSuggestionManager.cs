using System;
using System.Collections.Generic;
using System.Linq;
using COA.Mcp.Framework.Configuration;

namespace COA.Mcp.Framework.Services;

/// <summary>
/// Manages workflow suggestions and provides professional guidance for tool usage
/// without being manipulative or restrictive.
/// </summary>
public class WorkflowSuggestionManager
{
    private readonly List<WorkflowSuggestion> _workflows;
    private readonly Dictionary<string, List<WorkflowSuggestion>> _scenarioIndex;

    /// <summary>
    /// Initializes a new instance of the WorkflowSuggestionManager class.
    /// </summary>
    public WorkflowSuggestionManager()
    {
        _workflows = new List<WorkflowSuggestion>();
        _scenarioIndex = new Dictionary<string, List<WorkflowSuggestion>>();
        
        RegisterDefaultWorkflows();
    }

    /// <summary>
    /// Registers a workflow suggestion for the specified scenario.
    /// </summary>
    /// <param name="workflow">The workflow suggestion to register.</param>
    public void RegisterWorkflow(WorkflowSuggestion workflow)
    {
        if (workflow == null)
            throw new ArgumentNullException(nameof(workflow));

        if (string.IsNullOrWhiteSpace(workflow.Scenario))
            throw new ArgumentException("Workflow scenario cannot be null or empty.", nameof(workflow));

        _workflows.Add(workflow);

        // Update the scenario index
        if (!_scenarioIndex.ContainsKey(workflow.Scenario))
        {
            _scenarioIndex[workflow.Scenario] = new List<WorkflowSuggestion>();
        }
        
        _scenarioIndex[workflow.Scenario].Add(workflow);
    }

    /// <summary>
    /// Gets workflow suggestions for a specific scenario.
    /// </summary>
    /// <param name="scenario">The scenario to get workflows for.</param>
    /// <returns>Collection of workflow suggestions for the scenario.</returns>
    public IEnumerable<WorkflowSuggestion> GetWorkflowsForScenario(string scenario)
    {
        if (string.IsNullOrWhiteSpace(scenario))
            return Enumerable.Empty<WorkflowSuggestion>();

        return _scenarioIndex.TryGetValue(scenario, out var workflows) 
            ? workflows 
            : Enumerable.Empty<WorkflowSuggestion>();
    }

    /// <summary>
    /// Gets all high-impact workflow suggestions that should be prominently featured.
    /// </summary>
    /// <returns>Collection of high-impact workflow suggestions.</returns>
    public IEnumerable<WorkflowSuggestion> GetHighImpactWorkflows()
    {
        return _workflows.Where(w => w.IsHighImpact);
    }

    /// <summary>
    /// Gets all registered workflow suggestions.
    /// </summary>
    /// <returns>Collection of all workflow suggestions.</returns>
    public IEnumerable<WorkflowSuggestion> GetAllWorkflows()
    {
        return _workflows.AsReadOnly();
    }

    /// <summary>
    /// Gets workflow suggestions that include a specific tool in their recommended order.
    /// </summary>
    /// <param name="toolName">The tool name to search for.</param>
    /// <returns>Collection of workflows that recommend the specified tool.</returns>
    public IEnumerable<WorkflowSuggestion> GetWorkflowsUsingTool(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            return Enumerable.Empty<WorkflowSuggestion>();

        return _workflows.Where(w => w.RecommendedToolOrder?.Contains(toolName) == true);
    }

    /// <summary>
    /// Gets workflow suggestions where a tool could be used as an alternative.
    /// </summary>
    /// <param name="toolName">The tool name to find alternatives for.</param>
    /// <returns>Collection of workflows where the tool could be an alternative.</returns>
    public IEnumerable<WorkflowSuggestion> GetWorkflowsWithAlternative(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            return Enumerable.Empty<WorkflowSuggestion>();

        return _workflows.Where(w => w.AlternativeTools?.Contains(toolName) == true);
    }

    /// <summary>
    /// Generates professional instruction text for a set of available tools.
    /// </summary>
    /// <param name="availableTools">The tools available in the current server.</param>
    /// <param name="includeAlternatives">Whether to mention alternative tools in guidance.</param>
    /// <returns>Professional instruction text based on available tools and workflows.</returns>
    public string GenerateInstructionText(IEnumerable<string> availableTools, bool includeAlternatives = true)
    {
        if (availableTools == null)
            return string.Empty;

        var toolSet = new HashSet<string>(availableTools);
        var applicableWorkflows = new List<WorkflowSuggestion>();

        // Find workflows where we have the recommended tools
        foreach (var workflow in _workflows)
        {
            var recommendedCount = workflow.RecommendedToolOrder?.Count(toolSet.Contains) ?? 0;
            if (recommendedCount >= workflow.MinimumToolsForBenefit)
            {
                applicableWorkflows.Add(workflow);
            }
        }

        if (!applicableWorkflows.Any())
            return string.Empty;

        var instructions = new List<string>();

        // Add high-impact workflows first
        var highImpact = applicableWorkflows.Where(w => w.IsHighImpact).ToList();
        if (highImpact.Any())
        {
            instructions.Add("## Recommended Workflows");
            foreach (var workflow in highImpact)
            {
                instructions.Add(FormatWorkflowInstruction(workflow, toolSet, includeAlternatives));
            }
        }

        // Add other workflows
        var otherWorkflows = applicableWorkflows.Where(w => !w.IsHighImpact).ToList();
        if (otherWorkflows.Any())
        {
            if (highImpact.Any())
                instructions.Add("\n## Additional Workflows");
            else
                instructions.Add("## Recommended Workflows");

            foreach (var workflow in otherWorkflows)
            {
                instructions.Add(FormatWorkflowInstruction(workflow, toolSet, includeAlternatives));
            }
        }

        return string.Join("\n", instructions);
    }

    private string FormatWorkflowInstruction(WorkflowSuggestion workflow, HashSet<string> availableTools, bool includeAlternatives)
    {
        var instruction = new List<string>();
        
        instruction.Add($"### {workflow.Scenario}");
        
        // List available recommended tools
        var availableRecommended = workflow.RecommendedToolOrder?.Where(availableTools.Contains).ToList() ?? new List<string>();
        if (availableRecommended.Any())
        {
            instruction.Add($"**Recommended sequence**: {string.Join(" → ", availableRecommended)}");
        }

        instruction.Add($"**Rationale**: {workflow.Rationale}");

        if (!string.IsNullOrEmpty(workflow.ExpectedBenefit))
        {
            instruction.Add($"**Expected benefit**: {workflow.ExpectedBenefit}");
        }

        if (includeAlternatives && workflow.AlternativeTools?.Any() == true)
        {
            var availableAlternatives = workflow.AlternativeTools.Where(availableTools.Contains).ToList();
            if (availableAlternatives.Any())
            {
                instruction.Add($"**Alternatives available**: {string.Join(", ", availableAlternatives)} (less optimal)");
            }
        }

        return string.Join("\n", instruction) + "\n";
    }

    private void RegisterDefaultWorkflows()
    {
        // Code search workflow
        RegisterWorkflow(new WorkflowSuggestion(
            "code_navigation",
            new[] { "index_workspace", "symbol_search", "goto_definition" },
            "Symbol-based navigation provides precise results and eliminates guesswork when exploring unfamiliar code.")
        {
            ExpectedBenefit = "90% accuracy improvement over text search for type information",
            AlternativeTools = new[] { "text_search", "file_search" },
            IsHighImpact = true,
            MinimumToolsForBenefit = 2
        });

        // Type verification workflow
        RegisterWorkflow(new WorkflowSuggestion(
            "type_verification", 
            new[] { "symbol_search", "goto_definition", "find_references" },
            "Verify types and signatures before writing code to prevent compilation errors and reduce debugging iterations.")
        {
            ExpectedBenefit = "50% fewer type-related errors in generated code",
            IsHighImpact = true,
            MinimumToolsForBenefit = 1
        });

        // Refactoring workflow
        RegisterWorkflow(new WorkflowSuggestion(
            "refactoring",
            new[] { "find_references", "symbol_search", "search_and_replace" },
            "Understanding all usage contexts before making changes prevents breaking existing functionality.")
        {
            ExpectedBenefit = "Eliminates introduction of breaking changes during refactoring",
            AlternativeTools = new[] { "text_search" },
            MinimumToolsForBenefit = 2
        });

        // Workspace exploration workflow  
        RegisterWorkflow(new WorkflowSuggestion(
            "workspace_exploration",
            new[] { "index_workspace", "recent_files", "directory_search", "file_search" },
            "Systematic workspace exploration provides better project understanding than random file browsing.")
        {
            ExpectedBenefit = "3x faster project familiarization",
            MinimumToolsForBenefit = 2
        });
    }

    /// <summary>
    /// Clears all registered workflows and re-registers default ones.
    /// </summary>
    public void Reset()
    {
        _workflows.Clear();
        _scenarioIndex.Clear();
        RegisterDefaultWorkflows();
    }
}