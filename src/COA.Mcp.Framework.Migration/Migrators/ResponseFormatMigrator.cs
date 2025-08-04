using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.Extensions.Logging;
using COA.Mcp.Framework.Migration.Analyzers;

namespace COA.Mcp.Framework.Migration.Migrators;

/// <summary>
/// Migrates legacy response formats to AI-optimized response format
/// </summary>
public class ResponseFormatMigrator : IMigrator
{
    private readonly ILogger<ResponseFormatMigrator> _logger;

    public string PatternType => "LegacyResponseFormat";

    public ResponseFormatMigrator(ILogger<ResponseFormatMigrator> logger)
    {
        _logger = logger;
    }

    public bool CanMigrate(PatternMatch pattern)
    {
        return pattern.Type == PatternType || 
               pattern.Type == "MissingResponseMetadata" ||
               pattern.Type == "ManualJsonSerialization";
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
                case "LegacyResponseFormat":
                    await MigrateLegacyResponse(editor, pattern);
                    break;
                    
                case "MissingResponseMetadata":
                    await AddResponseMetadata(editor, pattern);
                    break;
                    
                case "ManualJsonSerialization":
                    // Manual review required
                    return new MigrationResult
                    {
                        Success = true,
                        Warnings = { "Manual JSON serialization detected. Manual review recommended." }
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
            _logger.LogError(ex, "Failed to migrate response format at {FilePath}:{Line}", 
                pattern.FilePath, pattern.Line);
            
            return new MigrationResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task MigrateLegacyResponse(DocumentEditor editor, PatternMatch pattern)
    {
        var root = await editor.OriginalDocument.GetSyntaxRootAsync();
        if (root == null) return;

        // Find the return statement at the pattern location
        var position = root.GetLocation().SourceTree?.GetText()
            .Lines[pattern.Line - 1].Start ?? 0;
        
        var node = root.FindNode(new Microsoft.CodeAnalysis.Text.TextSpan(position, 1));
        var returnStatement = node.AncestorsAndSelf()
            .OfType<ReturnStatementSyntax>()
            .FirstOrDefault();

        if (returnStatement?.Expression == null) return;

        // Analyze what's being returned
        if (returnStatement.Expression is ObjectCreationExpressionSyntax objCreation)
        {
            // Transform to AI-optimized response
            var newResponse = CreateAIOptimizedResponse(objCreation);
            editor.ReplaceNode(returnStatement, newResponse);
        }
        else if (returnStatement.Expression is AnonymousObjectCreationExpressionSyntax anonObj)
        {
            // Enhance anonymous object with AI fields
            var enhancedObj = EnhanceAnonymousObject(anonObj);
            editor.ReplaceNode(returnStatement.Expression, enhancedObj);
        }
    }

    private async Task AddResponseMetadata(DocumentEditor editor, PatternMatch pattern)
    {
        var root = await editor.OriginalDocument.GetSyntaxRootAsync();
        if (root == null) return;

        // Find the anonymous object at the pattern location
        var position = root.GetLocation().SourceTree?.GetText()
            .Lines[pattern.Line - 1].Start ?? 0;
        
        var node = root.FindNode(new Microsoft.CodeAnalysis.Text.TextSpan(position, 1));
        var anonymousObj = node.AncestorsAndSelf()
            .OfType<AnonymousObjectCreationExpressionSyntax>()
            .FirstOrDefault();

        if (anonymousObj == null) return;

        // Add metadata properties
        var metadataInitializer = SyntaxFactory.AnonymousObjectMemberDeclarator(
            SyntaxFactory.NameEquals("Meta"),
            SyntaxFactory.ObjectCreationExpression(
                SyntaxFactory.IdentifierName("ToolMetadata"))
                .WithInitializer(
                    SyntaxFactory.InitializerExpression(
                        SyntaxKind.ObjectInitializerExpression,
                        SyntaxFactory.SeparatedList<ExpressionSyntax>(new[]
                        {
                            SyntaxFactory.AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                SyntaxFactory.IdentifierName("ExecutionTime"),
                                SyntaxFactory.ParseExpression("$\"{sw.ElapsedMilliseconds}ms\"")),
                            SyntaxFactory.AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                SyntaxFactory.IdentifierName("TokensEstimated"),
                                SyntaxFactory.ParseExpression("EstimateTokens(result)")),
                            SyntaxFactory.AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                SyntaxFactory.IdentifierName("ToolVersion"),
                                SyntaxFactory.LiteralExpression(
                                    SyntaxKind.StringLiteralExpression,
                                    SyntaxFactory.Literal("1.0.0")))
                        }))));

