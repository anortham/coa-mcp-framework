using COA.Mcp.Framework.TokenOptimization;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Numeric;
using FluentAssertions.Primitives;
using System;

namespace COA.Mcp.Framework.Testing.Assertions
{
    /// <summary>
    /// Fluent assertions for token-related testing.
    /// </summary>
    public class TokenCountAssertions : NumericAssertions<int>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TokenCountAssertions"/> class.
        /// </summary>
        /// <param name="value">The token count value.</param>
        public TokenCountAssertions(int value) : base(value)
        {
        }

        /// <summary>
        /// Asserts that the token count is within safety limits.
        /// </summary>
        /// <param name="safetyMode">The safety mode to check against.</param>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<TokenCountAssertions> BeWithinSafetyLimit(
            TokenSafetyMode safetyMode = TokenSafetyMode.Default,
            string because = "",
            params object[] becauseArgs)
        {
            var limit = safetyMode switch
            {
                TokenSafetyMode.Conservative => TokenEstimator.CONSERVATIVE_SAFETY_LIMIT,
                _ => TokenEstimator.DEFAULT_SAFETY_LIMIT
            };

            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .ForCondition(Subject <= limit)
                .FailWith("Expected token count to be within {0} safety limit of {1}{reason}, but found {2}.",
                    safetyMode, limit, Subject);

            return new AndConstraint<TokenCountAssertions>(this);
        }

        /// <summary>
        /// Asserts that the token count is close to the expected value within a tolerance.
        /// </summary>
        /// <param name="expected">The expected token count.</param>
        /// <param name="tolerancePercentage">The tolerance as a percentage (default: 5%).</param>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<TokenCountAssertions> BeCloseTo(
            int expected,
            int tolerancePercentage = 5,
            string because = "",
            params object[] becauseArgs)
        {
            var tolerance = expected * tolerancePercentage / 100;
            var lowerBound = expected - tolerance;
            var upperBound = expected + tolerance;

            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .ForCondition(Subject >= lowerBound && Subject <= upperBound)
                .FailWith("Expected token count to be within {0}% of {1} ({2}-{3}){reason}, but found {4}.",
                    tolerancePercentage, expected, lowerBound, upperBound, Subject);

            return new AndConstraint<TokenCountAssertions>(this);
        }

        /// <summary>
        /// Asserts that the token count indicates efficient token usage.
        /// </summary>
        /// <param name="dataSize">The size of the data being tokenized.</param>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<TokenCountAssertions> BeEfficient(
            int dataSize,
            string because = "",
            params object[] becauseArgs)
        {
            // Rule of thumb: efficient tokenization should be less than 2 tokens per character on average
            var expectedMaxTokens = dataSize / 2;

            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .ForCondition(Subject <= expectedMaxTokens)
                .FailWith("Expected efficient tokenization (< {0} tokens for {1} chars){reason}, but found {2} tokens.",
                    expectedMaxTokens, dataSize, Subject);

            return new AndConstraint<TokenCountAssertions>(this);
        }
    }

    /// <summary>
    /// Fluent assertions for token estimation results.
    /// </summary>
    public class TokenEstimationAssertions : ReferenceTypeAssertions<object, TokenEstimationAssertions>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TokenEstimationAssertions"/> class.
        /// </summary>
        /// <param name="subject">The estimation result.</param>
        public TokenEstimationAssertions(object subject) : base(subject)
        {
            Identifier = "token estimation";
        }

        /// <inheritdoc/>
        protected override string Identifier { get; }

        /// <summary>
        /// Asserts that the estimation is accurate within a tolerance.
        /// </summary>
        /// <param name="actualTokens">The actual token count.</param>
        /// <param name="tolerancePercentage">The tolerance percentage.</param>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<TokenEstimationAssertions> BeAccurate(
            int actualTokens,
            int tolerancePercentage = 5,
            string because = "",
            params object[] becauseArgs)
        {
            var estimatedTokens = TokenEstimator.EstimateObject(Subject);
            var tolerance = actualTokens * tolerancePercentage / 100;
            var lowerBound = actualTokens - tolerance;
            var upperBound = actualTokens + tolerance;

            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .ForCondition(estimatedTokens >= lowerBound && estimatedTokens <= upperBound)
                .FailWith("Expected token estimation to be within {0}% of actual {1} tokens ({2}-{3}){reason}, but estimated {4}.",
                    tolerancePercentage, actualTokens, lowerBound, upperBound, estimatedTokens);

            return new AndConstraint<TokenEstimationAssertions>(this);
        }
    }

    /// <summary>
    /// Extensions for token assertions.
    /// </summary>
    public static class TokenAssertionExtensions
    {
        /// <summary>
        /// Returns assertions for token count.
        /// </summary>
        /// <param name="tokenCount">The token count.</param>
        /// <returns>Token count assertions.</returns>
        public static TokenCountAssertions Should(this int tokenCount)
        {
            return new TokenCountAssertions(tokenCount);
        }

        /// <summary>
        /// Asserts that the string has an expected token count.
        /// </summary>
        /// <param name="text">The text to check.</param>
        /// <param name="expectedTokens">The expected token count.</param>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>Token count assertions for chaining.</returns>
        public static TokenCountAssertions HaveTokenCount(
            this string text,
            int expectedTokens,
            string because = "",
            params object[] becauseArgs)
        {
            var actualTokens = TokenEstimator.EstimateString(text);

            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .ForCondition(actualTokens == expectedTokens)
                .FailWith("Expected text to have {0} tokens{reason}, but found {1}.",
                    expectedTokens, actualTokens);

            return new TokenCountAssertions(actualTokens);
        }

        /// <summary>
        /// Asserts that the object has token count less than the specified limit.
        /// </summary>
        /// <param name="obj">The object to check.</param>
        /// <param name="tokenLimit">The token limit.</param>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>Token count assertions for chaining.</returns>
        public static TokenCountAssertions HaveTokenCountLessThan(
            this object obj,
            int tokenLimit,
            string because = "",
            params object[] becauseArgs)
        {
            var actualTokens = TokenEstimator.EstimateObject(obj);

            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .ForCondition(actualTokens < tokenLimit)
                .FailWith("Expected object to have less than {0} tokens{reason}, but found {1}.",
                    tokenLimit, actualTokens);

            return new TokenCountAssertions(actualTokens);
        }

        /// <summary>
        /// Returns assertions for token estimation accuracy.
        /// </summary>
        /// <param name="obj">The object being estimated.</param>
        /// <returns>Token estimation assertions.</returns>
        public static TokenEstimationAssertions TokenEstimation(this object obj)
        {
            return new TokenEstimationAssertions(obj);
        }
    }
}