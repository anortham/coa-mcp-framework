# Type System Improvements - Implementation Plan

## Phase 1: Quick Wins (No Breaking Changes)
**Timeline: 1-2 days**

### 1.1 Fix Double Serialization
**Files to modify:**
- `src/COA.Mcp.Framework/Base/McpToolBase.Generic.cs`
- `src/COA.Mcp.Framework/Server/McpServer.cs`

**Changes:**
```csharp
// OLD (lines 127-129 in McpToolBase.Generic.cs)
else
{
    var json = JsonSerializer.Serialize(parameters, _jsonOptions);
    typedParams = JsonSerializer.Deserialize<TParams>(json, _jsonOptions);
}

// NEW
else if (parameters is JsonDocument doc)
{
    typedParams = doc.Deserialize<TParams>(_jsonOptions);
}
else
{
    // Only as last resort
    var bytes = JsonSerializer.SerializeToUtf8Bytes(parameters, _jsonOptions);
    typedParams = JsonSerializer.Deserialize<TParams>(bytes, _jsonOptions);
}
```

### 1.2 Add Capability Marker Types
**New file:** `src/COA.Mcp.Protocol/CapabilityMarkers.cs`
```csharp
namespace COA.Mcp.Protocol;

/// <summary>
/// Marker type indicating tool capability support.
/// </summary>
public sealed class ToolsCapabilityMarker 
{ 
    // Empty marker class is intentional per MCP spec
}

/// <summary>
/// Marker type indicating prompts capability support.
/// </summary>
public sealed class PromptsCapabilityMarker 
{ 
    // Empty marker class is intentional per MCP spec
}

/// <summary>
/// Marker type indicating sampling capability support.
/// </summary>
public sealed class SamplingCapabilityMarker 
{ 
    // Empty marker class is intentional per MCP spec
}
```

**Update:** `src/COA.Mcp.Protocol/McpTypes.cs`
```csharp
public class ServerCapabilities
{
    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ToolsCapabilityMarker? Tools { get; set; }

    [JsonPropertyName("prompts")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PromptsCapabilityMarker? Prompts { get; set; }
}

public class ClientCapabilities
{
    [JsonPropertyName("sampling")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SamplingCapabilityMarker? Sampling { get; set; }
}
```

## Phase 2: Schema Improvements
**Timeline: 2-3 days**

### 2.1 Replace Dictionary<string, object> with JsonDocument
**Files to modify:**
- `src/COA.Mcp.Framework/Schema/RuntimeJsonSchema.cs`
- `src/COA.Mcp.Framework/Schema/JsonSchema.cs`

```csharp
public class RuntimeJsonSchema : IJsonSchema
{
    private readonly JsonDocument _schema;
    private readonly Lazy<string> _jsonRepresentation;
    
    public RuntimeJsonSchema(JsonDocument schema)
    {
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _jsonRepresentation = new Lazy<string>(() => _schema.RootElement.GetRawText());
    }
    
    public JsonElement ToJsonElement() => _schema.RootElement.Clone();
}
```

### 2.2 Tool Input Schema Type Safety
**Update:** `src/COA.Mcp.Protocol/McpTypes.cs`
```csharp
public class Tool
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("inputSchema")]
    [JsonConverter(typeof(JsonDocumentConverter))]
    public JsonDocument InputSchema { get; set; } = null!;
}
```

**New converter:** `src/COA.Mcp.Protocol/Converters/JsonDocumentConverter.cs`
```csharp
public class JsonDocumentConverter : JsonConverter<JsonDocument>
{
    public override JsonDocument Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return JsonDocument.ParseValue(ref reader);
    }
    
    public override void Write(Utf8JsonWriter writer, JsonDocument value, JsonSerializerOptions options)
    {
        value.WriteTo(writer);
    }
}
```

## Phase 3: Request/Response Type Pattern
**Timeline: 3-4 days**

### 3.1 Typed Request Interface
**New file:** `src/COA.Mcp.Framework/Interfaces/ITypedRequest.cs`
```csharp
namespace COA.Mcp.Framework.Interfaces;

public interface ITypedRequest<TParams> where TParams : class
{
    TParams Parameters { get; }
    bool Validate(out ValidationResult result);
}

public interface ITypedResponse<TResult>
{
    TResult Result { get; }
    ResponseMetadata? Metadata { get; }
}
```

