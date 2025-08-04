using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace COA.Mcp.Framework.Migration.Analyzers;

/// <summary>
/// Analyzes token management patterns to identify manual implementations
/// </summary>
public class TokenManagementPatternAnalyzer : IPatternAnalyzer
{
    public Task<List<PatternMatch>> AnalyzeAsync(SyntaxNode root, string filePath)
    {
        var patterns = new List<PatternMatch>();

        // Look for manual token counting
        var tokenCountingCalls = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => IsManualTokenCounting(inv));

        foreach (var call in tokenCountingCalls)
        {
            var location = call.GetLocation();
            var lineSpan = location.GetLineSpan();

            patterns.Add(new PatternMatch
            {
                Type = "ManualTokenCounting",
                FilePath = filePath,
                Line = lineSpan.StartLinePosition.Line + 1,
                Column = lineSpan.StartLinePosition.Character + 1,
                Code = call.ToString(),
                Description = "Manual token counting should use framework's TokenEstimator",
                CanBeAutomated = true
            });
        }

        // Look for string length division patterns (common token estimation)
        var divisionExpressions = root.DescendantNodes()
            .OfType<BinaryExpressionSyntax>()
            .Where(expr => expr.Kind() == SyntaxKind.DivideExpression && 
                          IsLikelyTokenEstimation(expr));

        foreach (var expr in divisionExpressions)
        {
            var location = expr.GetLocation();
            var lineSpan = location.GetLineSpan();

            patterns.Add(new PatternMatch
            {
                Type = "SimplisticTokenEstimation",
                FilePath = filePath,
                Line = lineSpan.StartLinePosition.Line + 1,
                Column = lineSpan.StartLinePosition.Character + 1,
                Code = expr.ToString(),
                Description = "Simplistic token estimation (length/4) should use framework's advanced estimation",
                CanBeAutomated = true
            });
        }

        // Look for manual result truncation
        var truncationPatterns = root.DescendantNodes()
            .OfType<IfStatementSyntax>()
            .Where(ifStmt => IsLikelyTruncationLogic(ifStmt));

        foreach (var ifStmt in truncationPatterns)
        {
            var location = ifStmt.GetLocation();
            var lineSpan = location.GetLineSpan();

            patterns.Add(new PatternMatch
            {
                Type = "ManualResultTruncation",
                FilePath = filePath,
                Line = lineSpan.StartLinePosition.Line + 1,
                Column = lineSpan.StartLinePosition.Character + 1,
                Code = ifStmt.Condition.ToString(),
                Description = "Manual result truncation should use framework's progressive reduction",
                CanBeAutomated = true
            });
        }

        // Look for hard-coded token limits
        var tokenLimitConstants = root.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .Where(field => IsLikelyTokenLimitConstant(field));

        foreach (var field in tokenLimitConstants)
        {
            var location = field.GetLocation();
            var lineSpan = location.GetLineSpan();

            patterns.Add(new PatternMatch
            {
                Type = "HardCodedTokenLimit",
                FilePath = filePath,
                Line = lineSpan.StartLinePosition.Line + 1,
                Column = lineSpan.StartLinePosition.Character + 1,
                Code = field.ToString(),
                Description = "Hard-coded token limits should use framework's configurable limits",
                CanBeAutomated = false
            });
        }

        return Task.FromResult(patterns);
    }

    private bool IsManualTokenCounting(InvocationExpressionSyntax invocation)
    {
        var text = invocation.ToString().ToLower();
        return text.Contains("counttoken") ||
               text.Contains("tokencount") ||
               text.Contains("estimatetoken") ||
               text.Contains("calculatetoken") ||
               (text.Contains("encoding") && text.Contains("encode"));
    }

    private bool IsLikelyTokenEstimation(BinaryExpressionSyntax expr)
    {
        // Look for patterns like: text.Length / 4
        var left = expr.Left.ToString();
        var right = expr.Right.ToString();

        return left.Contains(".Length") && 
               (right == "4" || right == "3" || right == "2.5");
    }

    private bool IsLikelyTruncationLogic(IfStatementSyntax ifStmt)
    {
        var condition = ifStmt.Condition.ToString().ToLower();
        var thenBranch = ifStmt.Statement.ToString().ToLower();

        // Look for conditions checking length/count/tokens
        var hasLengthCheck = condition.Contains("length") || 
                            condition.Contains("count") || 
                            condition.Contains("token");

        // Look for truncation operations in then branch
        var hasTruncation = thenBranch.Contains("take(") ||
                           thenBranch.Contains("substring") ||
                           thenBranch.Contains("truncate") ||
                           thenBranch.Contains("limit");

        return hasLengthCheck && hasTruncation;
    }

    private bool IsLikelyTokenLimitConstant(FieldDeclarationSyntax field)
    {
        var declaration = field.Declaration.ToString().ToLower();
        
        // Look for const/static fields with token-related names
        return (field.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword) || 
                                        m.IsKind(SyntaxKind.StaticKeyword))) &&
               (declaration.Contains("token") && 
                (declaration.Contains("limit") || 
                 declaration.Contains("max") || 
                 declaration.Contains("budget")));
    }
}