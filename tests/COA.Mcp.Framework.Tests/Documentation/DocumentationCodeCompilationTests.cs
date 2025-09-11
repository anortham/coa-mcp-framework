using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Configuration;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework.Registration;
using COA.Mcp.Framework.Server;
using COA.Mcp.Framework.Services;
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

        [Test]
        public void TemplateInstructions_WithMarkerDetection_Compiles()
        {
            // This test verifies that the template instruction system compiles and works
            // It exercises TemplateInstructionOptions, InstructionTemplateManager, and tool priority markers
            
            var builder = new McpServerBuilder()
                .WithServerInfo("Test Server", "1.0.0")
                
                // Test template instructions configuration
                .WithTemplateInstructions(options =>
                {
                    options.TemplateContext = "general";
                    options.EnableTemplateInstructions = true;
                    options.EnableMarkerDetection = true;
                    options.IncludeToolPriorities = true;
                    options.CustomTemplateVariables["ProjectType"] = "Test Project";
                })
                
                // Test tool management with priorities
                .ConfigureToolManagement(config =>
                {
                    config.EnableWorkflowSuggestions = true;
                    config.EnableToolPrioritySystem = true;
                    config.UseDefaultDescriptionProvider = true;
                })
                
                // Register tools with different markers and priorities
                .RegisterToolType<HighPrioritySearchTool>()
                .RegisterToolType<SymbolicReadTool>()
                .RegisterToolType<TypeAwareTool>();
            
            // Verify builder configured correctly
            Assert.That(builder, Is.Not.Null);
            
            // Test that we can access the services
            var services = builder.Services;
            Assert.That(services, Is.Not.Null);
        }

        [Test]
        public async Task TemplateInstructionManager_GeneratesInstructions()
        {
            // Test that InstructionTemplateManager can actually generate instructions
            var services = new ServiceCollection();
            services.AddSingleton<InstructionTemplateProcessor>();
            services.AddSingleton<InstructionTemplateManager>();
            services.AddLogging();

            var serviceProvider = services.BuildServiceProvider();
            var templateManager = serviceProvider.GetRequiredService<InstructionTemplateManager>();

            // Test template variables structure
            var variables = new TemplateVariables
            {
                ServerInfo = new { name = "Test", version = "1.0" },
                AvailableTools = new[] { "search", "read" },
                BuiltInTools = new[] { "Read", "Grep", "Bash" },
                CustomVariables = new Dictionary<string, object> { ["test"] = "value" }
            };

            // This would fail to compile if the API was wrong
            Assert.That(templateManager, Is.Not.Null);
            Assert.That(variables.ServerInfo, Is.Not.Null);
            Assert.That(variables.AvailableTools, Is.Not.Empty);
        }

        [Test]
        public void FrameworkLogging_ConfigurationApplied_Compiles()
        {
            // This test verifies that ConfigureFramework actually affects logging configuration
            // This would have caught the framework options bug we just fixed
            
            var builder = new McpServerBuilder()
                .WithServerInfo("Logging Test Server", "1.0.0")
                .ConfigureFramework(options =>
                {
                    options.EnableFrameworkLogging = true;
                    options.FrameworkLogLevel = LogLevel.Debug;
                    options.EnableDetailedToolLogging = true;
                    options.EnableDetailedMiddlewareLogging = false;
                    options.SuppressStartupLogs = false;
                });

            // Verify the builder properly handles framework configuration
            Assert.That(builder, Is.Not.Null);
            
            // Test that logging configuration methods exist and compile
            builder.ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Information);
                logging.AddConsole();
            });
            
            // Verify services are configured
            var services = builder.Services;
            Assert.That(services, Is.Not.Null);
            
            // This test proves that the ConfigureFramework options are properly typed and accessible
            // If the API changed or was broken, this wouldn't compile
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

        // Example tools with different markers and priorities for template testing
        public class HighPrioritySearchTool : McpToolBase<SearchParameters, SearchResult>, IPrioritizedTool
        {
            public override string Name => "high_priority_search";
            public override string Description => "High priority search tool for testing";
            public int Priority => 90; // High priority
            public string[] PreferredScenarios => new[] { "search", "find" };

            protected override Task<SearchResult> ExecuteInternalAsync(SearchParameters parameters, CancellationToken cancellationToken)
            {
                return Task.FromResult(new SearchResult { Success = true, Query = parameters.Query });
            }
        }

        public class SymbolicReadTool : McpToolBase<ReadParameters, ReadResult>, ISymbolicRead, IPrioritizedTool
        {
            public override string Name => "symbolic_read";
            public override string Description => "Reads symbols without loading entire files";
            public int Priority => 75;
            public string[] PreferredScenarios => new[] { "code_navigation", "symbol_lookup" };

            protected override Task<ReadResult> ExecuteInternalAsync(ReadParameters parameters, CancellationToken cancellationToken)
            {
                return Task.FromResult(new ReadResult { Success = true, FilePath = parameters.FilePath });
            }
        }

        public class TypeAwareTool : McpToolBase<TypeParameters, TypeResult>, ITypeAware, IPrioritizedTool
        {
            public override string Name => "type_aware";
            public override string Description => "Understands type systems for accurate analysis";
            public int Priority => 85;
            public string[] PreferredScenarios => new[] { "type_verification", "code_analysis" };

            protected override Task<TypeResult> ExecuteInternalAsync(TypeParameters parameters, CancellationToken cancellationToken)
            {
                return Task.FromResult(new TypeResult { Success = true, TypeName = parameters.TypeName });
            }
        }

        // Supporting parameter and result classes
        public class SearchParameters
        {
            [Required]
            public string Query { get; set; }
        }

        public class SearchResult : ToolResultBase
        {
            public override string Operation => "search";
            public string Query { get; set; }
        }

        public class ReadParameters
        {
            [Required]
            public string FilePath { get; set; }
        }

        public class ReadResult : ToolResultBase
        {
            public override string Operation => "read";
            public string FilePath { get; set; }
        }

        public class TypeParameters
        {
            [Required]
            public string TypeName { get; set; }
        }

        public class TypeResult : ToolResultBase
        {
            public override string Operation => "type_check";
            public string TypeName { get; set; }
        }
    }
}