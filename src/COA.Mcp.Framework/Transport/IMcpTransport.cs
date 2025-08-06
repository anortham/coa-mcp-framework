using System;
using System.Threading;
using System.Threading.Tasks;

namespace COA.Mcp.Framework.Transport
{
    /// <summary>
    /// Defines the contract for MCP transport implementations.
    /// </summary>
    public interface IMcpTransport : IDisposable
    {
        /// <summary>
        /// Gets the transport type.
        /// </summary>
        TransportType Type { get; }
        
        /// <summary>
        /// Gets whether the transport is currently connected.
        /// </summary>
        bool IsConnected { get; }
        
        /// <summary>
        /// Starts the transport.
        /// </summary>
        Task StartAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Stops the transport.
        /// </summary>
        Task StopAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Reads the next message from the transport.
        /// </summary>
        Task<TransportMessage?> ReadMessageAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Writes a message to the transport.
        /// </summary>
        Task WriteMessageAsync(TransportMessage message, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Event raised when the transport is disconnected.
        /// </summary>
        event EventHandler<TransportDisconnectedEventArgs>? Disconnected;
    }
}