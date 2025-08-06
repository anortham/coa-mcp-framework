using COA.Mcp.Framework;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Models;
using COA.Mcp.Protocol;

namespace HttpMcpServer;

/// <summary>
/// Example time tool for demonstration.
/// </summary>
public class TimeTool : McpToolBase<TimeParams, TimeResult>
{
    public override string Name => "get_time";
    public override string Description => "Get current time in various timezones";
    public override ToolCategory Category => ToolCategory.Query;

    private static readonly Dictionary<string, TimeZoneInfo> TimeZones = new()
    {
        ["UTC"] = TimeZoneInfo.Utc,
        ["PST"] = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"),
        ["EST"] = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"),
        ["CST"] = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"),
        ["MST"] = TimeZoneInfo.FindSystemTimeZoneById("Mountain Standard Time"),
        ["GMT"] = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time"),
        ["CET"] = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time"),
        ["JST"] = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time"),
        ["AEST"] = TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time")
    };

    protected override Task<TimeResult> ExecuteInternalAsync(
        TimeParams parameters, 
        CancellationToken cancellationToken)
    {
        try
        {
            var timezone = parameters.Timezone?.ToUpper() ?? "UTC";
            
            if (!TimeZones.TryGetValue(timezone, out var tzInfo))
            {
                // Try to find by ID
                try
                {
                    tzInfo = TimeZoneInfo.FindSystemTimeZoneById(parameters.Timezone!);
                }
                catch
                {
                    return Task.FromResult(new TimeResult
                    {
                        Success = false,
                        Error = new ErrorInfo
                        {
                            Code = "INVALID_TIMEZONE",
                            Message = $"Unknown timezone: {parameters.Timezone}. Available: {string.Join(", ", TimeZones.Keys)}"
                        }
                    });
                }
            }

            var utcNow = DateTime.UtcNow;
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(utcNow, tzInfo);
            
            return Task.FromResult(new TimeResult
            {
                Success = true,
                Timezone = timezone,
                LocalTime = localTime.ToString("yyyy-MM-dd HH:mm:ss"),
                UtcTime = utcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                IsoFormat = localTime.ToString("O"),
                UnixTimestamp = ((DateTimeOffset)utcNow).ToUnixTimeSeconds(),
                DayOfWeek = localTime.DayOfWeek.ToString(),
                IsDaylightSaving = tzInfo.IsDaylightSavingTime(localTime)
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new TimeResult
            {
                Success = false,
                Error = new ErrorInfo
                {
                    Code = "TIME_ERROR",
                    Message = ex.Message
                }
            });
        }
    }

}

public class TimeParams
{
    public string? Timezone { get; set; }
}

public class TimeResult : ToolResultBase
{
    public override string Operation => "get_time";
    
    public string Timezone { get; set; } = string.Empty;
    public string LocalTime { get; set; } = string.Empty;
    public string UtcTime { get; set; } = string.Empty;
    public string IsoFormat { get; set; } = string.Empty;
    public long UnixTimestamp { get; set; }
    public string DayOfWeek { get; set; } = string.Empty;
    public bool IsDaylightSaving { get; set; }
    
    public override string ToString()
    {
        return Success 
            ? $"{Timezone}: {LocalTime} ({DayOfWeek}){(IsDaylightSaving ? " DST" : "")}" 
            : $"Time error: {Error?.Message}";
    }
}