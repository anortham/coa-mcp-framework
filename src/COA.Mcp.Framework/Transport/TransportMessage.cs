using System;
using System.Collections.Generic;

namespace COA.Mcp.Framework.Transport
{
    /// <summary>
    /// Represents a message transmitted through an MCP transport.
    /// </summary>
    public class TransportMessage
    {
        /// <summary>
        /// Gets or sets the unique message identifier.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Gets or sets the message content (typically JSON-RPC).
        /// </summary>
        public string Content { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the message headers for metadata.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new();
        
        /// <summary>
        /// Gets or sets the message timestamp.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Gets or sets the correlation ID for request-response matching.
        /// </summary>
        public string? CorrelationId { get; set; }
    }
}