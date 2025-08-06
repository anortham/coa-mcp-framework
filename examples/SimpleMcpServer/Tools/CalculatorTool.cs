using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Models;

namespace SimpleMcpServer.Tools;

/// <summary>
/// A simple calculator tool that performs basic arithmetic operations.
/// </summary>
public class CalculatorTool : McpToolBase<CalculatorParameters, CalculatorResult>
{
    public override string Name => "calculator";
    public override string Description => "Performs basic arithmetic operations (add, subtract, multiply, divide)";
    public override ToolCategory Category => ToolCategory.Utility;

    protected override async Task<CalculatorResult> ExecuteInternalAsync(CalculatorParameters parameters, CancellationToken cancellationToken)
    {
        // Validate inputs
        ValidateRequired(parameters.Operation, nameof(parameters.Operation));
        ValidateRequired(parameters.A, nameof(parameters.A));
        ValidateRequired(parameters.B, nameof(parameters.B));

        // Extract values after validation (we know they're not null)
        var a = parameters.A!.Value;
        var b = parameters.B!.Value;
        
        double result;
        string expression;

        switch (parameters.Operation.ToLower())
        {
            case "add":
            case "+":
                result = a + b;
                expression = $"{a} + {b}";
                break;
                
            case "subtract":
            case "-":
                result = a - b;
                expression = $"{a} - {b}";
                break;
                
            case "multiply":
            case "*":
                result = a * b;
                expression = $"{a} × {b}";
                break;
                
            case "divide":
            case "/":
                if (Math.Abs(b) < 0.0000001)
                {
                    return new CalculatorResult
                    {
                        Success = false,
                        Error = new ErrorInfo
                        {
                            Code = "DIVISION_BY_ZERO",
                            Message = "Division by zero is not allowed"
                        }
                    };
                }
                result = a / b;
                expression = $"{a} ÷ {b}";
                break;
                
            default:
                return new CalculatorResult
                {
                    Success = false,
                    Error = new ErrorInfo
                    {
                        Code = "UNKNOWN_OPERATION",
                        Message = $"Unknown operation: {parameters.Operation}. Supported operations: add, subtract, multiply, divide"
                    }
                };
        }

        await Task.CompletedTask; // Simulate async work

        return new CalculatorResult
        {
            Success = true,
            Result = result,
            Expression = expression,
            Calculation = $"{expression} = {result}"
        };
    }
}

public class CalculatorParameters
{
    [Required]
    [Description("The operation to perform: add, subtract, multiply, or divide")]
    public string Operation { get; set; } = string.Empty;

    [Required]
    [Description("The first operand")]
    public double? A { get; set; }  // Nullable to distinguish between unset and 0

    [Required]
    [Description("The second operand")]
    public double? B { get; set; }  // Nullable to distinguish between unset and 0
}

public class CalculatorResult : ToolResultBase
{
    public override string Operation => "calculator";
    public double? Result { get; set; }
    public string? Expression { get; set; }
    public string? Calculation { get; set; }
}