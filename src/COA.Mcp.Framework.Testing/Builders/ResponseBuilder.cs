using COA.Mcp.Framework.TokenOptimization.Models;
using COA.Mcp.Framework.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace COA.Mcp.Framework.Testing.Builders
{
    /// <summary>
    /// Builder for creating AI-optimized responses for testing.
    /// </summary>
    public class ResponseBuilder
    {
        private string _format = "ai-optimized";
        private string _summary = "Test response";
        private object? _results;
        private int _count;
        private readonly List<string> _insights = new();
        private readonly List<AIAction> _actions = new();
        private string _executionTime = "0ms";
        private bool _truncated;
        private string? _resourceUri;
        private readonly Dictionary<string, object> _additionalMeta = new();

        /// <summary>
        /// Sets the response format.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <returns>The builder for chaining.</returns>
        public ResponseBuilder WithFormat(string format)
        {
            _format = format;
            return this;
        }

        /// <summary>
        /// Sets the response summary.
        /// </summary>
        /// <param name="summary">The summary text.</param>
        /// <returns>The builder for chaining.</returns>
        public ResponseBuilder WithSummary(string summary)
        {
            _summary = summary;
            return this;
        }

        /// <summary>
        /// Sets the response results.
        /// </summary>
        /// <param name="results">The results object.</param>
        /// <returns>The builder for chaining.</returns>
        public ResponseBuilder WithResults(object results)
        {
            _results = results;
            
            // Auto-calculate count if it's a collection
            if (results is ICollection<object> collection)
            {
                _count = collection.Count;
            }
            else if (results != null)
            {
                _count = 1;
            }
            
            return this;
        }

        /// <summary>
        /// Sets the result count explicitly.
        /// </summary>
        /// <param name="count">The count value.</param>
        /// <returns>The builder for chaining.</returns>
        public ResponseBuilder WithCount(int count)
        {
            _count = count;
            return this;
        }

        /// <summary>
        /// Adds insights to the response.
        /// </summary>
        /// <param name="insights">The insights to add.</param>
        /// <returns>The builder for chaining.</returns>
        public ResponseBuilder WithInsights(params string[] insights)
        {
            _insights.AddRange(insights);
            return this;
        }

        /// <summary>
        /// Adds Insight objects to the response.
        /// </summary>
        /// <param name="insights">The insight objects to add.</param>
        /// <returns>The builder for chaining.</returns>
        public ResponseBuilder WithInsightObjects(params Insight[] insights)
        {
            _insights.AddRange(insights.Select(i => i.Text));
            return this;
        }

        /// <summary>
        /// Adds actions to the response.
        /// </summary>
        /// <param name="actions">The actions to add.</param>
        /// <returns>The builder for chaining.</returns>
        public ResponseBuilder WithActions(params AIAction[] actions)
        {
            _actions.AddRange(actions);
            return this;
        }

        /// <summary>
        /// Adds a single action using fluent syntax.
        /// </summary>
        /// <param name="tool">The tool name.</param>
        /// <param name="description">The action description.</param>
        /// <param name="parameters">Optional parameters.</param>
        /// <param name="rationale">Optional rationale.</param>
        /// <returns>The builder for chaining.</returns>
        public ResponseBuilder WithAction(
            string tool, 
            string description, 
            object? parameters = null,
            string? rationale = null)
        {
            Dictionary<string, object>? paramDict = null;
            if (parameters != null)
            {
                if (parameters is Dictionary<string, object> dict)
                {
                    paramDict = dict;
                }
                else
                {
                    // Convert anonymous object to dictionary
                    paramDict = new Dictionary<string, object>();
                    foreach (var prop in parameters.GetType().GetProperties())
                    {
                        paramDict[prop.Name] = prop.GetValue(parameters) ?? "";
                    }
                }
            }
            
            _actions.Add(new AIAction
            {
                Tool = tool,
                Description = description,
                Parameters = paramDict,
                Rationale = rationale
            });
            return this;
        }

        /// <summary>
        /// Sets the execution time.
        /// </summary>
        /// <param name="milliseconds">The execution time in milliseconds.</param>
        /// <returns>The builder for chaining.</returns>
        public ResponseBuilder WithExecutionTime(int milliseconds)
        {
            _executionTime = $"{milliseconds}ms";
            return this;
        }

        /// <summary>
        /// Marks the response as truncated.
        /// </summary>
        /// <param name="resourceUri">Optional resource URI for full results.</param>
        /// <returns>The builder for chaining.</returns>
        public ResponseBuilder WithTruncation(string? resourceUri = null)
        {
            _truncated = true;
            _resourceUri = resourceUri;
            
            // Add truncation insight if not already present
            if (!_insights.Any(i => i.Contains("truncated", StringComparison.OrdinalIgnoreCase)))
            {
                _insights.Add("Results were truncated to fit within token limits");
            }
            
            return this;
        }

        /// <summary>
        /// Adds additional metadata.
        /// </summary>
        /// <param name="key">The metadata key.</param>
        /// <param name="value">The metadata value.</param>
        /// <returns>The builder for chaining.</returns>
        public ResponseBuilder WithMetadata(string key, object value)
        {
            _additionalMeta[key] = value;
            return this;
        }

        /// <summary>
        /// Builds the AI-optimized response.
        /// </summary>
        /// <returns>The built response.</returns>
#pragma warning disable CS0618 // Type or member is obsolete - testing backward compatibility
        public AIOptimizedResponse Build()
        {
            var response = new AIOptimizedResponse
            {
                Format = _format,
                Data = new AIResponseData
                {
#pragma warning restore CS0618 // Type or member is obsolete
                    Summary = _summary,
                    Results = _results,
                    Count = _count
                },
                Insights = new List<string>(_insights),
                Actions = new List<AIAction>(_actions),
                Meta = new AIResponseMeta
                {
                    ExecutionTime = _executionTime,
                    Truncated = _truncated,
                    ResourceUri = _resourceUri
                }
            };

            // Add additional metadata
            foreach (var kvp in _additionalMeta)
            {
                // This assumes AIResponseMeta can be extended or has a way to add custom properties
                // For now, we'll just set the standard properties
            }

            return response;
        }

        /// <summary>
        /// Creates a successful response with minimal configuration.
        /// </summary>
        /// <param name="results">The results to include.</param>
        /// <returns>A built response.</returns>
#pragma warning disable CS0618 // Type or member is obsolete - testing backward compatibility
        public static AIOptimizedResponse Success(object results)
#pragma warning restore CS0618 // Type or member is obsolete
        {
            return new ResponseBuilder()
                .WithSummary("Operation completed successfully")
                .WithResults(results)
                .WithInsights("All results retrieved successfully")
                .Build();
        }

        /// <summary>
        /// Creates an error response.
        /// </summary>
        /// <param name="error">The error message.</param>
        /// <param name="suggestedAction">Optional suggested action.</param>
        /// <returns>A built error response.</returns>
#pragma warning disable CS0618 // Type or member is obsolete - testing backward compatibility
        public static AIOptimizedResponse Error(string error, string? suggestedAction = null)
#pragma warning restore CS0618 // Type or member is obsolete
        {
            var builder = new ResponseBuilder()
                .WithSummary("Operation failed")
                .WithResults(new { error })
                .WithCount(0)
                .WithInsights($"Error occurred: {error}");

            if (suggestedAction != null)
            {
                builder.WithAction("retry", suggestedAction, null, "The operation may succeed on retry");
            }

            return builder.Build();
        }

        /// <summary>
        /// Creates a response for empty results.
        /// </summary>
        /// <param name="entityType">The type of entity that was searched for.</param>
        /// <returns>A built empty response.</returns>
#pragma warning disable CS0618 // Type or member is obsolete - testing backward compatibility
        public static AIOptimizedResponse Empty(string entityType)
#pragma warning restore CS0618 // Type or member is obsolete
        {
            return new ResponseBuilder()
                .WithSummary($"No {entityType} found")
                .WithResults(Array.Empty<object>())
                .WithCount(0)
                .WithInsights(
                    $"No {entityType} matched the search criteria",
                    "Consider broadening your search parameters",
                    "Check if the search scope is correct")
                .WithAction("modify_search", $"Try different search criteria for {entityType}")
                .Build();
        }

        /// <summary>
        /// Creates a response for large result sets.
        /// </summary>
        /// <param name="results">The truncated results.</param>
        /// <param name="totalCount">The total count before truncation.</param>
        /// <param name="resourceUri">URI to access full results.</param>
        /// <returns>A built truncated response.</returns>
#pragma warning disable CS0618 // Type or member is obsolete - testing backward compatibility
        public static AIOptimizedResponse LargeResultSet(object results, int totalCount, string resourceUri)
#pragma warning restore CS0618 // Type or member is obsolete
        {
            return new ResponseBuilder()
                .WithSummary($"Found {totalCount} results (showing subset)")
                .WithResults(results)
                .WithCount(totalCount)
                .WithTruncation(resourceUri)
                .WithInsights(
                    $"Total of {totalCount} results found",
                    "Results truncated to stay within token limits",
                    $"Full results available at: {resourceUri}")
                .WithAction("get_more_results", "Retrieve additional results", new { offset = 100 })
                .Build();
        }
    }

    /// <summary>
    /// Builder for creating Insight objects.
    /// </summary>
    public class InsightBuilder
    {
        private string _content = "";
        private InsightImportance _importance = InsightImportance.Medium;
        private readonly Dictionary<string, object> _metadata = new();

        /// <summary>
        /// Sets the insight content.
        /// </summary>
        /// <param name="content">The insight text.</param>
        /// <returns>The builder for chaining.</returns>
        public InsightBuilder WithContent(string content)
        {
            _content = content;
            return this;
        }

        /// <summary>
        /// Sets the insight importance.
        /// </summary>
        /// <param name="importance">The importance level.</param>
        /// <returns>The builder for chaining.</returns>
        public InsightBuilder WithImportance(InsightImportance importance)
        {
            _importance = importance;
            return this;
        }

        /// <summary>
        /// Adds metadata to the insight.
        /// </summary>
        /// <param name="key">The metadata key.</param>
        /// <param name="value">The metadata value.</param>
        /// <returns>The builder for chaining.</returns>
        public InsightBuilder WithMetadata(string key, object value)
        {
            _metadata[key] = value;
            return this;
        }

        /// <summary>
        /// Makes the insight contextual by adding context metadata.
        /// </summary>
        /// <param name="context">The context information.</param>
        /// <returns>The builder for chaining.</returns>
        public InsightBuilder WithContext(string context)
        {
            _metadata["context"] = context;
            return this;
        }

        /// <summary>
        /// Builds the insight.
        /// </summary>
        /// <returns>The built insight.</returns>
        public Insight Build()
        {
            return new Insight
            {
                Text = _content,
                Importance = _importance,
                Metadata = new Dictionary<string, object>(_metadata)
            };
        }

        /// <summary>
        /// Creates a high-importance insight.
        /// </summary>
        /// <param name="content">The insight content.</param>
        /// <returns>A built insight.</returns>
        public static Insight Critical(string content)
        {
            return new InsightBuilder()
                .WithContent(content)
                .WithImportance(InsightImportance.Critical)
                .Build();
        }

        /// <summary>
        /// Creates a pattern-based insight.
        /// </summary>
        /// <param name="pattern">The pattern description.</param>
        /// <param name="occurrences">Number of occurrences.</param>
        /// <returns>A built insight.</returns>
        public static Insight Pattern(string pattern, int occurrences)
        {
            return new InsightBuilder()
                .WithContent($"Pattern detected: {pattern} ({occurrences} occurrences)")
                .WithImportance(occurrences > 10 ? InsightImportance.High : InsightImportance.Medium)
                .WithMetadata("type", "pattern")
                .WithMetadata("occurrences", occurrences)
                .Build();
        }
    }
}