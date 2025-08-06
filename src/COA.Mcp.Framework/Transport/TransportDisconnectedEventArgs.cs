using System;

namespace COA.Mcp.Framework.Transport
{
    /// <summary>
    /// Event arguments for transport disconnection events.
    /// </summary>
    public class TransportDisconnectedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the reason for disconnection.
        /// </summary>
        public string Reason { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the exception that caused disconnection, if any.
        /// </summary>
        public Exception? Exception { get; set; }
        
        /// <summary>
        /// Gets or sets whether the disconnection was clean.
        /// </summary>
        public bool WasClean { get; set; }
    }
}