using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.Examples;

/// <summary>
/// Example showing how to create a strongly-typed MCP tool using the framework.
/// This demonstrates compile-time type safety, automatic validation, and clean separation of concerns.
/// </summary>
public class WeatherExample
{
    public static async Task Main(string[] args)
    {
        // Method 1: Simple standalone server
        await RunSimpleServerAsync();
        
        // Method 2: With IHost integration (production-ready)
        // await RunWithHostAsync(args);
    }
    
    /// <summary>
    /// Simple standalone server example.
    /// </summary>
    private static async Task RunSimpleServerAsync()
    {
        var server = McpServer.CreateBuilder()
            .WithServerInfo("Weather MCP Server", "1.0.0")
            .AddService<IWeatherService, MockWeatherService>(ServiceLifetime.Singleton)
            .RegisterToolType<WeatherTool>()
            .DiscoverTools() // Discovers tools from calling assembly
            .Build();
            
        await server.StartAsync(CancellationToken.None);
        
        // Keep running until Ctrl+C
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) => 
        {
            e.Cancel = true;
            cts.Cancel();
        };
        
        await Task.Delay(Timeout.Infinite, cts.Token);
        await server.StopAsync(CancellationToken.None);
    }
    
    /// <summary>
    /// Production-ready example with IHost integration.
    /// </summary>
    private static async Task RunWithHostAsync(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole(options =>
                {
                    // MCP requires stderr for logging
                    options.LogToStandardErrorThreshold = LogLevel.Trace;
                });
            })
            .UseMcpServer(builder =>
            {
                builder
                    .WithServerInfo("Weather MCP Server", "1.0.0")
                    .AddService<IWeatherService, MockWeatherService>(ServiceLifetime.Singleton)
                    .RegisterToolType<WeatherTool>()
                    .DiscoverTools();
            })
            .Build();
            
        await host.RunAsync();
    }
}

/// <summary>
/// Strongly-typed parameters for the weather tool.
/// Uses data annotations for automatic validation.
/// </summary>
public class WeatherParameters
{
    /// <summary>
    /// The location to get weather for.
    /// </summary>
    [Required(ErrorMessage = "Location is required")]
    [Description("City name or coordinates to get weather for")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Location must be between 2 and 100 characters")]
    public string Location { get; set; } = string.Empty;
    
    /// <summary>
    /// Number of forecast days to return.
    /// </summary>
    [Description("Number of forecast days (1-10)")]
    [Range(1, 10, ErrorMessage = "Forecast days must be between 1 and 10")]
    public int ForecastDays { get; set; } = 3;
    
    /// <summary>
    /// Temperature unit preference.
    /// </summary>
    [Description("Temperature unit: Celsius or Fahrenheit")]
    public TemperatureUnit Unit { get; set; } = TemperatureUnit.Celsius;
}

/// <summary>
/// Strongly-typed result for the weather tool.
/// </summary>
public class WeatherResult
{
    public string Location { get; set; } = string.Empty;
    public CurrentWeather Current { get; set; } = new();
    public List<ForecastDay> Forecast { get; set; } = new();
    public WeatherInsights Insights { get; set; } = new();
}

public class CurrentWeather
{
    public double Temperature { get; set; }
    public string Condition { get; set; } = string.Empty;
    public double Humidity { get; set; }
    public double WindSpeed { get; set; }
}

public class ForecastDay
{
    public DateTime Date { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public string Condition { get; set; } = string.Empty;
    public double ChanceOfRain { get; set; }
}

public class WeatherInsights
{
    public string Summary { get; set; } = string.Empty;
    public List<string> Recommendations { get; set; } = new();
    public string Trend { get; set; } = string.Empty;
}

public enum TemperatureUnit
{
    Celsius,
    Fahrenheit
}

/// <summary>
/// Strongly-typed weather tool implementation.
/// Demonstrates compile-time type safety and automatic validation.
/// </summary>
public class WeatherTool : McpToolBase<WeatherParameters, WeatherResult>
{
    private readonly IWeatherService _weatherService;
    private readonly ILogger<WeatherTool> _logger;
    
    public WeatherTool(IWeatherService weatherService, ILogger<WeatherTool> logger)
        : base(null, logger)
    {
        _weatherService = weatherService;
        _logger = logger;
    }
    
    public override string Name => "get_weather";
    
    public override string Description => @"Gets current weather and forecast for a location.
Returns weather data including temperature, conditions, humidity, wind, and multi-day forecast.
Provides AI-friendly insights and recommendations based on weather patterns.
Prerequisites: None.
Use cases: Weather queries, travel planning, outdoor activity decisions.";
    
    public override ToolCategory Category => ToolCategory.Query;
    
    protected override async Task<WeatherResult> ExecuteInternalAsync(
        WeatherParameters parameters, 
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Getting weather for {Location} with {Days} day forecast", 
            parameters.Location, parameters.ForecastDays);
        
