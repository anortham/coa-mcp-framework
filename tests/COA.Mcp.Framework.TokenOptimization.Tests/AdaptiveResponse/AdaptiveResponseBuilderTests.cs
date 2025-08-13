using System.Data;
using COA.Mcp.Framework.TokenOptimization.AdaptiveResponse;
using COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Examples;
using COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Models;
using COA.Mcp.Framework.TokenOptimization.ResponseBuilders;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace COA.Mcp.Framework.TokenOptimization.Tests.AdaptiveResponse;

/// <summary>
/// Tests for the AdaptiveResponseBuilder framework.
/// </summary>
[TestFixture]
public class AdaptiveResponseBuilderTests
{
    private ILogger<AdaptiveSearchTool> _logger = null!;
    
    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<AdaptiveSearchTool>();
    }
    
    [Test]
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
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Query, Is.EqualTo("TestMethod"));
        Assert.That(result.Results, Is.Not.Null);
        Assert.That(result.Results.Count, Is.GreaterThan(0));
        Assert.That(result.Message, Is.Not.Null);
        Assert.That(result.Message, Does.Contain("TestMethod"));
        
        TestContext.WriteLine($"Generated {result.Results.Count} search results");
        TestContext.WriteLine($"Message length: {result.Message?.Length ?? 0} characters");
        TestContext.WriteLine($"IDE Display Hint: {result.IDEDisplayHint}");
    }
    
    [Test]
    public async Task BuildResponseAsync_WithSummaryMode_ShouldReturnConciseResult()
    {
        // Arrange
        var tool = new AdaptiveSearchTool(_logger);
        var searchParams = new SearchParams
        {
            Query = "function",
            ResponseMode = "summary"
        };
        var context = new ResponseContext
        {
            ResponseMode = "summary",
            ToolName = "adaptive_search"
        };
        
        // Act
        var result = await tool.BuildResponseAsync(searchParams, context);
        
        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Message, Is.Not.Null);
        
        // Summary mode should be more concise
        var messageLength = result.Message?.Length ?? 0;
        Assert.That(messageLength, Is.LessThan(2000));
        
        TestContext.WriteLine($"Summary mode message length: {messageLength} characters");
    }
    
    [Test]
    public void IDEEnvironment_Detect_ShouldReturnValidEnvironment()
    {
        // Act
        var environment = IDEEnvironment.Detect();
        
        // Assert
        Assert.That(environment, Is.Not.Null);
        Assert.That(environment.IDE, Is.Not.EqualTo(IDEType.Unknown));
        Assert.That(environment.GetDisplayName(), Is.Not.Empty);
        
        TestContext.WriteLine($"Detected IDE: {environment.GetDisplayName()}");
        TestContext.WriteLine($"Supports HTML: {environment.SupportsHTML}");
        TestContext.WriteLine($"Supports Markdown: {environment.SupportsMarkdown}");
        TestContext.WriteLine($"Supports Interactive: {environment.SupportsInteractive}");
    }
    
    [TestCase(IDEType.VSCode, true)]
    [TestCase(IDEType.VS2022, false)]
    [TestCase(IDEType.Terminal, false)]
    [TestCase(IDEType.Browser, true)]
    public void OutputFormatterFactory_CreateInlineFormatter_ShouldReturnCorrectFormatter(IDEType ideType, bool supportsHtml)
    {
        // Arrange
        var environment = new IDEEnvironment 
        { 
            IDE = ideType, 
            SupportsHTML = supportsHtml,
            SupportsMarkdown = ideType == IDEType.VSCode,
            SupportsInteractive = supportsHtml
        };
        var factory = new COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Formatters.OutputFormatterFactory();
        
        // Act
        var formatter = factory.CreateInlineFormatter(environment);
        
        // Assert
        Assert.That(formatter, Is.Not.Null);
        
        var formatterType = formatter.GetType().Name;
        TestContext.WriteLine($"Created formatter: {formatterType} for IDE: {ideType}");
        
        // Test basic formatting
        var summary = formatter.FormatSummary("Test Summary");
        Assert.That(summary, Is.Not.Empty);
        Assert.That(summary.ToUpperInvariant(), Does.Contain("TEST SUMMARY"));
    }
    
    [Test]
    public void FileReference_GetTerminalReference_ShouldGenerateCorrectFormat()
    {
        // Arrange
        var fileRef = new FileReference
        {
            FilePath = @"C:\source\test\example.cs",
            Line = 42,
            Column = 15,
            Description = "Test method"
        };
        
        // Act
        var reference = fileRef.GetTerminalReference();
        
        // Assert
        Assert.That(reference, Is.Not.Empty);
        Assert.That(reference, Does.Contain("example.cs"));
        Assert.That(reference, Does.Contain("42"));
        Assert.That(reference, Does.Contain("15"));
        
        TestContext.WriteLine($"Terminal reference: {reference}");
    }
    
    [Test]
    public void ActionItem_ShouldHaveRequiredProperties()
    {
        // Arrange & Act
        var action = new ActionItem
        {
            Title = "Test Action",
            Command = "test.command",
            Description = "Test description",
            Category = "Test"
        };
        
        // Assert
        Assert.That(action.Title, Is.EqualTo("Test Action"));
        Assert.That(action.Command, Is.EqualTo("test.command"));
        Assert.That(action.Description, Is.EqualTo("Test description"));
        Assert.That(action.Category, Is.EqualTo("Test"));
    }
}

/// <summary>
/// Simple test logger implementation for unit tests.
/// </summary>
public class TestLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        TestContext.WriteLine($"[{logLevel}] {message}");
        if (exception != null)
        {
            TestContext.WriteLine($"Exception: {exception}");
        }
    }
}