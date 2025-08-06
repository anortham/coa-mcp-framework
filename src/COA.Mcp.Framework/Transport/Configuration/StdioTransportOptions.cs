using System.IO;

namespace COA.Mcp.Framework.Transport.Configuration
{
    /// <summary>
    /// Configuration options for stdio transport.
    /// </summary>
    public class StdioTransportOptions
    {
        /// <summary>
        /// Gets or sets the input stream for reading messages.
        /// Defaults to Console.In if not specified.
        /// </summary>
        public TextReader? Input { get; set; }
        
        /// <summary>
        /// Gets or sets the output stream for writing messages.
        /// Defaults to Console.Out if not specified.
        /// </summary>
        public TextWriter? Output { get; set; }
    }
}