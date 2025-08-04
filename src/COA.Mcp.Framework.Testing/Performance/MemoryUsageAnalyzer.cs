using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.Threading.Tasks;

namespace COA.Mcp.Framework.Testing.Performance
{
    /// <summary>
    /// Analyzes memory usage patterns for MCP tools and operations.
    /// </summary>
    public class MemoryUsageAnalyzer
    {
        private readonly List<MemorySnapshot> _snapshots = new();
        private readonly List<MemoryLeakSuspicion> _leakSuspicions = new();

        /// <summary>
        /// Gets the memory snapshots.
        /// </summary>
        public IReadOnlyList<MemorySnapshot> Snapshots => _snapshots.AsReadOnly();

        /// <summary>
        /// Gets suspected memory leaks.
        /// </summary>
        public IReadOnlyList<MemoryLeakSuspicion> LeakSuspicions => _leakSuspicions.AsReadOnly();

        /// <summary>
        /// Takes a memory snapshot.
        /// </summary>
        /// <param name="label">Label for the snapshot.</param>
        /// <returns>The memory snapshot.</returns>
        public MemorySnapshot TakeSnapshot(string label)
        {
            // Force garbage collection for accurate measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var snapshot = new MemorySnapshot
            {
                Label = label,
                Timestamp = DateTime.UtcNow,
                TotalMemoryBytes = GC.GetTotalMemory(false),
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2),
                AllocatedBytes = GC.GetTotalAllocatedBytes(true),
                WorkingSetBytes = Process.GetCurrentProcess().WorkingSet64,
                PrivateMemoryBytes = Process.GetCurrentProcess().PrivateMemorySize64,
                VirtualMemoryBytes = Process.GetCurrentProcess().VirtualMemorySize64
            };

            _snapshots.Add(snapshot);
            return snapshot;
        }

        /// <summary>
        /// Analyzes memory usage during an operation.
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="operation">The operation to analyze.</param>
        /// <param name="operationName">Name of the operation.</param>
        /// <returns>Memory analysis result.</returns>
        public async Task<MemoryAnalysisResult<T>> AnalyzeOperationAsync<T>(
            Func<Task<T>> operation,
            string operationName)
        {
            var beforeSnapshot = TakeSnapshot($"{operationName}_Before");

            var sw = Stopwatch.StartNew();
            var result = await operation();
            sw.Stop();

            var afterSnapshot = TakeSnapshot($"{operationName}_After");

            // Force garbage collection and let objects settle
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await Task.Delay(100);
            var settledSnapshot = TakeSnapshot($"{operationName}_Settled");

            return new MemoryAnalysisResult<T>
            {
                OperationName = operationName,
                Result = result,
                ExecutionTime = sw.Elapsed,
                BeforeSnapshot = beforeSnapshot,
                AfterSnapshot = afterSnapshot,
                SettledSnapshot = settledSnapshot,
                MemoryAllocated = afterSnapshot.TotalMemoryBytes - beforeSnapshot.TotalMemoryBytes,
                MemoryRetained = settledSnapshot.TotalMemoryBytes - beforeSnapshot.TotalMemoryBytes,
                Gen0Collections = afterSnapshot.Gen0Collections - beforeSnapshot.Gen0Collections,
                Gen1Collections = afterSnapshot.Gen1Collections - beforeSnapshot.Gen1Collections,
                Gen2Collections = afterSnapshot.Gen2Collections - beforeSnapshot.Gen2Collections
            };
        }

        /// <summary>
        /// Monitors memory usage over time.
        /// </summary>
        /// <param name="duration">Monitoring duration.</param>
        /// <param name="interval">Sampling interval.</param>
        /// <param name="operation">Optional operation to run during monitoring.</param>
        /// <returns>Memory monitoring result.</returns>
        public async Task<MemoryMonitoringResult> MonitorMemoryAsync(
            TimeSpan duration,
            TimeSpan interval,
            Func<Task>? operation = null)
        {
            var result = new MemoryMonitoringResult
            {
                StartTime = DateTime.UtcNow,
                Duration = duration,
                SamplingInterval = interval
            };

            var endTime = DateTime.UtcNow + duration;
            var samples = new List<MemorySample>();

            // Start operation if provided
            var operationTask = operation?.Invoke() ?? Task.CompletedTask;

            while (DateTime.UtcNow < endTime)
            {
                samples.Add(new MemorySample
                {
                    Timestamp = DateTime.UtcNow,
                    TotalMemoryBytes = GC.GetTotalMemory(false),
                    WorkingSetBytes = Process.GetCurrentProcess().WorkingSet64
                });

                await Task.Delay(interval);
            }

            await operationTask;

            result.Samples = samples;
            result.EndTime = DateTime.UtcNow;

            // Analyze for potential leaks
            AnalyzeForLeaks(samples);

            return result;
        }

