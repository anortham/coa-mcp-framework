using System.IO;

namespace COA.Mcp.Framework.Transport.Configuration
{
    /// <summary>
    /// Configuration options for stdio transport.
    /// </summary>
    public class StdioTransportOptions
    {
        /// <summary>
        /// Gets or sets the raw input stream for reading messages (preferred).
        /// Defaults to Console.OpenStandardInput() if not specified.
        /// </summary>
        public Stream? InputStream { get; set; }

        /// <summary>
        /// Gets or sets the raw output stream for writing messages (preferred).
        /// Defaults to Console.OpenStandardOutput() if not specified.
        /// </summary>
        public Stream? OutputStream { get; set; }

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