### 3.2 Update McpServer to Use Typed Requests
```csharp
private async Task<TResult> HandleTypedRequest<TParams, TResult>(
    JsonElement? parameters,
    Func<TParams, CancellationToken, Task<TResult>> handler,
    CancellationToken cancellationToken)
    where TParams : class, new()
{
    var typedParams = parameters?.Deserialize<TParams>(_jsonOptions) ?? new TParams();
    return await handler(typedParams, cancellationToken);
}
```

## Phase 4: Performance Optimizations
**Timeline: 1 week**

### 4.1 Pooled Buffer Serialization
```csharp
public static class SerializationHelper
{
    private static readonly ArrayPool<byte> Pool = ArrayPool<byte>.Shared;
    
    public static T DeserializePooled<T>(ReadOnlySpan<byte> data, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Deserialize<T>(data, options);
    }
    
    public static byte[] SerializePooled<T>(T value, JsonSerializerOptions? options = null)
    {
        using var stream = new ArrayPoolBufferWriter();
        using var writer = new Utf8JsonWriter(stream);
        JsonSerializer.Serialize(writer, value, options);
        return stream.WrittenMemory.ToArray();
    }
}
```

### 4.2 Caching Compiled Schemas
```csharp
public class SchemaCache
{
    private static readonly ConcurrentDictionary<Type, IJsonSchema> _cache = new();
    
    public static IJsonSchema GetOrCreate<T>() where T : class
    {
        return _cache.GetOrAdd(typeof(T), type => new JsonSchema<T>());
    }
}
```

## Phase 5: Source Generators (Future)
**Timeline: 2-3 weeks**

### 5.1 Schema Source Generator
Create source generator to produce JSON schemas at compile time:
- Analyze types marked with `[GenerateSchema]`
- Generate static schema definitions
- Eliminate runtime reflection

### 5.2 Protocol Binding Generator
Generate strongly-typed protocol bindings from MCP spec:
- Parse MCP specification
- Generate C# types
- Ensure spec compliance

## Testing Strategy

### Unit Tests Required
- [ ] Marker type serialization/deserialization
- [ ] JsonDocument conversion performance
- [ ] Typed request validation
- [ ] Schema caching behavior
- [ ] Buffer pooling under load

### Integration Tests Required
- [ ] End-to-end tool execution with new types
- [ ] Backward compatibility with existing tools
- [ ] Performance benchmarks before/after

### Performance Benchmarks
```csharp
[Benchmark]
public void OldDoubleSerialization()
{
    var obj = new TestParams { Value = "test" };
    var json = JsonSerializer.Serialize(obj);
    var result = JsonSerializer.Deserialize<TestParams>(json);
}

[Benchmark]
public void NewDirectConversion()
{
    var obj = new TestParams { Value = "test" };
    using var doc = JsonSerializer.SerializeToDocument(obj);
    var result = doc.Deserialize<TestParams>();
}
```

## Migration Guide for Consumers

### For Framework Users
No changes required - all improvements are backward compatible.

### For Protocol Users
Optional migration to use new marker types:
```csharp
// Old
serverCapabilities.Tools = new { };

// New (optional, more type-safe)
serverCapabilities.Tools = new ToolsCapabilityMarker();
```

## Success Metrics

### Performance Goals
- Reduce serialization overhead by 20-30%
- Eliminate double serialization in hot paths
- Reduce memory allocations by 25-40%

### Type Safety Goals
- Zero uses of `object` in new code
- All tool parameters strongly typed
- Compile-time validation where possible

### Developer Experience Goals
- Better IntelliSense support
- Clearer error messages
- Simpler debugging

## Risk Mitigation

### Backward Compatibility
- All changes maintain API compatibility
- Existing tools continue to work
- Progressive enhancement approach

### Performance Regression
- Benchmark before each change
- A/B testing in production
- Rollback plan for each phase

### Testing Coverage
- Maintain 100% test coverage
- Add performance regression tests
- Include edge cases and error paths

## Timeline Summary

| Phase | Duration | Impact | Risk |
|-------|----------|--------|------|
| Quick Wins | 1-2 days | High | Low |
| Schema Improvements | 2-3 days | Medium | Low |
| Request/Response Pattern | 3-4 days | High | Medium |
| Performance Optimizations | 1 week | High | Low |
| Source Generators | 2-3 weeks | Medium | Medium |

**Total Timeline**: 4-5 weeks for all phases
**Recommended Approach**: Implement phases 1-3 immediately, evaluate results, then proceed with 4-5.