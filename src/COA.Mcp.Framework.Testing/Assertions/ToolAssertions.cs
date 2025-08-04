using COA.Mcp.Framework;
using COA.Mcp.Framework.Interfaces;
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
    /// Fluent assertions for MCP tools.
    /// </summary>
    public class ToolAssertions : ReferenceTypeAssertions<ITool, ToolAssertions>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ToolAssertions"/> class.
        /// </summary>
        /// <param name="tool">The tool to assert on.</param>
        public ToolAssertions(ITool tool) : base(tool)
        {
            Identifier = "tool";
        }

        /// <inheritdoc/>
        protected override string Identifier { get; }

        /// <summary>
        /// Asserts that the tool has the specified name.
        /// </summary>
        /// <param name="expectedName">The expected tool name.</param>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<ToolAssertions> HaveName(
            string expectedName,
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject)
                .ForCondition(tool => tool != null)
                .FailWith("Expected tool to have name {0}{reason}, but found <null>.", expectedName)
                .Then
                .ForCondition(tool => tool.ToolName == expectedName)
                .FailWith("Expected tool to have name {0}{reason}, but found {1}.", 
                    expectedName, Subject.ToolName);

            return new AndConstraint<ToolAssertions>(this);
        }

        /// <summary>
        /// Asserts that the tool belongs to the specified category.
        /// </summary>
        /// <param name="expectedCategory">The expected category.</param>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<ToolAssertions> BeInCategory(
            ToolCategory expectedCategory,
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject)
                .ForCondition(tool => tool != null)
                .FailWith("Expected tool to be in category {0}{reason}, but found <null>.", expectedCategory)
                .Then
                .ForCondition(tool => tool.Category == expectedCategory)
                .FailWith("Expected tool to be in category {0}{reason}, but found {1}.", 
                    expectedCategory, Subject.Category);

            return new AndConstraint<ToolAssertions>(this);
        }

        /// <summary>
        /// Asserts that the tool has a non-empty description.
        /// </summary>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<ToolAssertions> HaveDescription(
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject)
                .ForCondition(tool => tool != null)
                .FailWith("Expected tool to have a description{reason}, but found <null>.")
                .Then
                .ForCondition(tool => !string.IsNullOrWhiteSpace(tool.Description))
                .FailWith("Expected tool to have a non-empty description{reason}, but found empty or whitespace.");

            return new AndConstraint<ToolAssertions>(this);
        }

        /// <summary>
        /// Asserts that the tool description contains the specified text.
        /// </summary>
        /// <param name="expectedText">The text that should be in the description.</param>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<ToolAssertions> HaveDescriptionContaining(
            string expectedText,
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject)
                .ForCondition(tool => tool != null)
                .FailWith("Expected tool description to contain {0}{reason}, but found <null>.", expectedText)
                .Then
                .ForCondition(tool => tool.Description?.Contains(expectedText, StringComparison.OrdinalIgnoreCase) ?? false)
                .FailWith("Expected tool description to contain {0}{reason}, but found {1}.", 
                    expectedText, Subject.Description);

            return new AndConstraint<ToolAssertions>(this);
        }
    }

    /// <summary>
    /// Fluent assertions for tool metadata.
    /// </summary>
    public class ToolMetadataAssertions : ReferenceTypeAssertions<ToolMetadata, ToolMetadataAssertions>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ToolMetadataAssertions"/> class.
        /// </summary>
        /// <param name="metadata">The metadata to assert on.</param>
        public ToolMetadataAssertions(ToolMetadata metadata) : base(metadata)
        {
            Identifier = "tool metadata";
        }

        /// <inheritdoc/>
        protected override string Identifier { get; }

        /// <summary>
        /// Asserts that the metadata has the specified parameter count.
        /// </summary>
        /// <param name="expectedCount">The expected parameter count.</param>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<ToolMetadataAssertions> HaveParameterCount(
            int expectedCount,
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject?.Parameters)
                .ForCondition(parameters => parameters != null)
                .FailWith("Expected tool metadata to have {0} parameters{reason}, but parameters were <null>.", expectedCount)
                .Then
                .ForCondition(parameters => parameters.Properties.Count == expectedCount)
                .FailWith("Expected tool metadata to have {0} parameters{reason}, but found {1}.", 
                    expectedCount, Subject.Parameters.Properties.Count);

            return new AndConstraint<ToolMetadataAssertions>(this);
        }

        /// <summary>
        /// Asserts that the metadata has a parameter with the specified name.
        /// </summary>
        /// <param name="parameterName">The parameter name.</param>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<ToolMetadataAssertions> HaveParameter(
            string parameterName,
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject?.Parameters)
                .ForCondition(parameters => parameters != null)
                .FailWith("Expected tool metadata to have parameter {0}{reason}, but parameters were <null>.", parameterName)
                .Then
                .ForCondition(parameters => parameters.Properties.ContainsKey(parameterName))
                .FailWith("Expected tool metadata to have parameter {0}{reason}, but found parameters: {1}.", 
                    parameterName, string.Join(", ", Subject.Parameters.Properties.Keys));

            return new AndConstraint<ToolMetadataAssertions>(this);
        }

        /// <summary>
        /// Asserts that the metadata indicates the tool is enabled.
        /// </summary>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The current assertions for chaining.</returns>
        public AndConstraint<ToolMetadataAssertions> BeEnabled(
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject)
                .ForCondition(metadata => metadata != null)
                .FailWith("Expected tool metadata to be enabled{reason}, but found <null>.")
                .Then
                .ForCondition(metadata => metadata.Enabled)
                .FailWith("Expected tool metadata to be enabled{reason}, but it was not.");

            return new AndConstraint<ToolMetadataAssertions>(this);
        }
    }

    /// <summary>
    /// Extensions for tool assertions.
    /// </summary>
    public static class ToolAssertionExtensions
    {
        /// <summary>
        /// Returns assertions for the tool.
        /// </summary>
        /// <param name="tool">The tool to assert on.</param>
        /// <returns>Tool assertions.</returns>
        public static ToolAssertions Should(this ITool tool)
        {
            return new ToolAssertions(tool);
        }

        /// <summary>
        /// Returns assertions for the tool metadata.
        /// </summary>
        /// <param name="metadata">The metadata to assert on.</param>
        /// <returns>Tool metadata assertions.</returns>
        public static ToolMetadataAssertions Should(this ToolMetadata metadata)
        {
            return new ToolMetadataAssertions(metadata);
        }

        /// <summary>
        /// Asserts that the collection contains a tool with the specified name.
        /// </summary>
        /// <param name="assertions">The collection assertions.</param>
        /// <param name="toolName">The tool name to find.</param>
        /// <param name="because">Additional reason for the assertion.</param>
        /// <param name="becauseArgs">Arguments for the reason.</param>
        /// <returns>The tool for further assertions.</returns>
        public static ITool ContainToolNamed(
            this GenericCollectionAssertions<ITool> assertions,
            string toolName,
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => assertions.Subject)
                .ForCondition(tools => tools != null)
                .FailWith("Expected collection to contain tool named {0}{reason}, but found <null>.", toolName)
                .Then
                .ForCondition(tools => tools.Any(t => t.ToolName == toolName))
                .FailWith("Expected collection to contain tool named {0}{reason}, but found tools: {1}.", 
                    toolName, string.Join(", ", assertions.Subject.Select(t => t.ToolName)));

            return assertions.Subject.First(t => t.ToolName == toolName);
        }
    }
}