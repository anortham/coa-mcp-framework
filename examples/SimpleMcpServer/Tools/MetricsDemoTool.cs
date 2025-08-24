using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Visualization;
using COA.Mcp.Visualization.Helpers;
using Microsoft.Extensions.Logging;

namespace SimpleMcpServer.Tools;

/// <summary>
/// Parameters for the metrics demo tool
/// </summary>
public class MetricsDemoParams
{
    /// <summary>
    /// The type of metrics to generate
    /// </summary>
    [Required]
    public required string MetricType { get; set; }

    /// <summary>
    /// Number of data points to generate
    /// </summary>
    [Range(1, 100)]
    public int DataPoints { get; set; } = 10;
}

/// <summary>
/// Result from the metrics demo tool
/// </summary>
public class MetricsDemoResult : ToolResultBase
{
    /// <summary>
    /// The type of metrics generated
    /// </summary>
    public required string MetricType { get; set; }

    /// <summary>
    /// The metrics data
    /// </summary>
    public required object Metrics { get; set; }

    /// <summary>
    /// Summary statistics
    /// </summary>
    public required object Summary { get; set; }

    /// <inheritdoc/>
    public override string Operation => "metrics-demo";
}

/// <summary>
/// Demo tool that demonstrates chart visualization with system metrics
/// </summary>
public class MetricsDemoTool : McpToolBase<MetricsDemoParams, MetricsDemoResult>, IVisualizationProvider
{
    private MetricsDemoResult? _lastResult;

    public MetricsDemoTool(ILogger<MetricsDemoTool>? logger = null) : base(null, logger)
    {
    }

    public override string Name => "metrics_demo";
    public override string Description => "Demonstrates chart visualization with various system metrics";

    protected override async Task<MetricsDemoResult> ExecuteInternalAsync(
        MetricsDemoParams parameters, 
        CancellationToken cancellationToken)
    {
        // Simulate metrics collection delay
        await Task.Delay(Random.Shared.Next(200, 800), cancellationToken);

        var metrics = parameters.MetricType.ToLowerInvariant() switch
        {
            "performance" => GeneratePerformanceMetrics(parameters.DataPoints),
            "memory" => GenerateMemoryMetrics(parameters.DataPoints),
            "network" => GenerateNetworkMetrics(parameters.DataPoints),
            "disk" => GenerateDiskMetrics(parameters.DataPoints),
            _ => GenerateGenericMetrics(parameters.DataPoints)
        };

        var result = new MetricsDemoResult
        {
            MetricType = parameters.MetricType,
            Metrics = metrics.Data,
            Summary = metrics.Summary,
            Success = true,
            Message = $"Generated {parameters.DataPoints} data points for {parameters.MetricType} metrics"
        };

        _lastResult = result;
        return result;
    }

