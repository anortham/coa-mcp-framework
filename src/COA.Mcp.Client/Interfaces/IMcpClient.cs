using System;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Protocol;

namespace COA.Mcp.Client.Interfaces
{
    /// <summary>
    /// Interface for MCP client operations.
    /// </summary>
    public interface IMcpClient : IDisposable
    {
        /// <summary>
        /// Gets whether the client is connected to the server.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Gets the server information after initialization.
        /// </summary>
        InitializeResult? ServerInfo { get; }

        /// <summary>
        /// Connects to the MCP server.
        /// </summary>
        Task ConnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Disconnects from the MCP server.
        /// </summary>
        Task DisconnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Initializes the MCP session with the server.
        /// </summary>
        Task<InitializeResult> InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists available tools from the server.
        /// </summary>
        Task<ListToolsResult> ListToolsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Calls a tool by name with the specified parameters.
        /// </summary>
        Task<CallToolResult> CallToolAsync(string toolName, object? parameters = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists available resources from the server.
        /// </summary>
        Task<ListResourcesResult> ListResourcesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads a resource by URI.
        /// </summary>
        Task<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists available prompts from the server.
        /// </summary>
        Task<ListPromptsResult> ListPromptsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a prompt by name with optional arguments.
        /// </summary>
        Task<GetPromptResult> GetPromptAsync(string name, Dictionary<string, string>? arguments = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a raw JSON-RPC request.
        /// </summary>
        Task<TResponse> SendRequestAsync<TResponse>(string method, object? parameters = null, CancellationToken cancellationToken = default)
            where TResponse : class;

        /// <summary>
        /// Event raised when connected to the server.
        /// </summary>
        event EventHandler<ConnectedEventArgs>? Connected;

        /// <summary>
        /// Event raised when disconnected from the server.
        /// </summary>
        event EventHandler<DisconnectedEventArgs>? Disconnected;

        /// <summary>
        /// Event raised when a notification is received from the server.
        /// </summary>
        event EventHandler<NotificationEventArgs>? NotificationReceived;
    }

    /// <summary>
    /// Event arguments for connection events.
    /// </summary>
    public class ConnectedEventArgs : EventArgs
    {
        public DateTime ConnectedAt { get; set; }
        public string ServerUrl { get; set; } = string.Empty;
    }

    /// <summary>
    /// Event arguments for disconnection events.
    /// </summary>
    public class DisconnectedEventArgs : EventArgs
    {
        public DateTime DisconnectedAt { get; set; }
        public string? Reason { get; set; }
        public Exception? Exception { get; set; }
    }

    /// <summary>
    /// Event arguments for notification events.
    /// </summary>
    public class NotificationEventArgs : EventArgs
    {
        public string Method { get; set; } = string.Empty;
        public object? Parameters { get; set; }
        public DateTime ReceivedAt { get; set; }
    }
}