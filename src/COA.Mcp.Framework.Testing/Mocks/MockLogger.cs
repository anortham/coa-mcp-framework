using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace COA.Mcp.Framework.Testing.Mocks
{
    /// <summary>
    /// Mock logger implementation for testing.
    /// </summary>
    /// <typeparam name="T">The type whose name is used for the logger category name.</typeparam>
    public class MockLogger<T> : ILogger<T>
    {
        private readonly List<LogEntry> _logEntries = new();

        /// <summary>
        /// Gets all log entries.
        /// </summary>
        public IReadOnlyList<LogEntry> LogEntries => _logEntries.AsReadOnly();

        /// <summary>
        /// Gets or sets the minimum log level to capture.
        /// </summary>
        public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;

        /// <summary>
        /// Gets or sets whether logging is enabled.
        /// </summary>
        public bool IsLoggingEnabled { get; set; } = true;

        /// <inheritdoc/>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return new LogScope();
        }

        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel)
        {
            return IsLoggingEnabled && logLevel >= MinimumLevel;
        }

        /// <inheritdoc/>
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            var entry = new LogEntry
            {
                LogLevel = logLevel,
                EventId = eventId,
                Message = message,
                Exception = exception,
                State = state,
                Timestamp = DateTime.UtcNow,
                CategoryName = typeof(T).FullName ?? typeof(T).Name
            };

            _logEntries.Add(entry);
        }

        /// <summary>
        /// Clears all log entries.
        /// </summary>
        public void Clear()
        {
            _logEntries.Clear();
        }

        /// <summary>
        /// Gets log entries filtered by log level.
        /// </summary>
        /// <param name="logLevel">The log level to filter by.</param>
        /// <returns>Filtered log entries.</returns>
        public IEnumerable<LogEntry> GetLogs(LogLevel logLevel)
        {
            return _logEntries.Where(e => e.LogLevel == logLevel);
        }

        /// <summary>
        /// Gets log entries that contain the specified text.
        /// </summary>
        /// <param name="text">The text to search for.</param>
        /// <param name="comparison">The string comparison type.</param>
        /// <returns>Filtered log entries.</returns>
        public IEnumerable<LogEntry> GetLogsContaining(string text, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            return _logEntries.Where(e => e.Message.Contains(text, comparison));
        }

        /// <summary>
        /// Checks if any log entry contains the specified text.
        /// </summary>
        /// <param name="text">The text to search for.</param>
        /// <param name="logLevel">Optional log level filter.</param>
        /// <returns>True if found; otherwise, false.</returns>
        public bool ContainsLog(string text, LogLevel? logLevel = null)
        {
            var entries = logLevel.HasValue 
                ? _logEntries.Where(e => e.LogLevel == logLevel.Value)
                : _logEntries;

            return entries.Any(e => e.Message.Contains(text, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets the count of log entries by level.
        /// </summary>
        /// <param name="logLevel">The log level to count.</param>
        /// <returns>The count of entries.</returns>
        public int GetLogCount(LogLevel logLevel)
        {
            return _logEntries.Count(e => e.LogLevel == logLevel);
        }

        /// <summary>
        /// Gets all error log entries.
        /// </summary>
        public IEnumerable<LogEntry> Errors => GetLogs(LogLevel.Error);

        /// <summary>
        /// Gets all warning log entries.
        /// </summary>
        public IEnumerable<LogEntry> Warnings => GetLogs(LogLevel.Warning);

        /// <summary>
        /// Gets all information log entries.
        /// </summary>
        public IEnumerable<LogEntry> Information => GetLogs(LogLevel.Information);

        /// <summary>
        /// Represents a log scope.
        /// </summary>
        private class LogScope : IDisposable
        {
            public void Dispose()
            {
                // No-op for mock
            }
        }
    }

    /// <summary>
    /// Represents a captured log entry.
    /// </summary>
    public class LogEntry
    {
        /// <summary>
        /// Gets or sets the log level.
        /// </summary>
        public LogLevel LogLevel { get; set; }

        /// <summary>
        /// Gets or sets the event ID.
        /// </summary>
        public EventId EventId { get; set; }

        /// <summary>
        /// Gets or sets the log message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the exception, if any.
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// Gets or sets the state object.
        /// </summary>
        public object? State { get; set; }

        /// <summary>
        /// Gets or sets the timestamp.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the category name.
        /// </summary>
        public string CategoryName { get; set; } = string.Empty;

        /// <summary>
        /// Returns a string representation of the log entry.
        /// </summary>
        public override string ToString()
        {
            var exceptionInfo = Exception != null ? $" [Exception: {Exception.GetType().Name}]" : "";
            return $"[{Timestamp:HH:mm:ss.fff}] [{LogLevel}] {Message}{exceptionInfo}";
        }
    }
}