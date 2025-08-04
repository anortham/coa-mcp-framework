using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.Extensions.Logging;
using COA.Mcp.Framework.Migration.Analyzers;

namespace COA.Mcp.Framework.Migration.Migrators;

/// <summary>
/// Migrates manual token management to framework-based token management
/// </summary>
public class TokenManagementMigrator : IMigrator
{
    private readonly ILogger<TokenManagementMigrator> _logger;

    public string PatternType => "ManualTokenCounting";

    public TokenManagementMigrator(ILogger<TokenManagementMigrator> logger)
    {
        _logger = logger;
    }

    public bool CanMigrate(PatternMatch pattern)
    {
        return pattern.Type == PatternType || 
               pattern.Type == "SimplisticTokenEstimation" ||
               pattern.Type == "ManualResultTruncation" ||
               pattern.Type == "HardCodedTokenLimit";
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
                case "ManualTokenCounting":
                    await MigrateManualTokenCounting(editor, pattern);
                    break;
                    
                case "SimplisticTokenEstimation":
                    await MigrateTokenEstimation(editor, pattern);
                    break;
                    
                case "ManualResultTruncation":
                    await MigrateResultTruncation(editor, pattern);
                    break;
                    
                case "HardCodedTokenLimit":
                    // This requires manual review
                    return new MigrationResult
                    {
                        Success = true,
                        Warnings = { "Hard-coded token limit detected. Consider using framework configuration." }
                    };
            }

            // Add required usings
            await AddRequiredUsings(editor);

            return new MigrationResult
            {
                Success = true,
                ModifiedDocument = editor.GetChangedDocument()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to migrate token management at {FilePath}:{Line}", 
                pattern.FilePath, pattern.Line);
            
            return new MigrationResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task MigrateManualTokenCounting(DocumentEditor editor, PatternMatch pattern)
    {
        var root = await editor.OriginalDocument.GetSyntaxRootAsync();
        if (root == null) return;

        // Find the invocation at the pattern location
        var position = root.GetLocation().SourceTree?.GetText()
            .Lines[pattern.Line - 1].Start ?? 0;
        
        var node = root.FindNode(new Microsoft.CodeAnalysis.Text.TextSpan(position, 1));
        var invocation = node.AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault();

        if (invocation == null) return;

        // Determine what's being counted
        var argument = invocation.ArgumentList?.Arguments.FirstOrDefault()?.Expression;
        if (argument == null) return;

        // Replace with TokenEstimator call
        ExpressionSyntax newExpression;
        
        if (argument is IdentifierNameSyntax identifier && 
            identifier.Identifier.Text.Contains("string", StringComparison.OrdinalIgnoreCase))
        {
            newExpression = SyntaxFactory.ParseExpression($"TokenEstimator.EstimateString({argument})");
        }
        else
        {
            newExpression = SyntaxFactory.ParseExpression($"TokenEstimator.EstimateObject({argument})");
        }

        editor.ReplaceNode(invocation, newExpression);
    }

    private async Task MigrateTokenEstimation(DocumentEditor editor, PatternMatch pattern)
    {
        var root = await editor.OriginalDocument.GetSyntaxRootAsync();
        if (root == null) return;

        // Find the division expression at the pattern location
        var position = root.GetLocation().SourceTree?.GetText()
            .Lines[pattern.Line - 1].Start ?? 0;
        
        var node = root.FindNode(new Microsoft.CodeAnalysis.Text.TextSpan(position, 1));
        var divisionExpr = node.AncestorsAndSelf()
            .OfType<BinaryExpressionSyntax>()
            .FirstOrDefault(e => e.Kind() == SyntaxKind.DivideExpression);

        if (divisionExpr == null) return;

        // Extract the string being estimated
        var leftExpr = divisionExpr.Left;
        string variableName = "text";
        
        if (leftExpr is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name.Identifier.Text == "Length")
        {
            variableName = memberAccess.Expression.ToString();
        }

        // Replace with TokenEstimator
        var newExpression = SyntaxFactory.ParseExpression($"TokenEstimator.EstimateString({variableName})");
        editor.ReplaceNode(divisionExpr, newExpression);
    }

    private async Task MigrateResultTruncation(DocumentEditor editor, PatternMatch pattern)
    {
        var root = await editor.OriginalDocument.GetSyntaxRootAsync();
        if (root == null) return;

        // Find the if statement at the pattern location
        var position = root.GetLocation().SourceTree?.GetText()
            .Lines[pattern.Line - 1].Start ?? 0;
        
        var node = root.FindNode(new Microsoft.CodeAnalysis.Text.TextSpan(position, 1));
        var ifStatement = node.AncestorsAndSelf()
            .OfType<IfStatementSyntax>()
            .FirstOrDefault();

        if (ifStatement == null) return;

        // Find the containing method
        var method = ifStatement.Ancestors()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault();
        
        if (method == null) return;

        // Add a comment for manual review
        var comment = SyntaxFactory.Comment(
            "// TODO: Replace manual truncation with framework's progressive reduction");
        
        var trivia = SyntaxFactory.TriviaList(comment, SyntaxFactory.ElasticCarriageReturnLineFeed);
        var newIfStatement = ifStatement.WithLeadingTrivia(
            ifStatement.GetLeadingTrivia().AddRange(trivia));
        
        editor.ReplaceNode(ifStatement, newIfStatement);

        // Add example code in comment
        var exampleCode = @"
// Example using framework's progressive reduction:
// var reducedItems = TokenEstimator.ApplyProgressiveReduction(
//     items,
//     item => TokenEstimator.EstimateObject(item),
//     tokenLimit: 10000,
//     reductionSteps: new[] { 100, 75, 50, 30, 20, 10, 5 });";
        
        var exampleComment = SyntaxFactory.ParseTrailingTrivia(exampleCode);
        editor.InsertAfter(newIfStatement, 
            SyntaxFactory.EmptyStatement().WithTrailingTrivia(exampleComment));
    }

    private async Task AddRequiredUsings(DocumentEditor editor)
    {
        var root = await editor.OriginalDocument.GetSyntaxRootAsync() as CompilationUnitSyntax;
        if (root == null) return;

        var requiredUsings = new[]
        {
            "COA.Mcp.Framework.TokenOptimization",
            "COA.Mcp.Framework.TokenOptimization.Reduction"
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