        /// <summary>
        /// Performs stress testing to identify memory issues.
        /// </summary>
        /// <param name="operation">The operation to stress test.</param>
        /// <param name="iterations">Number of iterations.</param>
        /// <param name="concurrency">Level of concurrency.</param>
        /// <returns>Stress test result.</returns>
        public async Task<MemoryStressTestResult> StressTestMemoryAsync(
            Func<Task> operation,
            int iterations = 100,
            int concurrency = 1)
        {
            var result = new MemoryStressTestResult
            {
                Iterations = iterations,
                Concurrency = concurrency,
                StartTime = DateTime.UtcNow
            };

            var initialSnapshot = TakeSnapshot("StressTest_Initial");

            // Run stress test
            if (concurrency == 1)
            {
                for (int i = 0; i < iterations; i++)
                {
                    await operation();
                    
                    if (i % 10 == 0)
                    {
                        TakeSnapshot($"StressTest_Iteration_{i}");
                    }
                }
            }
            else
            {
                var tasks = new List<Task>();
                var iterationsPerTask = iterations / concurrency;

                for (int t = 0; t < concurrency; t++)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        for (int i = 0; i < iterationsPerTask; i++)
                        {
                            await operation();
                        }
                    }));
                }

                await Task.WhenAll(tasks);
            }

            var finalSnapshot = TakeSnapshot("StressTest_Final");

            // Force collection and take settled snapshot
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await Task.Delay(500);

            var settledSnapshot = TakeSnapshot("StressTest_Settled");

            result.EndTime = DateTime.UtcNow;
            result.InitialMemory = initialSnapshot.TotalMemoryBytes;
            result.PeakMemory = _snapshots.Where(s => s.Label.StartsWith("StressTest_"))
                .Max(s => s.TotalMemoryBytes);
            result.FinalMemory = finalSnapshot.TotalMemoryBytes;
            result.SettledMemory = settledSnapshot.TotalMemoryBytes;
            result.MemoryLeaked = settledSnapshot.TotalMemoryBytes - initialSnapshot.TotalMemoryBytes;
            result.HasPotentialLeak = result.MemoryLeaked > (initialSnapshot.TotalMemoryBytes * 0.1); // 10% growth

            return result;
        }

        /// <summary>
        /// Analyzes samples for potential memory leaks.
        /// </summary>
        private void AnalyzeForLeaks(List<MemorySample> samples)
        {
            if (samples.Count < 10) return;

            // Simple linear regression to detect trends
            var xValues = Enumerable.Range(0, samples.Count).Select(i => (double)i).ToArray();
            var yValues = samples.Select(s => (double)s.TotalMemoryBytes).ToArray();

            var xMean = xValues.Average();
            var yMean = yValues.Average();

            var slope = xValues.Zip(yValues, (x, y) => (x - xMean) * (y - yMean)).Sum() /
                       xValues.Sum(x => Math.Pow(x - xMean, 2));

            var correlation = CalculateCorrelation(xValues, yValues);

            // If strong positive correlation and significant slope, suspect a leak
            if (correlation > 0.8 && slope > 1000) // 1KB per sample growth
            {
                _leakSuspicions.Add(new MemoryLeakSuspicion
                {
                    DetectedAt = DateTime.UtcNow,
                    GrowthRatePerSecond = slope * (1000.0 / (samples[1].Timestamp - samples[0].Timestamp).TotalMilliseconds),
                    Correlation = correlation,
                    SampleCount = samples.Count,
                    StartMemory = samples.First().TotalMemoryBytes,
                    EndMemory = samples.Last().TotalMemoryBytes
                });
            }
        }

        /// <summary>
        /// Calculates correlation coefficient.
        /// </summary>
        private static double CalculateCorrelation(double[] x, double[] y)
        {
            var xMean = x.Average();
            var yMean = y.Average();

            var numerator = x.Zip(y, (xi, yi) => (xi - xMean) * (yi - yMean)).Sum();
            var denominator = Math.Sqrt(
                x.Sum(xi => Math.Pow(xi - xMean, 2)) *
                y.Sum(yi => Math.Pow(yi - yMean, 2))
            );

            return denominator == 0 ? 0 : numerator / denominator;
        }

        /// <summary>
        /// Clears all snapshots and analysis data.
        /// </summary>
        public void Clear()
        {
            _snapshots.Clear();
            _leakSuspicions.Clear();
        }

        /// <summary>
        /// Generates a memory analysis report.
        /// </summary>
        /// <returns>A formatted report.</returns>
        public string GenerateReport()
        {
            var report = new System.Text.StringBuilder();
            
            report.AppendLine("Memory Usage Analysis Report");
            report.AppendLine("============================");
            report.AppendLine();
            
            if (_snapshots.Any())
            {
                report.AppendLine("Memory Snapshots:");
                report.AppendLine("-----------------");
                foreach (var snapshot in _snapshots.OrderBy(s => s.Timestamp))
                {
                    report.AppendLine($"- {snapshot.Label}: {snapshot.TotalMemoryBytes / 1024.0 / 1024.0:F2} MB");
                }
            }
            
            if (_leakSuspicions.Any())
            {
                report.AppendLine();
                report.AppendLine("Potential Memory Leaks Detected:");
                report.AppendLine("--------------------------------");
                foreach (var leak in _leakSuspicions)
                {
                    report.AppendLine($"- Growth Rate: {leak.GrowthRatePerSecond / 1024.0:F2} KB/s");
                    report.AppendLine($"  Correlation: {leak.Correlation:F3}");
                    report.AppendLine($"  Memory Growth: {(leak.EndMemory - leak.StartMemory) / 1024.0 / 1024.0:F2} MB");
                }
            }
            
            return report.ToString();
        }
    }

    /// <summary>
    /// Represents a memory snapshot.
    /// </summary>
    public class MemorySnapshot
    {
        /// <summary>
        /// Gets or sets the snapshot label.
        /// </summary>
        public string Label { get; set; } = "";

        /// <summary>
        /// Gets or sets the timestamp.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the total managed memory in bytes.
        /// </summary>
        public long TotalMemoryBytes { get; set; }

        /// <summary>
        /// Gets or sets the generation 0 collection count.
        /// </summary>
        public int Gen0Collections { get; set; }

        /// <summary>
        /// Gets or sets the generation 1 collection count.
        /// </summary>
        public int Gen1Collections { get; set; }

        /// <summary>
        /// Gets or sets the generation 2 collection count.
        /// </summary>
        public int Gen2Collections { get; set; }

        /// <summary>
        /// Gets or sets the total allocated bytes.
        /// </summary>
        public long AllocatedBytes { get; set; }

        /// <summary>
        /// Gets or sets the working set size.
        /// </summary>
        public long WorkingSetBytes { get; set; }

        /// <summary>
        /// Gets or sets the private memory size.
        /// </summary>
        public long PrivateMemoryBytes { get; set; }

        /// <summary>
        /// Gets or sets the virtual memory size.
        /// </summary>
        public long VirtualMemoryBytes { get; set; }
    }

    /// <summary>
    /// Result of memory analysis for an operation.
    /// </summary>
    /// <typeparam name="T">The operation result type.</typeparam>
    public class MemoryAnalysisResult<T>
    {
        /// <summary>
        /// Gets or sets the operation name.
        /// </summary>
        public string OperationName { get; set; } = "";

        /// <summary>
        /// Gets or sets the operation result.
        /// </summary>
        public T? Result { get; set; }

        /// <summary>
        /// Gets or sets the execution time.
        /// </summary>
        public TimeSpan ExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets the snapshot taken before the operation.
        /// </summary>
        public MemorySnapshot BeforeSnapshot { get; set; } = null!;

        /// <summary>
        /// Gets or sets the snapshot taken after the operation.
        /// </summary>
        public MemorySnapshot AfterSnapshot { get; set; } = null!;

        /// <summary>
        /// Gets or sets the snapshot taken after memory settled.
        /// </summary>
        public MemorySnapshot SettledSnapshot { get; set; } = null!;

        /// <summary>
        /// Gets or sets the memory allocated during the operation.
        /// </summary>
        public long MemoryAllocated { get; set; }

        /// <summary>
        /// Gets or sets the memory retained after the operation.
        /// </summary>
        public long MemoryRetained { get; set; }

        /// <summary>
        /// Gets or sets the number of Gen0 collections.
        /// </summary>
        public int Gen0Collections { get; set; }

        /// <summary>
        /// Gets or sets the number of Gen1 collections.
        /// </summary>
        public int Gen1Collections { get; set; }

        /// <summary>
        /// Gets or sets the number of Gen2 collections.
        /// </summary>
        public int Gen2Collections { get; set; }
    }

    /// <summary>
    /// A single memory sample during monitoring.
    /// </summary>
    public class MemorySample
    {
        /// <summary>
        /// Gets or sets the sample timestamp.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the total memory in bytes.
        /// </summary>
        public long TotalMemoryBytes { get; set; }

        /// <summary>
        /// Gets or sets the working set in bytes.
        /// </summary>
        public long WorkingSetBytes { get; set; }
    }

    /// <summary>
    /// Result of memory monitoring.
    /// </summary>
    public class MemoryMonitoringResult
    {
        /// <summary>
        /// Gets or sets the start time.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the end time.
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Gets or sets the monitoring duration.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets the sampling interval.
        /// </summary>
        public TimeSpan SamplingInterval { get; set; }

        /// <summary>
        /// Gets or sets the memory samples.
        /// </summary>
        public List<MemorySample> Samples { get; set; } = new();
    }

    /// <summary>
    /// Result of memory stress testing.
    /// </summary>
    public class MemoryStressTestResult
    {
        /// <summary>
        /// Gets or sets the number of iterations.
        /// </summary>
        public int Iterations { get; set; }

        /// <summary>
        /// Gets or sets the concurrency level.
        /// </summary>
        public int Concurrency { get; set; }

        /// <summary>
        /// Gets or sets the start time.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the end time.
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Gets or sets the initial memory.
        /// </summary>
        public long InitialMemory { get; set; }

        /// <summary>
        /// Gets or sets the peak memory usage.
        /// </summary>
        public long PeakMemory { get; set; }

        /// <summary>
        /// Gets or sets the final memory.
        /// </summary>
        public long FinalMemory { get; set; }

        /// <summary>
        /// Gets or sets the settled memory after GC.
        /// </summary>
        public long SettledMemory { get; set; }

        /// <summary>
        /// Gets or sets the amount of memory leaked.
        /// </summary>
        public long MemoryLeaked { get; set; }

        /// <summary>
        /// Gets or sets whether there's a potential leak.
        /// </summary>
        public bool HasPotentialLeak { get; set; }
    }

    /// <summary>
    /// Information about a suspected memory leak.
    /// </summary>
    public class MemoryLeakSuspicion
    {
        /// <summary>
        /// Gets or sets when the leak was detected.
        /// </summary>
        public DateTime DetectedAt { get; set; }

        /// <summary>
        /// Gets or sets the growth rate in bytes per second.
        /// </summary>
        public double GrowthRatePerSecond { get; set; }

        /// <summary>
        /// Gets or sets the correlation coefficient.
        /// </summary>
        public double Correlation { get; set; }

        /// <summary>
        /// Gets or sets the number of samples analyzed.
        /// </summary>
        public int SampleCount { get; set; }

        /// <summary>
        /// Gets or sets the starting memory.
        /// </summary>
        public long StartMemory { get; set; }

        /// <summary>
        /// Gets or sets the ending memory.
        /// </summary>
        public long EndMemory { get; set; }
    }
}