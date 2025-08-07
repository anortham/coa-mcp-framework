using COA.Mcp.Framework.Prompts;
using COA.Mcp.Protocol;

namespace SimpleMcpServer.Prompts;

/// <summary>
/// A simple prompt that creates a personalized greeting message.
/// </summary>
public class GreetingPrompt : PromptBase
{
    public override string Name => "greeting";

    public override string Description => "Generate a personalized greeting message with optional customization";

    public override List<PromptArgument> Arguments => new()
    {
        new PromptArgument
        {
            Name = "name",
            Description = "The name of the person to greet",
            Required = true
        },
        new PromptArgument
        {
            Name = "style",
            Description = "The greeting style: formal, casual, or enthusiastic",
            Required = false
        },
        new PromptArgument
        {
            Name = "include_time",
            Description = "Whether to include time-based greeting (morning/afternoon/evening)",
            Required = false
        }
    };

    public override async Task<GetPromptResult> RenderAsync(Dictionary<string, object>? arguments = null, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // Satisfy async requirement
        
        var name = GetRequiredArgument<string>(arguments, "name");
        var style = GetOptionalArgument<string>(arguments, "style", "casual");
        var includeTime = GetOptionalArgument<bool>(arguments, "include_time", false);
        
        var messages = new List<PromptMessage>();
        
        // System message based on style
        var systemPrompt = style.ToLower() switch
        {
            "formal" => "You are a professional assistant who greets people formally and respectfully.",
            "enthusiastic" => "You are an enthusiastic and energetic assistant who loves meeting new people!",
            _ => "You are a friendly assistant who greets people warmly."
        };
        
        messages.Add(CreateSystemMessage(systemPrompt));
        
        // User message
        var userMessage = $"Please greet {name}";
        if (includeTime)
        {
            var hour = DateTime.Now.Hour;
            var timeOfDay = hour switch
            {
                < 12 => "morning",
                < 17 => "afternoon",
                _ => "evening"
            };
            userMessage += $". It's currently {timeOfDay}.";
        }
        
        messages.Add(CreateUserMessage(userMessage));
        
        // Example assistant response
        var assistantResponse = style.ToLower() switch
        {
            "formal" => $"Good day, {name}. It is my pleasure to assist you today. How may I be of service?",
            "enthusiastic" => $"Hello {name}! 🎉 Welcome! I'm absolutely thrilled to meet you! How can I help make your day amazing?",
            _ => $"Hello {name}! Welcome. I'm here to help you with whatever you need."
        };
        
        if (includeTime)
        {
            var hour = DateTime.Now.Hour;
            var greeting = hour switch
            {
                < 12 => "Good morning",
                < 17 => "Good afternoon",
                _ => "Good evening"
            };
            assistantResponse = assistantResponse.Replace("Good day", greeting).Replace("Hello", greeting);
        }
        
        messages.Add(CreateAssistantMessage(assistantResponse));
        
        return new GetPromptResult
        {
            Description = $"Personalized {style} greeting for {name}",
            Messages = messages
        };
    }
}