using COA.Mcp.Framework.Server;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using System.ComponentModel.DataAnnotations;

// Step 2: Multiple tools with validation
// This example shows how to build a server with multiple tools and proper validation

// Calculator tool with validation
public class CalculatorTool : McpToolBase<CalculatorParams, CalculatorResult>
{
    public override string Name => "calculator";
    public override string Description => "Performs basic arithmetic operations";
    
    protected override async Task<CalculatorResult> ExecuteInternalAsync(
        CalculatorParams parameters, CancellationToken cancellationToken)
    {
        // Validate required parameters (throws if missing/null)
        ValidateRequired(parameters.Operation, nameof(parameters.Operation));
        ValidateRequired(parameters.A, nameof(parameters.A));
        ValidateRequired(parameters.B, nameof(parameters.B));
        
        await Task.CompletedTask; // Simulate async work
        
        var a = parameters.A!.Value;
        var b = parameters.B!.Value;
        var op = parameters.Operation!.ToLower();
        
        double result = op switch
        {
            "add" or "+" => a + b,
            "subtract" or "-" => a - b,
            "multiply" or "*" => a * b,
            "divide" or "/" => b != 0 ? a / b : throw new InvalidOperationException("Division by zero"),
            _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
        };
        
        return new CalculatorResult
        {
            Success = true,
            Result = result,
            Expression = $"{a} {op} {b} = {result}"
        };
    }
}

// Text processing tool
public class TextTool : McpToolBase<TextParams, TextResult>
{
    public override string Name => "text";
    public override string Description => "Text processing operations";
    
    protected override async Task<TextResult> ExecuteInternalAsync(
        TextParams parameters, CancellationToken cancellationToken)
    {
        ValidateRequired(parameters.Text, nameof(parameters.Text));
        ValidateRequired(parameters.Operation, nameof(parameters.Operation));
        
        await Task.CompletedTask;
        
        var text = parameters.Text!;
        var result = parameters.Operation!.ToLower() switch
        {
            "uppercase" => text.ToUpper(),
            "lowercase" => text.ToLower(),
            "reverse" => new string(text.Reverse().ToArray()),
            "length" => text.Length.ToString(),
            "words" => text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length.ToString(),
            _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
        };
        
        return new TextResult
        {
            Success = true,
            Input = text,
            TextOperation = parameters.Operation,
            Output = result
        };
    }
}

// Random number generator
public class RandomTool : McpToolBase<RandomParams, RandomResult>
{
    private static readonly Random _random = new();
    
    public override string Name => "random";
    public override string Description => "Generates random numbers";
    
    protected override async Task<RandomResult> ExecuteInternalAsync(
        RandomParams parameters, CancellationToken cancellationToken)
    {
        // Use validation helpers with defaults
        var min = parameters.Min ?? 1;
        var max = parameters.Max ?? 100;
        var count = parameters.Count ?? 1;
        
        // Validate ranges
        ValidateRange(min, int.MinValue, int.MaxValue, nameof(parameters.Min));
        ValidateRange(max, int.MinValue, int.MaxValue, nameof(parameters.Max));
        ValidateRange(count, 1, 1000, nameof(parameters.Count));
        
        if (min >= max)
            throw new ArgumentException("Min must be less than max");
        
        await Task.CompletedTask;
        
        var numbers = new List<int>();
        for (int i = 0; i < count; i++)
        {
            numbers.Add(_random.Next(min, max + 1));
        }
        
        return new RandomResult
        {
            Success = true,
            Numbers = numbers,
            Count = numbers.Count,
            Min = min,
            Max = max
        };
    }
}

// Parameter classes
public class CalculatorParams
{
    [Required]
    public string? Operation { get; set; } // add, subtract, multiply, divide
    
    [Required]
    public double? A { get; set; }
    
    [Required]
    public double? B { get; set; }
}

public class TextParams
{
    [Required]
    public string? Text { get; set; }
    
    [Required]
    public string? Operation { get; set; } // uppercase, lowercase, reverse, length, words
}

public class RandomParams
{
    public int? Min { get; set; } // Optional - defaults to 1
    public int? Max { get; set; } // Optional - defaults to 100
    public int? Count { get; set; } // Optional - defaults to 1
}

// Result classes
public class CalculatorResult : ToolResultBase
{
    public override string Operation => "calculator";
    public double Result { get; set; }
    public string Expression { get; set; } = "";
}

public class TextResult : ToolResultBase
{
    public override string Operation => "text";
    public string Input { get; set; } = "";
    public string TextOperation { get; set; } = "";
    public string Output { get; set; } = "";
}

public class RandomResult : ToolResultBase
{
    public override string Operation => "random";
    public List<int> Numbers { get; set; } = new();
    public int Count { get; set; }
    public int Min { get; set; }
    public int Max { get; set; }
}

// Program entry point
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🚀 Basic Tools MCP Server starting...");
        
        var builder = new McpServerBuilder()
            .WithServerInfo("Basic Tools MCP Server", "1.0.0");
        
        // Register all our tools
        builder.RegisterToolType<CalculatorTool>();
        builder.RegisterToolType<TextTool>();
        builder.RegisterToolType<RandomTool>();
        
        Console.WriteLine("📝 Registered tools:");
        Console.WriteLine("  - calculator: Basic arithmetic operations");
        Console.WriteLine("  - text: Text processing operations");  
        Console.WriteLine("  - random: Random number generation");
        Console.WriteLine();
        Console.WriteLine("✅ Server ready! Press Ctrl+C to stop");
        
        await builder.RunAsync();
    }
}