using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace COA.Mcp.Framework.Migration.Analyzers;

/// <summary>
/// Analyzes base class usage patterns and inheritance
/// </summary>
public class BaseClassPatternAnalyzer : IPatternAnalyzer
{
    public Task<List<PatternMatch>> AnalyzeAsync(SyntaxNode root, string filePath)
    {
        var patterns = new List<PatternMatch>();

        // Look for tool classes not inheriting from McpToolBase
        var toolClasses = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(c => IsLikelyToolClass(c) && !InheritsFromMcpToolBase(c));

        foreach (var toolClass in toolClasses)
        {
            var location = toolClass.Identifier.GetLocation();
            var lineSpan = location.GetLineSpan();

            patterns.Add(new PatternMatch
            {
                Type = "NoBaseClass",
                FilePath = filePath,
                Line = lineSpan.StartLinePosition.Line + 1,
                Column = lineSpan.StartLinePosition.Character + 1,
                Code = toolClass.Identifier.ToString(),
                Description = $"Tool class '{toolClass.Identifier}' should inherit from McpToolBase",
                CanBeAutomated = true
            });
        }

        // Look for manual validation code that could use base class helpers
        var validationCode = root.DescendantNodes()
            .OfType<IfStatementSyntax>()
            .Where(ifStmt => IsLikelyValidationCode(ifStmt) && IsInToolClass(ifStmt));

        foreach (var validation in validationCode)
        {
            var location = validation.GetLocation();
            var lineSpan = location.GetLineSpan();

            patterns.Add(new PatternMatch
            {
                Type = "ManualValidation",
                FilePath = filePath,
                Line = lineSpan.StartLinePosition.Line + 1,
                Column = lineSpan.StartLinePosition.Character + 1,
                Code = validation.Condition.ToString(),
                Description = "Manual validation can be replaced with base class validation helpers",
                CanBeAutomated = true
            });
        }

        // Look for duplicate helper methods across tool classes
        var helperMethods = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(m => IsLikelyHelperMethod(m) && IsInToolClass(m));

        foreach (var helper in helperMethods)
        {
            var location = helper.Identifier.GetLocation();
            var lineSpan = location.GetLineSpan();

            patterns.Add(new PatternMatch
            {
                Type = "DuplicateHelperMethod",
                FilePath = filePath,
                Line = lineSpan.StartLinePosition.Line + 1,
                Column = lineSpan.StartLinePosition.Character + 1,
                Code = helper.Identifier.ToString(),
                Description = $"Helper method '{helper.Identifier}' might be available in McpToolBase",
                CanBeAutomated = false
            });
        }

        return Task.FromResult(patterns);
    }

    private bool IsLikelyToolClass(ClassDeclarationSyntax classDeclaration)
    {
        var className = classDeclaration.Identifier.ToString();
        
        // Check class name
        if (className.Contains("Tool", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check for tool attributes
        var hasToolAttribute = classDeclaration.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(a => a.Name.ToString().Contains("Tool"));

        // Check for Execute methods
        var hasExecuteMethod = classDeclaration.Members
            .OfType<MethodDeclarationSyntax>()
            .Any(m => m.Identifier.ToString().Contains("Execute"));

        return hasToolAttribute || hasExecuteMethod;
    }

    private bool InheritsFromMcpToolBase(ClassDeclarationSyntax classDeclaration)
    {
        if (classDeclaration.BaseList == null) return false;

        return classDeclaration.BaseList.Types
            .Any(t => t.ToString().Contains("McpToolBase") || 
                     t.ToString().Contains("ToolBase"));
    }

    private bool IsLikelyValidationCode(IfStatementSyntax ifStatement)
    {
        var condition = ifStatement.Condition.ToString().ToLower();
        var thenBranch = ifStatement.Statement.ToString().ToLower();

        // Check for null/empty checks
        var hasNullCheck = condition.Contains("== null") || 
                          condition.Contains("is null") ||
                          condition.Contains("string.isnullorempty") ||
                          condition.Contains("string.isnullorwhitespace");

        // Check for exception throwing
        var throwsException = thenBranch.Contains("throw") && 
                            (thenBranch.Contains("argumentnullexception") ||
                             thenBranch.Contains("argumentexception") ||
                             thenBranch.Contains("invalidoperationexception"));

        return hasNullCheck && throwsException;
    }

    private bool IsInToolClass(SyntaxNode node)
    {
        var containingClass = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        return containingClass != null && IsLikelyToolClass(containingClass);
    }

    private bool IsLikelyHelperMethod(MethodDeclarationSyntax method)
    {
        var methodName = method.Identifier.ToString();
        
        // Common helper method patterns
        return methodName.Contains("Validate", StringComparison.OrdinalIgnoreCase) ||
               methodName.Contains("EstimateToken", StringComparison.OrdinalIgnoreCase) ||
               methodName.Contains("BuildResponse", StringComparison.OrdinalIgnoreCase) ||
               methodName.Contains("GenerateInsight", StringComparison.OrdinalIgnoreCase) ||
               methodName.Contains("CreateAction", StringComparison.OrdinalIgnoreCase);
    }
}