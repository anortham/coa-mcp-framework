# Visualization Protocol Specification

## Overview

The COA MCP Visualization Protocol enables MCP servers (in any language) to provide structured visualization hints alongside their data responses. This allows rich UI clients like VS Code to render interactive visualizations while maintaining backward compatibility with text-only clients.

## Core Principles

1. **Language Agnostic**: Works with any language that can output JSON
2. **Progressive Enhancement**: Visualizations are optional - tools work without them
3. **Loose Coupling**: MCP servers don't depend on specific UI implementations
4. **Graceful Degradation**: Unknown visualization types fall back to JSON/text

## Protocol Structure

### 1. MCP Response with Visualization

Any MCP tool response can include an optional `_visualization` field:

```json
{
  "success": true,
  "data": { /* tool-specific data */ },
  "_visualization": {
    "type": "search-results",
    "version": "1.0",
    "data": { /* visualization data */ },
    "hint": { /* display hints */ }
  }
}
```

### 2. Visualization Descriptor

The `_visualization` field contains:

- **type** (required): Identifies the visualization type
- **version** (optional): Format version for compatibility
- **data** (required): The actual data to visualize
- **hint** (optional): Display preferences and fallback options
- **metadata** (optional): Additional context

### 3. Visualization Hints

Hints guide the client's rendering decisions:

```json
{
  "preferredView": "grid",        // Suggested view type
  "fallbackFormat": "json",       // If type unknown
  "interactive": true,             // Allow user interaction
  "consolidateTabs": true,         // Replace previous
  "priority": "normal",            // Display priority
  "maxConcurrentTabs": 1,         // Tab limit
  "options": {                    // Type-specific options
    "sortable": true,
    "filterable": true
  }
}
```

## Standard Visualization Types

### search-results
```json
{
  "type": "search-results",
  "data": {
    "query": "string",
    "totalHits": 42,
    "results": [{
      "filePath": "path/to/file",
      "line": 10,
      "snippet": "code snippet",
      "score": 0.95
    }]
  }
}
```

### hierarchy
```json
{
  "type": "hierarchy",
  "data": {
    "root": {
      "name": "Root",
      "children": [
        { "name": "Child1", "children": [] }
      ]
    }
  }
}
```

### data-grid
```json
{
  "type": "data-grid",
  "data": {
    "columns": [
      { "name": "Name", "type": "string" },
      { "name": "Value", "type": "number" }
    ],
    "rows": [
      ["Item1", 100],
      ["Item2", 200]
    ]
  }
}
```

### chart
```json
{
  "type": "chart",
  "data": {
    "chartType": "bar",
    "labels": ["A", "B", "C"],
    "datasets": [{
      "label": "Series 1",
      "data": [10, 20, 30]
    }]
  }
}
```

## Implementation Guide

### For MCP Servers (Any Language)

#### C# Implementation
```csharp
public class MyTool : IVisualizationProvider
{
    public VisualizationDescriptor GetVisualizationDescriptor()
    {
        return new VisualizationDescriptor
        {
            Type = StandardVisualizationTypes.SearchResults,
            Data = new { results = searchResults },
            Hint = new VisualizationHint
            {
                PreferredView = "grid",
                FallbackFormat = "json"
            }
        };
    }
}
```

#### TypeScript Implementation
```typescript
class MyTool {
  execute(params: any) {
    return {
      success: true,
      data: results,
      _visualization: {
        type: "search-results",
        version: "1.0",
        data: { results },
        hint: {
          preferredView: "grid",
          fallbackFormat: "json"
        }
      }
    };
  }
}
```

#### Python Implementation
```python
def execute(self, params):
    return {
        "success": True,
        "data": results,
        "_visualization": {
            "type": "search-results",
            "version": "1.0",
            "data": {"results": results},
            "hint": {
                "preferredView": "grid",
                "fallbackFormat": "json"
            }
        }
    }
```

### For Visualization Clients

Clients should:

1. Check for `_visualization` field in responses
2. Match `type` to available renderers
3. Use `hint.preferredView` if renderer supports multiple views
4. Fall back to `hint.fallbackFormat` if type unknown
5. Default to JSON tree if no fallback specified

```typescript
function handleMCPResponse(response: any) {
  if (response._visualization) {
    const viz = response._visualization;
    const renderer = getRenderer(viz.type);
    
    if (renderer) {
      return renderer.render(viz.data, viz.hint);
    } else {
      return fallbackRender(viz.data, viz.hint?.fallbackFormat);
    }
  }
  
  return defaultRender(response);
}
```

## Protocol Evolution

### Version Compatibility

- Clients should handle unknown fields gracefully
- Servers should include version for breaking changes
- New optional fields can be added without version bump
- Removing/changing required fields requires version bump

### Adding New Types

1. Define the type name and data structure
2. Document in this specification
3. Implement renderer in clients
4. Servers can start using immediately (with fallback)

## Best Practices

### For MCP Servers

1. **Always provide fallback format** for custom types
2. **Keep visualization data lean** - don't duplicate
3. **Use standard types** when possible
4. **Version custom types** for compatibility
5. **Make visualization optional** - tool should work without it

### For Clients

1. **Cache renderers** for performance
2. **Validate data structure** before rendering
3. **Handle errors gracefully** - fall back to JSON
4. **Support user preferences** over hints
5. **Log unknown types** for debugging

## Testing

### Server Testing
```csharp
[Test]
public void ShouldProvideVisualization()
{
    var result = tool.Execute(params);
    var viz = tool.GetVisualizationDescriptor();
    
    Assert.NotNull(viz);
    Assert.Equal("search-results", viz.Type);
    Assert.NotNull(viz.Data);
}
```

### Client Testing
```typescript
test('handles unknown visualization type', () => {
  const response = {
    _visualization: {
      type: 'unknown-type',
      data: { foo: 'bar' },
      hint: { fallbackFormat: 'json' }
    }
  };
  
  const result = handleVisualization(response);
  expect(result.renderer).toBe('json-tree');
});
```

## Migration Path

### From String Building
```csharp
// Before: Building markdown strings
var markdown = new StringBuilder();
markdown.AppendLine($"# Results");
// ... complex string manipulation

// After: Return structured data
return new VisualizationDescriptor
{
    Type = "search-results",
    Data = results
};
```

### Maintaining Backward Compatibility
```csharp
public Response Execute(params)
{
    // Still return text for AI
    var textResponse = BuildTextResponse(results);
    
    // Add visualization for UI
    var response = new Response { Data = textResponse };
    if (bridgeAvailable)
    {
        response._visualization = GetVisualizationDescriptor();
    }
    
    return response;
}
```

## Security Considerations

1. **Validate all data** before rendering
2. **Sanitize HTML** if rendering markdown
3. **Limit data size** to prevent DoS
4. **Use CSP** in webviews
5. **Don't execute** arbitrary code from visualizations

## Performance Guidelines

1. **Lazy load** renderers as needed
2. **Virtualize** large data sets
3. **Paginate** when appropriate
4. **Cache** rendered components
5. **Debounce** rapid updates

## Reference Implementation

See the VS Code Bridge implementation for a complete example of a visualization client that supports all standard types with Vue 3 components.