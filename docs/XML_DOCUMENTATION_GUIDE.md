# XML Documentation Guide for MCP Tools

A comprehensive guide for implementing enhanced XML documentation in MCP tools to provide rich, example-driven parameter descriptions that improve the developer experience.

## Overview

XML documentation in MCP tools serves two critical purposes:

1. **Compile-time validation** - Ensures documentation syntax is correct
2. **Runtime extraction** - The MCP framework dynamically loads XML documentation to enhance tool parameter descriptions with examples and detailed explanations

This approach transforms simple JSON schema descriptions into rich, contextual help that guides developers in using your MCP tools effectively.

## The Problem XML Documentation Solves

**Before**: Generic parameter descriptions
```json
{
  "searchPattern": {
    "type": "string",
    "description": "Pattern to search for"
  }
}
```

**After**: Rich, example-driven descriptions
```json
{
  "searchPattern": {
    "type": "string",
    "description": "Pattern to search for - supports multiple search types for flexible matching. Examples: oldMethodName, TODO.*urgent, class\\s+\\w+Service"
  }
}
```

## Implementation Guide

### 1. Enable XML Documentation Generation

Add to your `.csproj` file:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <DocumentationFile>bin\$(Configuration)\$(TargetFramework)\$(AssemblyName).xml</DocumentationFile>
</PropertyGroup>
```

### 2. Parameter Class Documentation Pattern

Follow this comprehensive pattern for all parameter classes:

```csharp
/// <summary>
/// Parameters for the text search operation.
/// </summary>
public class TextSearchParams
{
    /// <summary>
    /// The search query string - supports multiple search types including regex, wildcards, and intelligent code patterns.
    /// </summary>
    /// <example>class UserService</example>
    /// <example>*.findBy*</example>
    /// <example>TODO|FIXME</example>
    [Description("Search query. Examples: 'class UserService' (exact match), '*.findBy*' (wildcard), 'TODO|FIXME' (regex)")]
    public required string Query { get; set; }

    /// <summary>
    /// Path to the workspace directory to search. Can be absolute or relative path.
    /// </summary>
    /// <example>C:\\source\\MyProject</example>
    /// <example>./src</example>
    /// <example>../other-project</example>
    [Description("Workspace path. Examples: 'C:\\source\\MyProject' (absolute), './src' (relative), '../other-project' (parent)")]
    public required string WorkspacePath { get; set; }

