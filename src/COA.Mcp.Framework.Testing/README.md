# COA.Mcp.Framework.Testing

Comprehensive testing utilities for MCP tools and servers. This package provides base classes, assertions, mocks, and benchmarking tools to ensure your MCP implementations are reliable, performant, and correct.

## Features

- **Base test classes** for tools, prompts, and integration tests
- **Fluent assertions** for MCP-specific validations
- **Mock implementations** for isolated testing
- **Test data generators** for property-based testing
- **Performance benchmarks** for token estimation and response times
- **Memory usage analysis** tools
- **Scenario builders** for complex test cases

## Installation

```xml
<PackageReference Include="COA.Mcp.Framework.Testing" Version="1.4.0" />
<PackageReference Include="NUnit" Version="4.1.0" />
<PackageReference Include="FluentAssertions" Version="6.12.0" />
```

## Quick Start

### Testing a Tool

```csharp
using COA.Mcp.Framework.Testing.Base;
using NUnit.Framework;

[TestFixture]
public class WeatherToolTests : ToolTestBase<WeatherTool>
{
    [Test]
    public async Task GetWeather_ValidLocation_ReturnsWeatherData()
    {
        // Arrange
        var parameters = new WeatherParameters
        {
            Location = "Seattle",
            ForecastDays = 3
        };
        
        // Act
        var result = await ExecuteToolAsync(parameters);
        
        // Assert
        result.Should().BeSuccessful();
        result.Data.Should().NotBeNull();
        result.Data.Location.Should().Be("Seattle");
        result.Data.Forecast.Should().HaveCount(3);
    }
    
    [Test]
    public async Task GetWeather_InvalidLocation_ReturnsError()
    {
        // Arrange
        var parameters = new WeatherParameters
        {
            Location = "",  // Invalid
            ForecastDays = 3
        };
        
        // Act & Assert
        var result = await ExecuteToolAsync(parameters);
        
        result.Should().BeFailure()
            .WithErrorCode(ErrorCode.ValidationFailed)
            .WithRecoverySteps();
    }
}
```

## Base Test Classes

### ToolTestBase<TTool>

Base class for testing individual tools:

```csharp
public class MyToolTests : ToolTestBase<MyTool>
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        // Add test-specific services
        services.AddSingleton<IMyService, MockMyService>();
    }
    
    [Test]
    public async Task TestToolExecution()
    {
        // Arrange
        var parameters = CreateParameters(p =>
        {
            p.Input = "test";
            p.Count = 5;
        });
        
        // Act
        var result = await ExecuteToolAsync(parameters);
        
        // Assert
        result.Should().BeSuccessful();
        result.ExecutionTime.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }
}
```

### McpTestBase

Base class for testing MCP servers and protocols:

```csharp
public class ServerTests : McpTestBase
{
    [Test]
    public async Task Server_Initialize_NegotiatesCapabilities()
    {
        // Arrange
        var server = CreateServer(config =>
        {
            config.EnableTools = true;
            config.EnablePrompts = true;
        });
        
        // Act
        var response = await server.InitializeAsync(new InitializeRequest
        {
            ClientInfo = new ClientInfo { Name = "test-client" }
        });
        
        // Assert
        response.Should().HaveCapability("tools");
        response.Should().HaveCapability("prompts");
    }
}
```

### IntegrationTestBase

Base class for end-to-end integration tests:

```csharp
public class IntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task FullWorkflow_SearchAndProcess_Success()
    {
        // Arrange
        await StartServerAsync();
        var client = CreateClient();
        
        // Act
        var searchResult = await client.CallToolAsync("search", new { query = "test" });
        var processResult = await client.CallToolAsync("process", new { data = searchResult });
        
        // Assert
        searchResult.Should().BeSuccessful();
        processResult.Should().BeSuccessful();
        
        // Verify side effects
        await VerifyDatabaseStateAsync();
        await VerifyLogsAsync();
    }
}
```

## Fluent Assertions

### Tool Assertions

