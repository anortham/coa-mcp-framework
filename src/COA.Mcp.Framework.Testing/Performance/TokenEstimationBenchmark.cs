using COA.Mcp.Framework.TokenOptimization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace COA.Mcp.Framework.Testing.Performance
{
    /// <summary>
    /// Benchmarks for token estimation accuracy and performance.
    /// </summary>
    public class TokenEstimationBenchmark
    {
        private readonly List<BenchmarkResult> _results = new();

        /// <summary>
        /// Gets the benchmark results.
        /// </summary>
        public IReadOnlyList<BenchmarkResult> Results => _results.AsReadOnly();

        /// <summary>
        /// Runs a token estimation benchmark.
        /// </summary>
        /// <param name="name">Benchmark name.</param>
        /// <param name="data">Data to estimate.</param>
        /// <param name="actualTokens">Actual token count (if known).</param>
        /// <param name="iterations">Number of iterations.</param>
        /// <returns>The benchmark result.</returns>
        public BenchmarkResult RunBenchmark(
            string name,
            object data,
            int? actualTokens = null,
            int iterations = 100)
        {
            var result = new BenchmarkResult
            {
                Name = name,
                Iterations = iterations,
                DataType = data?.GetType().Name ?? "null"
            };

            // Warm up
            for (int i = 0; i < 10; i++)
            {
                TokenEstimator.EstimateObject(data);
            }

            // Measure estimation time
            var timings = new List<double>();
            var estimates = new List<int>();

            for (int i = 0; i < iterations; i++)
            {
                var sw = Stopwatch.StartNew();
                var estimate = TokenEstimator.EstimateObject(data);
                sw.Stop();

                timings.Add(sw.Elapsed.TotalMilliseconds);
                estimates.Add(estimate);
            }

            result.AverageTimeMs = timings.Average();
            result.MinTimeMs = timings.Min();
            result.MaxTimeMs = timings.Max();
            result.EstimatedTokens = (int)estimates.Average();

            // Calculate accuracy if actual tokens provided
            if (actualTokens.HasValue)
            {
                result.ActualTokens = actualTokens.Value;
                if (actualTokens.Value == 0)
                {
                    // For zero tokens, accuracy is 100% if estimate is also 0
                    result.AccuracyPercent = result.EstimatedTokens == 0 ? 100.0 : 0.0;
                }
                else
                {
                    result.AccuracyPercent = 100.0 - Math.Abs(result.EstimatedTokens - actualTokens.Value) * 100.0 / actualTokens.Value;
                }
            }

            _results.Add(result);
            return result;
        }

        /// <summary>
        /// Runs benchmarks for different data types.
        /// </summary>
        /// <returns>Summary of all benchmarks.</returns>
        public BenchmarkSummary RunStandardBenchmarks()
        {
            var generator = new Builders.TestDataGenerator();

            // String benchmarks - use realistic token counts
            RunBenchmark("Short String", "Hello, World!", 4);
            RunBenchmark("Medium String", generator.GenerateLorem(500), 125);
            RunBenchmark("Long String", generator.GenerateLorem(5000), 1250);

            // Collection benchmarks
            var smallList = generator.GenerateCollection(10, i => new { Id = i, Name = $"Item{i}" });
            var mediumList = generator.GenerateCollection(100, i => new { Id = i, Name = $"Item{i}" });
            var largeList = generator.GenerateCollection(1000, i => new { Id = i, Name = $"Item{i}" });

            RunBenchmark("Small Collection (10)", smallList);
            RunBenchmark("Medium Collection (100)", mediumList);
            RunBenchmark("Large Collection (1000)", largeList);

            // Complex object benchmarks
            var complexObject = new
            {
                Id = Guid.NewGuid(),
                Name = "Complex Object",
                Properties = new Dictionary<string, object>
                {
                    ["String"] = "Value",
                    ["Number"] = 42,
                    ["Boolean"] = true,
                    ["Array"] = new[] { 1, 2, 3, 4, 5 }
                },
                NestedObject = new
                {
                    Level1 = new
                    {
                        Level2 = new
                        {
                            Level3 = "Deep Value"
                        }
                    }
                }
            };

            RunBenchmark("Complex Nested Object", complexObject);

            // Null and edge cases
            RunBenchmark("Null Object", null!, 0);
            RunBenchmark("Empty String", "", 0);
            RunBenchmark("Empty Collection", new List<object>(), 2);

            return new BenchmarkSummary(_results);
        }

        /// <summary>
        /// Clears all benchmark results.
        /// </summary>
        public void Clear()
        {
            _results.Clear();
        }
    }

    /// <summary>
    /// Represents a single benchmark result.
    /// </summary>
    public class BenchmarkResult
    {
        /// <summary>
        /// Gets or sets the benchmark name.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Gets or sets the data type being benchmarked.
        /// </summary>
        public string DataType { get; set; } = "";

        /// <summary>
        /// Gets or sets the number of iterations.
        /// </summary>
        public int Iterations { get; set; }

        /// <summary>
        /// Gets or sets the average time in milliseconds.
        /// </summary>
        public double AverageTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the minimum time in milliseconds.
        /// </summary>
        public double MinTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the maximum time in milliseconds.
        /// </summary>
        public double MaxTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the estimated token count.
        /// </summary>
        public int EstimatedTokens { get; set; }

        /// <summary>
        /// Gets or sets the actual token count (if known).
        /// </summary>
        public int? ActualTokens { get; set; }

        /// <summary>
        /// Gets or sets the accuracy percentage.
        /// </summary>
        public double? AccuracyPercent { get; set; }

        /// <summary>
        /// Gets a summary string of the result.
        /// </summary>
        public override string ToString()
        {
            var accuracyStr = AccuracyPercent.HasValue ? $", Accuracy: {AccuracyPercent:F1}%" : "";
            return $"{Name}: {AverageTimeMs:F2}ms avg, {EstimatedTokens} tokens{accuracyStr}";
        }
    }

    /// <summary>
    /// Summary of benchmark results.
    /// </summary>
    public class BenchmarkSummary
    {
        private readonly List<BenchmarkResult> _results;

        /// <summary>
        /// Initializes a new instance of the <see cref="BenchmarkSummary"/> class.
        /// </summary>
        /// <param name="results">The benchmark results.</param>
        public BenchmarkSummary(List<BenchmarkResult> results)
        {
            _results = results;
        }

        /// <summary>
        /// Gets the average estimation time across all benchmarks.
        /// </summary>
        public double OverallAverageTimeMs => _results.Average(r => r.AverageTimeMs);

        /// <summary>
        /// Gets the average accuracy across benchmarks with known actual tokens.
        /// </summary>
        public double? OverallAccuracyPercent
        {
            get
            {
                var withAccuracy = _results.Where(r => r.AccuracyPercent.HasValue).ToList();
                return withAccuracy.Any() ? withAccuracy.Average(r => r.AccuracyPercent!.Value) : null;
            }
        }

        /// <summary>
        /// Gets the fastest benchmark.
        /// </summary>
        public BenchmarkResult? Fastest => _results.OrderBy(r => r.AverageTimeMs).FirstOrDefault();

        /// <summary>
        /// Gets the slowest benchmark.
        /// </summary>
        public BenchmarkResult? Slowest => _results.OrderByDescending(r => r.AverageTimeMs).FirstOrDefault();

        /// <summary>
        /// Gets the most accurate benchmark.
        /// </summary>
        public BenchmarkResult? MostAccurate => _results
            .Where(r => r.AccuracyPercent.HasValue)
            .OrderByDescending(r => r.AccuracyPercent!.Value)
            .FirstOrDefault();

        /// <summary>
        /// Gets the least accurate benchmark.
        /// </summary>
        public BenchmarkResult? LeastAccurate => _results
            .Where(r => r.AccuracyPercent.HasValue)
            .OrderBy(r => r.AccuracyPercent!.Value)
            .FirstOrDefault();

        /// <summary>
        /// Generates a report of the benchmark results.
        /// </summary>
        /// <returns>A formatted report string.</returns>
        public string GenerateReport()
        {
            var report = new System.Text.StringBuilder();
            
            report.AppendLine("Token Estimation Benchmark Report");
            report.AppendLine("=================================");
            report.AppendLine();
            report.AppendLine($"Total Benchmarks: {_results.Count}");
            report.AppendLine($"Overall Average Time: {OverallAverageTimeMs:F2}ms");
            
            if (OverallAccuracyPercent.HasValue)
            {
                report.AppendLine($"Overall Accuracy: {OverallAccuracyPercent:F1}%");
            }
            
            report.AppendLine();
            report.AppendLine("Individual Results:");
            report.AppendLine("-------------------");
            
            foreach (var result in _results.OrderBy(r => r.Name))
            {
                report.AppendLine(result.ToString());
            }
            
            if (Fastest != null)
            {
                report.AppendLine();
                report.AppendLine($"Fastest: {Fastest.Name} ({Fastest.AverageTimeMs:F2}ms)");
            }
            
            if (Slowest != null)
            {
                report.AppendLine($"Slowest: {Slowest.Name} ({Slowest.AverageTimeMs:F2}ms)");
            }
            
            if (MostAccurate != null)
            {
                report.AppendLine($"Most Accurate: {MostAccurate.Name} ({MostAccurate.AccuracyPercent:F1}%)");
            }
            
            if (LeastAccurate != null)
            {
                report.AppendLine($"Least Accurate: {LeastAccurate.Name} ({LeastAccurate.AccuracyPercent:F1}%)");
            }
            
            return report.ToString();
        }
    }
}