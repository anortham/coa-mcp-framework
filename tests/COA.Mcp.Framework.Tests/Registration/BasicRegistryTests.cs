using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Registration;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace COA.Mcp.Framework.Tests.Registration
{
    [TestFixture]
    public class BasicRegistryTests
    {
        private ServiceProvider _serviceProvider;
        private McpToolRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            var services = new ServiceCollection();
            services.AddSingleton<McpToolRegistry>();
            _serviceProvider = services.BuildServiceProvider();
            _registry = _serviceProvider.GetRequiredService<McpToolRegistry>();
        }

        [TearDown]
        public void TearDown()
        {
            _serviceProvider?.Dispose();
        }

        [Test]
        public void Registry_ShouldStartEmpty()
        {
            // Assert
            _registry.GetAllTools().Should().BeEmpty();
        }

        [Test]
        public void RegisterTool_WithSimpleTool_ShouldSucceed()
        {
            // Arrange
            var tool = new SimpleTool();

            // Act
            _registry.RegisterTool<SimpleParams, SimpleResult>(tool);

            // Assert
            _registry.IsToolRegistered("simple_tool").Should().BeTrue();
            _registry.GetTool("simple_tool").Should().BeSameAs(tool);
        }

        [Test]
        public void GetAllTools_AfterRegistration_ShouldReturnTools()
        {
            // Arrange
            var tool1 = new SimpleTool();
            var tool2 = new AnotherTool();
            _registry.RegisterTool<SimpleParams, SimpleResult>(tool1);
            _registry.RegisterTool<SimpleParams, SimpleResult>(tool2);

            // Act
            var tools = _registry.GetAllTools().ToList();

            // Assert
            tools.Should().HaveCount(2);
            tools.Should().Contain(tool1);
            tools.Should().Contain(tool2);
        }

        [Test]
        public void UnregisterTool_ShouldRemoveTool()
        {
            // Arrange
            var tool = new SimpleTool();
            _registry.RegisterTool<SimpleParams, SimpleResult>(tool);

            // Act
            var removed = _registry.UnregisterTool("simple_tool");

            // Assert
            removed.Should().BeTrue();
            _registry.IsToolRegistered("simple_tool").Should().BeFalse();
        }

        [Test]
        public void Clear_ShouldRemoveAllTools()
        {
            // Arrange
            _registry.RegisterTool<SimpleParams, SimpleResult>(new SimpleTool());
            _registry.RegisterTool<SimpleParams, SimpleResult>(new AnotherTool());

            // Act
            _registry.Clear();

            // Assert
            _registry.GetAllTools().Should().BeEmpty();
        }

        [Test]
        public void GetProtocolTools_ShouldReturnCorrectFormat()
        {
            // Arrange
            var tool = new SimpleTool();
            _registry.RegisterTool<SimpleParams, SimpleResult>(tool);

            // Act
            var protocolTools = _registry.GetProtocolTools();

            // Assert
            protocolTools.Should().HaveCount(1);
            protocolTools[0].Name.Should().Be("simple_tool");
            protocolTools[0].Description.Should().Be("A simple test tool");
            protocolTools[0].InputSchema.Should().NotBeNull();
        }

        // Simple test tool implementations
        private class SimpleTool : McpToolBase<SimpleParams, SimpleResult>
        {
            public override string Name => "simple_tool";
            public override string Description => "A simple test tool";
            public override ToolCategory Category => ToolCategory.General;

            protected override Task<SimpleResult> ExecuteInternalAsync(SimpleParams parameters, CancellationToken cancellationToken)
            {
                return Task.FromResult(new SimpleResult { Success = true, Message = "Done" });
            }
        }

        private class AnotherTool : McpToolBase<SimpleParams, SimpleResult>
        {
            public override string Name => "another_tool";
            public override string Description => "Another test tool";
            public override ToolCategory Category => ToolCategory.Utility;

            protected override Task<SimpleResult> ExecuteInternalAsync(SimpleParams parameters, CancellationToken cancellationToken)
            {
                return Task.FromResult(new SimpleResult { Success = true });
            }
        }

        private class SimpleParams
        {
            public string? Value { get; set; }
        }

        private class SimpleResult
        {
            public bool Success { get; set; }
            public string? Message { get; set; }
        }
    }
}