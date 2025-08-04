using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace COA.Mcp.Framework.Migration.Analyzers;

/// <summary>
/// Analyzes tool registration patterns to identify manual registration that can be migrated
/// </summary>
public class ToolRegistrationPatternAnalyzer : IPatternAnalyzer
{
    public Task<List<PatternMatch>> AnalyzeAsync(SyntaxNode root, string filePath)
    {
        var patterns = new List<PatternMatch>();

        // Look for manual tool registration patterns
        var registrationCalls = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => IsToolRegistrationCall(inv));

        foreach (var call in registrationCalls)
        {
            var location = call.GetLocation();
            var lineSpan = location.GetLineSpan();

            patterns.Add(new PatternMatch
            {
                Type = "ManualToolRegistration",
                FilePath = filePath,
                Line = lineSpan.StartLinePosition.Line + 1,
                Column = lineSpan.StartLinePosition.Character + 1,
                Code = call.ToString(),
                Description = "Manual tool registration can be replaced with attribute-based registration",
                CanBeAutomated = true
            });
        }

        // Look for AddMcpServer or similar service registration
        var serviceRegistrations = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => IsServiceRegistrationCall(inv));

        foreach (var registration in serviceRegistrations)
        {
            var location = registration.GetLocation();
            var lineSpan = location.GetLineSpan();

            patterns.Add(new PatternMatch
            {
                Type = "LegacyServiceRegistration",
                FilePath = filePath,
                Line = lineSpan.StartLinePosition.Line + 1,
                Column = lineSpan.StartLinePosition.Character + 1,
                Code = registration.ToString(),
                Description = "Legacy service registration pattern can be replaced with AddMcpFramework",
                CanBeAutomated = true
            });
        }

        // Look for tool classes without proper attributes
        var toolClasses = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(c => IsLikelyToolClass(c) && !HasMcpAttributes(c));

        foreach (var toolClass in toolClasses)
        {
            var location = toolClass.Identifier.GetLocation();
            var lineSpan = location.GetLineSpan();

            patterns.Add(new PatternMatch
            {
                Type = "MissingToolAttributes",
                FilePath = filePath,
                Line = lineSpan.StartLinePosition.Line + 1,
                Column = lineSpan.StartLinePosition.Character + 1,
                Code = toolClass.Identifier.ToString(),
                Description = $"Tool class '{toolClass.Identifier}' is missing [McpServerToolType] attribute",
                CanBeAutomated = true
            });
        }

        return Task.FromResult(patterns);
    }

    private bool IsToolRegistrationCall(InvocationExpressionSyntax invocation)
    {
        var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
        if (memberAccess == null) return false;

        var methodName = memberAccess.Name.ToString();
        return methodName.Contains("RegisterTool") || 
               methodName.Contains("AddTool") ||
               methodName.Contains("Tool.Register");
    }

    private bool IsServiceRegistrationCall(InvocationExpressionSyntax invocation)
    {
        var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
        if (memberAccess == null) return false;

        var methodName = memberAccess.Name.ToString();
        return methodName == "AddMcpServer" || 
               methodName == "RegisterMcpServer" ||
               (methodName == "AddSingleton" && invocation.ToString().Contains("McpServer"));
    }

    private bool IsLikelyToolClass(ClassDeclarationSyntax classDeclaration)
    {
        var className = classDeclaration.Identifier.ToString();
        
        // Check if class name contains "Tool"
        if (className.Contains("Tool", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check if class has Execute method
        var hasExecuteMethod = classDeclaration.Members
            .OfType<MethodDeclarationSyntax>()
            .Any(m => m.Identifier.ToString().Contains("Execute"));

        // Check if implements ITool or similar interface
        var implementsToolInterface = classDeclaration.BaseList?.Types
            .Any(t => t.ToString().Contains("ITool") || t.ToString().Contains("IServerTool")) ?? false;

        return hasExecuteMethod || implementsToolInterface;
    }

    private bool HasMcpAttributes(ClassDeclarationSyntax classDeclaration)
    {
        return classDeclaration.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(a => a.Name.ToString().Contains("McpServerToolType") || 
                     a.Name.ToString().Contains("McpServerTool"));
    }
}