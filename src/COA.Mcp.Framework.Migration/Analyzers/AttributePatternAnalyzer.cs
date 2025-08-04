using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace COA.Mcp.Framework.Migration.Analyzers;

/// <summary>
/// Analyzes attribute usage patterns to identify migration opportunities
/// </summary>
public class AttributePatternAnalyzer : IPatternAnalyzer
{
    public Task<List<PatternMatch>> AnalyzeAsync(SyntaxNode root, string filePath)
    {
        var patterns = new List<PatternMatch>();

        // Look for custom attribute definitions that duplicate framework functionality
        var customAttributes = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(c => c.BaseList?.Types.Any(t => t.ToString().Contains("Attribute")) ?? false)
            .Where(c => IsLikelyDuplicateAttribute(c));

        foreach (var attr in customAttributes)
        {
            var location = attr.Identifier.GetLocation();
            var lineSpan = location.GetLineSpan();

            patterns.Add(new PatternMatch
            {
                Type = "DuplicateAttributeDefinition",
                FilePath = filePath,
                Line = lineSpan.StartLinePosition.Line + 1,
                Column = lineSpan.StartLinePosition.Character + 1,
                Code = attr.Identifier.ToString(),
                Description = $"Custom attribute '{attr.Identifier}' may duplicate framework functionality",
                CanBeAutomated = false
            });
        }

        // Look for tool methods without proper Description attribute
        var toolMethods = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(m => HasToolAttribute(m) && !HasDescriptionAttribute(m));

        foreach (var method in toolMethods)
        {
            var location = method.Identifier.GetLocation();
            var lineSpan = location.GetLineSpan();

            patterns.Add(new PatternMatch
            {
                Type = "MissingDescriptionAttribute",
                FilePath = filePath,
                Line = lineSpan.StartLinePosition.Line + 1,
                Column = lineSpan.StartLinePosition.Character + 1,
                Code = method.Identifier.ToString(),
                Description = $"Tool method '{method.Identifier}' is missing [Description] attribute",
                CanBeAutomated = true
            });
        }

        // Look for parameter classes without description attributes
        var parameterClasses = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(c => IsLikelyParameterClass(c));

        foreach (var paramClass in parameterClasses)
        {
            var propertiesWithoutDescription = paramClass.Members
                .OfType<PropertyDeclarationSyntax>()
                .Where(p => !HasDescriptionAttribute(p));

            foreach (var prop in propertiesWithoutDescription)
            {
                var location = prop.Identifier.GetLocation();
                var lineSpan = location.GetLineSpan();

                patterns.Add(new PatternMatch
                {
                    Type = "MissingParameterDescription",
                    FilePath = filePath,
                    Line = lineSpan.StartLinePosition.Line + 1,
                    Column = lineSpan.StartLinePosition.Character + 1,
                    Code = $"{paramClass.Identifier}.{prop.Identifier}",
                    Description = $"Parameter property '{prop.Identifier}' is missing [Description] attribute",
                    CanBeAutomated = true
                });
            }
        }

        return Task.FromResult(patterns);
    }

    private bool IsLikelyDuplicateAttribute(ClassDeclarationSyntax classDeclaration)
    {
        var name = classDeclaration.Identifier.ToString();
        
        // Check for common attribute names that framework provides
        return name.Contains("Description", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("ToolAttribute", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("ValidationAttribute", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("ParameterAttribute", StringComparison.OrdinalIgnoreCase);
    }

    private bool HasToolAttribute(MethodDeclarationSyntax method)
    {
        return method.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(a => a.Name.ToString().Contains("Tool") || 
                     a.Name.ToString().Contains("ServerTool"));
    }

    private bool HasDescriptionAttribute(MemberDeclarationSyntax member)
    {
        return member.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(a => a.Name.ToString().Contains("Description"));
    }

    private bool IsLikelyParameterClass(ClassDeclarationSyntax classDeclaration)
    {
        var className = classDeclaration.Identifier.ToString();
        
        // Check if class name suggests it's a parameter class
        if (className.EndsWith("Params", StringComparison.OrdinalIgnoreCase) ||
            className.EndsWith("Parameters", StringComparison.OrdinalIgnoreCase) ||
            className.EndsWith("Request", StringComparison.OrdinalIgnoreCase) ||
            className.EndsWith("Input", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Check if class has only properties (typical for parameter classes)
        var hasOnlyProperties = classDeclaration.Members
            .All(m => m is PropertyDeclarationSyntax || 
                     m is ConstructorDeclarationSyntax);

        return hasOnlyProperties && classDeclaration.Members.OfType<PropertyDeclarationSyntax>().Any();
    }
}