        // The parameters are already validated by the base class!
        // No need for manual validation or JSON parsing
        
        var weatherData = await _weatherService.GetWeatherAsync(
            parameters.Location, 
            parameters.ForecastDays,
            parameters.Unit,
            cancellationToken);
        
        // Build strongly-typed result
        var result = new WeatherResult
        {
            Location = parameters.Location,
            Current = new CurrentWeather
            {
                Temperature = weatherData.Temperature,
                Condition = weatherData.Condition,
                Humidity = weatherData.Humidity,
                WindSpeed = weatherData.WindSpeed
            },
            Forecast = weatherData.Forecast.Select(f => new ForecastDay
            {
                Date = f.Date,
                High = f.High,
                Low = f.Low,
                Condition = f.Condition,
                ChanceOfRain = f.ChanceOfRain
            }).ToList(),
            Insights = GenerateInsights(weatherData)
        };
        
        return result;
    }
    
    protected override int EstimateTokenUsage()
    {
        // Provide accurate token estimation for this tool
        // Base estimate + forecast days * tokens per day
        return 500 + (5 * 100); // Roughly 1000 tokens for full response
    }
    
    private WeatherInsights GenerateInsights(WeatherData data)
    {
        var insights = new WeatherInsights
        {
            Summary = $"Current conditions in {data.Location} are {data.Condition.ToLower()} " +
                     $"with a temperature of {data.Temperature}°.",
            Recommendations = new List<string>()
        };
        
        // Add weather-based recommendations
        if (data.Temperature > 30)
        {
            insights.Recommendations.Add("Stay hydrated and seek shade during peak hours");
        }
        else if (data.Temperature < 10)
        {
            insights.Recommendations.Add("Dress warmly in layers");
        }
        
        if (data.Forecast.Any(f => f.ChanceOfRain > 50))
        {
            insights.Recommendations.Add("Bring an umbrella for the coming days");
        }
        
        // Determine trend
        var temps = data.Forecast.Select(f => f.High).ToList();
        if (temps.Count > 1)
        {
            var firstHalf = temps.Take(temps.Count / 2).Average();
            var secondHalf = temps.Skip(temps.Count / 2).Average();
            insights.Trend = secondHalf > firstHalf ? "Warming" : "Cooling";
        }
        
        return insights;
    }
}

/// <summary>
/// Weather service interface.
/// </summary>
public interface IWeatherService
{
    Task<WeatherData> GetWeatherAsync(
        string location, 
        int forecastDays, 
        TemperatureUnit unit,
        CancellationToken cancellationToken);
}

/// <summary>
/// Mock weather service for demonstration.
/// </summary>
public class MockWeatherService : IWeatherService
{
    private readonly Random _random = new();
    
    public Task<WeatherData> GetWeatherAsync(
        string location, 
        int forecastDays, 
        TemperatureUnit unit,
        CancellationToken cancellationToken)
    {
        var data = new WeatherData
        {
            Location = location,
            Temperature = _random.Next(15, 30),
            Condition = GetRandomCondition(),
            Humidity = _random.Next(40, 80),
            WindSpeed = _random.Next(5, 25),
            Forecast = new List<WeatherForecast>()
        };
        
        // Generate forecast
        for (int i = 1; i <= forecastDays; i++)
        {
            data.Forecast.Add(new WeatherForecast
            {
                Date = DateTime.Today.AddDays(i),
                High = _random.Next(20, 35),
                Low = _random.Next(10, 20),
                Condition = GetRandomCondition(),
                ChanceOfRain = _random.Next(0, 100)
            });
        }
        
        // Convert units if needed
        if (unit == TemperatureUnit.Fahrenheit)
        {
            data.Temperature = CelsiusToFahrenheit(data.Temperature);
            foreach (var forecast in data.Forecast)
            {
                forecast.High = CelsiusToFahrenheit(forecast.High);
                forecast.Low = CelsiusToFahrenheit(forecast.Low);
            }
        }
        
        return Task.FromResult(data);
    }
    
    private string GetRandomCondition()
    {
        var conditions = new[] { "Sunny", "Cloudy", "Partly Cloudy", "Rainy", "Stormy" };
        return conditions[_random.Next(conditions.Length)];
    }
    
    private double CelsiusToFahrenheit(double celsius)
    {
        return Math.Round(celsius * 9 / 5 + 32, 1);
    }
}

/// <summary>
/// Internal weather data model.
/// </summary>
public class WeatherData
{
    public string Location { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public string Condition { get; set; } = string.Empty;
    public double Humidity { get; set; }
    public double WindSpeed { get; set; }
    public List<WeatherForecast> Forecast { get; set; } = new();
}

public class WeatherForecast
{
    public DateTime Date { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public string Condition { get; set; } = string.Empty;
    public double ChanceOfRain { get; set; }
}