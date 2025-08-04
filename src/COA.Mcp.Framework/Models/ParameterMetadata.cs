namespace COA.Mcp.Framework
{
    /// <summary>
    /// Metadata for a tool parameter.
    /// </summary>
    public class ParameterMetadata
    {
        /// <summary>
        /// Gets or sets the parameter type.
        /// </summary>
        public string Type { get; set; } = "string";

        /// <summary>
        /// Gets or sets whether the parameter is required.
        /// </summary>
        public bool Required { get; set; }

        /// <summary>
        /// Gets or sets the parameter description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the default value.
        /// </summary>
        public object? DefaultValue { get; set; }
    }
}