using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.Transport
{
    /// <summary>
    /// Stdio transport implementation for console-based communication.
    /// </summary>
    public class StdioTransport : IMcpTransport, IDisposable
    {
        private readonly TextReader _input;
        private readonly TextWriter _output;
        private readonly ILogger<StdioTransport>? _logger;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private bool _isConnected;
        private bool _disposed;

        public TransportType Type => TransportType.Stdio;
        public bool IsConnected => _isConnected;

        public event EventHandler<TransportDisconnectedEventArgs>? Disconnected;

        public StdioTransport(
            TextReader? input = null,
            TextWriter? output = null,
            ILogger<StdioTransport>? logger = null)
        {
            _input = input ?? Console.In;
            _output = output ?? Console.Out;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            _logger?.LogDebug("Starting stdio transport");
            _isConnected = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            _logger?.LogDebug("Stopping stdio transport");
            _isConnected = false;
            
            Disconnected?.Invoke(this, new TransportDisconnectedEventArgs
            {
                Reason = "Transport stopped",
                WasClean = true
            });
            
            return Task.CompletedTask;
        }

        public async Task<TransportMessage?> ReadMessageAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var line = await _input.ReadLineAsync();
                
                if (line == null)
                {
                    _logger?.LogDebug("End of input stream reached");
                    _isConnected = false;
                    
                    Disconnected?.Invoke(this, new TransportDisconnectedEventArgs
                    {
                        Reason = "End of input stream",
                        WasClean = true
                    });
                    
                    return null;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    return null;
                }

                return new TransportMessage
                {
                    Content = line,
                    Headers = { ["transport"] = "stdio" }
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error reading from stdio");
                
                Disconnected?.Invoke(this, new TransportDisconnectedEventArgs
                {
                    Reason = "Read error",
                    Exception = ex,
                    WasClean = false
                });
                
                throw;
            }
        }

        public async Task WriteMessageAsync(TransportMessage message, CancellationToken cancellationToken = default)
        {
            await _writeLock.WaitAsync(cancellationToken);
            try
            {
                await _output.WriteLineAsync(message.Content);
                await _output.FlushAsync();
                
                _logger?.LogTrace("Sent message: {MessageId}", message.Id);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            
            if (disposing)
            {
                // Dispose managed resources
                _writeLock?.Dispose();
            }
            
            _disposed = true;
        }
    }
}