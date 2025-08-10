using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.Pipeline.SimpleMiddleware;

/// <summary>
/// Simple middleware that provides execution logging.
/// </summary>
public class LoggingSimpleMiddleware : SimpleMiddlewareBase
{
    private readonly ILogger<LoggingSimpleMiddleware> _logger;
    private readonly LogLevel _logLevel;

    /// <summary>
    /// Initializes a new instance of the LoggingSimpleMiddleware class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="logLevel">The log level for execution details.</param>
    public LoggingSimpleMiddleware(ILogger<LoggingSimpleMiddleware> logger, LogLevel logLevel = LogLevel.Information)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logLevel = logLevel;
        Order = 10; // Run early
    }

    /// <inheritdoc/>
    public override Task OnBeforeExecutionAsync(string toolName, object? parameters)
    {
        _logger.Log(_logLevel, "Starting execution of tool '{ToolName}'", toolName);
        
        if (_logger.IsEnabled(LogLevel.Debug) && parameters != null)
        {
            try
            {
                var json = JsonSerializer.Serialize(parameters);
                _logger.LogDebug("Tool '{ToolName}' parameters: {Parameters}", toolName, json);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not serialize parameters for tool '{ToolName}'", toolName);
            }
        }
        
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task OnAfterExecutionAsync(string toolName, object? parameters, object? result, long elapsedMs)
    {
        _logger.Log(_logLevel, 
            "Tool '{ToolName}' completed successfully in {ElapsedMs}ms", 
            toolName, elapsedMs);
        
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task OnErrorAsync(string toolName, object? parameters, Exception exception, long elapsedMs)
    {
        _logger.LogError(exception, 
            "Tool '{ToolName}' failed after {ElapsedMs}ms: {ErrorMessage}", 
            toolName, elapsedMs, exception.Message);
        
        return Task.CompletedTask;
    }
}