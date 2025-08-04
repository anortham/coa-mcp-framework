using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace COA.Mcp.Framework.Testing.Base
{
    /// <summary>
    /// Base class for testing MCP tools, providing tool-specific test infrastructure.
    /// </summary>
    /// <typeparam name="TTool">The type of tool being tested.</typeparam>
    public abstract class ToolTestBase<TTool> : McpTestBase where TTool : class
    {
        /// <summary>
        /// Gets the tool instance being tested.
        /// </summary>
        protected TTool Tool { get; private set; } = null!;

        /// <summary>
        /// Gets the logger for the tool.
        /// </summary>
        protected Mock<ILogger<TTool>> ToolLoggerMock { get; private set; } = null!;

        /// <summary>
        /// Setup method that creates the tool instance.
        /// </summary>
        public override void SetUp()
        {
            base.SetUp();

            // Create tool-specific logger
            ToolLoggerMock = new Mock<ILogger<TTool>>();
            Services.AddSingleton(ToolLoggerMock.Object);

            // Create the tool instance
            Tool = CreateTool();

            // Validate tool was created
            Assert.That(Tool, Is.Not.Null, "CreateTool() must return a non-null tool instance");
        }

        /// <summary>
        /// Creates the tool instance to be tested.
        /// Override this method to create and configure your tool.
        /// </summary>
        /// <returns>The tool instance.</returns>
        protected abstract TTool CreateTool();

        /// <summary>
        /// Executes a tool method and captures the result.
        /// </summary>
        /// <typeparam name="TResult">The expected result type.</typeparam>
        /// <param name="toolMethod">The tool method to execute.</param>
        /// <returns>The tool execution result.</returns>
        protected async Task<ToolExecutionResult<TResult>> ExecuteToolAsync<TResult>(
            Func<Task<object>> toolMethod)
        {
            var startTime = DateTime.UtcNow;
            Exception? exception = null;
            object? result = null;

            try
            {
                result = await toolMethod();
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            var endTime = DateTime.UtcNow;
            var executionTime = endTime - startTime;

            return new ToolExecutionResult<TResult>
            {
                Success = exception == null,
                Result = exception == null && result is TResult typedResult ? typedResult : default,
                RawResult = result,
                Exception = exception,
                ExecutionTime = executionTime,
                StartTime = startTime,
                EndTime = endTime
            };
        }

        /// <summary>
        /// Verifies that the logger received the expected log message.
        /// </summary>
        /// <param name="logLevel">The expected log level.</param>
        /// <param name="messageContains">Text that should be contained in the log message.</param>
        /// <param name="times">The number of times the log should have been called.</param>
        protected void VerifyLog(LogLevel logLevel, string messageContains, Times? times = null)
        {
            times ??= Times.Once();

            ToolLoggerMock.Verify(
                x => x.Log(
                    logLevel,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains(messageContains)),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                times.Value);
        }

        /// <summary>
        /// Verifies that no errors were logged.
        /// </summary>
        protected void VerifyNoErrors()
        {
            ToolLoggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never());
        }

        /// <summary>
        /// Verifies that no warnings were logged.
        /// </summary>
        protected void VerifyNoWarnings()
        {
            ToolLoggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never());
        }
    }

    /// <summary>
    /// Represents the result of a tool execution.
    /// </summary>
    /// <typeparam name="TResult">The expected result type.</typeparam>
    public class ToolExecutionResult<TResult>
    {
        /// <summary>
        /// Gets or sets whether the execution was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the typed result.
        /// </summary>
        public TResult? Result { get; set; }

        /// <summary>
        /// Gets or sets the raw result object.
        /// </summary>
        public object? RawResult { get; set; }

        /// <summary>
        /// Gets or sets any exception that occurred.
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// Gets or sets the execution time.
        /// </summary>
        public TimeSpan ExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets the start time.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the end time.
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Gets the execution time in milliseconds.
        /// </summary>
        public double ExecutionTimeMs => ExecutionTime.TotalMilliseconds;
    }
}