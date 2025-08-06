using COA.Mcp.Framework;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Registration;
using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;

namespace COA.Mcp.Framework.Testing.Mocks
{
    /// <summary>
    /// Mock implementation of McpToolRegistry for testing.
    /// </summary>
    public class MockToolRegistry : McpToolRegistry
    {
        /// <summary>
        /// Initializes a new instance of the MockToolRegistry class.
        /// </summary>
        public MockToolRegistry() : base(CreateServiceProvider())
        {
        }

        /// <summary>
        /// Initializes a new instance of the MockToolRegistry class with a custom service provider.
        /// </summary>
        public MockToolRegistry(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        private static IServiceProvider CreateServiceProvider()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            return services.BuildServiceProvider();
        }
        /// <summary>
        /// Gets the number of registered tools.
        /// </summary>
        public int ToolCount => GetAllTools().Count();

        /// <summary>
        /// Gets or sets whether to throw an exception when a tool is not found.
        /// </summary>
        public bool ThrowOnNotFound { get; set; }

        /// <summary>
        /// Gets the list of tool names that were queried.
        /// </summary>
        public List<string> QueriedTools { get; } = new();

        /// <summary>
        /// Gets the list of tools that were registered.
        /// </summary>
        public List<string> RegisteredTools { get; } = new();

        /// <summary>
        /// Gets the list of tools that were unregistered.
        /// </summary>
        public List<string> UnregisteredTools { get; } = new();

        /// <summary>
        /// Registers an IMcpTool and tracks the registration.
        /// </summary>
        public new void RegisterTool(IMcpTool tool)
        {
            if (tool == null) throw new ArgumentNullException(nameof(tool));
            
            base.RegisterTool(tool);
            RegisteredTools.Add(tool.Name);
        }

        /// <summary>
        /// Gets a tool by name and tracks the query.
        /// </summary>
        public new IMcpTool? GetTool(string toolName)
        {
            QueriedTools.Add(toolName);
            
            var tool = base.GetTool(toolName);
            
            if (tool == null && ThrowOnNotFound)
            {
                throw new KeyNotFoundException($"Tool '{toolName}' not found in registry.");
            }
            
            return tool;
        }

        /// <summary>
        /// Unregisters a tool by name.
        /// </summary>
        public new bool UnregisterTool(string toolName)
        {
            UnregisteredTools.Add(toolName);
            return base.UnregisterTool(toolName);
        }

        /// <summary>
        /// Clears all registered tools.
        /// </summary>
        public new void Clear()
        {
            RegisteredTools.Clear();
            QueriedTools.Clear();
            UnregisteredTools.Clear();
            base.Clear();
        }

        /// <summary>
        /// Creates a simple mock tool for testing.
        /// </summary>
        public static IMcpTool CreateMockTool(string name = "test_tool", string description = "Test tool", ToolCategory category = ToolCategory.General)
        {
            return new SimpleMockTool(name, description, category);
        }

        /// <summary>
        /// Adds a simple mock tool to the registry.
        /// </summary>
        public void AddMockTool(string name = "test_tool", string description = "Test tool", ToolCategory category = ToolCategory.General)
        {
            var tool = CreateMockTool(name, description, category);
            RegisterTool(tool);
        }

        /// <summary>
        /// Gets tools by category.
        /// </summary>
        public IEnumerable<IMcpTool> GetToolsByCategory(ToolCategory category)
        {
            return GetAllTools().Where(t => t.Category == category);
        }

        // Simple mock tool implementation for testing
        private class SimpleMockTool : IMcpTool
        {
            private readonly string _name;
            private readonly string _description;
            private readonly ToolCategory _category;

            public SimpleMockTool(string name, string description, ToolCategory category)
            {
                _name = name;
                _description = description;
                _category = category;
            }

            public string Name => _name;
            public string Description => _description;
            public ToolCategory Category => _category;
            public Type ParameterType => typeof(object);
            public Type ResultType => typeof(object);

            public object GetInputSchema()
            {
                return new
                {
                    type = "object",
                    properties = new Dictionary<string, object>()
                };
            }

            public Task<object?> ExecuteAsync(object? parameters, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<object?>(new { Success = true, Message = "Mock execution" });
            }
        }
    }
}