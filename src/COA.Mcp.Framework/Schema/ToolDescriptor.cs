using System;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework.Interfaces;

namespace COA.Mcp.Framework.Schema
{
    /// <summary>
    /// Strongly-typed descriptor for MCP tools with compile-time type information.
    /// </summary>
    /// <typeparam name="TParams">The type of the tool's input parameters.</typeparam>
    /// <typeparam name="TResult">The type of the tool's result.</typeparam>
    public class ToolDescriptor<TParams, TResult> where TParams : class
    {
        /// <summary>
        /// Gets or sets the unique name of the tool.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human-readable description of the tool.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the category this tool belongs to.
        /// </summary>
        public ToolCategory Category { get; set; }

        /// <summary>
        /// Gets or sets the JSON schema for the tool's input parameters.
        /// </summary>
        public JsonSchema<TParams> InputSchema { get; set; } = new();

        /// <summary>
        /// Gets the parameter type for this tool.
        /// </summary>
        public Type ParameterType => typeof(TParams);

        /// <summary>
        /// Gets the result type for this tool.
        /// </summary>
        public Type ResultType => typeof(TResult);

        /// <summary>
        /// Gets or sets the tool instance.
        /// </summary>
        public IMcpTool<TParams, TResult>? Tool { get; set; }

        /// <summary>
        /// Creates a descriptor from an existing tool.
        /// </summary>
        public static ToolDescriptor<TParams, TResult> FromTool(IMcpTool<TParams, TResult> tool)
        {
            if (tool == null) throw new ArgumentNullException(nameof(tool));

            return new ToolDescriptor<TParams, TResult>
            {
                Name = tool.Name,
                Description = tool.Description,
                Category = tool.Category,
                InputSchema = new JsonSchema<TParams>(),
                Tool = tool
            };
        }

        /// <summary>
        /// Invokes the tool with the specified parameters.
        /// </summary>
        public async Task<TResult> InvokeAsync(
            TParams parameters, 
            CancellationToken cancellationToken = default)
        {
            if (Tool == null)
            {
                throw new InvalidOperationException($"Tool '{Name}' has no implementation");
            }

            return await Tool.ExecuteAsync(parameters, cancellationToken);
        }

        /// <summary>
        /// Validates the parameters against the schema.
        /// </summary>
        public bool ValidateParameters(TParams parameters)
        {
            return InputSchema.IsValid(parameters);
        }

        /// <summary>
        /// Creates a typed invocation delegate for this tool.
        /// </summary>
        public Func<TParams, CancellationToken, Task<TResult>> CreateInvoker()
        {
            if (Tool == null)
            {
                throw new InvalidOperationException($"Tool '{Name}' has no implementation");
            }

            return (parameters, cancellationToken) => Tool.ExecuteAsync(parameters, cancellationToken);
        }
    }

    /// <summary>
    /// Non-generic tool descriptor for runtime operations.
    /// </summary>
    public class ToolDescriptor
    {
        /// <summary>
        /// Gets or sets the unique name of the tool.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human-readable description of the tool.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the category this tool belongs to.
        /// </summary>
        public ToolCategory Category { get; set; }

        /// <summary>
        /// Gets or sets the JSON schema for the tool's input parameters.
        /// </summary>
        public IJsonSchema InputSchema { get; set; } = null!;

        /// <summary>
        /// Gets or sets the parameter type for this tool.
        /// </summary>
        public Type ParameterType { get; set; } = typeof(object);

        /// <summary>
        /// Gets or sets the result type for this tool.
        /// </summary>
        public Type ResultType { get; set; } = typeof(object);

        /// <summary>
        /// Gets or sets the tool instance.
        /// </summary>
        public IMcpTool? Tool { get; set; }

        /// <summary>
        /// Creates a non-generic descriptor from a tool.
        /// </summary>
        public static ToolDescriptor FromTool(IMcpTool tool)
        {
            if (tool == null) throw new ArgumentNullException(nameof(tool));

            return new ToolDescriptor
            {
                Name = tool.Name,
                Description = tool.Description,
                Category = tool.Category,
                ParameterType = tool.ParameterType,
                ResultType = tool.ResultType,
                Tool = tool
            };
        }
    }
}