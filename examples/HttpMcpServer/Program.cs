using COA.Mcp.Framework.Server;
using COA.Mcp.Framework.Transport.Configuration;
using COA.Mcp.Protocol;
using Microsoft.Extensions.Logging;

namespace HttpMcpServer;

/// <summary>
/// Example MCP server demonstrating HTTP and WebSocket transport.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        // Parse command line arguments
        var port = args.Length > 0 && int.TryParse(args[0], out var p) ? p : 5000;
        var enableWebSocket = !args.Contains("--no-websocket");
        var useHttps = args.Contains("--https");
        var certPath = args.FirstOrDefault(a => a.StartsWith("--cert="))?.Substring(7);
        var certPassword = args.FirstOrDefault(a => a.StartsWith("--cert-password="))?.Substring(16);
        
        var protocol = useHttps ? "https" : "http";
        var wsProtocol = useHttps ? "wss" : "ws";
        
        Console.WriteLine("===========================================");
        Console.WriteLine("  HTTP/WebSocket MCP Server Example");
        Console.WriteLine("===========================================");
        Console.WriteLine($"Port: {port}");
        Console.WriteLine($"Protocol: {protocol.ToUpper()}");
        Console.WriteLine($"WebSocket: {(enableWebSocket ? "Enabled" : "Disabled")}");
        if (useHttps && string.IsNullOrEmpty(certPath))
        {
            Console.WriteLine($"Certificate: Development (self-signed)");
            Console.WriteLine();
            Console.WriteLine("⚠️  WARNING: Using development certificate.");
            Console.WriteLine("   This is only suitable for testing.");
            Console.WriteLine("   Provide --cert=path/to/cert.pfx for production.");
        }
        else if (useHttps)
        {
            Console.WriteLine($"Certificate: {certPath}");
        }
        Console.WriteLine($"Endpoints:");
        Console.WriteLine($"  - {protocol}://localhost:{port}/mcp/health");
        Console.WriteLine($"  - {protocol}://localhost:{port}/mcp/tools");
        Console.WriteLine($"  - {protocol}://localhost:{port}/mcp/rpc");
        if (enableWebSocket)
        {
            Console.WriteLine($"  - {wsProtocol}://localhost:{port}/mcp/ws");
        }
        Console.WriteLine("===========================================");
        Console.WriteLine();

        // Create and configure the server
        var builder = McpServer.CreateBuilder()
            .WithServerInfo("HTTP MCP Example Server", "1.0.0")
            .UseHttpTransport(options =>
            {
                options.Port = port;
                options.Host = "localhost";
                options.UseHttps = useHttps;
                options.CertificatePath = certPath;
                options.CertificatePassword = certPassword;
                options.EnableWebSocket = enableWebSocket;
                options.EnableCors = true;
                options.AllowedOrigins = new[] { "*" };
            })
            .ConfigureLogging(logging =>
            {
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            });

        // Register example tools
        builder.RegisterToolType<WeatherTool>();
        builder.RegisterToolType<CalculatorTool>();
        builder.RegisterToolType<TimeTool>();
        
        // Resources would be added here if using ResourceRegistry
        // For now, this example focuses on tools and transport

        Console.WriteLine("Starting server...");
        
        // Create cancellation token for graceful shutdown
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\nShutdown requested...");
        };

        try
        {
            // Run the server
            await builder.RunAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Server stopped gracefully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Server error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}