        var newObj = anonymousObj.AddInitializers(metadataInitializer);
        editor.ReplaceNode(anonymousObj, newObj);
    }

    private StatementSyntax CreateAIOptimizedResponse(ObjectCreationExpressionSyntax objCreation)
    {
        // Create a new return statement with AI-optimized response structure
        var responseCode = @"return new
        {
            Success = true,
            Data = /* original response data */,
            Insights = new[]
            {
                ""TODO: Add meaningful insights about the operation"",
                ""TODO: Add contextual information for the AI""
            },
            Actions = new[]
            {
                new { Tool = ""next_tool"", Description = ""TODO: Suggest next action"" }
            },
            Meta = new ToolMetadata
            {
                ExecutionTime = $""{sw.ElapsedMilliseconds}ms"",
                TokensEstimated = EstimateTokens(result),
                ToolVersion = ""1.0.0""
            }
        }";

        return SyntaxFactory.ParseStatement(responseCode);
    }

    private AnonymousObjectCreationExpressionSyntax EnhanceAnonymousObject(
        AnonymousObjectCreationExpressionSyntax anonObj)
    {
        var initializers = anonObj.Initializers.ToList();

        // Check if Insights property exists
        if (!initializers.Any(i => i.ToString().Contains("Insights", StringComparison.OrdinalIgnoreCase)))
        {
            initializers.Add(
                SyntaxFactory.AnonymousObjectMemberDeclarator(
                    SyntaxFactory.NameEquals("Insights"),
                    SyntaxFactory.ArrayCreationExpression(
                        SyntaxFactory.ArrayType(
                            SyntaxFactory.PredefinedType(
                                SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                            SyntaxFactory.SingletonList(
                                SyntaxFactory.ArrayRankSpecifier())))
                        .WithInitializer(
                            SyntaxFactory.InitializerExpression(
                                SyntaxKind.ArrayInitializerExpression,
                                SyntaxFactory.SeparatedList<ExpressionSyntax>(new[]
                                {
                                    SyntaxFactory.LiteralExpression(
                                        SyntaxKind.StringLiteralExpression,
                                        SyntaxFactory.Literal("TODO: Add insights"))
                                })))));
        }

        // Check if Actions property exists
        if (!initializers.Any(i => i.ToString().Contains("Actions", StringComparison.OrdinalIgnoreCase)))
        {
            initializers.Add(
                SyntaxFactory.AnonymousObjectMemberDeclarator(
                    SyntaxFactory.NameEquals("Actions"),
                    SyntaxFactory.ParseExpression(
                        @"new[] { new { Tool = ""next_tool"", Description = ""TODO: Add action"" } }")));
        }

        // Check if Meta property exists
        if (!initializers.Any(i => i.ToString().Contains("Meta", StringComparison.OrdinalIgnoreCase)))
        {
            initializers.Add(
                SyntaxFactory.AnonymousObjectMemberDeclarator(
                    SyntaxFactory.NameEquals("Meta"),
                    SyntaxFactory.ParseExpression(
                        @"new ToolMetadata 
                        { 
                            ExecutionTime = ""0ms"", 
                            TokensEstimated = 100,
                            ToolVersion = ""1.0.0""
                        }")));
        }

        return anonObj.WithInitializers(
            SyntaxFactory.SeparatedList(initializers));
    }

    private async Task AddRequiredUsings(DocumentEditor editor)
    {
        var root = await editor.OriginalDocument.GetSyntaxRootAsync() as CompilationUnitSyntax;
        if (root == null) return;

        var requiredUsings = new[]
        {
            "COA.Mcp.Framework.Models",
            "COA.Mcp.Framework.TokenOptimization",
            "COA.Mcp.Framework.TokenOptimization.Models"
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