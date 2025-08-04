using COA.Mcp.Framework;
using COA.Mcp.Framework.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace COA.Mcp.Framework.Testing.Mocks
{
    /// <summary>
    /// Mock implementation of IToolRegistry for testing.
    /// </summary>
    public class MockToolRegistry : IToolRegistry
    {
        private readonly Dictionary<string, ITool> _tools = new();
        private readonly Dictionary<string, ToolMetadata> _toolMetadata = new();

        /// <summary>
        /// Gets the number of registered tools.
        /// </summary>
        public int ToolCount => _tools.Count;

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

        /// <inheritdoc/>
        public void RegisterTool(ITool tool)
        {
            if (tool == null) throw new ArgumentNullException(nameof(tool));

            _tools[tool.ToolName] = tool;
            RegisteredTools.Add(tool.ToolName);

            // Create metadata from tool
            var metadata = new ToolMetadata
            {
                Name = tool.ToolName,
                Description = tool.Description,
                Category = tool.Category,
                DeclaringType = tool.GetType(),
                ToolInstance = tool
            };
            _toolMetadata[tool.ToolName] = metadata;
        }

        /// <inheritdoc/>
        public void RegisterTool(ToolMetadata toolMetadata)
        {
            if (toolMetadata == null) throw new ArgumentNullException(nameof(toolMetadata));

            _toolMetadata[toolMetadata.Name] = toolMetadata;
            RegisteredTools.Add(toolMetadata.Name);

            // If we have a tool instance, register it too
            if (toolMetadata.ToolInstance != null)
            {
                _tools[toolMetadata.Name] = toolMetadata.ToolInstance;
            }
        }

        /// <inheritdoc/>
        public ITool? GetTool(string toolName)
        {
            QueriedTools.Add(toolName);

            if (_tools.TryGetValue(toolName, out var tool))
            {
                return tool;
            }

            if (ThrowOnNotFound)
            {
                throw new KeyNotFoundException($"Tool '{toolName}' not found in registry.");
            }

            return null;
        }

        /// <inheritdoc/>
        public ToolMetadata? GetToolMetadata(string toolName)
        {
            QueriedTools.Add(toolName);

            if (_toolMetadata.TryGetValue(toolName, out var metadata))
            {
                return metadata;
            }

            if (ThrowOnNotFound)
            {
                throw new KeyNotFoundException($"Tool metadata for '{toolName}' not found in registry.");
            }

            return null;
        }

        /// <inheritdoc/>
        public IEnumerable<ITool> GetAllTools()
        {
            return _tools.Values.ToList();
        }

        /// <inheritdoc/>
        public IEnumerable<ToolMetadata> GetAllToolMetadata()
        {
            return _toolMetadata.Values.ToList();
        }

        /// <inheritdoc/>
        public bool IsToolRegistered(string toolName)
        {
            return _tools.ContainsKey(toolName);
        }

        /// <inheritdoc/>
        public bool UnregisterTool(string toolName)
        {
            UnregisteredTools.Add(toolName);
            
            var removed = _tools.Remove(toolName);
            _toolMetadata.Remove(toolName);
            
            return removed;
        }

        /// <inheritdoc/>
        public IEnumerable<ITool> GetToolsByCategory(ToolCategory category)
        {
            return _tools.Values.Where(t => t.Category == category).ToList();
        }

        /// <summary>
        /// Clears all registered tools.
        /// </summary>
        public void Clear()
        {
            _tools.Clear();
            _toolMetadata.Clear();
            QueriedTools.Clear();
            RegisteredTools.Clear();
            UnregisteredTools.Clear();
        }

        /// <summary>
        /// Adds a simple mock tool for testing.
        /// </summary>
        /// <param name="name">The tool name.</param>
        /// <param name="description">The tool description.</param>
        /// <param name="category">The tool category.</param>
        /// <param name="executeFunc">Optional execution function.</param>
        public void AddMockTool(
            string name, 
            string description = "Mock tool", 
            ToolCategory category = ToolCategory.General,
            Func<object, Task<object>>? executeFunc = null)
        {
            var mockTool = new SimpleMockTool(name, description, category, executeFunc);
            RegisterTool(mockTool);
        }

        /// <summary>
        /// Simple mock tool implementation.
        /// </summary>
        private class SimpleMockTool : ITool
        {
            private readonly Func<object, Task<object>>? _executeFunc;

            public SimpleMockTool(
                string name, 
                string description, 
                ToolCategory category,
                Func<object, Task<object>>? executeFunc = null)
            {
                ToolName = name;
                Description = description;
                Category = category;
                _executeFunc = executeFunc;
            }

            public string ToolName { get; }
            public string Description { get; }
            public ToolCategory Category { get; }

            public async Task<object> ExecuteAsync(object parameters)
            {
                if (_executeFunc != null)
                {
                    return await _executeFunc(parameters);
                }

                await Task.CompletedTask;
                return new { success = true, parameters };
            }
        }
    }
}