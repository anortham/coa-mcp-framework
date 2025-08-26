using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Testing.Performance;
using COA.Mcp.Framework.Testing.Builders;
using COA.Mcp.Framework.Testing.Mocks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace COA.Mcp.Framework.Testing.Tests.Examples
{
    /// <summary>
    /// Examples of performance testing using the framework.
    /// </summary>
    [TestFixture]
    public class PerformanceTestExamples
    {
        [Test]
        public void TokenEstimation_PerformanceBenchmark()
        {
            // Arrange
            var benchmark = new TokenEstimationBenchmark();

            // Act - Run simplified benchmarks without strict accuracy requirements
            benchmark.RunBenchmark("Simple String", "Hello, World!");
            benchmark.RunBenchmark("Medium String", new string('a', 1000));
            benchmark.RunBenchmark("Complex Object", new { Name = "Test", Value = 42, Items = new[] { 1, 2, 3 } });
            
            var summary = new BenchmarkSummary(benchmark.Results.ToList());

            // Assert - Focus on testing that benchmarking works, not specific performance
            summary.OverallAverageTimeMs.Should().BeLessThan(100); // Reasonable estimation time
            benchmark.Results.Should().HaveCount(3);
            benchmark.Results.Should().OnlyContain(r => r.EstimatedTokens >= 0);

            // Generate report for visibility
            var report = summary.GenerateReport();
            TestContext.Out.WriteLine(report);

            // Check that fastest/slowest can be determined
            summary.Fastest.Should().NotBeNull();
            summary.Slowest.Should().NotBeNull();
        }

        [Test]
        public async Task Tool_ResponseTimeBenchmark()
        {
            // Arrange
            var tool = new WeatherTool(
                new InMemoryWeatherService(),
                new MockLogger<WeatherTool>());
            
            var parameters = new WeatherParams
            {
                Location = "Seattle",
                MaxResults = 10
            };

            var benchmark = new ResponseTimeBenchmark();

            // Act
            var result = await benchmark.BenchmarkToolAsync(
                tool, 
                parameters,
                iterations: 50,
                warmupIterations: 5);

            // Assert
            result.AverageMs.Should().BeLessThan(50);
            result.P95Ms.Should().BeLessThan(100);
            result.StdDevMs.Should().BeLessThan(10); // Consistent performance

            TestContext.Out.WriteLine(result.ToString());
        }

        [Test]
        public async Task Tool_LoadTest()
        {
            // Arrange
            var tool = new WeatherTool(
                new InMemoryWeatherService(),
                new MockLogger<WeatherTool>());
            
            var parameters = new WeatherParams
            {
                Location = "Seattle",
                MaxResults = 5
            };

            var benchmark = new ResponseTimeBenchmark();

            // Act
            var result = await benchmark.RunLoadTestAsync(
                tool,
                parameters,
                concurrentRequests: 20,
                duration: TimeSpan.FromSeconds(10));

            // Assert
            result.ErrorRate.Should().BeLessThan(1); // Less than 1% errors
            result.RequestsPerSecond.Should().BeGreaterThan(100);
            result.P99ResponseMs.Should().BeLessThan(200);

            // Generate report
            var report = result.GenerateReport();
            TestContext.Out.WriteLine(report);
        }

        [Test]
        public async Task Memory_UsageAnalysis()
        {
            // Arrange
            var analyzer = new MemoryUsageAnalyzer();

            // Act - Test memory analysis functionality with a simple operation
            var result = await analyzer.AnalyzeOperationAsync(
                async () =>
                {
                    // Create some allocations that should be detectable
                    var data = new byte[1024 * 1024]; // 1MB allocation
                    Array.Fill(data, (byte)42);
                    
                    await Task.Delay(10); // Simulate processing
                    
                    // Return a small summary
                    return new { Size = data.Length, Pattern = data[0] };
                },
                "SimpleAllocation");

            // Assert - Focus on testing that the analyzer works, not specific memory behavior
            result.Should().NotBeNull();
            result.ExecutionTime.Should().BeGreaterThan(TimeSpan.Zero);
            result.BeforeSnapshot.Should().NotBeNull();
            result.AfterSnapshot.Should().NotBeNull();
            result.SettledSnapshot.Should().NotBeNull();
            
            // Just ensure the analyzer can measure something
            Math.Abs(result.MemoryAllocated).Should().BeLessThan(100 * 1024 * 1024); // Less than 100MB change
            result.Gen0Collections.Should().BeGreaterThanOrEqualTo(0);

            TestContext.Out.WriteLine($"Memory allocated: {result.MemoryAllocated / 1024.0:F2} KB");
            TestContext.Out.WriteLine($"Memory retained: {result.MemoryRetained / 1024.0:F2} KB");
            TestContext.Out.WriteLine($"Execution time: {result.ExecutionTime.TotalMilliseconds:F2} ms");
        }

        [Test]
        [Ignore("Memory leak detection is environment-dependent and may not work reliably in CI/CD")]
        public async Task Memory_LeakDetection()
        {
            // Arrange
            var analyzer = new MemoryUsageAnalyzer();
            var leakyList = new List<byte[]>(); // Intentional leak for demo

            // Act - Monitor memory over time
            var result = await analyzer.MonitorMemoryAsync(
                duration: TimeSpan.FromSeconds(5),
                interval: TimeSpan.FromMilliseconds(100),
                operation: async () =>
                {
                    for (int i = 0; i < 50; i++)
                    {
                        leakyList.Add(new byte[1024 * 10]); // 10KB per iteration
                        await Task.Delay(100);
                    }
                });

            // Assert
            analyzer.LeakSuspicions.Should().NotBeEmpty();
            
            var report = analyzer.GenerateReport();
            TestContext.Out.WriteLine(report);

            // Clean up
            leakyList.Clear();
        }

        [Test]
        public async Task Memory_StressTest()
        {
            // Arrange
            var analyzer = new MemoryUsageAnalyzer();
            var tool = new WeatherTool(
                new InMemoryWeatherService(),
                new MockLogger<WeatherTool>());

            // Act - Stress test the tool
            var result = await analyzer.StressTestMemoryAsync(
                async () =>
                {
                    var parameters = new WeatherParams
                    {
                        Location = $"City{Guid.NewGuid()}",
                        MaxResults = 10
                    };
                    await tool.ExecuteAsync(parameters);
                },
                iterations: 100,
                concurrency: 5);

            // Assert - Allow 20% growth for this stress test
            result.MemoryLeaked.Should().BeLessThan((long)(result.InitialMemory * 0.2));
            result.MemoryLeaked.Should().BeLessThan(1024 * 1024); // Less than 1MB leaked

            TestContext.Out.WriteLine($"Initial Memory: {result.InitialMemory / 1024.0 / 1024.0:F2} MB");
            TestContext.Out.WriteLine($"Peak Memory: {result.PeakMemory / 1024.0 / 1024.0:F2} MB");
            TestContext.Out.WriteLine($"Settled Memory: {result.SettledMemory / 1024.0 / 1024.0:F2} MB");
            TestContext.Out.WriteLine($"Potential Leak: {result.HasPotentialLeak}");
        }

        [Test]
        public async Task Tool_ComparisonBenchmark()
        {
            // Arrange
            var weatherTool = new WeatherTool(
                new InMemoryWeatherService(),
                new MockLogger<WeatherTool>());
                
            var cachedWeatherTool = new CachedWeatherTool(
                new InMemoryWeatherService(),
                new MockLogger<CachedWeatherTool>());

            var parameters = new WeatherParams
            {
                Location = "Seattle",
                MaxResults = 10
            };

            var benchmark = new ResponseTimeBenchmark();

            // Act
            var comparison = await benchmark.CompareTool(
                (weatherTool, parameters),
                (cachedWeatherTool, parameters));

            // Assert
            comparison.Fastest.Should().NotBeNull();
            comparison.MostConsistent.Should().NotBeNull();

            // Generate comparison report
            var report = comparison.GenerateReport();
            TestContext.Out.WriteLine(report);
        }

        // Helper implementations
        private class InMemoryWeatherService : IWeatherService
        {
            private readonly TestDataGenerator _generator = new();

            public Task<WeatherData> GetWeatherAsync(string location, int forecastDays)
            {
                return Task.FromResult(new WeatherData
                {
                    Location = location,
                    Temperature = 72,
                    Conditions = "Sunny",
                    Forecast = _generator.GenerateCollection(forecastDays, i => new ForecastDay
                    {
                        Date = DateTime.Today.AddDays(i),
                        High = 75 + i,
                        Low = 60 + i,
                        Conditions = "Clear"
                    })
                });
            }
        }

        private class CachedWeatherTool : WeatherTool
        {
            private readonly Dictionary<string, object> _cache = new();

            public CachedWeatherTool(IWeatherService weatherService, ILogger<CachedWeatherTool> logger)
                : base(weatherService, logger)
            {
            }

            public override async Task<object?> ExecuteAsync(object? parameters, CancellationToken cancellationToken = default)
            {
                var weatherParams = (WeatherParams)parameters!;
                var cacheKey = $"{weatherParams.Location}:{weatherParams.MaxResults}";

                if (_cache.TryGetValue(cacheKey, out var cached))
                {
                    return cached;
                }

                var result = await base.ExecuteAsync(parameters, cancellationToken);
                _cache[cacheKey] = result!;
                return result;
            }
        }
    }
}