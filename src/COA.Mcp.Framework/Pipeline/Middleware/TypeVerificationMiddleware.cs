using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using COA.Mcp.Framework.Configuration;
using COA.Mcp.Framework.Exceptions;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace COA.Mcp.Framework.Pipeline.Middleware;

/// <summary>
/// Middleware that enforces type verification before allowing code generation operations.
/// Blocks Edit, Write, and MultiEdit tools that use unverified types, forcing Claude to use
/// CodeNav tools for proper type verification first.
/// </summary>
public class TypeVerificationMiddleware : SimpleMiddlewareBase
{
    private readonly IVerificationStateManager _verificationStateManager;
    private readonly ILogger<TypeVerificationMiddleware> _logger;
    private readonly TypeVerificationOptions _options;
    
    private static readonly HashSet<string> EditTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "Edit", "Write", "MultiEdit", "NotebookEdit"
    };

    private static readonly HashSet<string> WhitelistedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        // C# primitive types
        "string", "int", "bool", "double", "float", "decimal", "long",
        "short", "byte", "char", "object", "void", "var", "dynamic",
        
        // C# common BCL types
        "String", "Int32", "Boolean", "Double", "Single", "Decimal",
        "DateTime", "TimeSpan", "Guid", "Exception", "ArgumentException",
        "InvalidOperationException", "NotImplementedException",
        "List", "Dictionary", "IEnumerable", "ICollection", "IList",
        "Array", "StringBuilder", "Task", "CancellationToken",
        "ILogger", "IConfiguration", "IServiceCollection", "JsonElement",
        
        // TypeScript built-in types
        "number", "boolean", "undefined", "null", "any", "unknown",
        "never", "symbol", "bigint",
        
        // TypeScript common types
        "Array", "Date", "RegExp", "Error", "Promise", "Map", "Set",
        "WeakMap", "WeakSet", "JSON", "Math", "Console",
        
        // Utility types
        "Partial", "Required", "Readonly", "Record", "Pick", "Omit",
        "Exclude", "Extract", "NonNullable", "ReturnType", "Parameters"
    };

    /// <summary>
    /// Initializes a new instance of the TypeVerificationMiddleware class.
    /// </summary>
    public TypeVerificationMiddleware(
        IVerificationStateManager verificationStateManager,
        ILogger<TypeVerificationMiddleware> logger,
        IOptions<TypeVerificationOptions> options)
    {
        _verificationStateManager = verificationStateManager ?? throw new ArgumentNullException(nameof(verificationStateManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        
        Order = 5; // Run very early in the pipeline
        IsEnabled = _options.Enabled;
    }

    /// <inheritdoc/>
    public override async Task OnBeforeExecutionAsync(string toolName, object? parameters)
    {
        if (!IsEnabled || !EditTools.Contains(toolName) || parameters == null)
        {
            return;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("TypeVerificationMiddleware: Checking tool {ToolName}", toolName);
        }

        try
        {
            var extractedCode = ExtractCodeFromParameters(toolName, parameters);
            if (string.IsNullOrWhiteSpace(extractedCode))
            {
                return;
            }

            var filePath = ExtractFilePathFromParameters(toolName, parameters);
            var extractedTypes = ExtractTypesFromCode(extractedCode, filePath);
            
            if (!extractedTypes.Any())
            {
                return;
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Extracted {TypeCount} types from {ToolName}: {Types}", 
                    extractedTypes.Count, toolName, string.Join(", ", extractedTypes));
            }

            var unverifiedTypes = new List<string>();
            var memberIssues = new List<MemberIssue>();

            // Filter whitelisted types first to avoid unnecessary async calls
            var typesToVerify = extractedTypes
                .Where(typeRef => !WhitelistedTypes.Contains(typeRef.TypeName) && 
                                 _options.WhitelistedTypes?.Contains(typeRef.TypeName) != true)
                .ToList();

            if (typesToVerify.Any())
            {
                // Concurrent type verification for performance
                var typeVerificationTasks = typesToVerify
                    .Select(async typeRef => new
                    {
                        TypeRef = typeRef,
                        IsVerified = await _verificationStateManager.IsTypeVerifiedAsync(typeRef.TypeName)
                    })
                    .ToArray();

                var verificationResults = await Task.WhenAll(typeVerificationTasks);

                // Process results and handle member verification concurrently
                var memberVerificationTasks = new List<Task<MemberVerificationResult>>();

                foreach (var result in verificationResults)
                {
                    if (!result.IsVerified)
                    {
                        unverifiedTypes.Add(result.TypeRef.TypeName);
                        continue;
                    }

                    // Check member access if verification is required
                    if (_options.RequireMemberVerification && !string.IsNullOrEmpty(result.TypeRef.MemberName))
                    {
                        memberVerificationTasks.Add(VerifyMemberAsync(result.TypeRef));
                    }
                }

                // Process member verification results concurrently
                if (memberVerificationTasks.Any())
                {
                    var memberResults = await Task.WhenAll(memberVerificationTasks);
                    memberIssues.AddRange(memberResults.Where(r => r.Issue != null).Select(r => r.Issue!));
                }
            }

            if (unverifiedTypes.Any() || memberIssues.Any())
            {
                await HandleVerificationFailure(toolName, filePath, unverifiedTypes, memberIssues, extractedTypes);
            }
            else
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("All types verified for {ToolName}", toolName);
                }
                await _verificationStateManager.LogVerificationSuccessAsync(toolName, filePath, 
                    extractedTypes.Select(t => t.TypeName).Distinct().ToList());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TypeVerificationMiddleware for tool {ToolName}", toolName);
            
            // In case of errors, fail open in warning mode, fail closed in strict mode
            if (_options.Mode == TypeVerificationMode.Strict)
            {
                throw new McpException($"Type verification failed due to error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Handles verification failure by logging and optionally throwing an exception.
    /// </summary>
    private async Task HandleVerificationFailure(
        string toolName, 
        string filePath, 
        IList<string> unverifiedTypes, 
        IList<MemberIssue> memberIssues,
        IList<TypeReference> allTypes)
    {
        var errorMessage = BuildVerificationErrorMessage(unverifiedTypes, memberIssues, filePath);
        
        await _verificationStateManager.LogVerificationFailureAsync(toolName, filePath, 
            unverifiedTypes, memberIssues.Select(m => m.TypeName).ToList());

        _logger.LogWarning("Type verification failed for {ToolName}: {UnverifiedTypes} unverified, {MemberIssues} member issues",
            toolName, unverifiedTypes.Count, memberIssues.Count);

        if (_options.Mode == TypeVerificationMode.Strict)
        {
            throw new TypeVerificationException(errorMessage);
        }
        else if (_options.Mode == TypeVerificationMode.Warning)
        {
            _logger.LogWarning("Type verification warning for {ToolName}: {Message}", toolName, errorMessage);
        }
    }

    /// <summary>
    /// Builds a detailed error message for verification failures.
    /// </summary>
    private static string BuildVerificationErrorMessage(
        IList<string> unverifiedTypes, 
        IList<MemberIssue> memberIssues,
        string filePath)
    {
        var message = new List<string>();

        if (unverifiedTypes.Any())
        {
            message.Add("🚫 BLOCKED: Unverified types detected");
            message.Add("");
            message.Add("Types requiring verification:");
            
            foreach (var type in unverifiedTypes.Take(5))
            {
                message.Add($"  • {type}");
            }
            
            if (unverifiedTypes.Count > 5)
            {
                message.Add($"  • ... and {unverifiedTypes.Count - 5} more");
            }

            message.Add("");
            message.Add("🔍 Required actions:");
            message.Add("1. Verify types using CodeNav tools:");
            
            var firstType = unverifiedTypes.First();
            message.Add($"   mcp__codenav__csharp_symbol_search query:\"{firstType}\"");
            message.Add($"   mcp__codenav__csharp_goto_definition <file> <line> <col>");
            message.Add($"   mcp__codenav__csharp_hover <file> <line> <col>");
            message.Add("");
            message.Add("2. For complete type information:");
            message.Add($"   mcp__codenav__csharp_get_type_members <file> <line> <col>");
            message.Add("");
        }

        if (memberIssues.Any())
        {
            message.Add("🚫 BLOCKED: Unknown type members detected");
            message.Add("");
            
            foreach (var issue in memberIssues.Take(3))
            {
                message.Add($"❌ {issue.TypeName}.{issue.MemberName} does not exist");
                
                if (issue.AvailableMembers.Any())
                {
                    var available = issue.AvailableMembers.Take(5).ToList();
                    message.Add($"   Available members: {string.Join(", ", available)}");
                    
                    if (issue.AvailableMembers.Count > 5)
                    {
                        message.Add($"   ... and {issue.AvailableMembers.Count - 5} more");
                    }
                }
                message.Add("");
            }

            message.Add("🔍 Required actions:");
            message.Add("1. Check correct member names with:");
            message.Add($"   mcp__codenav__csharp_get_type_members <file> <line> <col>");
            message.Add("2. Or use hover for member details:");
            message.Add($"   mcp__codenav__csharp_hover <file> <line> <col>");
            message.Add("");
        }

        message.Add("ℹ️  After verification, retry your operation.");
        message.Add("");
        message.Add("💡 Tip: The cache will remember verified types for faster future operations.");

        return string.Join("\n", message);
    }

    /// <summary>
    /// Extracts code content from tool parameters based on tool type.
    /// </summary>
    private static string ExtractCodeFromParameters(string toolName, object parameters)
    {
        if (parameters is JsonElement jsonElement)
        {
            return toolName switch
            {
                "Edit" => TryGetStringProperty(jsonElement, "new_string") ?? "",
                "Write" => TryGetStringProperty(jsonElement, "content") ?? "",
                "MultiEdit" => ExtractMultiEditCode(jsonElement),
                "NotebookEdit" => TryGetStringProperty(jsonElement, "new_source") ?? "",
                _ => ""
            };
        }

        // Handle direct object parameters using reflection
        var parametersType = parameters.GetType();
        return toolName switch
        {
            "Edit" => GetPropertyValue<string>(parameters, "new_string") ?? "",
            "Write" => GetPropertyValue<string>(parameters, "content") ?? "",
            "MultiEdit" => ExtractMultiEditCodeFromObject(parameters),
            "NotebookEdit" => GetPropertyValue<string>(parameters, "new_source") ?? "",
            _ => ""
        };
    }

    /// <summary>
    /// Extracts file path from tool parameters.
    /// </summary>
    private static string ExtractFilePathFromParameters(string toolName, object parameters)
    {
        if (parameters is JsonElement jsonElement)
        {
            return TryGetStringProperty(jsonElement, "file_path") ?? 
                   TryGetStringProperty(jsonElement, "filePath") ?? 
                   TryGetStringProperty(jsonElement, "notebook_path") ?? "";
        }

        return GetPropertyValue<string>(parameters, "file_path") ?? 
               GetPropertyValue<string>(parameters, "filePath") ?? 
               GetPropertyValue<string>(parameters, "notebook_path") ?? "";
    }

    /// <summary>
    /// Extracts code from MultiEdit operations.
    /// </summary>
    private static string ExtractMultiEditCode(JsonElement jsonElement)
    {
        if (jsonElement.TryGetProperty("edits", out var editsElement) && editsElement.ValueKind == JsonValueKind.Array)
        {
            var codes = new List<string>();
            foreach (var edit in editsElement.EnumerateArray())
            {
                var newString = TryGetStringProperty(edit, "new_string");
                if (!string.IsNullOrEmpty(newString))
                {
                    codes.Add(newString);
                }
            }
            return string.Join("\n", codes);
        }
        return "";
    }

    /// <summary>
    /// Extracts code from MultiEdit object parameters.
    /// </summary>
    private static string ExtractMultiEditCodeFromObject(object parameters)
    {
        var edits = GetPropertyValue<object>(parameters, "edits");
        if (edits is IEnumerable<object> enumerable)
        {
            var codes = new List<string>();
            foreach (var edit in enumerable)
            {
                var newString = GetPropertyValue<string>(edit, "new_string");
                if (!string.IsNullOrEmpty(newString))
                {
                    codes.Add(newString);
                }
            }
            return string.Join("\n", codes);
        }
        return "";
    }

    /// <summary>
    /// Safely gets a string property from JsonElement.
    /// </summary>
    private static string? TryGetStringProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String 
            ? prop.GetString() 
            : null;
    }

    /// <summary>
    /// Gets a property value using reflection.
    /// </summary>
    private static T? GetPropertyValue<T>(object obj, string propertyName)
    {
        var property = obj.GetType().GetProperty(propertyName);
        return property != null ? (T?)property.GetValue(obj) : default(T);
    }

    /// <summary>
    /// Extracts type references from code based on file type.
    /// </summary>
    private static List<TypeReference> ExtractTypesFromCode(string code, string filePath)
    {
        if (string.IsNullOrWhiteSpace(code))
            return new List<TypeReference>();

        var isCSharp = filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                       filePath.EndsWith(".csx", StringComparison.OrdinalIgnoreCase) ||
                       ContainsCSharpKeywords(code);

        var isTypeScript = filePath.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) ||
                          filePath.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase) ||
                          filePath.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
                          filePath.EndsWith(".jsx", StringComparison.OrdinalIgnoreCase) ||
                          ContainsTypeScriptKeywords(code);

        var types = new List<TypeReference>();

        if (isCSharp)
        {
            types.AddRange(ExtractCSharpTypes(code));
        }

        if (isTypeScript)
        {
            types.AddRange(ExtractTypeScriptTypes(code));
        }

        // If neither language detected, default to C#
        if (!isCSharp && !isTypeScript)
        {
            types.AddRange(ExtractCSharpTypes(code));
        }

        return types.GroupBy(t => new { t.TypeName, t.MemberName })
                   .Select(g => g.First())
                   .ToList();
    }

    /// <summary>
    /// Checks if code contains C# specific keywords.
    /// </summary>
    private static bool ContainsCSharpKeywords(string code)
    {
        var csharpKeywords = new[] { "using ", "namespace ", "class ", "public class", "private ", "protected " };
        return csharpKeywords.Any(keyword => code.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if code contains TypeScript specific keywords.
    /// </summary>
    private static bool ContainsTypeScriptKeywords(string code)
    {
        var tsKeywords = new[] { "interface ", "type ", "import ", "export ", "const ", "let " };
        return tsKeywords.Any(keyword => code.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Extracts C# type references from code.
    /// </summary>
    private static List<TypeReference> ExtractCSharpTypes(string code)
    {
        var types = new List<TypeReference>();
        
        var patterns = new[]
        {
            (@"\bnew\s+([A-Z]\w*)", "Constructor"), // new User()
            (@"\b([A-Z]\w*)\s+\w+\s*[=;]", "Variable Declaration"), // User user = 
            (@"\b([A-Z]\w*)\?\s+\w+", "Nullable Variable"), // User? user
            (@":\s*([A-Z]\w*)", "Type Annotation"), // : BaseClass
            (@"<([A-Z]\w*)>", "Generic Type"), // List<User>
            (@"<([A-Z]\w*),", "Generic Type Parameter"), // Dictionary<User, 
            (@",\s*([A-Z]\w*)>", "Generic Type Argument"), // Dictionary<string, User>
            (@"\b([A-Z]\w*)\.(\w+)", "Member Access"), // User.Property
            (@"typeof\(([A-Z]\w*)\)", "Type Reference"), // typeof(User)
            (@"\bis\s+([A-Z]\w*)", "Type Check"), // is User
            (@"\bas\s+([A-Z]\w*)", "Type Cast"), // as User
            (@"\(([A-Z]\w*)\s+\w+\)", "Parameter Type"), // (User user)
            (@"\bclass\s+([A-Z]\w*)", "Class Declaration"), // class NewClass
            (@"\binterface\s+([A-Z]\w*)", "Interface Declaration"), // interface IUser
            (@"\benum\s+([A-Z]\w*)", "Enum Declaration"), // enum Status
        };

        foreach (var (pattern, context) in patterns)
        {
            var matches = Regex.Matches(code, pattern, RegexOptions.Multiline);
            foreach (Match match in matches)
            {
                if (pattern.Contains(@"\.(\w+)")) // Member access pattern
                {
                    if (match.Groups.Count >= 3)
                    {
                        types.Add(new TypeReference
                        {
                            TypeName = match.Groups[1].Value,
                            MemberName = match.Groups[2].Value,
                            Context = context
                        });
                    }
                }
                else
                {
                    types.Add(new TypeReference
                    {
                        TypeName = match.Groups[1].Value,
                        Context = context
                    });
                }
            }
        }

        return types;
    }

    /// <summary>
    /// Extracts TypeScript type references from code.
    /// </summary>
    private static List<TypeReference> ExtractTypeScriptTypes(string code)
    {
        var types = new List<TypeReference>();
        
        var patterns = new[]
        {
            (@":\s*([A-Z]\w*)", "Type Annotation"), // : User
            (@"\bas\s+([A-Z]\w*)", "Type Assertion"), // as User  
            (@"\bnew\s+([A-Z]\w*)", "Constructor"), // new User()
            (@"<([A-Z]\w*)>", "Generic Type"), // Array<User>
            (@"<([A-Z]\w*),", "Generic Type Parameter"), // Map<User,
            (@",\s*([A-Z]\w*)>", "Generic Type Argument"), // Map<string, User>
            (@"\b([A-Z]\w*)\.(\w+)", "Member Access"), // User.property
            (@"\binstanceof\s+([A-Z]\w*)", "Instance Check"), // instanceof User
            (@"\binterface\s+([A-Z]\w*)", "Interface Declaration"), // interface User
            (@"\btype\s+([A-Z]\w*)", "Type Alias"), // type User
            (@"\bclass\s+([A-Z]\w*)", "Class Declaration"), // class User
            (@"\bextends\s+([A-Z]\w*)", "Inheritance"), // extends BaseUser
            (@"\bimplements\s+([A-Z]\w*)", "Interface Implementation"), // implements IUser
        };

        foreach (var (pattern, context) in patterns)
        {
            var matches = Regex.Matches(code, pattern, RegexOptions.Multiline);
            foreach (Match match in matches)
            {
                if (pattern.Contains(@"\.(\w+)")) // Member access pattern
                {
                    if (match.Groups.Count >= 3)
                    {
                        types.Add(new TypeReference
                        {
                            TypeName = match.Groups[1].Value,
                            MemberName = match.Groups[2].Value,
                            Context = context
                        });
                    }
                }
                else
                {
                    types.Add(new TypeReference
                    {
                        TypeName = match.Groups[1].Value,
                        Context = context
                    });
                }
            }
        }

        return types;
    }

    /// <summary>
    /// Verifies a member access asynchronously for concurrent processing.
    /// </summary>
    private async Task<MemberVerificationResult> VerifyMemberAsync(TypeReference typeRef)
    {
        var hasMember = await _verificationStateManager.HasVerifiedMemberAsync(
            typeRef.TypeName, typeRef.MemberName!);
        
        if (!hasMember)
        {
            var availableMembers = await _verificationStateManager.GetAvailableMembersAsync(typeRef.TypeName);
            return new MemberVerificationResult
            {
                Issue = new MemberIssue
                {
                    TypeName = typeRef.TypeName,
                    MemberName = typeRef.MemberName!,
                    AvailableMembers = availableMembers?.ToList() ?? new List<string>()
                }
            };
        }

        return new MemberVerificationResult { Issue = null };
    }

    /// <summary>
    /// Represents the result of member verification.
    /// </summary>
    private class MemberVerificationResult
    {
        public MemberIssue? Issue { get; set; }
    }

    /// <summary>
    /// Represents a type reference found in code.
    /// </summary>
    private class TypeReference
    {
        public string TypeName { get; set; } = "";
        public string? MemberName { get; set; }
        public string Context { get; set; } = "";
    }

    /// <summary>
    /// Represents a member access issue.
    /// </summary>
    private class MemberIssue
    {
        public string TypeName { get; set; } = "";
        public string MemberName { get; set; } = "";
        public List<string> AvailableMembers { get; set; } = new();
    }
}

/// <summary>
/// Exception thrown when type verification fails.
/// </summary>
public class TypeVerificationException : McpException
{
    public TypeVerificationException(string message) : base(message, "TYPE_VERIFICATION_FAILED") { }
    public TypeVerificationException(string message, Exception innerException) : base(message, "TYPE_VERIFICATION_FAILED", innerException) { }
}