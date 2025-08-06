using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace COA.Mcp.Framework.Transport.Correlation
{
    /// <summary>
    /// Represents the context of an HTTP request awaiting a response.
    /// </summary>
    public class HttpRequestContext
    {
        /// <summary>
        /// The HTTP listener context for the request.
        /// </summary>
        public HttpListenerContext HttpContext { get; }

        /// <summary>
        /// The correlation ID for matching request to response.
        /// </summary>
        public string CorrelationId { get; }

        /// <summary>
        /// The JSON-RPC request ID if present.
        /// </summary>
        public object? JsonRpcId { get; set; }

        /// <summary>
        /// Timestamp when the request was received.
        /// </summary>
        public DateTime ReceivedAt { get; }

        /// <summary>
        /// The original request content.
        /// </summary>
        public string RequestContent { get; }

        /// <summary>
        /// Cancellation token for the request.
        /// </summary>
        public CancellationToken CancellationToken { get; }

        public HttpRequestContext(
            HttpListenerContext httpContext,
            string correlationId,
            string requestContent,
            CancellationToken cancellationToken = default)
        {
            HttpContext = httpContext ?? throw new ArgumentNullException(nameof(httpContext));
            CorrelationId = correlationId ?? throw new ArgumentNullException(nameof(correlationId));
            RequestContent = requestContent ?? throw new ArgumentNullException(nameof(requestContent));
            CancellationToken = cancellationToken;
            ReceivedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Sends a response back to the HTTP client.
        /// </summary>
        public async Task SendResponseAsync(string responseContent, int statusCode = 200)
        {
            try
            {
                HttpContext.Response.StatusCode = statusCode;
                HttpContext.Response.ContentType = "application/json";
                
                var bytes = System.Text.Encoding.UTF8.GetBytes(responseContent);
                HttpContext.Response.ContentLength64 = bytes.Length;
                
                await HttpContext.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length, CancellationToken);
                HttpContext.Response.Close();
            }
            catch (Exception ex)
            {
                // Log error if logger available
                try
                {
                    HttpContext.Response.StatusCode = 500;
                    HttpContext.Response.Close();
                }
                catch
                {
                    // Best effort
                }
                
                throw new InvalidOperationException("Failed to send HTTP response", ex);
            }
        }

        /// <summary>
        /// Sends an error response back to the HTTP client.
        /// </summary>
        public async Task SendErrorResponseAsync(string errorMessage, int errorCode = -32603, int httpStatusCode = 500)
        {
            var errorResponse = new
            {
                jsonrpc = "2.0",
                error = new
                {
                    code = errorCode,
                    message = errorMessage,
                    data = new
                    {
                        timestamp = DateTime.UtcNow,
                        correlationId = CorrelationId
                    }
                },
                id = JsonRpcId
            };

            var json = System.Text.Json.JsonSerializer.Serialize(errorResponse);
            await SendResponseAsync(json, httpStatusCode);
        }
    }
}