    /// <summary>
    /// Case sensitive search.
    /// </summary>
    [Description("Enable case-sensitive matching")]
    public bool CaseSensitive { get; set; } = false;
}
```

### 3. XML Documentation Best Practices

#### Required Elements

**`<summary>`**: Clear, concise description with context
- Start with the parameter's purpose
- Include supported formats/types
- Mention key capabilities

**`<example>`**: Multiple concrete examples showing real usage
- Show different use cases
- Use realistic values (not "test123")
- Include edge cases where relevant

**`[Description]` attribute**: Condensed version for JSON schema
- Combine summary with key examples
- Keep under 200 characters when possible
- Focus on practical usage

#### Example Patterns by Parameter Type

**File/Directory Paths**:
```csharp
/// <summary>
/// Absolute or relative path to the file to modify. Must be an existing file with write permissions.
/// </summary>
/// <example>C:\\source\\MyProject\\UserService.cs</example>
/// <example>./src/components/Button.tsx</example>
/// <example>../config/settings.json</example>
[Description("File path. Examples: 'C:\\source\\MyProject\\UserService.cs', './src/components/Button.tsx', '../config/settings.json'")]
public required string FilePath { get; set; }
```

**Search Patterns**:
```csharp
/// <summary>
/// The search pattern supporting glob patterns, wildcards, and recursive directory traversal.
/// </summary>
/// <example>*.cs</example>
/// <example>**/*.test.js</example>
/// <example>src/**/*Controller.cs</example>
/// <example>UserService*</example>
[Description("Search pattern supporting glob patterns. Examples: '*.cs', '**/*.test.js', 'src/**/*Controller.cs'")]
public required string Pattern { get; set; }
```

**Numeric Ranges/Limits**:
```csharp
/// <summary>
/// Number of context lines to show around each match for better understanding of usage.
/// </summary>
/// <example>2</example>
/// <example>5</example>
/// <example>0</example>
[Description("Context lines around matches. Examples: '2' (minimal), '5' (standard), '0' (no context)")]
public int ContextLines { get; set; } = 2;
```

**Boolean Flags**:
```csharp
/// <summary>
/// Include usage count for each symbol showing how many references exist across the codebase.
/// Useful for understanding symbol popularity and impact.
/// </summary>
/// <example>true</example>
/// <example>false</example>
[Description("Include reference counts. Examples: 'true' (show usage stats), 'false' (definitions only)")]
public bool IncludeReferences { get; set; } = false;
```

**Enums/Options**:
```csharp
/// <summary>
/// Direction to trace: 'up' (callers), 'down' (callees), or 'both' (full hierarchy) for comprehensive analysis.
/// </summary>
/// <example>up</example>
/// <example>down</example>
/// <example>both</example>
[Description("Trace direction. Examples: 'up' (who calls this), 'down' (what this calls), 'both' (full tree)")]
public required string Direction { get; set; }
```

### 4. Framework Integration

The MCP framework automatically discovers and extracts XML documentation using reflection:

```csharp
// Framework code - handles this automatically
var xmlFile = Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, ".xml");
if (File.Exists(xmlFile))
{
    var xmlDoc = new XPathDocument(xmlFile);
    // Extract <summary> and <example> tags
    // Enhance parameter descriptions with examples
}
```

### 5. Common Pitfalls and Solutions

#### Malformed XML
❌ **Problem**: Unclosed tags or invalid XML
```csharp
/// <summary>
/// Find IRepository<T> implementations
/// <example>UserRepository
/// </summary>
```

✅ **Solution**: Proper XML escaping and closed tags
```csharp
/// <summary>
/// Find IRepository&lt;T&gt; implementations.
/// </summary>
/// <example>UserRepository</example>
```

#### Generic Examples
❌ **Problem**: Vague examples that don't help
```csharp
/// <example>test123</example>
/// <example>example</example>
/// <example>value</example>
```

✅ **Solution**: Real-world, contextual examples
```csharp
/// <example>UserService</example>
/// <example>FindByEmailAsync</example>
/// <example>ProcessPayment</example>
```

#### Missing Context
❌ **Problem**: Examples without explanation
```csharp
/// <example>*.cs</example>
```

✅ **Solution**: Examples with purpose
```csharp
/// <example>*.cs</example> (all C# files)
/// <example>**/*.test.js</example> (all test files recursively)
/// <example>src/**/*Controller.cs</example> (controllers in src)
```

### 6. Validation and Testing

**Build Validation**:
```bash
# Ensure clean build with XML generation
dotnet build --verbosity normal
# Check for CS1570, CS1571, CS1572 warnings
```

**Runtime Testing**:
```csharp
[Test]
public void Should_Extract_XML_Documentation()
{
    // Verify framework can load and parse XML docs
    var toolInfo = _framework.GetToolInfo<MyTool>();
    Assert.That(toolInfo.Parameters["searchPattern"].Description,
                Contains.Substring("Examples:"));
}
```

## Framework Support

### Automatic Enhancement

The COA MCP Framework automatically:
- Loads XML documentation files at runtime
- Extracts `<summary>` and `<example>` content
- Merges XML docs with `[Description]` attributes
- Enhances JSON schema with rich descriptions

### Token Optimization

XML documentation integrates with token optimization:
- Examples are included in development/debug modes
- Production mode can strip examples to save tokens
- Progressive reduction maintains core descriptions

## Migration from Basic Descriptions

### Step 1: Add XML Generation
Enable XML documentation in your project file.

### Step 2: Enhance Existing Parameters
Add XML documentation to all parameter classes using the patterns above.

### Step 3: Validate Build
Ensure no XML documentation warnings.

### Step 4: Test Runtime Extraction
Verify the framework successfully loads enhanced descriptions.

## Best Practices Summary

1. **Always provide multiple examples** - Show different use cases
2. **Use realistic values** - Avoid "test123" or placeholder text
3. **Include context in examples** - Explain what each example demonstrates
4. **Escape XML properly** - Handle `<>` characters in generic types
5. **Keep descriptions focused** - Balance detail with readability
6. **Test XML validity** - Ensure clean builds without warnings
7. **Follow established patterns** - Consistency across tools improves UX

## Result

Following this guide transforms your MCP tools from basic parameter lists into guided, example-rich experiences that help developers understand and use your tools effectively. The enhanced documentation bridges the gap between simple JSON schemas and comprehensive developer guidance, creating a professional-grade development experience.

---

*This guide is based on the successful implementation in COA CodeSearch MCP, which achieved 100% parameter coverage and zero build warnings using these patterns.*