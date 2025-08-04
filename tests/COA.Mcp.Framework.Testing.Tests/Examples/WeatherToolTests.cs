using COA.Mcp.Framework;
using COA.Mcp.Framework.Attributes;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Testing.Assertions;
using COA.Mcp.Framework.Testing.Base;
using COA.Mcp.Framework.Testing.Builders;
using COA.Mcp.Framework.Testing.Mocks;
using COA.Mcp.Framework.TokenOptimization.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace COA.Mcp.Framework.Testing.Tests.Examples
{
    /// <summary>
    /// Example test demonstrating how to use ToolTestBase for testing MCP tools.
    /// </summary>
    [TestFixture]
    public class WeatherToolTests : ToolTestBase<WeatherTool>
    {
        private Mock<IWeatherService> _weatherServiceMock = null!;

        protected override WeatherTool CreateTool()
        {
            // Create mocks
            _weatherServiceMock = CreateMock<IWeatherService>();

            // Create tool with dependencies
            return new WeatherTool(_weatherServiceMock.Object, ToolLoggerMock.Object);
        }

        [Test]
        public async Task GetWeather_WithValidLocation_ReturnsWeatherData()
        {
            // Arrange - Use parameter builder
            var parameters = ToolParameterBuilder.Create<WeatherParams>()
                .WithLocation("Seattle")
                .WithMaxResults(5)
                .Build();

            var expectedWeather = new WeatherData
            {
                Location = "Seattle",
                Temperature = 72,
                Conditions = "Partly Cloudy",
                Forecast = TestData.SmallCollection(i => new ForecastDay
                {
                    Date = DateTime.Today.AddDays(i),
                    High = 75 + i,
                    Low = 60 + i,
                    Conditions = "Sunny"
                })
            };

            _weatherServiceMock
                .Setup(x => x.GetWeatherAsync("Seattle", 5))
                .ReturnsAsync(expectedWeather);

            // Act
            var result = await ExecuteToolAsync<AIOptimizedResponse>(
                () => Tool.ExecuteAsync(parameters));

            // Assert - Use fluent assertions
            result.Should().BeSuccessful()
                .And.CompleteWithinMs(100);

            result.Result.Should().BeSuccessful()
                .And.HaveInsightCount(3, 5)
                .And.NotBeTruncated();

            // Verify logs
            VerifyLog(LogLevel.Information, "Fetching weather for Seattle");
            VerifyNoErrors();
        }

        [Test]
        public async Task GetWeather_WithInvalidLocation_ReturnsError()
        {
            // Arrange
            var parameters = new WeatherParams { Location = "" };

            // Act & Assert
            Assert.ThrowsAsync<ArgumentException>(
                async () => await Tool.ExecuteAsync(parameters));
        }

        [Test]
        public async Task GetWeather_WithLargeForecast_AppliesTokenLimits()
        {
            // Arrange - Generate large dataset
            var generator = new TestDataGenerator();
            var parameters = new WeatherParams
            {
                Location = "Seattle",
                MaxResults = 100
            };

            var largeWeatherData = new WeatherData
            {
                Location = "Seattle",
                Temperature = 72,
                Conditions = "Variable",
                Forecast = generator.GenerateCollection(100, i => new ForecastDay
                {
                    Date = DateTime.Today.AddDays(i),
                    High = 70 + (i % 20),
                    Low = 50 + (i % 15),
                    Conditions = generator.GenerateString(50),
                    Description = generator.GenerateLorem(200)
                })
            };

            _weatherServiceMock
                .Setup(x => x.GetWeatherAsync("Seattle", 100))
                .ReturnsAsync(largeWeatherData);

            // Act
            var result = await ExecuteToolAsync<AIOptimizedResponse>(
                () => Tool.ExecuteAsync(parameters));

            // Assert - Check token optimization
            result.Should().BeSuccessful();
            result.Result.Should().BeTruncated()
                .And.HaveResourceUri()
                .And.HaveTruncationMessage();

            // Verify token usage
            result.Result.Should().HaveTokenCountLessThan(10000);
        }
    }

    // Example tool implementation
    [McpServerToolType]
    public class WeatherTool : ITool
    {
        private readonly IWeatherService _weatherService;
        private readonly ILogger<WeatherTool> _logger;

        public string ToolName => "get_weather";
        public string Description => "Gets weather information for a location";
        public ToolCategory Category => ToolCategory.Query;

        public WeatherTool(IWeatherService weatherService, ILogger<WeatherTool> logger)
        {
            _weatherService = weatherService;
            _logger = logger;
        }

        [McpServerTool("get_weather")]
        public virtual async Task<object> ExecuteAsync(object parameters)
        {
            var weatherParams = (WeatherParams)parameters;

            if (string.IsNullOrEmpty(weatherParams.Location))
            {
                throw new ArgumentException("Location is required");
            }

            _logger.LogInformation("Fetching weather for {Location}", weatherParams.Location);

            var weather = await _weatherService.GetWeatherAsync(
                weatherParams.Location,
                weatherParams.MaxResults ?? 5);

            // Build response with token awareness
            var builder = new ResponseBuilder()
                .WithSummary($"Weather for {weather.Location}")
                .WithResults(weather)
                .WithInsights(
                    $"Current temperature: {weather.Temperature}°F",
                    $"Conditions: {weather.Conditions}",
                    $"{weather.Forecast.Count} day forecast available")
                .WithAction("get_extended_forecast", "Get 10-day forecast",
                    new { location = weather.Location, days = 10 });

            // Apply truncation if needed
            if (weather.Forecast.Count > 10)
            {
                builder.WithTruncation($"weather://{weather.Location}/full-forecast");
            }

            return builder.Build();
        }
    }

    // Supporting classes
    public class WeatherParams
    {
        public string? Location { get; set; }
        public int? MaxResults { get; set; }
    }

    public interface IWeatherService
    {
        Task<WeatherData> GetWeatherAsync(string location, int forecastDays);
    }

    public class WeatherData
    {
        public string Location { get; set; } = "";
        public int Temperature { get; set; }
        public string Conditions { get; set; } = "";
        public List<ForecastDay> Forecast { get; set; } = new();
    }

    public class ForecastDay
    {
        public DateTime Date { get; set; }
        public int High { get; set; }
        public int Low { get; set; }
        public string Conditions { get; set; } = "";
        public string? Description { get; set; }
    }
}