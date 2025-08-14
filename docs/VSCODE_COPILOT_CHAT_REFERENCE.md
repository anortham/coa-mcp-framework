# VS Code Copilot Chat MCP Integration Reference

## Overview
This document provides a comprehensive reference for how GitHub Copilot Chat extension in VS Code handles MCP (Model Context Protocol) tool responses and integrations. This knowledge is essential for building MCP servers that work optimally with VS Code.

**Source Code Location**: `C:\source\vscode-copilot-chat`  
**Last Updated**: 2025-01-14

## Key Findings

### 1. Tool Response Rendering

#### Automatic File Path Extraction
- VS Code **automatically extracts file paths** from tool responses
- File paths become **clickable links** that open in the editor
- Directory paths open in the **Explorer panel**
- This happens **regardless of markdown formatting** in the response

#### Accordion UI Pattern
- Tool invocations appear in **collapsible accordion sections**
- Raw tool response data is shown inside the accordion
- Extracted actionable items (files, directories) appear **below** the accordion
- The accordion shows the tool name and can be expanded to see details

### 2. Response Part Types

VS Code supports specific response part types through `ChatResponseStream`:

```typescript
// File: src/util/common/chatResponseStreamImpl.ts
- ChatResponseMarkdownPart      // Regular markdown content
- ChatResponseAnchorPart        // Links to files/locations
- ChatResponseFileTreePart      // File tree visualization
- ChatResponseReferencePart     // File/variable references
- ChatResponseProgressPart      // Progress indicators
- ChatResponseCommandButtonPart // Actionable buttons
- ChatResponseWarningPart       // Warning messages
- ChatResponseCodeCitationPart  // Code citations with licenses
- ChatResponseTextEditPart      // Text edits to apply
- ChatResponseNotebookEditPart  // Notebook cell edits
```

### 3. File References

#### How They Work
- File paths in tool responses are automatically detected
- VS Code creates `ChatResponseReferencePart` objects internally
- These become clickable elements in the UI
- Line and column numbers are supported for precise navigation

#### Best Practices
- Return **absolute file paths** for reliability
- Include **line numbers** when referencing specific code
- Use forward slashes even on Windows (VS Code handles conversion)
- Keep file descriptions concise

### 4. MCP Tool Integration Points

#### Tool Calling Loop
- Location: `src/extension/mcp/vscode-node/mcpToolCallingLoop.tsx`
- Handles tool invocation and response processing
- Manages the conversation flow with tools

#### Tool Response Processing
- Tool results are wrapped in `LanguageModelToolResult`
- Text parts are extracted and rendered
- File references are parsed and made interactive

### 5. UI Rendering Behavior

#### What VS Code Handles Automatically
- File path extraction and clickable links
- Directory navigation in Explorer
- Syntax highlighting in code blocks
- Collapsible tool invocation sections
- Progress indicators during tool execution

#### What MCP Servers Should NOT Do
- Don't create complex markdown for file lists (VS Code extracts them anyway)
- Don't try to format clickable links manually
- Don't include UI instructions (VS Code has its own UI)
- Don't worry about response formatting for VS Code specifically

## Implications for MCP Server Development

### 1. Response Format Recommendations

#### Keep It Simple
```json
{
  "success": true,
  "files": [
    "C:/source/project/file1.cs",
    "C:/source/project/file2.cs"
  ],
  "message": "Found 2 files matching your criteria"
}
```

#### Let VS Code Handle Rendering
- Return structured data, not formatted markdown
- Include file paths as simple strings
- VS Code will make them interactive automatically

### 2. What Actually Works vs What We Thought

| What We Thought | What Actually Happens |
|----------------|----------------------|
| Need markdown formatting for file links | VS Code extracts paths automatically |
| Need special response builders | Standard JSON responses work fine |
| Need to format tables/lists specially | VS Code handles basic formatting |
| Tool responses appear inline | They appear in accordion UI |

