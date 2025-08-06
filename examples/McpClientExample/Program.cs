using System;
using System.Threading.Tasks;
using COA.Mcp.Client;
using COA.Mcp.Client.Configuration;
using COA.Mcp.Framework.Models;
using Microsoft.Extensions.Logging;

namespace McpClientExample
{
    /// <summary>
    /// Example application demonstrating the MCP Client library usage.
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            // Setup logging
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            Console.WriteLine("=== MCP Client Example Application ===\n");

            // Determine server URL from args or use default
            var serverUrl = args.Length > 0 ? args[0] : "http://localhost:5000";
            Console.WriteLine($"Connecting to MCP server at: {serverUrl}\n");

            try
            {
                // Example 1: Basic client usage
                await BasicClientExample(serverUrl, loggerFactory);

                // Example 2: Fluent API usage
                await FluentApiExample(serverUrl, loggerFactory);

                // Example 3: Typed client usage
                await TypedClientExample(serverUrl, loggerFactory);

                // Example 4: Advanced features
                await AdvancedFeaturesExample(serverUrl, loggerFactory);

                Console.WriteLine("\n✅ All examples completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        /// <summary>
        /// Example 1: Basic client usage with manual configuration.
        /// </summary>
        static async Task BasicClientExample(string serverUrl, ILoggerFactory loggerFactory)
        {
            Console.WriteLine("--- Example 1: Basic Client Usage ---\n");

            // Create client with manual configuration
            var options = new McpClientOptions
            {
                BaseUrl = serverUrl,
                TimeoutSeconds = 30,
                EnableRetries = true,
                MaxRetryAttempts = 3,
                ClientInfo = new ClientInfo
                {
                    Name = "Example MCP Client",
                    Version = "1.0.0"
                }
            };

            var logger = loggerFactory.CreateLogger<McpHttpClient>();
            using var client = new McpHttpClient(options, logger: logger);

            // Connect and initialize
            await client.ConnectAsync();
            Console.WriteLine("✓ Connected to server");

            var initResponse = await client.InitializeAsync();
            Console.WriteLine($"✓ Initialized session with server: {initResponse.ServerInfo?.Name} v{initResponse.ServerInfo?.Version}");

            // List available tools
            var tools = await client.ListToolsAsync();
            Console.WriteLine($"\n✓ Found {tools.Tools?.Count ?? 0} tools:");
            
            if (tools.Tools != null)
            {
                foreach (var tool in tools.Tools)
                {
                    Console.WriteLine($"  - {tool.Name}: {tool.Description}");
                }
            }

            // Call a tool (if available)
            if (tools.Tools?.Count > 0)
            {
                var firstTool = tools.Tools[0];
                Console.WriteLine($"\n✓ Calling tool: {firstTool.Name}");
                
                var result = await client.CallToolAsync(firstTool.Name, new { });
                Console.WriteLine($"  Result: {(result.IsError ? "Error" : "Success")}");
            }

            await client.DisconnectAsync();
            Console.WriteLine("\n✓ Disconnected from server\n");
        }

        /// <summary>
        /// Example 2: Fluent API for client configuration and tool invocation.
        /// </summary>
        static async Task FluentApiExample(string serverUrl, ILoggerFactory loggerFactory)
        {
            Console.WriteLine("--- Example 2: Fluent API Usage ---\n");

            // Build client using fluent API
            var client = await McpClientBuilder
                .Create(serverUrl)
                .WithTimeout(TimeSpan.FromSeconds(60))
                .WithRetry(maxAttempts: 3, delayMs: 1000)
                .WithCircuitBreaker(failureThreshold: 5, durationSeconds: 30)
                .WithClientInfo("Fluent API Client", "1.0.0")
                .WithRequestLogging(true)
                .UseLoggerFactory(loggerFactory)
                .BuildAndInitializeAsync();

            Console.WriteLine("✓ Client built and initialized using fluent API");

            // List tools
            var tools = await client.ListToolsAsync();
            Console.WriteLine($"✓ Available tools: {tools.Tools?.Count ?? 0}");

            // Use fluent tool invocation (if calculator tool exists)
            if (tools.Tools?.Any(t => t.Name == "calculator") == true)
            {
                Console.WriteLine("\n✓ Calling calculator tool using fluent API:");

                var result = await client
                    .CallTool("calculator")
                    .WithParameters(new { operation = "add", a = 10, b = 20 })
                    .ExecuteAsync();

                Console.WriteLine($"  10 + 20 = {result.Content?.FirstOrDefault()}");
            }

            client.Dispose();
            Console.WriteLine("\n✓ Client disposed\n");
        }

        /// <summary>
        /// Example 3: Strongly-typed client for type-safe tool interactions.
        /// </summary>
        static async Task TypedClientExample(string serverUrl, ILoggerFactory loggerFactory)
        {
            Console.WriteLine("--- Example 3: Typed Client Usage ---\n");

            // Define parameter and result types
            var typedClient = await McpClientBuilder
                .Create(serverUrl)
                .WithTimeout(TimeSpan.FromSeconds(30))
                .UseLoggerFactory(loggerFactory)
                .BuildTypedAndInitializeAsync<CalculatorParams, CalculatorResult>();

            Console.WriteLine("✓ Typed client created for CalculatorParams -> CalculatorResult");

            // Check if calculator tool exists
            var tools = await typedClient.ListToolsAsync();
            if (tools.Tools?.Any(t => t.Name == "calculator") == true)
            {
                // Use strongly-typed tool invocation
                var parameters = new CalculatorParams
                {
                    Operation = "multiply",
                    A = 7,
                    B = 6
                };

                Console.WriteLine($"\n✓ Calling calculator with typed parameters: {parameters.A} * {parameters.B}");

                var result = await typedClient
                    .CallTool("calculator")
                    .WithParameters(parameters)
                    .WithRetry()
                    .ExecuteAsync();

                if (result.Success)
                {
                    Console.WriteLine($"  Result: {result.Value} (strongly-typed)");
                }
                else
                {
                    Console.WriteLine($"  Error: {result.Error?.Message}");
                }

                // Batch operations
                Console.WriteLine("\n✓ Executing batch operations:");
                var batchCalls = new Dictionary<string, CalculatorParams>
                {
                    ["add"] = new CalculatorParams { Operation = "add", A = 5, B = 3 },
                    ["subtract"] = new CalculatorParams { Operation = "subtract", A = 10, B = 4 },
                    ["multiply"] = new CalculatorParams { Operation = "multiply", A = 3, B = 3 }
                };

                var batchResults = await typedClient.CallToolsBatchAsync(batchCalls);
                
                foreach (var (operation, batchResult) in batchResults)
                {
                    if (batchResult.Success)
                    {
                        Console.WriteLine($"  {operation}: {batchResult.Value}");
                    }
                }
            }
            else
            {
                Console.WriteLine("  Calculator tool not available on server");
            }

            typedClient.Dispose();
            Console.WriteLine("\n✓ Typed client disposed\n");
        }

        /// <summary>
        /// Example 4: Advanced features - authentication, custom headers, events.
        /// </summary>
        static async Task AdvancedFeaturesExample(string serverUrl, ILoggerFactory loggerFactory)
        {
            Console.WriteLine("--- Example 4: Advanced Features ---\n");

            // Build client with advanced configuration
            var builder = McpClientBuilder
                .Create(serverUrl)
                .WithApiKey("demo-api-key-12345")  // API key authentication
                .WithHeader("X-Custom-Header", "CustomValue")  // Custom headers
                .WithCircuitBreaker(3, 10)  // Circuit breaker
                .WithMetrics(true)  // Enable metrics
                .UseLoggerFactory(loggerFactory);

            var client = builder.Build();

            // Subscribe to events
            client.Connected += (sender, e) =>
            {
                Console.WriteLine($"  [Event] Connected at {e.ConnectedAt:HH:mm:ss}");
            };

            client.Disconnected += (sender, e) =>
            {
                Console.WriteLine($"  [Event] Disconnected: {e.Reason}");
            };

            client.NotificationReceived += (sender, e) =>
            {
                Console.WriteLine($"  [Event] Notification: {e.Method}");
            };

            try
            {
                // Connect with authentication
                Console.WriteLine("✓ Connecting with API key authentication...");
                await client.ConnectAsync();
                await client.InitializeAsync();

                // List resources (if supported)
                Console.WriteLine("\n✓ Checking for resources...");
                var resources = await client.ListResourcesAsync();
                Console.WriteLine($"  Found {resources.Resources?.Count ?? 0} resources");

                if (resources.Resources?.Count > 0)
                {
                    var firstResource = resources.Resources[0];
                    Console.WriteLine($"  Reading resource: {firstResource.Name}");
                    
                    var resourceContent = await client.ReadResourceAsync(firstResource.Uri);
                    Console.WriteLine($"  Resource content type: {resourceContent.Contents?.FirstOrDefault()?.MimeType}");
                }

                // List prompts (if supported)
                Console.WriteLine("\n✓ Checking for prompts...");
                var prompts = await client.ListPromptsAsync();
                Console.WriteLine($"  Found {prompts.Prompts?.Count ?? 0} prompts");

                // Demonstrate error handling
                Console.WriteLine("\n✓ Testing error handling...");
                try
                {
                    await client.CallToolAsync("non_existent_tool", new { });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Expected error caught: {ex.Message}");
                }
            }
            finally
            {
                await client.DisconnectAsync();
                client.Dispose();
            }

            Console.WriteLine("\n✓ Advanced features example completed\n");
        }
    }

    /// <summary>
    /// Example calculator parameters for typed client.
    /// </summary>
    public class CalculatorParams
    {
        public string Operation { get; set; } = "add";
        public double A { get; set; }
        public double B { get; set; }
    }

    /// <summary>
    /// Example calculator result for typed client.
    /// </summary>
    public class CalculatorResult : ToolResultBase
    {
        public override string Operation => "calculate";
        public double Value { get; set; }
        public string? Expression { get; set; }
    }
}