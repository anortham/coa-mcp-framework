using COA.Mcp.Framework.TokenOptimization.Models;
using FluentAssertions;
using FluentAssertions.Collections;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;

namespace COA.Mcp.Framework.Testing.Assertions
{
    /// <summary>
    /// Fluent assertions for Insight objects.
    /// </summary>
    public class InsightAssertions : ReferenceTypeAssertions<Insight, InsightAssertions>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InsightAssertions"/> class.
        /// </summary>
        /// <param name="insight">The insight to assert on.</param>
        public InsightAssertions(Insight insight) : base(insight)
        {
            Identifier = "insight";
        }

        /// <inheritdoc/>
        protected override string Identifier { get; }

        /// <summary>
        /// Asserts that the insight has the specified importance level.
        /// </summary>
        /// <param name="expectedImportance">The expected importance.</param>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<InsightAssertions> HaveImportance(
            InsightImportance expectedImportance,
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject)
                .ForCondition(insight => insight != null)
                .FailWith("Expected insight to have importance {0}{reason}, but found <null>.", expectedImportance)
                .Then
                .ForCondition(insight => insight.Importance == expectedImportance)
                .FailWith("Expected insight to have importance {0}{reason}, but found {1}.",
                    expectedImportance, Subject.Importance);

            return new AndConstraint<InsightAssertions>(this);
        }

        /// <summary>
        /// Asserts that the insight content contains the specified text.
        /// </summary>
        /// <param name="expectedText">The expected text.</param>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<InsightAssertions> ContainText(
            string expectedText,
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject?.Text)
                .ForCondition(content => !string.IsNullOrEmpty(content))
                .FailWith("Expected insight to contain text {0}{reason}, but content was empty.", expectedText)
                .Then
                .ForCondition(content => content.Contains(expectedText, StringComparison.OrdinalIgnoreCase))
                .FailWith("Expected insight to contain text {0}{reason}, but found {1}.",
                    expectedText, Subject.Text);

            return new AndConstraint<InsightAssertions>(this);
        }

        /// <summary>
        /// Asserts that the insight has metadata with the specified key.
        /// </summary>
        /// <param name="key">The metadata key.</param>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<InsightAssertions> HaveMetadata(
            string key,
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject?.Metadata)
                .ForCondition(metadata => metadata != null)
                .FailWith("Expected insight to have metadata with key {0}{reason}, but metadata was null.", key)
                .Then
                .ForCondition(metadata => metadata.ContainsKey(key))
                .FailWith("Expected insight to have metadata with key {0}{reason}, but found keys: {1}.",
                    key, string.Join(", ", Subject.Metadata.Keys));

            return new AndConstraint<InsightAssertions>(this);
        }

        /// <summary>
        /// Asserts that the insight is contextual (has context metadata).
        /// </summary>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<InsightAssertions> BeContextual(
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject)
                .ForCondition(insight => insight != null)
                .FailWith("Expected insight to be contextual{reason}, but found <null>.")
                .Then
                .ForCondition(insight => insight.Metadata != null && 
                    (insight.Metadata.ContainsKey("context") || 
                     insight.Metadata.ContainsKey("operation") ||
                     insight.Metadata.ContainsKey("history")))
                .FailWith("Expected insight to be contextual{reason}, but no context metadata found.");

            return new AndConstraint<InsightAssertions>(this);
        }
    }

    /// <summary>
    /// Fluent assertions for collections of insights.
    /// </summary>
    public class InsightCollectionAssertions : GenericCollectionAssertions<Insight>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InsightCollectionAssertions"/> class.
        /// </summary>
        /// <param name="insights">The collection of insights.</param>
        public InsightCollectionAssertions(IEnumerable<Insight> insights) : base(insights)
        {
        }

        /// <summary>
        /// Asserts that all insights have at least the specified importance.
        /// </summary>
        /// <param name="minImportance">The minimum importance level.</param>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<InsightCollectionAssertions> AllHaveMinimumImportance(
            InsightImportance minImportance,
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject)
                .ForCondition(insights => insights != null)
                .FailWith("Expected all insights to have minimum importance {0}{reason}, but collection was null.", minImportance)
                .Then
                .ForCondition(insights => insights.All(i => i.Importance >= minImportance))
                .FailWith("Expected all insights to have minimum importance {0}{reason}, but found insights with importance: {1}.",
                    minImportance, string.Join(", ", Subject.Where(i => i.Importance < minImportance).Select(i => i.Importance)));

            return new AndConstraint<InsightCollectionAssertions>(this);
        }

        /// <summary>
        /// Asserts that the collection is properly prioritized (sorted by importance).
        /// </summary>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<InsightCollectionAssertions> BePrioritized(
            string because = "",
            params object[] becauseArgs)
        {
            var insightsList = Subject?.ToList() ?? new List<Insight>();
            var sortedList = insightsList.OrderByDescending(i => i.Importance).ToList();

            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .ForCondition(insightsList.SequenceEqual(sortedList))
                .FailWith("Expected insights to be prioritized by importance{reason}, but they were not properly sorted.");

            return new AndConstraint<InsightCollectionAssertions>(this);
        }

        /// <summary>
        /// Asserts that the collection contains an insight about the specified topic.
        /// </summary>
        /// <param name="topic">The topic to find.</param>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<InsightCollectionAssertions> ContainInsightAbout(
            string topic,
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject)
                .ForCondition(insights => insights != null)
                .FailWith("Expected insights to contain one about {0}{reason}, but collection was null.", topic)
                .Then
                .ForCondition(insights => insights.Any(i => i.Text.Contains(topic, StringComparison.OrdinalIgnoreCase)))
                .FailWith("Expected insights to contain one about {0}{reason}, but none found.", topic);

            return new AndConstraint<InsightCollectionAssertions>(this);
        }

        /// <summary>
        /// Asserts that the collection has diverse insight types (based on metadata).
        /// </summary>
        /// <param name="minDiversity">Minimum number of different insight types.</param>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<InsightCollectionAssertions> BeDiverse(
            int minDiversity = 2,
            string because = "",
            params object[] becauseArgs)
        {
            var insightTypes = Subject?
                .Where(i => i.Metadata?.ContainsKey("type") == true)
                .Select(i => i.Metadata["type"])
                .Distinct()
                .Count() ?? 0;

            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .ForCondition(insightTypes >= minDiversity)
                .FailWith("Expected insights to have at least {0} different types{reason}, but found {1}.",
                    minDiversity, insightTypes);

            return new AndConstraint<InsightCollectionAssertions>(this);
        }
    }

    /// <summary>
    /// Fluent assertions for AI actions.
    /// </summary>
    public class AIActionAssertions : ReferenceTypeAssertions<AIAction, AIActionAssertions>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AIActionAssertions"/> class.
        /// </summary>
        /// <param name="action">The action to assert on.</param>
        public AIActionAssertions(AIAction action) : base(action)
        {
            Identifier = "AI action";
        }

        /// <inheritdoc/>
        protected override string Identifier { get; }

        /// <summary>
        /// Asserts that the action uses the specified tool.
        /// </summary>
        /// <param name="expectedTool">The expected tool name.</param>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<AIActionAssertions> UseTool(
            string expectedTool,
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject)
                .ForCondition(action => action != null)
                .FailWith("Expected action to use tool {0}{reason}, but found <null>.", expectedTool)
                .Then
                .ForCondition(action => action.Tool == expectedTool)
                .FailWith("Expected action to use tool {0}{reason}, but found {1}.",
                    expectedTool, Subject.Tool);

            return new AndConstraint<AIActionAssertions>(this);
        }

        /// <summary>
        /// Asserts that the action has a rationale.
        /// </summary>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<AIActionAssertions> HaveRationale(
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject?.Rationale)
                .ForCondition(rationale => !string.IsNullOrWhiteSpace(rationale))
                .FailWith("Expected action to have a rationale{reason}, but it was empty or null.");

            return new AndConstraint<AIActionAssertions>(this);
        }

        /// <summary>
        /// Asserts that the action belongs to the specified category.
        /// </summary>
        /// <param name="expectedCategory">The expected category.</param>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<AIActionAssertions> BeInCategory(
            string expectedCategory,
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject)
                .ForCondition(action => action != null)
                .FailWith("Expected action to be in category {0}{reason}, but found <null>.", expectedCategory)
                .Then
                .ForCondition(action => action.Category == expectedCategory)
                .FailWith("Expected action to be in category {0}{reason}, but found {1}.",
                    expectedCategory, Subject.Category);

            return new AndConstraint<AIActionAssertions>(this);
        }

        /// <summary>
        /// Asserts that the action has valid parameters.
        /// </summary>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<AIActionAssertions> HaveValidParameters(
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject)
                .ForCondition(action => action != null)
                .FailWith("Expected action to have valid parameters{reason}, but found <null>.")
                .Then
                .ForCondition(action => action.Parameters != null)
                .FailWith("Expected action to have valid parameters{reason}, but parameters were null.");

            return new AndConstraint<AIActionAssertions>(this);
        }
    }

    /// <summary>
    /// Extensions for insight and action assertions.
    /// </summary>
    public static class InsightAssertionExtensions
    {
        /// <summary>
        /// Returns assertions for an insight.
        /// </summary>
        /// <param name="insight">The insight to assert on.</param>
        /// <returns>Insight assertions.</returns>
        public static InsightAssertions Should(this Insight insight)
        {
            return new InsightAssertions(insight);
        }

        /// <summary>
        /// Returns assertions for a collection of insights.
        /// </summary>
        /// <param name="insights">The insights to assert on.</param>
        /// <returns>Insight collection assertions.</returns>
        public static InsightCollectionAssertions Should(this IEnumerable<Insight> insights)
        {
            return new InsightCollectionAssertions(insights);
        }

        /// <summary>
        /// Returns assertions for an AI action.
        /// </summary>
        /// <param name="action">The action to assert on.</param>
        /// <returns>AI action assertions.</returns>
        public static AIActionAssertions Should(this AIAction action)
        {
            return new AIActionAssertions(action);
        }
    }
}