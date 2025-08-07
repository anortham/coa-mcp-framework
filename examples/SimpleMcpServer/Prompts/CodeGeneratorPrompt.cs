using COA.Mcp.Framework.Prompts;
using COA.Mcp.Protocol;

namespace SimpleMcpServer.Prompts;

/// <summary>
/// A prompt that helps generate code based on requirements.
/// </summary>
public class CodeGeneratorPrompt : PromptBase
{
    public override string Name => "code-generator";

    public override string Description => "Generate code snippets based on requirements with best practices";

    public override List<PromptArgument> Arguments => new()
    {
        new PromptArgument
        {
            Name = "language",
            Description = "Programming language (csharp, python, javascript, typescript)",
            Required = true
        },
        new PromptArgument
        {
            Name = "type",
            Description = "Type of code to generate (class, function, interface, test)",
            Required = true
        },
        new PromptArgument
        {
            Name = "name",
            Description = "Name of the component to generate",
            Required = true
        },
        new PromptArgument
        {
            Name = "description",
            Description = "Description of what the code should do",
            Required = true
        },
        new PromptArgument
        {
            Name = "include_comments",
            Description = "Include XML documentation comments (default: true)",
            Required = false
        },
        new PromptArgument
        {
            Name = "include_tests",
            Description = "Include unit test examples (default: false)",
            Required = false
        }
    };

    public override async Task<GetPromptResult> RenderAsync(Dictionary<string, object>? arguments = null, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // Satisfy async requirement
        
        var language = GetRequiredArgument<string>(arguments, "language");
        var type = GetRequiredArgument<string>(arguments, "type");
        var name = GetRequiredArgument<string>(arguments, "name");
        var description = GetRequiredArgument<string>(arguments, "description");
        var includeComments = GetOptionalArgument<bool>(arguments, "include_comments", true);
        var includeTests = GetOptionalArgument<bool>(arguments, "include_tests", false);
        
        var messages = new List<PromptMessage>();
        
        // System message
        var systemPrompt = $@"You are an expert {language} developer who follows best practices and writes clean, maintainable code.
You focus on:
- Clear naming conventions
- SOLID principles
- Proper error handling
- Performance considerations
{(includeComments ? "- Comprehensive documentation" : "- Minimal comments")}
{(includeTests ? "- Test-driven development" : "")}

Language specifics for {language}:
{GetLanguageSpecifics(language)}";
        
        messages.Add(CreateSystemMessage(systemPrompt));
        
        // User message
        var userPrompt = $@"Generate a {type} named '{name}' in {language}.

Requirements:
{description}

Please ensure the code:
1. Follows {language} naming conventions
2. Implements proper error handling
3. Is production-ready
{(includeComments ? "4. Includes comprehensive documentation" : "")}
{(includeTests ? "5. Includes unit test examples" : "")}";
        
        messages.Add(CreateUserMessage(userPrompt));
        
        // Assistant response with example
        var exampleCode = GenerateExampleCode(language, type, name, includeComments);
        messages.Add(CreateAssistantMessage($"I'll generate a {type} named '{name}' in {language} that {description}.\n\n```{GetLanguageIdentifier(language)}\n{exampleCode}\n```"));
        
        return new GetPromptResult
        {
            Description = $"Code generation prompt for {language} {type}: {name}",
            Messages = messages
        };
    }
    
    private static string GetLanguageSpecifics(string language) => language.ToLower() switch
    {
        "csharp" => "- Use PascalCase for types and methods, camelCase for parameters\n- Use async/await for asynchronous operations\n- Implement IDisposable when appropriate",
        "python" => "- Use snake_case for functions and variables\n- Follow PEP 8 style guide\n- Use type hints for better code clarity",
        "javascript" or "typescript" => "- Use camelCase for variables and functions\n- Use const/let instead of var\n- Implement proper Promise handling",
        _ => "- Follow language-specific conventions"
    };
    
    private static string GetLanguageIdentifier(string language) => language.ToLower() switch
    {
        "csharp" => "csharp",
        "python" => "python",
        "javascript" => "javascript",
        "typescript" => "typescript",
        _ => "plaintext"
    };
    
    private static string GenerateExampleCode(string language, string type, string name, bool includeComments)
    {
        if (language.ToLower() == "csharp" && type.ToLower() == "class")
        {
            return includeComments
                ? $@"/// <summary>
/// {name} implementation.
/// </summary>
public class {name}
{{
    private readonly ILogger<{name}> _logger;
    
    /// <summary>
    /// Initializes a new instance of the {name} class.
    /// </summary>
    public {name}(ILogger<{name}> logger)
    {{
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }}
    
    /// <summary>
    /// Processes the request.
    /// </summary>
    public async Task<Result> ProcessAsync(Request request, CancellationToken cancellationToken = default)
    {{
        // Implementation here
        await Task.CompletedTask;
        return new Result {{ Success = true }};
    }}
}}"
                : $@"public class {name}
{{
    private readonly ILogger<{name}> _logger;
    
    public {name}(ILogger<{name}> logger)
    {{
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }}
    
    public async Task<Result> ProcessAsync(Request request, CancellationToken cancellationToken = default)
    {{
        await Task.CompletedTask;
        return new Result {{ Success = true }};
    }}
}}";
        }
        
        // Return a simple template for other combinations
        return $"// {type} {name} implementation will be generated here based on requirements";
    }
}