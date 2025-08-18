using COA.Mcp.Framework;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Registration;
using COA.Mcp.Framework.Testing.Assertions;
using COA.Mcp.Framework.Testing.Base;
using COA.Mcp.Framework.Testing.Builders;
using COA.Mcp.Framework.TokenOptimization;
using COA.Mcp.Framework.TokenOptimization.Models;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace COA.Mcp.Framework.Testing.Tests.Examples
{
    /// <summary>
    /// Example integration tests demonstrating IntegrationTestBase usage.
    /// </summary>
    [TestFixture]
    public class McpServerIntegrationTests : IntegrationTestBase
    {
        protected override void ConfigureHostServices(HostBuilderContext context, IServiceCollection services)
        {
            // Configure MCP framework (simplified for example)
            // In real implementation, this would use the actual extension method
            services.AddSingleton<McpToolRegistry>();

            // Add services
            services.AddSingleton<IWeatherService, MockWeatherService>();
            
            // Register tools
            services.AddSingleton<WeatherTool>();
            
            // Register a startup service to discover tools after DI is built
            services.AddHostedService<ToolRegistrationService>();
        }

        [Test]
        public async Task McpServer_Startup_RegistersAllTools()
        {
            // Arrange
            var toolRegistry = GetRequiredService<McpToolRegistry>();

            // Act - Wait for the hosted service to complete registration
            var registered = await WaitForConditionAsync(
                () => toolRegistry.GetAllTools().Any(),
                TimeSpan.FromSeconds(2));
            
            var tools = toolRegistry.GetAllTools();

            // Assert
            registered.Should().BeTrue("tools should be registered within timeout");
            tools.Should().NotBeEmpty();
            tools.Should().ContainToolNamed("get_weather");
        }

        [Test]
        public async Task McpServer_ExecuteTool_Success()
        {
            // Arrange
            var server = CreateTestServer();
            var parameters = new { location = "Seattle", maxResults = 3 };

            // Act
            var result = await server.CallToolAsync("get_weather", parameters);

            // Assert
            result.Should().NotBeNull();
            
            // Use AI response assertion if result is an AI response
#pragma warning disable CS0618 // Type or member is obsolete - testing backward compatibility
            if (result is AIOptimizedResponse aiResponse)
#pragma warning restore CS0618 // Type or member is obsolete
            {
                aiResponse.Should().BeSuccessful();
            }
        }

        [Test]
        public async Task McpServer_ConcurrentRequests_HandlesLoadCorrectly()
        {
            // Arrange
            var scenario = ScenarioBuilder.PerformanceScenario("McpServer", 100)
                .AddStep("Prepare concurrent requests", ctx =>
                {
                    ctx["requests"] = Enumerable.Range(0, 10)
                        .Select(i => new { location = $"City{i}", maxResults = 5 })
                        .ToArray();
                })
                .AddAsyncStep("Execute concurrent requests", async ctx =>
                {
                    var server = CreateTestServer();
                    var requests = (object[])ctx["requests"];
                    
                    var tasks = requests.Select(req => 
                        server.CallToolAsync("get_weather", req));
                    
                    var results = await Task.WhenAll(tasks);
                    ctx["results"] = results;
                })
                .AddStepWithValidation("Verify all succeeded",
                    ctx => { },
                    ctx =>
                    {
                        var results = (object[])ctx["results"];
                        results.Should().OnlyContain(r => r != null);
                    })
                .Build();

            // Act & Assert
            var result = await scenario.ExecuteAsync();
            result.Success.Should().BeTrue();
        }

        [Test]
        public async Task McpServer_TokenLimits_RespectedAcrossTools()
        {
            // Arrange
            var server = CreateTestServer();
            var generator = new TestDataGenerator();
            
            // Generate request that would exceed token limits
            var largeLocation = generator.GenerateStringWithTokens(5000);
            var parameters = new { location = largeLocation, maxResults = 100 };

            // Act
            var result = await server.CallToolAsync("get_weather", parameters);

            // Assert
            result.Should().NotBeNull();
            result.HaveTokenCountLessThan(10000);
        }

        [Test]
        public async Task McpServer_Configuration_AppliesCorrectly()
        {
            // Assert configuration was applied
            var config = Configuration["Logging:LogLevel:Default"];
            config.Should().Be("Debug");

            // Verify services are registered
            var weatherService = GetService<IWeatherService>();
            weatherService.Should().NotBeNull()
                .And.BeOfType<MockWeatherService>();
        }

        // Mock implementation for testing
        private class MockWeatherService : IWeatherService
        {
            public Task<WeatherData> GetWeatherAsync(string location, int forecastDays)
            {
                var generator = new TestDataGenerator();
                
                return Task.FromResult(new WeatherData
                {
                    Location = location,
                    Temperature = generator._random.Next(50, 90),
                    Conditions = "Test Conditions",
                    Forecast = generator.GenerateCollection(forecastDays, i => new ForecastDay
                    {
                        Date = DateTime.Today.AddDays(i),
                        High = 75 + i,
                        Low = 60 + i,
                        Conditions = "Sunny"
                    })
                });
            }
        }
    }
    
    /// <summary>
    /// Service to register tools after DI container is built.
    /// </summary>
    internal class ToolRegistrationService : IHostedService
    {
        private readonly McpToolRegistry _toolRegistry;
        private readonly IServiceProvider _serviceProvider;

        public ToolRegistrationService(
            McpToolRegistry toolRegistry,
            IServiceProvider serviceProvider)
        {
            _toolRegistry = toolRegistry;
            _serviceProvider = serviceProvider;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Get the WeatherTool instance from DI and register it
            var weatherTool = _serviceProvider.GetService<WeatherTool>();
            if (weatherTool != null)
            {
                _toolRegistry.RegisterTool(weatherTool);
            }
            
            // Alternatively, use auto-discovery for the assembly
            // _toolRegistry.DiscoverAndRegisterTools(typeof(WeatherTool).Assembly);
            
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}