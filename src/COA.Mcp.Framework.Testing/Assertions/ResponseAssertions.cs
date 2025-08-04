using COA.Mcp.Framework.Testing.Base;
using COA.Mcp.Framework.TokenOptimization.Models;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using System;
using System.Linq;

namespace COA.Mcp.Framework.Testing.Assertions
{
    /// <summary>
    /// Fluent assertions for AI-optimized responses.
    /// </summary>
    public class AIOptimizedResponseAssertions : ReferenceTypeAssertions<AIOptimizedResponse, AIOptimizedResponseAssertions>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AIOptimizedResponseAssertions"/> class.
        /// </summary>
        /// <param name="response">The response to assert on.</param>
        public AIOptimizedResponseAssertions(AIOptimizedResponse response) : base(response)
        {
            Identifier = "AI-optimized response";
        }

        /// <inheritdoc/>
        protected override string Identifier { get; }

        /// <summary>
        /// Asserts that the response is successful.
        /// </summary>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<AIOptimizedResponseAssertions> BeSuccessful(
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject)
                .ForCondition(response => response != null)
                .FailWith("Expected response to be successful{reason}, but found <null>.")
                .Then
                .ForCondition(response => response.Data != null)
                .FailWith("Expected response to be successful{reason}, but Data was null.")
                .Then
                .ForCondition(response => response.Data.Results != null)
                .FailWith("Expected response to be successful{reason}, but Results was null.");

            return new AndConstraint<AIOptimizedResponseAssertions>(this);
        }

        /// <summary>
        /// Asserts that the response was truncated.
        /// </summary>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<AIOptimizedResponseAssertions> BeTruncated(
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject?.Meta)
                .ForCondition(meta => meta != null)
                .FailWith("Expected response to be truncated{reason}, but Meta was null.")
                .Then
                .ForCondition(meta => meta.Truncated)
                .FailWith("Expected response to be truncated{reason}, but it was not.");

            return new AndConstraint<AIOptimizedResponseAssertions>(this);
        }

        /// <summary>
        /// Asserts that the response was not truncated.
        /// </summary>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<AIOptimizedResponseAssertions> NotBeTruncated(
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject?.Meta)
                .ForCondition(meta => meta != null)
                .FailWith("Expected response not to be truncated{reason}, but Meta was null.")
                .Then
                .ForCondition(meta => !meta.Truncated)
                .FailWith("Expected response not to be truncated{reason}, but it was.");

            return new AndConstraint<AIOptimizedResponseAssertions>(this);
        }

        /// <summary>
        /// Asserts that the response has the specified number of insights.
        /// </summary>
        /// <param name="minCount">Minimum number of insights.</param>
        /// <param name="maxCount">Maximum number of insights.</param>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<AIOptimizedResponseAssertions> HaveInsightCount(
            int minCount,
            int? maxCount = null,
            string because = "",
            params object[] becauseArgs)
        {
            var actualMax = maxCount ?? minCount;
            var insightCount = Subject?.Insights?.Count ?? 0;

            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .ForCondition(insightCount >= minCount && insightCount <= actualMax)
                .FailWith("Expected response to have between {0} and {1} insights{reason}, but found {2}.",
                    minCount, actualMax, insightCount);

            return new AndConstraint<AIOptimizedResponseAssertions>(this);
        }

        /// <summary>
        /// Asserts that the response has an insight containing the specified text.
        /// </summary>
        /// <param name="expectedText">The expected text in an insight.</param>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<AIOptimizedResponseAssertions> HaveInsightContaining(
            string expectedText,
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject?.Insights)
                .ForCondition(insights => insights != null && insights.Any())
                .FailWith("Expected response to have insight containing {0}{reason}, but no insights found.", expectedText)
                .Then
                .ForCondition(insights => insights.Any(i => i.Contains(expectedText, StringComparison.OrdinalIgnoreCase)))
                .FailWith("Expected response to have insight containing {0}{reason}, but found insights: {1}.",
                    expectedText, string.Join(", ", Subject.Insights.Select(i => $"'{i}'")));

            return new AndConstraint<AIOptimizedResponseAssertions>(this);
        }

        /// <summary>
        /// Asserts that the response has a next action with the specified tool.
        /// </summary>
        /// <param name="toolName">The expected tool name.</param>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<AIOptimizedResponseAssertions> HaveNextAction(
            string toolName,
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject?.Actions)
                .ForCondition(actions => actions != null && actions.Any())
                .FailWith("Expected response to have next action {0}{reason}, but no actions found.", toolName)
                .Then
                .ForCondition(actions => actions.Any(a => a.Tool == toolName))
                .FailWith("Expected response to have next action {0}{reason}, but found actions: {1}.",
                    toolName, string.Join(", ", Subject.Actions.Select(a => a.Tool)));

            return new AndConstraint<AIOptimizedResponseAssertions>(this);
        }

        /// <summary>
        /// Asserts that the response has a resource URI.
        /// </summary>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<AIOptimizedResponseAssertions> HaveResourceUri(
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject?.Meta?.ResourceUri)
                .ForCondition(uri => !string.IsNullOrWhiteSpace(uri))
                .FailWith("Expected response to have resource URI{reason}, but it was empty or null.");

            return new AndConstraint<AIOptimizedResponseAssertions>(this);
        }

        /// <summary>
        /// Asserts that the response is token optimized.
        /// </summary>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<AIOptimizedResponseAssertions> BeTokenOptimized(
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject)
                .ForCondition(response => response != null)
                .FailWith("Expected response to be token optimized{reason}, but found <null>.")
                .Then
                .ForCondition(response => response.Format == "ai-optimized")
                .FailWith("Expected response to be token optimized{reason}, but format was {0}.", Subject.Format)
                .Then
                .ForCondition(response => response.Meta != null)
                .FailWith("Expected response to be token optimized{reason}, but Meta was null.");

            return new AndConstraint<AIOptimizedResponseAssertions>(this);
        }

        /// <summary>
        /// Asserts that the response has a truncation message in insights.
        /// </summary>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<AIOptimizedResponseAssertions> HaveTruncationMessage(
            string because = "",
            params object[] becauseArgs)
        {
            var truncationKeywords = new[] { "truncated", "reduced", "limited", "partial" };

            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject?.Insights)
                .ForCondition(insights => insights != null && insights.Any())
                .FailWith("Expected response to have truncation message{reason}, but no insights found.")
                .Then
                .ForCondition(insights => insights.Any(i => 
                    truncationKeywords.Any(keyword => i.Contains(keyword, StringComparison.OrdinalIgnoreCase))))
                .FailWith("Expected response to have truncation message{reason}, but no truncation-related insights found.");

            return new AndConstraint<AIOptimizedResponseAssertions>(this);
        }
    }

    /// <summary>
    /// Fluent assertions for tool execution results.
    /// </summary>
    public class ToolExecutionResultAssertions<TResult> : ReferenceTypeAssertions<ToolExecutionResult<TResult>, ToolExecutionResultAssertions<TResult>>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ToolExecutionResultAssertions{TResult}"/> class.
        /// </summary>
        /// <param name="result">The execution result.</param>
        public ToolExecutionResultAssertions(ToolExecutionResult<TResult> result) : base(result)
        {
            Identifier = "tool execution result";
        }

        /// <inheritdoc/>
        protected override string Identifier { get; }

        /// <summary>
        /// Asserts that the execution was successful.
        /// </summary>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<ToolExecutionResultAssertions<TResult>> BeSuccessful(
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject)
                .ForCondition(result => result != null)
                .FailWith("Expected execution to be successful{reason}, but found <null>.")
                .Then
                .ForCondition(result => result.Success)
                .FailWith("Expected execution to be successful{reason}, but it failed with exception: {0}.",
                    Subject.Exception?.Message);

            return new AndConstraint<ToolExecutionResultAssertions<TResult>>(this);
        }

        /// <summary>
        /// Asserts that the execution completed within the specified time.
        /// </summary>
        /// <param name="maxMilliseconds">Maximum execution time in milliseconds.</param>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<ToolExecutionResultAssertions<TResult>> CompleteWithinMs(
            double maxMilliseconds,
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject)
                .ForCondition(result => result != null)
                .FailWith("Expected execution to complete within {0}ms{reason}, but found <null>.", maxMilliseconds)
                .Then
                .ForCondition(result => result.ExecutionTimeMs <= maxMilliseconds)
                .FailWith("Expected execution to complete within {0}ms{reason}, but took {1}ms.",
                    maxMilliseconds, Subject.ExecutionTimeMs);

            return new AndConstraint<ToolExecutionResultAssertions<TResult>>(this);
        }

        /// <summary>
        /// Asserts that the execution produced valid JSON.
        /// </summary>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<ToolExecutionResultAssertions<TResult>> ProduceValidJson(
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject?.RawResult)
                .ForCondition(result => result != null)
                .FailWith("Expected execution to produce valid JSON{reason}, but result was null.")
                .Then
                .ForCondition(result =>
                {
                    try
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(result);
                        return !string.IsNullOrWhiteSpace(json);
                    }
                    catch
                    {
                        return false;
                    }
                })
                .FailWith("Expected execution to produce valid JSON{reason}, but serialization failed.");

            return new AndConstraint<ToolExecutionResultAssertions<TResult>>(this);
        }
    }

    /// <summary>
    /// Extensions for response assertions.
    /// </summary>
    public static class ResponseAssertionExtensions
    {
        /// <summary>
        /// Returns assertions for AI-optimized response.
        /// </summary>
        /// <param name="response">The response to assert on.</param>
        /// <returns>Response assertions.</returns>
        public static AIOptimizedResponseAssertions Should(this AIOptimizedResponse response)
        {
            return new AIOptimizedResponseAssertions(response);
        }

        /// <summary>
        /// Returns assertions for tool execution result.
        /// </summary>
        /// <typeparam name="TResult">The result type.</typeparam>
        /// <param name="result">The execution result.</param>
        /// <returns>Execution result assertions.</returns>
        public static ToolExecutionResultAssertions<TResult> Should<TResult>(this ToolExecutionResult<TResult> result)
        {
            return new ToolExecutionResultAssertions<TResult>(result);
        }
    }
}