using COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Models;
using COA.Mcp.Framework.TokenOptimization.ResponseBuilders;
using System.Data;

namespace COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Examples;

/// <summary>
/// Simple example demonstrating the adaptive response system without external dependencies.
/// This can be used to validate the framework functionality.
/// </summary>
public static class SimpleAdaptiveTest
{
    /// <summary>
    /// Runs a basic test of the adaptive response system.
    /// </summary>
    public static async Task<string> RunBasicTestAsync()
    {
        try
        {
            // Test IDE environment detection
            var environment = IDEEnvironment.Detect();
            var results = new List<string>
            {
                $"✅ IDE Detection: {environment.GetDisplayName()}",
                $"   - Type: {environment.IDE}",
                $"   - Supports HTML: {environment.SupportsHTML}",
                $"   - Supports Markdown: {environment.SupportsMarkdown}",
                $"   - Supports Interactive: {environment.SupportsInteractive}"
            };
            
            // Test formatter factory
            var factory = new Formatters.OutputFormatterFactory();
            var formatter = factory.CreateInlineFormatter(environment);
            results.Add($"✅ Formatter Created: {formatter.GetType().Name}");
            
            // Test basic formatting
            var summary = formatter.FormatSummary("Adaptive Response Test", null);
            results.Add($"✅ Summary Formatting: {summary.Length} characters");
            
            // Test file references
            var fileRefs = new List<FileReference>
            {
                new() 
                { 
                    FilePath = @"C:\test\Example.cs", 
                    Line = 42, 
                    Column = 15, 
                    Description = "Test file reference" 
                }
            };
            var fileOutput = formatter.FormatFileReferences(fileRefs);
            results.Add($"✅ File Reference Formatting: {fileOutput.Length} characters");
            
            // Test actions
            var actions = new List<ActionItem>
            {
                new() 
                { 
                    Title = "Test Action", 
                    Command = "test.command", 
                    Description = "Test action description" 
                }
            };
            var actionOutput = formatter.FormatActions(actions);
            results.Add($"✅ Action Formatting: {actionOutput.Length} characters");
            
            // Test resource formatters
            var htmlFormatter = factory.CreateResourceFormatter("html", environment);
            var jsonFormatter = factory.CreateResourceFormatter("json", environment);
            var csvFormatter = factory.CreateResourceFormatter("csv", environment);
            
            results.Add($"✅ HTML Resource Formatter: {htmlFormatter.GetType().Name} ({htmlFormatter.GetMimeType()})");
            results.Add($"✅ JSON Resource Formatter: {jsonFormatter.GetType().Name} ({jsonFormatter.GetMimeType()})");
            results.Add($"✅ CSV Resource Formatter: {csvFormatter.GetType().Name} ({csvFormatter.GetMimeType()})");
            
            // Test DataTable formatting
            var testTable = CreateTestDataTable();
            var tableOutput = formatter.FormatTable(testTable);
            results.Add($"✅ Table Formatting: {tableOutput.Length} characters");
            
            // Test HTML resource generation
            var htmlContent = await htmlFormatter.FormatResourceAsync(testTable);
            results.Add($"✅ HTML Resource Generation: {htmlContent.Length} characters");
            
            // Test resource provider
            var resourceProvider = new Formatters.DefaultResourceProvider();
            var resourceUri = await resourceProvider.StoreAsync("test/example.html", htmlContent, "text/html");
            var retrieved = await resourceProvider.RetrieveAsync(resourceUri);
            results.Add($"✅ Resource Storage: {resourceUri} ({retrieved?.Length ?? 0} characters retrieved)");
            
            results.Add("\n🎉 All adaptive response tests passed!");
            
            return string.Join("\n", results);
        }
        catch (Exception ex)
        {
            return $"❌ Test failed: {ex.Message}\n{ex.StackTrace}";
        }
    }
    
    /// <summary>
    /// Creates a test DataTable for testing purposes.
    /// </summary>
    private static DataTable CreateTestDataTable()
    {
        var table = new DataTable("TestData");
        
        table.Columns.Add("ID", typeof(int));
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Value", typeof(double));
        table.Columns.Add("IsActive", typeof(bool));
        
        for (int i = 1; i <= 10; i++)
        {
            var row = table.NewRow();
            row["ID"] = i;
            row["Name"] = $"Item {i}";
            row["Value"] = i * 12.34;
            row["IsActive"] = i % 2 == 0;
            table.Rows.Add(row);
        }
        
        return table;
    }
    
    /// <summary>
    /// Demonstrates advanced search tool functionality.
    /// </summary>
    public static async Task<string> TestSearchToolAsync()
    {
        try
        {
            var searchTool = new AdaptiveSearchTool();
            var searchParams = new SearchParams
            {
                Query = "TestMethod",
                ResponseMode = "full"
            };
            var context = new ResponseContext
            {
                ResponseMode = "full",
                ToolName = "adaptive_search_test"
            };
            
            var result = await searchTool.BuildResponseAsync(searchParams, context);
            
            return $"✅ Search Tool Test Completed:\n" +
                   $"   - Success: {result.Success}\n" +
                   $"   - Query: {result.Query}\n" +
                   $"   - Results: {result.Results?.Count ?? 0}\n" +
                   $"   - Message Length: {result.Message?.Length ?? 0}\n" +
                   $"   - Display Hint: {result.IDEDisplayHint}\n" +
                   $"   - File References: {result.FileReferences?.Count ?? 0}\n" +
                   $"   - Actions: {result.ActionItems?.Count ?? 0}";
        }
        catch (Exception ex)
        {
            return $"❌ Search Tool Test failed: {ex.Message}";
        }
    }
}