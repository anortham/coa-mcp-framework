using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.Extensions.Logging;
using COA.Mcp.Framework.Migration.Analyzers;

namespace COA.Mcp.Framework.Migration.Migrators;

/// <summary>
/// Migrates manual tool registration to attribute-based registration
/// </summary>
public class ToolRegistrationMigrator : IMigrator
{
    private readonly ILogger<ToolRegistrationMigrator> _logger;

    public string PatternType => "ManualToolRegistration";

    public ToolRegistrationMigrator(ILogger<ToolRegistrationMigrator> logger)
    {
        _logger = logger;
    }

    public bool CanMigrate(PatternMatch pattern)
    {
        return pattern.Type == PatternType || 
               pattern.Type == "MissingToolAttributes" ||
               pattern.Type == "LegacyServiceRegistration";
    }

    public async Task<MigrationResult> MigrateAsync(PatternMatch pattern, Document document)
    {
        try
        {
            var root = await document.GetSyntaxRootAsync();
            if (root == null)
            {
                return new MigrationResult 
                { 
                    Success = false, 
                    Error = "Could not get syntax root" 
                };
            }

            var editor = await DocumentEditor.CreateAsync(document);

            switch (pattern.Type)
            {
                case "MissingToolAttributes":
                    await MigrateToolClass(editor, pattern);
                    break;
                    
                case "ManualToolRegistration":
                    await RemoveManualRegistration(editor, pattern);
                    break;
                    
                case "LegacyServiceRegistration":
                    await MigrateServiceRegistration(editor, pattern);
                    break;
            }

            return new MigrationResult
            {
                Success = true,
                ModifiedDocument = editor.GetChangedDocument()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to migrate pattern {PatternType} at {FilePath}:{Line}", 
                pattern.Type, pattern.FilePath, pattern.Line);
            
            return new MigrationResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task MigrateToolClass(DocumentEditor editor, PatternMatch pattern)
    {
        var root = await editor.OriginalDocument.GetSyntaxRootAsync();
        if (root == null) return;

        // Find the class at the pattern location
        var position = root.GetLocation().SourceTree?.GetText()
            .Lines[pattern.Line - 1].Start ?? 0;
        
        var node = root.FindNode(new Microsoft.CodeAnalysis.Text.TextSpan(position, 1));
        var classDeclaration = node.AncestorsAndSelf()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault();

        if (classDeclaration == null) return;

        // Add [McpServerToolType] attribute
        var toolTypeAttribute = SyntaxFactory.Attribute(
            SyntaxFactory.IdentifierName("McpServerToolType"));
        
        var attributeList = SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(toolTypeAttribute));

        var newClass = classDeclaration.AddAttributeLists(attributeList);

        // Update base class if needed
        if (classDeclaration.BaseList == null || 
            !classDeclaration.BaseList.Types.Any(t => t.ToString().Contains("McpToolBase")))
        {
            var baseType = SyntaxFactory.SimpleBaseType(
                SyntaxFactory.IdentifierName("McpToolBase"));
            
            if (classDeclaration.BaseList == null)
            {
                newClass = newClass.WithBaseList(
                    SyntaxFactory.BaseList(
                        SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(baseType)));
            }
            else
            {
                newClass = newClass.WithBaseList(
                    classDeclaration.BaseList.AddTypes(baseType));
            }

            // Add required overrides
            var toolNameProperty = CreateToolNameProperty(classDeclaration.Identifier.Text);
            var categoryProperty = CreateCategoryProperty();
            
            newClass = newClass.AddMembers(toolNameProperty, categoryProperty);
        }

        editor.ReplaceNode(classDeclaration, newClass);

        // Add using directives if needed
        await AddRequiredUsings(editor);
    }

    private async Task RemoveManualRegistration(DocumentEditor editor, PatternMatch pattern)
    {
        var root = await editor.OriginalDocument.GetSyntaxRootAsync();
        if (root == null) return;

        // Find the registration call at the pattern location
        var position = root.GetLocation().SourceTree?.GetText()
            .Lines[pattern.Line - 1].Start ?? 0;
        
        var node = root.FindNode(new Microsoft.CodeAnalysis.Text.TextSpan(position, 1));
        var invocation = node.AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault();

        if (invocation == null) return;

        // Remove the entire statement
        var statement = invocation.Ancestors()
            .OfType<StatementSyntax>()
            .FirstOrDefault();
        
        if (statement != null)
        {
            editor.RemoveNode(statement);
        }
    }

    private async Task MigrateServiceRegistration(DocumentEditor editor, PatternMatch pattern)
    {
        var root = await editor.OriginalDocument.GetSyntaxRootAsync();
        if (root == null) return;

        // Find the service registration call
        var position = root.GetLocation().SourceTree?.GetText()
            .Lines[pattern.Line - 1].Start ?? 0;
        
        var node = root.FindNode(new Microsoft.CodeAnalysis.Text.TextSpan(position, 1));
        var invocation = node.AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault();

        if (invocation == null) return;

        // Create the new AddMcpFramework call
        var newCall = SyntaxFactory.ParseExpression(
            @"services.AddMcpFramework(options =>
            {
                options.DiscoverToolsFromAssembly(typeof(Program).Assembly);
                options.UseTokenOptimization(TokenOptimizationLevel.Aggressive);
            })");

        editor.ReplaceNode(invocation, newCall);
    }

    private PropertyDeclarationSyntax CreateToolNameProperty(string className)
    {
        var toolName = className.Replace("Tool", "").ToLowerInvariant();
        
        return SyntaxFactory.PropertyDeclaration(
            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
            SyntaxFactory.Identifier("ToolName"))
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.OverrideKeyword)))
            .WithExpressionBody(
                SyntaxFactory.ArrowExpressionClause(
                    SyntaxFactory.LiteralExpression(
                        SyntaxKind.StringLiteralExpression,
                        SyntaxFactory.Literal(toolName))))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
    }

    private PropertyDeclarationSyntax CreateCategoryProperty()
    {
        return SyntaxFactory.PropertyDeclaration(
            SyntaxFactory.IdentifierName("ToolCategory"),
            SyntaxFactory.Identifier("Category"))
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.OverrideKeyword)))
            .WithExpressionBody(
                SyntaxFactory.ArrowExpressionClause(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("ToolCategory"),
                        SyntaxFactory.IdentifierName("Query"))))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
    }

    private async Task AddRequiredUsings(DocumentEditor editor)
    {
        var root = await editor.OriginalDocument.GetSyntaxRootAsync() as CompilationUnitSyntax;
        if (root == null) return;

        var requiredUsings = new[]
        {
            "COA.Mcp.Framework",
            "COA.Mcp.Framework.Attributes",
            "COA.Mcp.Framework.Base",
            "COA.Mcp.Framework.Models"
        };

        var existingUsings = root.Usings
            .Select(u => u.Name?.ToString())
            .Where(n => n != null)
            .ToHashSet();

        foreach (var usingName in requiredUsings)
        {
            if (!existingUsings.Contains(usingName))
            {
                var usingDirective = SyntaxFactory.UsingDirective(
                    SyntaxFactory.ParseName(usingName));
                
                editor.InsertAfter(root.Usings.LastOrDefault() ?? (SyntaxNode)root, usingDirective);
            }
        }
    }
}