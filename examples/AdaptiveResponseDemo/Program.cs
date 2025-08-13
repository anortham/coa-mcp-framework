using COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Examples;

namespace AdaptiveResponseDemo;

/// <summary>
/// Demo program to test the Adaptive Response Framework.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🚀 COA MCP Framework - Adaptive Response Demo");
        Console.WriteLine("==============================================\n");
        
        // Run basic tests
        Console.WriteLine("Running basic adaptive response tests...\n");
        var basicResults = await SimpleAdaptiveTest.RunBasicTestAsync();
        Console.WriteLine(basicResults);
        
        Console.WriteLine("\n" + new string('-', 60) + "\n");
        
        // Run search tool test
        Console.WriteLine("Testing adaptive search tool...\n");
        var searchResults = await SimpleAdaptiveTest.TestSearchToolAsync();
        Console.WriteLine(searchResults);
        
        Console.WriteLine("\n🎉 Demo completed! Press any key to exit...");
        Console.ReadKey();
    }
}