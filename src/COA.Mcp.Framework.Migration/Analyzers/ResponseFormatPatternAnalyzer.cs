using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace COA.Mcp.Framework.Migration.Analyzers;

/// <summary>
/// Analyzes response format patterns to identify legacy formats that should be migrated
/// </summary>
public class ResponseFormatPatternAnalyzer : IPatternAnalyzer
{
    public Task<List<PatternMatch>> AnalyzeAsync(SyntaxNode root, string filePath)
    {
        var patterns = new List<PatternMatch>();

        // Look for direct object returns without AI optimization
        var returnStatements = root.DescendantNodes()
            .OfType<ReturnStatementSyntax>()
            .Where(r => IsInToolMethod(r));

        foreach (var returnStmt in returnStatements)
        {
            if (IsLegacyResponseFormat(returnStmt))
            {
                var location = returnStmt.GetLocation();
                var lineSpan = location.GetLineSpan();

                patterns.Add(new PatternMatch
                {
                    Type = "LegacyResponseFormat",
                    FilePath = filePath,
                    Line = lineSpan.StartLinePosition.Line + 1,
                    Column = lineSpan.StartLinePosition.Character + 1,
                    Code = returnStmt.ToString(),
                    Description = "Response should use AI-optimized format with insights and actions",
                    CanBeAutomated = true
                });
            }
        }

        // Look for manual JSON serialization
        var jsonSerializationCalls = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => IsJsonSerializationCall(inv) && IsInToolMethod(inv));

        foreach (var call in jsonSerializationCalls)
        {
            var location = call.GetLocation();
            var lineSpan = location.GetLineSpan();

            patterns.Add(new PatternMatch
            {
                Type = "ManualJsonSerialization",
                FilePath = filePath,
                Line = lineSpan.StartLinePosition.Line + 1,
                Column = lineSpan.StartLinePosition.Character + 1,
                Code = call.ToString(),
                Description = "Manual JSON serialization can be handled by framework response builders",
                CanBeAutomated = false
            });
        }

        // Look for responses without metadata
        var anonymousObjectCreations = root.DescendantNodes()
            .OfType<AnonymousObjectCreationExpressionSyntax>()
            .Where(obj => IsInToolMethod(obj) && !HasResponseMetadata(obj));

        foreach (var obj in anonymousObjectCreations)
        {
            var location = obj.GetLocation();
            var lineSpan = location.GetLineSpan();

            patterns.Add(new PatternMatch
            {
                Type = "MissingResponseMetadata",
                FilePath = filePath,
                Line = lineSpan.StartLinePosition.Line + 1,
                Column = lineSpan.StartLinePosition.Character + 1,
                Code = obj.ToString().Substring(0, Math.Min(obj.ToString().Length, 100)) + "...",
                Description = "Response is missing metadata (execution time, token count, etc.)",
                CanBeAutomated = true
            });
        }

        return Task.FromResult(patterns);
    }

    private bool IsInToolMethod(SyntaxNode node)
    {
        var method = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (method == null) return false;

        // Check if method name suggests it's a tool execution method
        var methodName = method.Identifier.ToString();
        return methodName.Contains("Execute") || 
               methodName.Contains("Run") ||
               methodName.Contains("Process") ||
               method.AttributeLists.SelectMany(al => al.Attributes)
                   .Any(a => a.Name.ToString().Contains("McpServerTool"));
    }

    private bool IsLegacyResponseFormat(ReturnStatementSyntax returnStatement)
    {
        if (returnStatement.Expression == null) return false;

        // Check if returning a simple object without AI optimization
        if (returnStatement.Expression is ObjectCreationExpressionSyntax objCreation)
        {
            var typeName = objCreation.Type.ToString();
            // If it's not using framework response types
            return !typeName.Contains("AIOptimizedResponse") && 
                   !typeName.Contains("TokenAwareResponse");
        }

        // Check for anonymous objects without insights/actions
        if (returnStatement.Expression is AnonymousObjectCreationExpressionSyntax anonObj)
        {
            var hasInsights = anonObj.Initializers
                .Any(i => i.ToString().Contains("Insights", StringComparison.OrdinalIgnoreCase));
            var hasActions = anonObj.Initializers
                .Any(i => i.ToString().Contains("Actions", StringComparison.OrdinalIgnoreCase));
            
            return !hasInsights || !hasActions;
        }

        return true;
    }

    private bool IsJsonSerializationCall(InvocationExpressionSyntax invocation)
    {
        var text = invocation.ToString();
        return text.Contains("JsonSerializer.Serialize") ||
               text.Contains("JsonConvert.SerializeObject") ||
               text.Contains("ToJson");
    }

    private bool HasResponseMetadata(AnonymousObjectCreationExpressionSyntax obj)
    {
        var initializers = obj.Initializers.Select(i => i.ToString().ToLower());
        return initializers.Any(i => i.Contains("meta") || 
                                   i.Contains("executiontime") || 
                                   i.Contains("tokens"));
    }
}