```csharp
using COA.Mcp.Framework.Testing.Assertions;

// Success assertions
result.Should().BeSuccessful();
result.Should().HaveOperation("calculate");
result.Should().HaveNoWarnings();
result.Should().CompleteWithin(TimeSpan.FromSeconds(1));

// Error assertions
result.Should().BeFailure();
result.Should().HaveErrorCode(ErrorCode.NotFound);
result.Should().HaveErrorMessage("File not found");
result.Should().HaveRecoverySteps();

// Data assertions
result.Should().HaveData<WeatherData>();
result.Should().HaveDataMatching<WeatherData>(d => d.Temperature > 0);
```

### Response Assertions

```csharp
// Token assertions
response.Should().FitWithinTokenLimit(5000);
response.Should().HaveEstimatedTokens(1500, tolerance: 100);
response.Should().UseReductionStrategy("progressive");

// Content assertions
response.Should().ContainInsight("No results found");
response.Should().HaveInsightCount(3);
response.Should().SuggestAction("retry");
response.Should().HaveActionCount(2);

// Metadata assertions
response.Should().BeTruncated();
response.Should().HaveResourceUri();
response.Should().HaveCacheKey();
```

### Token Assertions

```csharp
// Estimation assertions
var text = "Some long text...";
text.Should().HaveEstimatedTokens(50, tolerance: 5);
text.Should().FitWithinTokenBudget(100);

// Object assertions
var obj = new { Name = "Test", Items = new[] { 1, 2, 3 } };
obj.Should().HaveEstimatedTokens(25);
obj.Should().RequireReduction(whenBudgetIs: 20);
```

## Mock Implementations

### MockToolRegistry

```csharp
[Test]
public void Registry_RegisterTool_Success()
{
    // Arrange
    var registry = new MockToolRegistry();
    var tool = new MockTool("test-tool");
    
    // Act
    registry.Register(tool);
    
    // Assert
    registry.Should().ContainTool("test-tool");
    registry.GetTool("test-tool").Should().BeSameAs(tool);
}
```

### MockLogger

Capture and verify log messages:

```csharp
[Test]
public async Task Tool_LogsWarning_WhenDataMissing()
{
    // Arrange
    var logger = new MockLogger<MyTool>();
    var tool = new MyTool(logger);
    
    // Act
    await tool.ExecuteAsync(new MyParams { IncludeData = false });
    
    // Assert
    logger.Should().HaveLoggedWarning("Data not available");
    logger.Should().HaveLoggedExactly(1, LogLevel.Warning);
}
```

### MockServiceProvider

```csharp
[Test]
public void Tool_ResolvesServices_FromProvider()
{
    // Arrange
    var provider = new MockServiceProvider()
        .WithService<IMyService>(new MockMyService())
        .WithService<ILogger>(new MockLogger());
    
    // Act
    var tool = provider.CreateInstance<MyTool>();
    
    // Assert
    tool.Should().NotBeNull();
    tool.Should().HaveService<IMyService>();
}
```

### MockResponseBuilder

```csharp
[Test]
public async Task ResponseBuilder_CreatesValidResponse()
{
    // Arrange
    var builder = new MockResponseBuilder()
        .WithInsights("Insight 1", "Insight 2")
        .WithActions(new AIAction { Id = "retry" })
        .WithTokenLimit(1000);
    
    // Act
    var response = await builder.BuildAsync(data);
    
    // Assert
    response.Should().HaveInsightCount(2);
    response.Should().HaveActionCount(1);
    response.Should().FitWithinTokenLimit(1000);
}
```

## Test Data Generators

### TestDataGenerator

Generate test data for property-based testing:

```csharp
[Test]
public void Tool_HandlesAllInputSizes(
    [Values(0, 1, 10, 100, 1000)] int itemCount)
{
    // Arrange
    var generator = new TestDataGenerator();
    var items = generator.GenerateList<string>(itemCount);
    var parameters = new ProcessParams { Items = items };
    
    // Act
    var result = ExecuteToolAsync(parameters);
    
    // Assert
    result.Should().BeSuccessful();
    result.Data.ProcessedCount.Should().Be(itemCount);
}

// Generate specific data types
var users = generator.GenerateUsers(50);
var documents = generator.GenerateDocuments(20);
var jsonData = generator.GenerateJson(maxDepth: 3);
var largeText = generator.GenerateText(tokens: 5000);
```

