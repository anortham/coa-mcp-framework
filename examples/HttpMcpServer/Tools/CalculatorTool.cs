using COA.Mcp.Framework;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Models;
using COA.Mcp.Protocol;

namespace HttpMcpServer;

/// <summary>
/// Example calculator tool for demonstration.
/// </summary>
public class CalculatorTool : McpToolBase<CalculatorParams, CalculatorResult>
{
    public override string Name => "calculate";
    public override string Description => "Perform basic mathematical calculations";
    public override ToolCategory Category => ToolCategory.Utility;

    protected override Task<CalculatorResult> ExecuteInternalAsync(
        CalculatorParams parameters, 
        CancellationToken cancellationToken)
    {
        try
        {
            double result = parameters.Operation.ToLower() switch
            {
                "add" or "+" => parameters.A + parameters.B,
                "subtract" or "-" => parameters.A - parameters.B,
                "multiply" or "*" => parameters.A * parameters.B,
                "divide" or "/" => parameters.B != 0 ? parameters.A / parameters.B : 
                    throw new DivideByZeroException("Cannot divide by zero"),
                "power" or "^" => Math.Pow(parameters.A, parameters.B),
                "modulo" or "%" => parameters.A % parameters.B,
                _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
            };

            return Task.FromResult(new CalculatorResult
            {
                Success = true,
                OperationType = parameters.Operation,
                A = parameters.A,
                B = parameters.B,
                Result = result,
                Expression = $"{parameters.A} {parameters.Operation} {parameters.B} = {result}"
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new CalculatorResult
            {
                Success = false,
                OperationType = parameters.Operation,
                A = parameters.A,
                B = parameters.B,
                Error = new ErrorInfo
                {
                    Code = "CALC_ERROR",
                    Message = ex.Message
                }
            });
        }
    }

}

public class CalculatorParams
{
    public string Operation { get; set; } = string.Empty;
    public double A { get; set; }
    public double B { get; set; }
}

public class CalculatorResult : ToolResultBase
{
    public override string Operation => "calculate";
    public string OperationType { get; set; } = string.Empty;
    public double A { get; set; }
    public double B { get; set; }
    public double Result { get; set; }
    public string Expression { get; set; } = string.Empty;
    
    public override string ToString()
    {
        return Success ? Expression : $"Calculation failed: {Error?.Message}";
    }
}