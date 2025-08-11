using System;
using COA.Mcp.Framework.TokenOptimization.Models;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework.TokenOptimization.ResponseBuilders;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace COA.Mcp.Framework.Testing.Mocks
{
    /// <summary>
    /// Mock response builder for testing response building scenarios with strong typing.
    /// </summary>
    /// <typeparam name="TInput">The type of input data being processed.</typeparam>
    /// <typeparam name="TResult">The type of result being returned.</typeparam>
    public class MockResponseBuilder<TInput, TResult> : BaseResponseBuilder<TInput, TResult>
        where TResult : new()
    {
        private readonly List<string> _insights = new();
        private readonly List<AIAction> _actions = new();
        private Func<TInput, ResponseContext, Task<TResult>>? _buildResponseFunc;
        private Func<TInput, string, List<string>>? _generateInsightsFunc;
        private Func<TInput, int, List<AIAction>>? _generateActionsFunc;

        /// <summary>
        /// Gets the list of build requests.
        /// </summary>
        public List<BuildRequest> BuildRequests { get; } = new();

        /// <summary>
        /// Gets or sets whether to simulate truncation.
        /// </summary>
        public bool SimulateTruncation { get; set; }

        /// <summary>
        /// Gets or sets the simulated execution time in milliseconds.
        /// </summary>
        public int SimulatedExecutionTimeMs { get; set; } = 100;

        /// <summary>
        /// Configures the insights to return.
        /// </summary>
        /// <param name="insights">The insights to return.</param>
        /// <returns>The builder for chaining.</returns>
        public MockResponseBuilder<TInput, TResult> WithInsights(params string[] insights)
        {
            _insights.Clear();
            _insights.AddRange(insights);
            return this;
        }

        /// <summary>
        /// Configures the actions to return.
        /// </summary>
        /// <param name="actions">The actions to return.</param>
        /// <returns>The builder for chaining.</returns>
        public MockResponseBuilder<TInput, TResult> WithActions(params AIAction[] actions)
        {
            _actions.Clear();
            _actions.AddRange(actions);
            return this;
        }

        /// <summary>
        /// Configures a custom build response function.
        /// </summary>
        /// <param name="buildFunc">The build function.</param>
        /// <returns>The builder for chaining.</returns>
        public MockResponseBuilder<TInput, TResult> WithBuildFunction(Func<TInput, ResponseContext, Task<TResult>> buildFunc)
        {
            _buildResponseFunc = buildFunc;
            return this;
        }

        /// <summary>
        /// Configures a custom insights generation function.
        /// </summary>
        /// <param name="insightsFunc">The insights function.</param>
        /// <returns>The builder for chaining.</returns>
        public MockResponseBuilder<TInput, TResult> WithInsightsFunction(Func<TInput, string, List<string>> insightsFunc)
        {
            _generateInsightsFunc = insightsFunc;
            return this;
        }

        /// <summary>
        /// Configures a custom actions generation function.
        /// </summary>
        /// <param name="actionsFunc">The actions function.</param>
        /// <returns>The builder for chaining.</returns>
        public MockResponseBuilder<TInput, TResult> WithActionsFunction(Func<TInput, int, List<AIAction>> actionsFunc)
        {
            _generateActionsFunc = actionsFunc;
            return this;
        }

        /// <inheritdoc/>
        public override async Task<TResult> BuildResponseAsync(TInput data, ResponseContext context)
        {
            var request = new BuildRequest
            {
                Data = data,
                Context = context,
                Timestamp = DateTime.UtcNow
            };
            BuildRequests.Add(request);

            if (_buildResponseFunc != null)
            {
                return await _buildResponseFunc(data, context);
            }

            // Simulate execution delay
            if (SimulatedExecutionTimeMs > 0)
            {
                await Task.Delay(SimulatedExecutionTimeMs);
            }

            var startTime = DateTime.UtcNow.AddMilliseconds(-SimulatedExecutionTimeMs);

            // Build default response - check if TResult is AIOptimizedResponse
            if (typeof(TResult).IsGenericType && 
                typeof(TResult).GetGenericTypeDefinition() == typeof(AIOptimizedResponse<>))
            {
                var responseType = typeof(TResult).GetGenericArguments()[0];
                var responseDataType = typeof(AIResponseData<>).MakeGenericType(responseType);
                var responseData = Activator.CreateInstance(responseDataType);
                
                responseDataType.GetProperty("Summary")?.SetValue(responseData, $"Mock response for {typeof(TInput).Name}");
                responseDataType.GetProperty("Results")?.SetValue(responseData, data);
                responseDataType.GetProperty("Count")?.SetValue(responseData, data is ICollection<object> collection ? collection.Count : 1);
                
                var response = new TResult();
                var responseObj = response as object;
                responseObj?.GetType().GetProperty("Format")?.SetValue(responseObj, "ai-optimized");
                responseObj?.GetType().GetProperty("Data")?.SetValue(responseObj, responseData);
                responseObj?.GetType().GetProperty("Insights")?.SetValue(responseObj, GenerateInsights(data, context.ResponseMode));
                responseObj?.GetType().GetProperty("Actions")?.SetValue(responseObj, GenerateActions(data, CalculateTokenBudget(context)));
                responseObj?.GetType().GetProperty("Meta")?.SetValue(responseObj, CreateMetadata(startTime, SimulateTruncation));
                
                return response;
            }
            else
            {
                // For non-AIOptimizedResponse types, return a new instance
                return new TResult();
            }
        }

        /// <inheritdoc/>
        protected override List<string> GenerateInsights(TInput data, string responseMode)
        {
            if (_generateInsightsFunc != null)
            {
                return _generateInsightsFunc(data, responseMode);
            }

            if (_insights.Count > 0)
            {
                return new List<string>(_insights);
            }

            // Generate default insights
            return new List<string>
            {
                $"Processing {typeof(TInput).Name} in {responseMode} mode",
                "This is a mock insight for testing",
                SimulateTruncation ? "Results were truncated to fit token limits" : "All results included"
            };
        }

        /// <inheritdoc/>
        protected override List<AIAction> GenerateActions(TInput data, int tokenBudget)
        {
            if (_generateActionsFunc != null)
            {
                return _generateActionsFunc(data, tokenBudget);
            }

            if (_actions.Count > 0)
            {
                return new List<AIAction>(_actions);
            }

            // Generate default actions
            return new List<AIAction>
            {
                new AIAction
                {
                    Tool = "mock_tool",
                    Description = "Continue with mock action",
                    Parameters = new Dictionary<string, object> { ["test"] = true },
                    Rationale = "This is a suggested next step for testing"
                }
            };
        }

        /// <summary>
        /// Resets all configuration and history.
        /// </summary>
        public void Reset()
        {
            BuildRequests.Clear();
            _insights.Clear();
            _actions.Clear();
            _buildResponseFunc = null;
            _generateInsightsFunc = null;
            _generateActionsFunc = null;
            SimulateTruncation = false;
            SimulatedExecutionTimeMs = 100;
        }

        /// <summary>
        /// Represents a build request for tracking.
        /// </summary>
        public class BuildRequest
        {
            /// <summary>
            /// Gets or sets the data that was built.
            /// </summary>
            public TInput Data { get; set; } = default!;

            /// <summary>
            /// Gets or sets the context used.
            /// </summary>
            public ResponseContext Context { get; set; } = null!;

            /// <summary>
            /// Gets or sets the timestamp of the request.
            /// </summary>
            public DateTime Timestamp { get; set; }
        }
    }
    
    /// <summary>
    /// Non-generic mock response builder for backward compatibility.
    /// </summary>
    /// <typeparam name="TData">The type of data being processed.</typeparam>
    [Obsolete("Use MockResponseBuilder<TInput, TResult> instead for better type safety. This single-parameter version will be removed in version 2.0.0.", false)]
    public class MockResponseBuilder<TData> : MockResponseBuilder<TData, AIOptimizedResponse>
    {
    }

    /// <summary>
    /// Non-generic mock response builder for simple testing scenarios.
    /// </summary>
    public class MockResponseBuilder : MockResponseBuilder<object, AIOptimizedResponse>
    {
        /// <summary>
        /// Creates a simple successful response.
        /// </summary>
        /// <param name="data">The data to include.</param>
        /// <returns>A successful response.</returns>
        public static AIOptimizedResponse CreateSuccessResponse(object data)
        {
            return new AIOptimizedResponse
            {
                Format = "ai-optimized",
                Data = new AIResponseData
                {
                    Summary = "Success",
                    Results = data,
                    Count = 1
                },
                Insights = new List<string> { "Operation completed successfully" },
                Actions = new List<AIAction>(),
                Meta = new AIResponseMeta
                {
                    ExecutionTime = "0ms",
                    Truncated = false
                }
            };
        }

        /// <summary>
        /// Creates an error response.
        /// </summary>
        /// <param name="error">The error message.</param>
        /// <returns>An error response.</returns>
        public static AIOptimizedResponse CreateErrorResponse(string error)
        {
            return new AIOptimizedResponse
            {
                Format = "ai-optimized",
                Data = new AIResponseData
                {
                    Summary = "Error",
                    Results = new { error },
                    Count = 0
                },
                Insights = new List<string> { $"Error occurred: {error}" },
                Actions = new List<AIAction>
                {
                    new AIAction
                    {
                        Tool = "retry",
                        Description = "Retry the operation",
                        Rationale = "The operation failed and may succeed on retry"
                    }
                },
                Meta = new AIResponseMeta
                {
                    ExecutionTime = "0ms",
                    Truncated = false
                }
            };
        }
    }
}