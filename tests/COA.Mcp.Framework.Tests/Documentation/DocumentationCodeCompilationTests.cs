using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework.Registration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using RangeAttribute = System.ComponentModel.DataAnnotations.RangeAttribute;

namespace COA.Mcp.Framework.Tests.Documentation
{
    /// <summary>
    /// Tests that verify the code examples in our documentation actually compile.
    /// This ensures our documentation remains accurate.
    /// </summary>
    [TestFixture]
    public class DocumentationCodeCompilationTests
    {
        [Test]
        public void MainReadme_WeatherToolExample_Compiles()
        {
            // This test verifies that the WeatherTool example from the main README compiles
            var tool = new WeatherTool();
            Assert.That(tool.Name, Is.EqualTo("get_weather"));
            Assert.That(tool.Description, Is.EqualTo("Get weather for a location"));
            Assert.That(tool.Category, Is.EqualTo(ToolCategory.Query));
        }

        [Test]
        public void MainReadme_ServiceRegistration_Compiles()
        {
            // This test verifies that the service registration example compiles
            var services = new ServiceCollection();
            
            // This confirms AddMcpFramework is the correct method
            services.AddMcpFramework(options =>
            {
                options.DiscoverToolsFromAssembly(typeof(WeatherTool).Assembly);
            });
            
            Assert.That(services, Is.Not.Null);
        }

        [Test]
        public async Task MainReadme_ExecuteInternalAsync_IsCorrectMethod()
        {
            // This test verifies ExecuteInternalAsync is the correct method to override
            var tool = new WeatherTool();
            var parameters = new WeatherParameters
            {
                Location = "Seattle",
                ForecastDays = 3
            };
            
            // This would fail to compile if ExecuteInternalAsync wasn't the right method
            var result = await tool.TestableExecuteInternalAsync(parameters, CancellationToken.None);
            
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Location, Is.EqualTo("Seattle"));
        }

        // Example classes from documentation
        public class WeatherParameters
        {
            [Required]
            [System.ComponentModel.Description("City name or coordinates")]
            public string Location { get; set; }
            
            [Range(1, 10)]
            [System.ComponentModel.Description("Number of forecast days (1-10)")]
            public int ForecastDays { get; set; } = 3;
        }

        public class WeatherResult : ToolResultBase
        {
            public override string Operation => "get_weather";
            public string Location { get; set; }
            public double Temperature { get; set; }
            public string Condition { get; set; }
        }

        public class WeatherTool : McpToolBase<WeatherParameters, WeatherResult>
        {
            public override string Name => "get_weather";
            public override string Description => "Get weather for a location";
            public override ToolCategory Category => ToolCategory.Query;
            
            protected override async Task<WeatherResult> ExecuteInternalAsync(
                WeatherParameters parameters,
                CancellationToken cancellationToken)
            {
                // This confirms ExecuteInternalAsync is the correct method name
                await Task.Delay(1, cancellationToken); // Simulate async work
                
                return new WeatherResult
                {
                    Success = true,
                    Location = parameters.Location,
                    Temperature = 72.5,
                    Condition = "Sunny"
                };
            }
            
            // Expose for testing
            public Task<WeatherResult> TestableExecuteInternalAsync(
                WeatherParameters parameters,
                CancellationToken cancellationToken)
            {
                return ExecuteInternalAsync(parameters, cancellationToken);
            }
        }
    }
}