### ToolParameterBuilder

Build complex parameter objects fluently:

```csharp
[Test]
public async Task Tool_WithComplexParameters_Success()
{
    // Arrange
    var parameters = new ToolParameterBuilder<SearchParams>()
        .With(p => p.Query, "test query")
        .With(p => p.Filters, new FilterBuilder()
            .WithDateRange(DateTime.Now.AddDays(-7), DateTime.Now)
            .WithTags("important", "urgent")
            .Build())
        .WithRandomValues() // Fill remaining with random data
        .Build();
    
    // Act & Assert
    var result = await ExecuteToolAsync(parameters);
    result.Should().BeSuccessful();
}
```

### ScenarioBuilder

Build complex test scenarios:

```csharp
[Test]
public async Task ComplexScenario_MultipleToolCalls_Success()
{
    // Arrange
    var scenario = new ScenarioBuilder()
        .WithServer("test-server")
        .WithClient("test-client")
        .AddToolCall("search", new { query = "users" })
            .ExpectSuccess()
            .CaptureResultAs("searchResult")
        .AddToolCall("filter", new { data = "${searchResult}", status = "active" })
            .ExpectSuccess()
            .CaptureResultAs("filteredResult")
        .AddToolCall("export", new { data = "${filteredResult}", format = "csv" })
            .ExpectSuccess();
    
    // Act
    var results = await scenario.ExecuteAsync();
    
    // Assert
    results.Should().AllBeSuccessful();
    results["filteredResult"].Should().HaveDataCount(10);
}
```

## Performance Testing

### ResponseTimeBenchmark

Benchmark tool response times:

```csharp
[Test]
[Benchmark]
public async Task BenchmarkToolPerformance()
{
    var benchmark = new ResponseTimeBenchmark<MyTool>();
    
    var results = await benchmark
        .WithIterations(100)
        .WithWarmup(10)
        .WithParameters(new MyParams { /* ... */ })
        .RunAsync();
    
    results.Should().HaveMedianResponseTime(TimeSpan.FromMilliseconds(50));
    results.Should().Have95thPercentile(TimeSpan.FromMilliseconds(100));
    results.Should().HaveNoOutliers();
}
```

### TokenEstimationBenchmark

Benchmark token estimation accuracy:

```csharp
[Test]
public void BenchmarkTokenEstimation()
{
    var benchmark = new TokenEstimationBenchmark();
    
    var results = benchmark
        .AddTestCase("small", smallObject)
        .AddTestCase("medium", mediumObject)
        .AddTestCase("large", largeObject)
        .Run();
    
    results.Should().HaveAccuracy(0.95); // 95% accurate
    results.Should().HaveMaxDeviation(50); // Max 50 tokens off
}
```

### MemoryUsageAnalyzer

Analyze memory usage:

```csharp
[Test]
public async Task Tool_NoMemoryLeaks()
{
    var analyzer = new MemoryUsageAnalyzer();
    
    // Baseline
    analyzer.TakeSnapshot("before");
    
    // Execute tool multiple times
    for (int i = 0; i < 100; i++)
    {
        await ExecuteToolAsync(parameters);
    }
    
    // Force GC and take snapshot
    GC.Collect();
    GC.WaitForPendingFinalizers();
    analyzer.TakeSnapshot("after");
    
    // Assert
    analyzer.Should().HaveNoMemoryLeak();
    analyzer.GetGrowth().Should().BeLessThan(1_000_000); // Less than 1MB growth
}
```

## Advanced Testing Patterns

### Parameterized Tests

