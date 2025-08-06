using COA.Mcp.Framework;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Models;
using COA.Mcp.Protocol;

namespace HttpMcpServer;

/// <summary>
/// Example weather tool for demonstration.
/// </summary>
public class WeatherTool : McpToolBase<WeatherParams, WeatherResult>
{
    public override string Name => "get_weather";
    public override string Description => "Get current weather for a location";
    public override ToolCategory Category => ToolCategory.Query;

    protected override async Task<WeatherResult> ExecuteInternalAsync(
        WeatherParams parameters, 
        CancellationToken cancellationToken)
    {
        // Simulate async weather API call
        await Task.Delay(100, cancellationToken);
        
        // Mock weather data
        var random = new Random();
        var temperature = random.Next(0, 40);
        var conditions = new[] { "Sunny", "Cloudy", "Rainy", "Partly Cloudy", "Snowy" };
        var condition = conditions[random.Next(conditions.Length)];
        
        return new WeatherResult
        {
            Success = true,
            Location = parameters.Location,
            Temperature = temperature,
            Condition = condition,
            Humidity = random.Next(30, 90),
            WindSpeed = random.Next(0, 30),
            Unit = "Celsius"
        };
    }

}

public class WeatherParams
{
    public string Location { get; set; } = string.Empty;
}

public class WeatherResult : ToolResultBase
{
    public override string Operation => "get_weather";
    
    public string Location { get; set; } = string.Empty;
    public int Temperature { get; set; }
    public string Condition { get; set; } = string.Empty;
    public int Humidity { get; set; }
    public int WindSpeed { get; set; }
    public string Unit { get; set; } = string.Empty;
    
    public override string ToString()
    {
        return $"Weather in {Location}: {Temperature}°{Unit}, {Condition}, Humidity: {Humidity}%, Wind: {WindSpeed} km/h";
    }
}