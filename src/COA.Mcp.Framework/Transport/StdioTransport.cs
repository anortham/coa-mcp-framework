using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.Transport
{
    /// <summary>
    /// Stdio transport implementation for console-based communication.
    /// </summary>
    public class StdioTransport : IMcpTransport
    {
        private readonly Stream _inputStream;
        private readonly Stream _outputStream;
        private readonly Encoding _encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private readonly ILogger<StdioTransport>? _logger;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private bool _isConnected;
        private bool _disposed;

        public TransportType Type => TransportType.Stdio;
        public bool IsConnected => _isConnected;

        public event EventHandler<TransportDisconnectedEventArgs>? Disconnected;

        public StdioTransport(Stream? inputStream = null, Stream? outputStream = null, ILogger<StdioTransport>? logger = null)
        {
            _inputStream = inputStream ?? Console.OpenStandardInput();
            _outputStream = outputStream ?? Console.OpenStandardOutput();
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
                var content = await ReadJsonRpcFrameAsync(cancellationToken);

                if (content == null)
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

                return new TransportMessage
                {
                    Content = content,
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
                var payload = _encoding.GetBytes(message.Content);
                var header = _encoding.GetBytes($"Content-Length: {payload.Length}\r\n\r\n");

                if (_logger?.IsEnabled(LogLevel.Trace) == true)
                {
                    _logger.LogTrace("STDIO write frame: headerBytes={HeaderBytes}, payloadBytes={PayloadBytes}, messageId={MessageId}",
                        header.Length, payload.Length, message.Id);
                }

                await _outputStream.WriteAsync(header.AsMemory(0, header.Length), cancellationToken);
                await _outputStream.WriteAsync(payload.AsMemory(0, payload.Length), cancellationToken);
                await _outputStream.FlushAsync(cancellationToken);
                
                _logger?.LogTrace("Sent message: {MessageId}", message.Id);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            _writeLock?.Dispose();
            _disposed = true;
        }

        private async Task<string?> ReadJsonRpcFrameAsync(CancellationToken cancellationToken)
        {
            // Read headers until blank line (\r\n\r\n or \n\n). If the stream starts with '{' assume line-based JSON fallback.
            var headerBuffer = new MemoryStream();
            var temp = new byte[256];
            bool sawHeaderEnd = false;
            int bytesRead;

            while (!sawHeaderEnd)
            {
                bytesRead = await _inputStream.ReadAsync(temp.AsMemory(0, temp.Length), cancellationToken);
                if (bytesRead == 0)
                {
                    if (headerBuffer.Length == 0)
                        return null; // EOF
                    break; // Unexpected EOF after partial headers
                }

                headerBuffer.Write(temp, 0, bytesRead);

                var hb = headerBuffer.GetBuffer();
                var len = (int)headerBuffer.Length;

                // Fallback: if the first non-whitespace byte is '{' or '[' assume raw JSON line
                for (int i = 0; i < len; i++)
                {
                    byte b = hb[i];
                    if (b == (byte)' ' || b == (byte)'\t' || b == (byte)'\r' || b == (byte)'\n') continue;
                    if (b == (byte)'{' || b == (byte)'[')
                    {
                        // Read until newline then return
                        var line = await ReadLineJsonAsync(hb, len, cancellationToken);
                        if (_logger?.IsEnabled(LogLevel.Trace) == true)
                        {
                            _logger.LogTrace("STDIO read fallback: single-line JSON, bytes={Bytes}",
                                line.Length);
                        }
                        return line;
                    }
                    break;
                }

                // Check for header terminator
                if (len >= 4)
                {
                    for (int i = 0; i <= len - 4; i++)
                    {
                        if (hb[i] == (byte)'\r' && hb[i + 1] == (byte)'\n' && hb[i + 2] == (byte)'\r' && hb[i + 3] == (byte)'\n')
                        {
                            sawHeaderEnd = true;
                            break;
                        }
                    }
                    if (!sawHeaderEnd)
                    {
                        // Also support \n\n
                        for (int i = 0; i <= len - 2; i++)
                        {
                            if (hb[i] == (byte)'\n' && hb[i + 1] == (byte)'\n')
                            {
                                sawHeaderEnd = true;
                                break;
                            }
                        }
                    }
                }
            }

            // Parse headers and compute how many bytes already in buffer after header end
            var all = headerBuffer.ToArray();
            int headerEnd = IndexOfHeaderEnd(all);
            if (headerEnd < 0)
            {
                // Malformed headers; attempt to decode as UTF8 JSON
                return _encoding.GetString(all);
            }

            var headerText = Encoding.ASCII.GetString(all, 0, headerEnd);
            if (_logger?.IsEnabled(LogLevel.Trace) == true)
            {
                _logger.LogTrace("STDIO read headers: headerEnd={HeaderEnd} bytes", headerEnd);
            }
            int contentLength = ParseContentLength(headerText);
            if (contentLength < 0)
            {
                // No Content-Length; treat as raw JSON
                var raw = _encoding.GetString(all, headerEnd, all.Length - headerEnd);
                if (_logger?.IsEnabled(LogLevel.Trace) == true)
                {
                    _logger.LogTrace("STDIO read without Content-Length: bytes={Bytes}", raw.Length);
                }
                return raw;
            }

            int prefixLen = all.Length - (headerEnd + HeaderTerminatorLength(all, headerEnd));
            // prefixLen is bytes already read from body
            int offset = headerEnd + HeaderTerminatorLength(all, headerEnd);
            int remaining = contentLength - prefixLen;
            var body = new byte[contentLength];
            if (prefixLen > 0)
            {
                Buffer.BlockCopy(all, offset, body, 0, Math.Min(prefixLen, contentLength));
            }

            int written = Math.Max(0, prefixLen);
            if (_logger?.IsEnabled(LogLevel.Trace) == true)
            {
                _logger.LogTrace("STDIO read frame: contentLength={ContentLength}, alreadyBuffered={Buffered}, toRead={ToRead}",
                    contentLength, prefixLen, remaining);
            }
            while (written < contentLength)
            {
                int n = await _inputStream.ReadAsync(body.AsMemory(written, contentLength - written), cancellationToken);
                if (n == 0)
                {
                    break; // EOF
                }
                written += n;
            }

            if (_logger?.IsEnabled(LogLevel.Trace) == true)
            {
                _logger.LogTrace("STDIO read complete: totalBodyBytes={BodyBytes}", written);
            }
            return _encoding.GetString(body, 0, written);
        }

        private async Task<string> ReadLineJsonAsync(byte[] initialBuffer, int initialLength, CancellationToken cancellationToken)
        {
            // Return bytes up to first \n (inclusive) from initialBuffer, else read until found
            int idx = Array.IndexOf(initialBuffer, (byte)'\n', 0, initialLength);
            if (idx >= 0)
            {
                int start = 0;
                // Trim preceding CR if present at end
                int length = idx + 1;
                if (length > 0 && initialBuffer[length - 1] == (byte)'\n')
                {
                    // ok
                }
                return _encoding.GetString(initialBuffer, start, length).Trim();
            }

            using var ms = new MemoryStream();
            ms.Write(initialBuffer, 0, initialLength);
            var one = new byte[1];
            while (true)
            {
                int n = await _inputStream.ReadAsync(one.AsMemory(0, 1), cancellationToken);
                if (n == 0) break;
                ms.WriteByte(one[0]);
                if (one[0] == (byte)'\n') break;
            }
            return _encoding.GetString(ms.ToArray()).Trim();
        }

        private static int IndexOfHeaderEnd(byte[] buffer)
        {
            for (int i = 0; i <= buffer.Length - 4; i++)
            {
                if (buffer[i] == (byte)'\r' && buffer[i + 1] == (byte)'\n' && buffer[i + 2] == (byte)'\r' && buffer[i + 3] == (byte)'\n')
                    return i;
            }
            for (int i = 0; i <= buffer.Length - 2; i++)
            {
                if (buffer[i] == (byte)'\n' && buffer[i + 1] == (byte)'\n')
                    return i;
            }
            return -1;
        }

        private static int HeaderTerminatorLength(byte[] buffer, int headerEnd)
        {
            // Determine if terminator was CRLFCRLF (4) or LFLF (2)
            if (headerEnd + 3 < buffer.Length &&
                buffer[headerEnd] == (byte)'\r' && buffer[headerEnd + 1] == (byte)'\n' &&
                buffer[headerEnd + 2] == (byte)'\r' && buffer[headerEnd + 3] == (byte)'\n')
                return 4;
            if (headerEnd + 1 < buffer.Length && buffer[headerEnd] == (byte)'\n' && buffer[headerEnd + 1] == (byte)'\n')
                return 2;
            return 4; // default
        }

        private static int ParseContentLength(string headerText)
        {
            var lines = headerText.Split(new[] {"\r\n", "\n"}, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var idx = line.IndexOf(':');
                if (idx <= 0) continue;
                var name = line.Substring(0, idx).Trim();
                if (!name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) continue;
                var value = line.Substring(idx + 1).Trim();
                if (int.TryParse(value, out var len)) return len;
            }
            return -1;
        }
    }
}
