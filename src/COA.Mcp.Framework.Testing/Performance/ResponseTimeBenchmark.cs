using COA.Mcp.Framework.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace COA.Mcp.Framework.Testing.Performance
{
    /// <summary>
    /// Benchmarks for tool response times and performance.
    /// </summary>
    public class ResponseTimeBenchmark
    {
        private readonly List<ResponseTimeResult> _results = new();

        /// <summary>
        /// Gets the benchmark results.
        /// </summary>
        public IReadOnlyList<ResponseTimeResult> Results => _results.AsReadOnly();

        /// <summary>
        /// Benchmarks a tool's response time.
        /// </summary>
        /// <param name="tool">The tool to benchmark.</param>
        /// <param name="parameters">The parameters to use.</param>
        /// <param name="iterations">Number of iterations.</param>
        /// <param name="warmupIterations">Number of warmup iterations.</param>
        /// <returns>The benchmark result.</returns>
        public async Task<ResponseTimeResult> BenchmarkToolAsync(
            ITool tool,
            object parameters,
            int iterations = 10,
            int warmupIterations = 3)
        {
            var result = new ResponseTimeResult
            {
                ToolName = tool.ToolName,
                Iterations = iterations,
                Timestamp = DateTime.UtcNow
            };

            // Warmup
            for (int i = 0; i < warmupIterations; i++)
            {
                await tool.ExecuteAsync(parameters);
            }

            // Measure
            var timings = new List<double>();
            var memorySamples = new List<long>();

            for (int i = 0; i < iterations; i++)
            {
                // Force garbage collection for consistent memory measurements
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                var memoryBefore = GC.GetTotalMemory(false);
                
                var sw = Stopwatch.StartNew();
                var response = await tool.ExecuteAsync(parameters);
                sw.Stop();

                var memoryAfter = GC.GetTotalMemory(false);
                var memoryUsed = memoryAfter - memoryBefore;

                timings.Add(sw.Elapsed.TotalMilliseconds);
                memorySamples.Add(memoryUsed);

                result.LastResponse = response;
            }

            // Calculate statistics
            result.AverageMs = timings.Average();
            result.MinMs = timings.Min();
            result.MaxMs = timings.Max();
            result.MedianMs = CalculateMedian(timings);
            result.StdDevMs = CalculateStandardDeviation(timings);
            
            // Calculate percentiles
            var sortedTimings = timings.OrderBy(t => t).ToList();
            result.P95Ms = GetPercentile(sortedTimings, 0.95);
            result.P99Ms = GetPercentile(sortedTimings, 0.99);

            // Memory statistics
            result.AverageMemoryBytes = (long)memorySamples.Average();
            result.MaxMemoryBytes = memorySamples.Max();

            _results.Add(result);
            return result;
        }

        /// <summary>
        /// Benchmarks multiple tools for comparison.
        /// </summary>
        /// <param name="toolBenchmarks">Tool and parameter pairs to benchmark.</param>
        /// <returns>Comparison result.</returns>
        public async Task<ToolComparisonResult> CompareTool
            (params (ITool tool, object parameters)[] toolBenchmarks)
        {
            var results = new List<ResponseTimeResult>();

            foreach (var (tool, parameters) in toolBenchmarks)
            {
                var result = await BenchmarkToolAsync(tool, parameters);
                results.Add(result);
            }

            return new ToolComparisonResult(results);
        }

        /// <summary>
        /// Runs a load test on a tool.
        /// </summary>
        /// <param name="tool">The tool to test.</param>
        /// <param name="parameters">The parameters to use.</param>
        /// <param name="concurrentRequests">Number of concurrent requests.</param>
        /// <param name="duration">Test duration.</param>
        /// <returns>Load test result.</returns>
        public async Task<LoadTestResult> RunLoadTestAsync(
            ITool tool,
            object parameters,
            int concurrentRequests = 10,
            TimeSpan? duration = null)
        {
            duration ??= TimeSpan.FromSeconds(30);
            
            var result = new LoadTestResult
            {
                ToolName = tool.ToolName,
                ConcurrentRequests = concurrentRequests,
                Duration = duration.Value
            };

            var endTime = DateTime.UtcNow + duration.Value;
            var requestTimes = new List<double>();
            var errors = new List<Exception>();
            var totalRequests = 0;

            // Create tasks for concurrent requests
            var tasks = new List<Task>();
            
            for (int i = 0; i < concurrentRequests; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    while (DateTime.UtcNow < endTime)
                    {
                        try
                        {
                            var sw = Stopwatch.StartNew();
                            await tool.ExecuteAsync(parameters);
                            sw.Stop();

                            lock (requestTimes)
                            {
                                requestTimes.Add(sw.Elapsed.TotalMilliseconds);
                                totalRequests++;
                            }
                        }
                        catch (Exception ex)
                        {
                            lock (errors)
                            {
                                errors.Add(ex);
                            }
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Calculate results
            result.TotalRequests = totalRequests;
            result.SuccessfulRequests = totalRequests - errors.Count;
            result.FailedRequests = errors.Count;
            result.RequestsPerSecond = totalRequests / duration.Value.TotalSeconds;
            
            if (requestTimes.Any())
            {
                result.AverageResponseMs = requestTimes.Average();
                result.MinResponseMs = requestTimes.Min();
                result.MaxResponseMs = requestTimes.Max();
                result.MedianResponseMs = CalculateMedian(requestTimes);
                
                var sortedTimes = requestTimes.OrderBy(t => t).ToList();
                result.P95ResponseMs = GetPercentile(sortedTimes, 0.95);
                result.P99ResponseMs = GetPercentile(sortedTimes, 0.99);
            }

            result.ErrorRate = errors.Count * 100.0 / totalRequests;
            result.Errors = errors.GroupBy(e => e.GetType().Name)
                .Select(g => new ErrorInfo
                {
                    ErrorType = g.Key,
                    Count = g.Count(),
                    Message = g.First().Message
                })
                .ToList();

            return result;
        }

        /// <summary>
        /// Clears all benchmark results.
        /// </summary>
        public void Clear()
        {
            _results.Clear();
        }

        private static double CalculateMedian(List<double> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            int n = sorted.Count;
            
            if (n % 2 == 0)
                return (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
            else
                return sorted[n / 2];
        }

        private static double CalculateStandardDeviation(List<double> values)
        {
            if (values.Count <= 1) return 0;

            var avg = values.Average();
            var sum = values.Sum(v => Math.Pow(v - avg, 2));
            return Math.Sqrt(sum / (values.Count - 1));
        }

        private static double GetPercentile(List<double> sortedValues, double percentile)
        {
            int index = (int)Math.Ceiling(percentile * sortedValues.Count) - 1;
            return sortedValues[Math.Max(0, Math.Min(index, sortedValues.Count - 1))];
        }
    }

    /// <summary>
    /// Result of a response time benchmark.
    /// </summary>
    public class ResponseTimeResult
    {
        /// <summary>
        /// Gets or sets the tool name.
        /// </summary>
        public string ToolName { get; set; } = "";

        /// <summary>
        /// Gets or sets the number of iterations.
        /// </summary>
        public int Iterations { get; set; }

        /// <summary>
        /// Gets or sets the timestamp.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the average response time in milliseconds.
        /// </summary>
        public double AverageMs { get; set; }

        /// <summary>
        /// Gets or sets the minimum response time in milliseconds.
        /// </summary>
        public double MinMs { get; set; }

        /// <summary>
        /// Gets or sets the maximum response time in milliseconds.
        /// </summary>
        public double MaxMs { get; set; }

        /// <summary>
        /// Gets or sets the median response time in milliseconds.
        /// </summary>
        public double MedianMs { get; set; }

        /// <summary>
        /// Gets or sets the standard deviation in milliseconds.
        /// </summary>
        public double StdDevMs { get; set; }

        /// <summary>
        /// Gets or sets the 95th percentile response time.
        /// </summary>
        public double P95Ms { get; set; }

        /// <summary>
        /// Gets or sets the 99th percentile response time.
        /// </summary>
        public double P99Ms { get; set; }

        /// <summary>
        /// Gets or sets the average memory usage in bytes.
        /// </summary>
        public long AverageMemoryBytes { get; set; }

        /// <summary>
        /// Gets or sets the maximum memory usage in bytes.
        /// </summary>
        public long MaxMemoryBytes { get; set; }

        /// <summary>
        /// Gets or sets the last response for inspection.
        /// </summary>
        public object? LastResponse { get; set; }

        /// <summary>
        /// Gets a summary of the result.
        /// </summary>
        public override string ToString()
        {
            return $"{ToolName}: Avg={AverageMs:F2}ms, Min={MinMs:F2}ms, Max={MaxMs:F2}ms, P95={P95Ms:F2}ms";
        }
    }

    /// <summary>
    /// Result of comparing multiple tools.
    /// </summary>
    public class ToolComparisonResult
    {
        private readonly List<ResponseTimeResult> _results;

        /// <summary>
        /// Initializes a new instance of the <see cref="ToolComparisonResult"/> class.
        /// </summary>
        /// <param name="results">The benchmark results to compare.</param>
        public ToolComparisonResult(List<ResponseTimeResult> results)
        {
            _results = results;
        }

        /// <summary>
        /// Gets the fastest tool based on average response time.
        /// </summary>
        public ResponseTimeResult? Fastest => _results.OrderBy(r => r.AverageMs).FirstOrDefault();

        /// <summary>
        /// Gets the slowest tool based on average response time.
        /// </summary>
        public ResponseTimeResult? Slowest => _results.OrderByDescending(r => r.AverageMs).FirstOrDefault();

        /// <summary>
        /// Gets the most consistent tool based on standard deviation.
        /// </summary>
        public ResponseTimeResult? MostConsistent => _results.OrderBy(r => r.StdDevMs).FirstOrDefault();

        /// <summary>
        /// Gets the least consistent tool based on standard deviation.
        /// </summary>
        public ResponseTimeResult? LeastConsistent => _results.OrderByDescending(r => r.StdDevMs).FirstOrDefault();

        /// <summary>
        /// Gets the most memory efficient tool.
        /// </summary>
        public ResponseTimeResult? MostMemoryEfficient => _results.OrderBy(r => r.AverageMemoryBytes).FirstOrDefault();

        /// <summary>
        /// Generates a comparison report.
        /// </summary>
        /// <returns>A formatted comparison report.</returns>
        public string GenerateReport()
        {
            var report = new System.Text.StringBuilder();
            
            report.AppendLine("Tool Performance Comparison");
            report.AppendLine("===========================");
            report.AppendLine();
            
            // Create comparison table
            report.AppendLine("Tool Name           | Avg (ms) | Min (ms) | Max (ms) | P95 (ms) | StdDev | Memory (KB)");
            report.AppendLine("-------------------|----------|----------|----------|----------|--------|------------");
            
            foreach (var result in _results.OrderBy(r => r.AverageMs))
            {
                report.AppendLine(
                    $"{result.ToolName,-18} | {result.AverageMs,8:F2} | {result.MinMs,8:F2} | " +
                    $"{result.MaxMs,8:F2} | {result.P95Ms,8:F2} | {result.StdDevMs,6:F2} | " +
                    $"{result.AverageMemoryBytes / 1024,10:F0}");
            }
            
            report.AppendLine();
            report.AppendLine("Summary:");
            report.AppendLine($"- Fastest: {Fastest?.ToolName} ({Fastest?.AverageMs:F2}ms average)");
            report.AppendLine($"- Most Consistent: {MostConsistent?.ToolName} ({MostConsistent?.StdDevMs:F2}ms std dev)");
            report.AppendLine($"- Most Memory Efficient: {MostMemoryEfficient?.ToolName} ({MostMemoryEfficient?.AverageMemoryBytes / 1024:F0}KB average)");
            
            return report.ToString();
        }
    }

    /// <summary>
    /// Result of a load test.
    /// </summary>
    public class LoadTestResult
    {
        /// <summary>
        /// Gets or sets the tool name.
        /// </summary>
        public string ToolName { get; set; } = "";

        /// <summary>
        /// Gets or sets the number of concurrent requests.
        /// </summary>
        public int ConcurrentRequests { get; set; }

        /// <summary>
        /// Gets or sets the test duration.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets the total number of requests.
        /// </summary>
        public int TotalRequests { get; set; }

        /// <summary>
        /// Gets or sets the number of successful requests.
        /// </summary>
        public int SuccessfulRequests { get; set; }

        /// <summary>
        /// Gets or sets the number of failed requests.
        /// </summary>
        public int FailedRequests { get; set; }

        /// <summary>
        /// Gets or sets the requests per second.
        /// </summary>
        public double RequestsPerSecond { get; set; }

        /// <summary>
        /// Gets or sets the average response time.
        /// </summary>
        public double AverageResponseMs { get; set; }

        /// <summary>
        /// Gets or sets the minimum response time.
        /// </summary>
        public double MinResponseMs { get; set; }

        /// <summary>
        /// Gets or sets the maximum response time.
        /// </summary>
        public double MaxResponseMs { get; set; }

        /// <summary>
        /// Gets or sets the median response time.
        /// </summary>
        public double MedianResponseMs { get; set; }

        /// <summary>
        /// Gets or sets the 95th percentile response time.
        /// </summary>
        public double P95ResponseMs { get; set; }

        /// <summary>
        /// Gets or sets the 99th percentile response time.
        /// </summary>
        public double P99ResponseMs { get; set; }

        /// <summary>
        /// Gets or sets the error rate percentage.
        /// </summary>
        public double ErrorRate { get; set; }

        /// <summary>
        /// Gets or sets error information.
        /// </summary>
        public List<ErrorInfo> Errors { get; set; } = new();

        /// <summary>
        /// Generates a load test report.
        /// </summary>
        /// <returns>A formatted report.</returns>
        public string GenerateReport()
        {
            var report = new System.Text.StringBuilder();
            
            report.AppendLine($"Load Test Report: {ToolName}");
            report.AppendLine("================================");
            report.AppendLine();
            report.AppendLine($"Test Configuration:");
            report.AppendLine($"- Concurrent Requests: {ConcurrentRequests}");
            report.AppendLine($"- Duration: {Duration.TotalSeconds:F0} seconds");
            report.AppendLine();
            report.AppendLine($"Results:");
            report.AppendLine($"- Total Requests: {TotalRequests:N0}");
            report.AppendLine($"- Successful: {SuccessfulRequests:N0}");
            report.AppendLine($"- Failed: {FailedRequests:N0}");
            report.AppendLine($"- Error Rate: {ErrorRate:F2}%");
            report.AppendLine($"- Requests/Second: {RequestsPerSecond:F2}");
            report.AppendLine();
            report.AppendLine($"Response Times:");
            report.AppendLine($"- Average: {AverageResponseMs:F2}ms");
            report.AppendLine($"- Min: {MinResponseMs:F2}ms");
            report.AppendLine($"- Max: {MaxResponseMs:F2}ms");
            report.AppendLine($"- Median: {MedianResponseMs:F2}ms");
            report.AppendLine($"- P95: {P95ResponseMs:F2}ms");
            report.AppendLine($"- P99: {P99ResponseMs:F2}ms");
            
            if (Errors.Any())
            {
                report.AppendLine();
                report.AppendLine("Errors:");
                foreach (var error in Errors.OrderByDescending(e => e.Count))
                {
                    report.AppendLine($"- {error.ErrorType}: {error.Count} occurrences");
                    report.AppendLine($"  Message: {error.Message}");
                }
            }
            
            return report.ToString();
        }
    }

    /// <summary>
    /// Information about errors during load testing.
    /// </summary>
    public class ErrorInfo
    {
        /// <summary>
        /// Gets or sets the error type name.
        /// </summary>
        public string ErrorType { get; set; } = "";

        /// <summary>
        /// Gets or sets the number of occurrences.
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string Message { get; set; } = "";
    }
}