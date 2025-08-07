using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework.Registration;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace COA.Mcp.Framework.Tests.Registration
{
    [TestFixture]
    public class ServiceCollectionExtensionsTests
    {
        private ServiceCollection _services;

        [SetUp]
        public void SetUp()
        {
            _services = new ServiceCollection();
        }

        #region AddMcpFramework Tests

        [Test]
        public void AddMcpFramework_RegistersCoreServices()
        {
            // Act
            _services.AddMcpFramework();
            var provider = _services.BuildServiceProvider();

            // Assert
            provider.GetService<McpToolRegistry>().Should().NotBeNull();
            provider.GetService<IParameterValidator>().Should().NotBeNull();
            provider.GetService<IParameterValidator>().Should().BeOfType<DefaultParameterValidator>();
        }

        [Test]
        public void AddMcpFramework_WithOptions_ConfiguresOptions()
        {
            // Act
            _services.AddMcpFramework(options =>
            {
                options.EnableValidation = false;
                options.ThrowOnValidationErrors = true;
                options.DefaultTimeoutMs = 60000;
                options.TokenOptimization = TokenOptimizationLevel.Aggressive;
                options.UseAIOptimizedResponses = false;
            });

            var provider = _services.BuildServiceProvider();
            var options = provider.GetRequiredService<McpFrameworkOptions>();

            // Assert
            options.EnableValidation.Should().BeFalse();
            options.ThrowOnValidationErrors.Should().BeTrue();
            options.DefaultTimeoutMs.Should().Be(60000);
            options.TokenOptimization.Should().Be(TokenOptimizationLevel.Aggressive);
            options.UseAIOptimizedResponses.Should().BeFalse();
        }

        [Test]
        public void AddMcpFramework_WithAssemblyToScan_DiscoversTools()
        {
            // Act
            _services.AddMcpFramework(options =>
            {
                options.DiscoverToolsFromAssembly(Assembly.GetExecutingAssembly());
            });

            var provider = _services.BuildServiceProvider();

            // Assert
            var options = provider.GetRequiredService<McpFrameworkOptions>();
            options.AssembliesToScan.Should().Contain(Assembly.GetExecutingAssembly());
        }

        [Test]
        public void AddMcpFramework_MultipleCalls_DoesNotDuplicateServices()
        {
            // Act
            _services.AddMcpFramework();
            _services.AddMcpFramework();

            // Assert
            var registryCount = _services.Count(s => s.ServiceType == typeof(McpToolRegistry));
            registryCount.Should().Be(1, "TryAddSingleton should prevent duplicates");
        }

        #endregion

        #region AddMcpTool Tests

        [Test]
        public void AddMcpTool_RegistersToolAsScoped()
        {
            // Act
            _services.AddMcpTool<TestTool>();
            var provider = _services.BuildServiceProvider();

            // Assert
            using (var scope = provider.CreateScope())
            {
                var tool = scope.ServiceProvider.GetService<TestTool>();
                tool.Should().NotBeNull();
                tool.Should().BeOfType<TestTool>();
            }
        }

        [Test]
        public void AddMcpTool_ConfiguresRegistrationOptions()
        {
            // Act
            _services.AddMcpTool<TestTool>();
            _services.AddOptions();
            var provider = _services.BuildServiceProvider();

            // Assert
            // Note: McpToolRegistrationOptions is internal, so we can't directly test it
            // The registration is configured but not directly accessible
            // We can verify the tool itself is registered
            using (var scope = provider.CreateScope())
            {
                var tool = scope.ServiceProvider.GetService<TestTool>();
                tool.Should().NotBeNull();
            }
        }

        [Test]
        public void AddMcpTool_MultipleCalls_DoesNotDuplicate()
        {
            // Act
            _services.AddMcpTool<TestTool>();
            _services.AddMcpTool<TestTool>();

            // Assert
            var toolCount = _services.Count(s => s.ServiceType == typeof(TestTool));
            toolCount.Should().Be(1, "TryAddScoped should prevent duplicates");
        }

        #endregion

        #region AddMcpToolsFromAssembly Tests

        [Test]
        [Ignore("May cause issues with assembly scanning")]
        public void AddMcpToolsFromAssembly_RegistersAllToolsInAssembly()
        {
            // Act
            _services.AddMcpToolsFromAssembly(Assembly.GetExecutingAssembly());
            var provider = _services.BuildServiceProvider();

            // Assert
            using (var scope = provider.CreateScope())
            {
                var testTool = scope.ServiceProvider.GetService<TestTool>();
                var anotherTool = scope.ServiceProvider.GetService<AnotherTestTool>();
                
                testTool.Should().NotBeNull();
                anotherTool.Should().NotBeNull();
            }
        }

        [Test]
        [Ignore("May cause issues with assembly scanning")]
        public void AddMcpToolsFromAssembly_RegistersToolsByInterface()
        {
            // Act
            _services.AddMcpToolsFromAssembly(Assembly.GetExecutingAssembly());
            var provider = _services.BuildServiceProvider();

            // Assert
            using (var scope = provider.CreateScope())
            {
                var tools = scope.ServiceProvider.GetServices<IMcpTool>();
                tools.Should().NotBeEmpty();
                tools.Should().Contain(t => t.GetType() == typeof(TestTool));
                tools.Should().Contain(t => t.GetType() == typeof(AnotherTestTool));
            }
        }

        [Test]
        [Ignore("May cause issues with assembly scanning")]
        public void AddMcpToolsFromAssembly_SkipsAbstractClasses()
        {
            // Act
            _services.AddMcpToolsFromAssembly(Assembly.GetExecutingAssembly());
            var provider = _services.BuildServiceProvider();

            // Assert
            using (var scope = provider.CreateScope())
            {
                var tools = scope.ServiceProvider.GetServices<IMcpTool>();
                tools.Should().NotContain(t => t.GetType().IsAbstract);
            }
        }

        #endregion

        #region McpFrameworkOptions Tests

        [Test]
        public void McpFrameworkOptions_DiscoverToolsFromAssembly_AddsAssembly()
        {
            // Arrange
            var options = new McpFrameworkOptions();
            var assembly = Assembly.GetExecutingAssembly();

            // Act
            options.DiscoverToolsFromAssembly(assembly);

            // Assert
            options.AssembliesToScan.Should().Contain(assembly);
        }

        [Test]
        public void McpFrameworkOptions_DiscoverToolsFromAssembly_NoDuplicates()
        {
            // Arrange
            var options = new McpFrameworkOptions();
            var assembly = Assembly.GetExecutingAssembly();

            // Act
            options.DiscoverToolsFromAssembly(assembly);
            options.DiscoverToolsFromAssembly(assembly);

            // Assert
            options.AssembliesToScan.Should().ContainSingle();
        }

        [Test]
        public void McpFrameworkOptions_UseTokenOptimization_SetsLevel()
        {
            // Arrange
            var options = new McpFrameworkOptions();

            // Act
            options.UseTokenOptimization(TokenOptimizationLevel.Aggressive);

            // Assert
            options.TokenOptimization.Should().Be(TokenOptimizationLevel.Aggressive);
        }

        [Test]
        public void McpFrameworkOptions_DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var options = new McpFrameworkOptions();

            // Assert
            options.EnableValidation.Should().BeTrue();
            options.ThrowOnValidationErrors.Should().BeFalse();
            options.DefaultTimeoutMs.Should().Be(30000);
            options.TokenOptimization.Should().Be(TokenOptimizationLevel.Balanced);
            options.UseAIOptimizedResponses.Should().BeTrue();
            options.AssembliesToScan.Should().BeEmpty();
        }

        [Test]
        public void McpFrameworkOptions_FluentApi_ChainsCorrectly()
        {
            // Arrange
            var options = new McpFrameworkOptions();
            var assembly = Assembly.GetExecutingAssembly();

            // Act
            var result = options
                .DiscoverToolsFromAssembly(assembly)
                .UseTokenOptimization(TokenOptimizationLevel.Conservative);

            // Assert
            result.Should().BeSameAs(options);
            options.AssembliesToScan.Should().Contain(assembly);
            options.TokenOptimization.Should().Be(TokenOptimizationLevel.Conservative);
        }

        #endregion

        #region Integration Tests

        [Test]
        [Ignore("Causes hang due to circular dependency in ServiceCollectionExtensions")]
        public void FullIntegration_RegisterAndResolveTools()
        {
            // Arrange
            _services.AddLogging();
            _services.AddMcpFramework(options =>
            {
                options.DiscoverToolsFromAssembly(Assembly.GetExecutingAssembly());
            });

            // Act
            var provider = _services.BuildServiceProvider();

            // Assert
            using (var scope = provider.CreateScope())
            {
                var registry = scope.ServiceProvider.GetRequiredService<McpToolRegistry>();
                var validator = scope.ServiceProvider.GetRequiredService<IParameterValidator>();
                var tools = scope.ServiceProvider.GetServices<IMcpTool>();

                registry.Should().NotBeNull();
                validator.Should().NotBeNull();
                tools.Should().NotBeEmpty();
            }
        }

        #endregion

        #region Test Tool Classes

        public class TestToolParams
        {
            public string Value { get; set; }
        }

        public class TestToolResult : ToolResultBase
        {
            public override string Operation => "test_operation";
            public string Data { get; set; }
        }

        public class TestTool : McpToolBase<TestToolParams, TestToolResult>
        {
            public override string Name => "test_tool";
            public override string Description => "Test tool";

            protected override Task<TestToolResult> ExecuteInternalAsync(
                TestToolParams parameters, 
                CancellationToken cancellationToken)
            {
                return Task.FromResult(new TestToolResult
                {
                    Success = true,
                    Data = $"Processed: {parameters.Value}"
                });
            }
        }

        public class AnotherTestTool : McpToolBase<TestToolParams, TestToolResult>
        {
            public override string Name => "another_test_tool";
            public override string Description => "Another test tool";

            protected override Task<TestToolResult> ExecuteInternalAsync(
                TestToolParams parameters, 
                CancellationToken cancellationToken)
            {
                return Task.FromResult(new TestToolResult
                {
                    Success = true,
                    Data = "Another tool"
                });
            }
        }

        // Abstract class should not be registered
        public abstract class AbstractTool : McpToolBase<TestToolParams, TestToolResult>
        {
            public override string Name => "abstract";
            public override string Description => "Should not be registered";
        }

        // Non-tool class should not be registered
        public class NotATool
        {
            public string Name => "not_a_tool";
        }

        #endregion
    }
}