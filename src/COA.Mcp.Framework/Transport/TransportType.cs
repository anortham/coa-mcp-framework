namespace COA.Mcp.Framework.Transport
{
    /// <summary>
    /// Defines the available transport types for MCP communication.
    /// </summary>
    public enum TransportType
    {
        /// <summary>
        /// Standard input/output transport.
        /// </summary>
        Stdio,
        
        /// <summary>
        /// HTTP/HTTPS transport.
        /// </summary>
        Http,
        
        /// <summary>
        /// WebSocket transport.
        /// </summary>
        WebSocket,
        
        /// <summary>
        /// Named pipe transport.
        /// </summary>
        NamedPipe,
        
        /// <summary>
        /// TCP socket transport.
        /// </summary>
        Tcp
    }
}