```csharp
[TestCase("Seattle", 3, true)]
[TestCase("InvalidCity", 5, false)]
[TestCase("", 1, false)]
public async Task Weather_VariousInputs(
    string location,
    int days,
    bool shouldSucceed)
{
    var parameters = new WeatherParameters
    {
        Location = location,
        ForecastDays = days
    };
    
    var result = await ExecuteToolAsync(parameters);
    
    if (shouldSucceed)
        result.Should().BeSuccessful();
    else
        result.Should().BeFailure();
}
```

### Theory Tests with Data Sources

```csharp
[Theory]
[InlineData("add", 5, 3, 8)]
[InlineData("subtract", 10, 4, 6)]
[InlineData("multiply", 3, 7, 21)]
public async Task Calculator_Operations(
    string operation,
    double a,
    double b,
    double expected)
{
    var result = await ExecuteToolAsync(new CalculatorParams
    {
        Operation = operation,
        A = a,
        B = b
    });
    
    result.Data.Result.Should().Be(expected);
}
```

### Timeout Tests

```csharp
[Test]
[Timeout(5000)] // 5 seconds
public async Task Tool_CompletesWithinTimeout()
{
    var parameters = new LongRunningParams { MaxDuration = 10000 };
    
    var result = await ExecuteToolAsync(parameters);
    
    result.Should().BeSuccessful();
}
```

### Retry Tests

```csharp
[Test]
[Retry(3)] // Retry up to 3 times on failure
public async Task Tool_EventuallySucceeds()
{
    // Test flaky external service
    var result = await ExecuteToolWithExternalServiceAsync();
    
    result.Should().BeSuccessful();
}
```

## Test Organization

### Test Categories

```csharp
[TestFixture]
[Category("Unit")]
public class UnitTests { }

[TestFixture]
[Category("Integration")]
public class IntegrationTests { }

[TestFixture]
[Category("Performance")]
public class PerformanceTests { }

// Run specific categories
// dotnet test --filter Category=Unit
```

### Test Fixtures

```csharp
[TestFixture]
public class DatabaseToolTests
{
    private TestDatabase _database;
    
    [OneTimeSetUp]
    public async Task SetupFixture()
    {
        _database = await TestDatabase.CreateAsync();
    }
    
    [OneTimeTearDown]
    public async Task TearDownFixture()
    {
        await _database.DisposeAsync();
    }
    
    [SetUp]
    public async Task SetupTest()
    {
        await _database.ResetAsync();
    }
}
```

## Best Practices

1. **Test in isolation** - Use mocks for external dependencies
2. **Test edge cases** - Empty inputs, nulls, large datasets
3. **Verify error handling** - Test failure paths
4. **Assert on behavior** - Not implementation details
5. **Keep tests fast** - Mock slow operations
6. **Use descriptive names** - Test_Condition_ExpectedResult
7. **One assertion per test** - Or use FluentAssertions chaining
8. **Clean up resources** - Use proper setup/teardown

## Configuration

### Test Settings

```json
{
  "TestSettings": {
    "DefaultTimeout": 5000,
    "EnableParallelExecution": true,
    "MaxParallelThreads": 4,
    "CaptureOutput": true,
    "LogLevel": "Debug"
  }
}
```

### Custom Test Configuration

```csharp
public class TestConfig : ToolTestBase<MyTool>
{
    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.Configure<TestOptions>(options =>
        {
            options.UseInMemoryDatabase = true;
            options.MockExternalServices = true;
            options.EnableDetailedLogging = true;
        });
    }
}
```

## Troubleshooting

### Common Issues

| Issue | Solution |
|-------|----------|
| Tests timing out | Increase timeout or mock slow operations |
| Flaky tests | Use retry attribute or fix race conditions |
| Memory leaks in tests | Properly dispose resources in teardown |
| Can't mock a service | Ensure interface is used, not concrete class |

## Examples

See `/tests/` directory for comprehensive examples:
- Unit tests for all framework components
- Integration tests for servers
- Performance benchmarks
- Property-based tests

## License

Part of the COA MCP Framework. See LICENSE for details.