### 3. Focus Areas for MCP Servers

#### Data Quality Over Formatting
- Ensure file paths are correct and absolute
- Include relevant metadata (descriptions, line numbers)
- Keep responses concise and focused
- Let VS Code handle the presentation

#### Tool Design Principles
1. **Single Responsibility** - Each tool does one thing well
2. **Clear Returns** - Obvious what data is being returned
3. **Minimal Formatting** - Let VS Code handle UI
4. **Rich Metadata** - Include context VS Code can use

## Short-Term Strategy Revision

### Original Plan Issues
- **Over-engineering**: Adaptive response builders unnecessary
- **Wrong focus**: Formatting instead of data quality
- **Misunderstood UI**: Didn't realize accordion behavior

### Revised Approach

#### 1. Immediate Actions
- **Remove** complex response formatting from CodeSearch MCP
- **Simplify** tool responses to basic JSON structures
- **Document** the actual VS Code behavior for the team

#### 2. SQL Server MCP as Proof of Concept
**Yes, proceed with SQL Server MCP** because it will test:
- **Table rendering**: How VS Code displays tabular data
- **Large result sets**: Pagination and performance
- **Charts/graphs**: Whether these need special handling
- **Documentation rendering**: How help text appears

#### 3. SQL Server MCP Focus Areas

##### Test Cases for UI Elements
1. **Query Results as Tables**
   - Return results as JSON arrays
   - Test if VS Code renders them as tables
   - Check performance with large datasets

2. **Schema Documentation**
   - Return structured documentation
   - See how VS Code formats it
   - Test collapsible sections for complex docs

3. **Visual Elements**
   - Test if we can return SVG for diagrams
   - Check if images can be embedded
   - Explore chart rendering options

4. **File References**
   - Link to SQL scripts on disk
   - Reference stored procedures by file
   - Test navigation to definition

## Testing Checklist

### For Any New MCP Server
- [ ] File paths are extracted and clickable
- [ ] Directory paths open in Explorer
- [ ] Tool responses appear in accordion UI
- [ ] Large responses don't break the UI
- [ ] Error messages are clear and actionable
- [ ] Progress indicators work during long operations

### SQL Server MCP Specific
- [ ] Query results display properly
- [ ] Table/column documentation is readable
- [ ] Connection management works smoothly
- [ ] Error messages from SQL are helpful
- [ ] Performance with large result sets

## Code Examples

### Simple Tool Response (Recommended)
```typescript
return {
  success: true,
  data: {
    files: ["C:/source/file1.cs", "C:/source/file2.cs"],
    totalCount: 2
  },
  message: "Search completed"
};
```

### What NOT to Do
```typescript
// DON'T: Try to format markdown links
return {
  message: "## Results\n- [file1.cs](C:/source/file1.cs)\n- [file2.cs](C:/source/file2.cs)"
};

// DON'T: Over-format responses
return {
  markdown: "```\n<complex formatted table>\n```"
};
```

## Resources

### Key Files to Reference
- `src/extension/mcp/vscode-node/mcpToolCallingLoop.tsx` - Tool calling implementation
- `src/util/common/chatResponseStreamImpl.ts` - Response part types
- `src/extension/tools/node/` - Example tool implementations
- `src/extension/prompt/common/fileTreeParser.ts` - File tree handling

### Documentation
- [MCP Specification](https://modelcontextprotocol.io)
- [VS Code Extension API](https://code.visualstudio.com/api)
- Source code: `C:\source\vscode-copilot-chat`

## Conclusion

The key insight is that **VS Code Copilot Chat does the heavy lifting** for UI rendering. MCP servers should focus on:
1. **Returning clean, structured data**
2. **Ensuring file paths are valid**
3. **Keeping responses simple**
4. **Letting VS Code handle presentation**

This simplifies our development significantly and means we can focus on functionality rather than formatting.