    public override VisualizationDescriptor? GetVisualizationDescriptor()
    {
        if (_lastResult == null) return null;

        return VisualizationHelpers.CreateMetrics(
            new
            {
                type = _lastResult.MetricType,
                data = _lastResult.Metrics,
                summary = _lastResult.Summary,
                chartConfig = new
                {
                    type = GetChartType(_lastResult.MetricType),
                    animate = true,
                    showLegend = true,
                    showGrid = true
                }
            },
            builder => builder
                .WithPriority(COA.Mcp.Visualization.VisualizationPriority.Normal)
                .WithInteractive(true)
                .WithMetadata("tool", "metrics_demo")
                .WithMetadata("metricType", _lastResult.MetricType)
                .WithMetadata("timestamp", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))
        );
    }

    private static string GetChartType(string metricType) => metricType.ToLowerInvariant() switch
    {
        "performance" => "line",
        "memory" => "area",
        "network" => "line",
        "disk" => "bar",
        _ => "line"
    };

    private static (object Data, object Summary) GeneratePerformanceMetrics(int dataPoints)
    {
        var timestamps = new List<string>();
        var cpuValues = new List<double>();
        var responseTimeValues = new List<double>();
        var throughputValues = new List<double>();

        var baseTime = DateTime.UtcNow.AddMinutes(-dataPoints);
        
        for (int i = 0; i < dataPoints; i++)
        {
            timestamps.Add(baseTime.AddMinutes(i).ToString("HH:mm"));
            cpuValues.Add(Math.Round(Random.Shared.NextDouble() * 100, 1));
            responseTimeValues.Add(Math.Round(Random.Shared.NextDouble() * 500 + 50, 1));
            throughputValues.Add(Math.Round(Random.Shared.NextDouble() * 1000 + 100, 0));
        }

        var data = new
        {
            labels = timestamps,
            datasets = new[]
            {
                new { label = "CPU Usage (%)", data = cpuValues, borderColor = "#ff6384", backgroundColor = "rgba(255, 99, 132, 0.1)" },
                new { label = "Response Time (ms)", data = responseTimeValues, borderColor = "#36a2eb", backgroundColor = "rgba(54, 162, 235, 0.1)" },
                new { label = "Throughput (req/s)", data = throughputValues, borderColor = "#4bc0c0", backgroundColor = "rgba(75, 192, 192, 0.1)" }
            }
        };

        var summary = new
        {
            avgCpu = Math.Round(cpuValues.Average(), 1),
            maxCpu = cpuValues.Max(),
            avgResponseTime = Math.Round(responseTimeValues.Average(), 1),
            maxResponseTime = responseTimeValues.Max(),
            avgThroughput = Math.Round(throughputValues.Average(), 0),
            maxThroughput = throughputValues.Max()
        };

        return (data, summary);
    }

    private static (object Data, object Summary) GenerateMemoryMetrics(int dataPoints)
    {
        var timestamps = new List<string>();
        var usedMemory = new List<double>();
        var freeMemory = new List<double>();
        var totalMemory = 16384; // 16GB

        var baseTime = DateTime.UtcNow.AddMinutes(-dataPoints);
        
        for (int i = 0; i < dataPoints; i++)
        {
            timestamps.Add(baseTime.AddMinutes(i).ToString("HH:mm"));
            var used = Math.Round(Random.Shared.NextDouble() * totalMemory * 0.8 + totalMemory * 0.2, 0);
            usedMemory.Add(used);
            freeMemory.Add(totalMemory - used);
        }

        var data = new
        {
            labels = timestamps,
            datasets = new[]
            {
                new { label = "Used Memory (MB)", data = usedMemory, backgroundColor = "#ff6384" },
                new { label = "Free Memory (MB)", data = freeMemory, backgroundColor = "#36a2eb" }
            }
        };

        var summary = new
        {
            totalMemory,
            avgUsed = Math.Round(usedMemory.Average(), 0),
            maxUsed = usedMemory.Max(),
            avgUtilization = Math.Round(usedMemory.Average() / totalMemory * 100, 1)
        };

        return (data, summary);
    }

    private static (object Data, object Summary) GenerateNetworkMetrics(int dataPoints)
    {
        var timestamps = new List<string>();
        var bytesIn = new List<double>();
        var bytesOut = new List<double>();
        var packetsIn = new List<double>();
        var packetsOut = new List<double>();

        var baseTime = DateTime.UtcNow.AddMinutes(-dataPoints);
        
        for (int i = 0; i < dataPoints; i++)
        {
            timestamps.Add(baseTime.AddMinutes(i).ToString("HH:mm"));
            bytesIn.Add(Math.Round(Random.Shared.NextDouble() * 1000000 + 100000, 0));
            bytesOut.Add(Math.Round(Random.Shared.NextDouble() * 800000 + 50000, 0));
            packetsIn.Add(Math.Round(Random.Shared.NextDouble() * 1000 + 100, 0));
            packetsOut.Add(Math.Round(Random.Shared.NextDouble() * 800 + 50, 0));
        }

        var data = new
        {
            labels = timestamps,
            datasets = new[]
            {
                new { label = "Bytes In", data = bytesIn, borderColor = "#4bc0c0", fill = false },
                new { label = "Bytes Out", data = bytesOut, borderColor = "#ff9f40", fill = false },
                new { label = "Packets In", data = packetsIn, borderColor = "#9966ff", fill = false },
                new { label = "Packets Out", data = packetsOut, borderColor = "#ffcd56", fill = false }
            }
        };

        var summary = new
        {
            totalBytesIn = bytesIn.Sum(),
            totalBytesOut = bytesOut.Sum(),
            avgBytesIn = Math.Round(bytesIn.Average(), 0),
            avgBytesOut = Math.Round(bytesOut.Average(), 0),
            totalPackets = packetsIn.Sum() + packetsOut.Sum()
        };

        return (data, summary);
    }

    private static (object Data, object Summary) GenerateDiskMetrics(int dataPoints)
    {
        var driveNames = new[] { "C:", "D:", "E:", "F:" };
        var categories = driveNames.Take(Math.Min(dataPoints, driveNames.Length)).ToArray();
        var usedSpace = new List<double>();
        var freeSpace = new List<double>();

        for (int i = 0; i < categories.Length; i++)
        {
            var totalSize = Random.Shared.Next(100, 2000); // GB
            var used = Math.Round(Random.Shared.NextDouble() * totalSize * 0.8 + totalSize * 0.1, 1);
            usedSpace.Add(used);
            freeSpace.Add(totalSize - used);
        }

        var data = new
        {
            labels = categories,
            datasets = new[]
            {
                new { label = "Used Space (GB)", data = usedSpace, backgroundColor = "#ff6384" },
                new { label = "Free Space (GB)", data = freeSpace, backgroundColor = "#36a2eb" }
            }
        };

        var summary = new
        {
            totalDrives = categories.Length,
            totalUsedSpace = Math.Round(usedSpace.Sum(), 1),
            totalFreeSpace = Math.Round(freeSpace.Sum(), 1),
            avgUtilization = Math.Round(usedSpace.Sum() / (usedSpace.Sum() + freeSpace.Sum()) * 100, 1)
        };

        return (data, summary);
    }

    private static (object Data, object Summary) GenerateGenericMetrics(int dataPoints)
    {
        var timestamps = new List<string>();
        var values = new List<double>();

        var baseTime = DateTime.UtcNow.AddMinutes(-dataPoints);
        
        for (int i = 0; i < dataPoints; i++)
        {
            timestamps.Add(baseTime.AddMinutes(i).ToString("HH:mm"));
            values.Add(Math.Round(Random.Shared.NextDouble() * 100, 2));
        }

        var data = new
        {
            labels = timestamps,
            datasets = new[]
            {
                new { label = "Value", data = values, borderColor = "#36a2eb", backgroundColor = "rgba(54, 162, 235, 0.1)" }
            }
        };

        var summary = new
        {
            count = values.Count,
            min = values.Min(),
            max = values.Max(),
            avg = Math.Round(values.Average(), 2),
            total = Math.Round(values.Sum(), 2)
        };

        return (data, summary);
    }
}