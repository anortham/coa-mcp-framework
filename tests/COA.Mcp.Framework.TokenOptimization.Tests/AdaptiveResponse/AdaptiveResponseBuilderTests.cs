using System.Data;
using COA.Mcp.Framework.TokenOptimization.AdaptiveResponse;
using COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Examples;
using COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Models;
using COA.Mcp.Framework.TokenOptimization.ResponseBuilders;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace COA.Mcp.Framework.TokenOptimization.Tests.AdaptiveResponse;

/// <summary>
/// Tests for the AdaptiveResponseBuilder framework.
/// </summary>
public class AdaptiveResponseBuilderTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<AdaptiveSearchTool> _logger;
    
    public AdaptiveResponseBuilderTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = new TestLogger<AdaptiveSearchTool>(output);
    }
    
    [Fact]
    public async Task BuildResponseAsync_WithValidSearch_ShouldReturnFormattedResult()
    {
        // Arrange
        var tool = new AdaptiveSearchTool(_logger);
        var searchParams = new SearchParams
        {
            Query = "TestMethod",
            ResponseMode = "full"
        };
        var context = new ResponseContext
        {
            ResponseMode = "full",
            ToolName = "adaptive_search"
        };
        
        // Act
        var result = await tool.BuildResponseAsync(searchParams, context);
        
        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("TestMethod", result.Query);
        Assert.NotNull(result.Results);
        Assert.True(result.Results.Count > 0);
        Assert.NotNull(result.Message);
        Assert.Contains("TestMethod", result.Message);
        
        _output.WriteLine($"Generated {result.Results.Count} search results");
        _output.WriteLine($"Message length: {result.Message?.Length ?? 0} characters");
        _output.WriteLine($"IDE Display Hint: {result.IDEDisplayHint}");
    }
    
    [Fact]
    public async Task BuildResponseAsync_WithSummaryMode_ShouldReturnConciseResult()
    {
        // Arrange
        var tool = new AdaptiveSearchTool(_logger);
        var searchParams = new SearchParams
        {
            Query = "Configuration",
            ResponseMode = "summary"
        };
        var context = new ResponseContext
        {
            ResponseMode = "summary",
            TokenLimit = 2000,
            ToolName = "adaptive_search"
        };
        
        // Act
        var result = await tool.BuildResponseAsync(searchParams, context);
        
        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Message);
        
        // Summary mode should be more concise
        var estimatedTokens = result.Message?.Length / 4 ?? 0; // Rough estimate
        Assert.True(estimatedTokens < 2000, "Summary should be within token limit");
        
        _output.WriteLine($"Summary message length: {result.Message?.Length ?? 0} characters");
        _output.WriteLine($"Estimated tokens: {estimatedTokens}");
    }
    
    [Fact]
    public void IDEEnvironment_Detect_ShouldReturnValidEnvironment()
    {
        // Act
        var environment = IDEEnvironment.Detect();
        
        // Assert
        Assert.NotNull(environment);
        Assert.NotEqual(IDEType.Unknown, environment.IDE);
        Assert.NotNull(environment.Version);
        
        _output.WriteLine($"Detected IDE: {environment.IDE}");
        _output.WriteLine($"Version: {environment.Version}");
        _output.WriteLine($"Supports HTML: {environment.SupportsHTML}");
        _output.WriteLine($"Supports Markdown: {environment.SupportsMarkdown}");
        _output.WriteLine($"Supports Interactive: {environment.SupportsInteractive}");
        _output.WriteLine($"Display Name: {environment.GetDisplayName()}");
    }
    
    [Fact]
    public async Task AdaptiveResponseBuilder_WithLargeDataset_ShouldCreateResource()
    {
        // Arrange
        var tool = new AdaptiveSearchTool(_logger);
        var searchParams = new SearchParams
        {
            Query = "LargeDatasetTest",
            ResponseMode = "full"
        };
        var context = new ResponseContext
        {
            ResponseMode = "full",
            TokenLimit = 1000, // Low limit to force resource creation
            ToolName = "adaptive_search"
        };
        
        // Act
        var result = await tool.BuildResponseAsync(searchParams, context);
        
        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        
        // Should create resource if message is too large
        if (result.Meta?.Truncated == true)
        {
            Assert.NotNull(result.ResourceUri);
            Assert.StartsWith("mcp://", result.ResourceUri);
            
            _output.WriteLine($"Resource created: {result.ResourceUri}");
            _output.WriteLine($"Estimated tokens: {result.Meta.Tokens}");
        }
    }
    
    [Theory]
    [InlineData(IDEType.VSCode)]
    [InlineData(IDEType.VS2022)]
    [InlineData(IDEType.Terminal)]
    [InlineData(IDEType.Browser)]
    public void OutputFormatterFactory_ShouldCreateAppropriateFormatter(IDEType ideType)
    {
        // Arrange
        var environment = new IDEEnvironment { IDE = ideType };
        var factory = new COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Formatters.OutputFormatterFactory();
        
        // Act
        var formatter = factory.CreateInlineFormatter(environment);
        
        // Assert
        Assert.NotNull(formatter);
        
        var formatterType = formatter.GetType().Name;
        _output.WriteLine($"IDE: {ideType} -> Formatter: {formatterType}");
        
        // Verify correct formatter type
        switch (ideType)
        {
            case IDEType.VSCode:
                Assert.Contains("VSCode", formatterType);
                break;
            case IDEType.VS2022:
                Assert.Contains("VS2022", formatterType);
                break;
            case IDEType.Terminal:
                Assert.Contains("Terminal", formatterType);
                break;
            case IDEType.Browser:
                Assert.Contains("Browser", formatterType);
                break;
        }
    }
    
    [Fact]
    public async Task HTMLResourceFormatter_ShouldGenerateValidHTML()
    {
        // Arrange
        var environment = new IDEEnvironment { IDE = IDEType.VSCode };
        var formatter = new COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Formatters.HTMLResourceFormatter(environment);
        
        // Create test data table
        var dataTable = new DataTable("TestData");
        dataTable.Columns.Add("ID", typeof(int));
        dataTable.Columns.Add("Name", typeof(string));
        dataTable.Columns.Add("Value", typeof(double));
        
        for (int i = 1; i <= 5; i++)
        {
            var row = dataTable.NewRow();
            row["ID"] = i;
            row["Name"] = $"Item {i}";
            row["Value"] = i * 10.5;
            dataTable.Rows.Add(row);
        }
        
        // Act
        var html = await formatter.FormatResourceAsync(dataTable);
        
        // Assert
        Assert.NotNull(html);
        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("<table", html);
        Assert.Contains("Item 1", html);
        Assert.Contains("function sortTable", html); // Should include JavaScript
        
        _output.WriteLine($"Generated HTML length: {html.Length} characters");
        _output.WriteLine($"MIME Type: {formatter.GetMimeType()}");
        _output.WriteLine($"Extension: {formatter.GetFileExtension()}");
    }
    
    [Fact]
    public void FileReference_ShouldGenerateCorrectURIs()
    {
        // Arrange
        var fileRef = new COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Models.FileReference
        {
            FilePath = @"C:\source\project\src\Example.cs",
            Line = 42,
            Column = 15,
            Description = "Test method implementation"
        };
        
        // Act & Assert
        var vsCodeUri = fileRef.GetVSCodeUri();
        var vs2022Ref = fileRef.GetVS2022Reference();
        var terminalRef = fileRef.GetTerminalReference();
        
        Assert.StartsWith("vscode://file/", vsCodeUri);
        Assert.Contains(":42:15", vsCodeUri);
        
        Assert.EndsWith("(42,15)", vs2022Ref);
        Assert.StartsWith(@"C:\source\project\src\Example.cs", vs2022Ref);
        
        Assert.EndsWith(":42:15", terminalRef);
        
        _output.WriteLine($"VS Code URI: {vsCodeUri}");
        _output.WriteLine($"VS 2022 Reference: {vs2022Ref}");
        _output.WriteLine($"Terminal Reference: {terminalRef}");
    }
}

/// <summary>
/// Test logger implementation that writes to xUnit output.
/// </summary>
public class TestLogger<T> : ILogger<T>
{
    private readonly ITestOutputHelper _output;
    
    public TestLogger(ITestOutputHelper output)
    {
        _output = output;
    }
    
    public IDisposable BeginScope<TState>(TState state) => new TestScope();
    
    public bool IsEnabled(LogLevel logLevel) => true;
    
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        _output.WriteLine($"[{logLevel}] {message}");
        
        if (exception != null)
        {
            _output.WriteLine($"Exception: {exception}");
        }
    }
    
    private class TestScope : IDisposable
    {
        public void Dispose() { }
    }
}