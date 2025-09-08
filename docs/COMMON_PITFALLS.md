# Common Pitfalls & Solutions

The top issues developers hit when building MCP servers and how to fix them fast.

## 🚨 "My tool isn't being found"

**Problem:** You registered a tool but Claude (or your client) says it doesn't exist.

**Common causes:**
```csharp
// ❌ Name doesn't match what you're calling
public override string Name => "my_tool";
// But you're calling "myTool" or "my-tool"

// ❌ Forgot to register the tool
var builder = new McpServerBuilder();
// Missing: builder.RegisterToolType<MyTool>();

// ❌ Tool class is not public
internal class MyTool : McpToolBase<...> // Won't work!
```

**✅ Solutions:**
```csharp
// Make sure Name matches exactly what you call
public override string Name => "echo"; // Call with "echo"

// Always register your tools
builder.RegisterToolType<MyTool>();

// Make your tool class public
public class MyTool : McpToolBase<MyParams, MyResult>
```

**Quick test:**
```bash
# List your tools to see what's actually registered
curl http://localhost:5000/tools/list
```

---

## 🔧 "Parameter validation is failing"

**Problem:** Getting validation errors even when sending valid data.

**Common causes:**
```csharp
// ❌ Using non-nullable for required fields
public class MyParams
{
    [Required]
    public int Count { get; set; } // Can't tell if 0 is valid or missing!
}

// ❌ Calling ValidateRequired on non-nullable
ValidateRequired(parameters.Count, nameof(parameters.Count)); // Always passes!
```

**✅ Solutions:**
```csharp
// Use nullable types for required parameters
public class MyParams
{
    [Required]
    public int? Count { get; set; } // Now we can detect missing values
    
    [Required]
    public string? Name { get; set; } // Strings should be nullable too
}

// Check nullable values properly
protected override async Task<MyResult> ExecuteInternalAsync(MyParams parameters, ...)
{
    ValidateRequired(parameters.Count, nameof(parameters.Count)); // Now works!
    ValidateRequired(parameters.Name, nameof(parameters.Name));
    
    // Use .Value after validation
    int count = parameters.Count!.Value;
    string name = parameters.Name!;
}
```

**Quick test:**
Send a request without the required field and check you get a proper error.

---

## 🌐 "Can't connect to my server"

**Problem:** Server starts but clients can't connect.

**Common causes:**
```csharp
// ❌ Wrong transport for your client
var builder = new McpServerBuilder()
    .WithServerInfo("My Server", "1.0.0");
// Uses STDIO, but you're trying to curl it

// ❌ Port already in use
.UseHttpTransport(options => options.Port = 5000);
// Something else is using port 5000
```

**✅ Solutions:**
```csharp
// Match transport to your client type
// For curl/HTTP clients:
.UseHttpTransport(options => options.Port = 5000);

// For Claude Desktop:
// Don't specify transport (uses STDIO by default)

// Check for port conflicts
.UseHttpTransport(options => options.Port = 5001); // Try different port
```

**Quick diagnostics:**
```bash
# Check if port is in use (Windows)
netstat -an | findstr :5000

# Check if port is in use (Linux/Mac)
lsof -i :5000

# Test connection
curl http://localhost:5000/health
```

---

## 📝 "Too much boilerplate code"

**Problem:** Need 3 classes for every simple tool.

**This is by design** - but here are shortcuts:

**✅ Copy-paste templates:**
```csharp
// Basic tool template - just change the names and logic
public class [TOOL_NAME] : McpToolBase<[TOOL_NAME]Params, [TOOL_NAME]Result>
{
    public override string Name => "[tool_name]";
    public override string Description => "[What this tool does]";
    
    protected override async Task<[TOOL_NAME]Result> ExecuteInternalAsync(
        [TOOL_NAME]Params parameters, CancellationToken cancellationToken)
    {
        // TODO: Your logic here
        await Task.CompletedTask;
        
        return new [TOOL_NAME]Result 
        { 
            Success = true,
            // TODO: Set your result properties
        };
    }
}

public class [TOOL_NAME]Params 
{ 
    public string? SomeInput { get; set; }
}

public class [TOOL_NAME]Result : ToolResultBase 
{ 
    public override string Operation => "[tool_name]";
    public string? SomeOutput { get; set; }
}
```

**Use snippets:** Check `/docs/snippets/` for copy-paste templates for common scenarios.

---

## 🎯 "Result type errors"

**Problem:** Compiler errors about result types or ToolResultBase.

**Common causes:**
```csharp
// ❌ Forgot to inherit from ToolResultBase
public class MyResult // Missing base class!
{
    public bool Success { get; set; }
}

// ❌ Didn't implement Operation property
public class MyResult : ToolResultBase
{
    // Missing: public override string Operation => "my_tool";
}

// ❌ Wrong generic type in tool
public class MyTool : McpToolBase<MyParams, string> // Should be MyResult!
```

**✅ Solutions:**
```csharp
// Always inherit from ToolResultBase
public class MyResult : ToolResultBase
{
    public override string Operation => "my_tool"; // Required!
    
    // Your custom properties
    public string? Data { get; set; }
    public int Count { get; set; }
}

// Use your result type in the tool
public class MyTool : McpToolBase<MyParams, MyResult>
```

---

## ⏱️ "Async/await confusion"

**Problem:** Compiler warnings or errors about async methods.

**Common causes:**
```csharp
// ❌ Async method with no await
protected override async Task<MyResult> ExecuteInternalAsync(...)
{
    return new MyResult { Success = true }; // Warning: no await!
}

// ❌ Synchronous code in async method
protected override async Task<MyResult> ExecuteInternalAsync(...)
{
    var result = SomeSlowOperation(); // Should be awaited
    return new MyResult { Data = result };
}
```

**✅ Solutions:**
```csharp
// If you're not doing async work, add this line:
protected override async Task<MyResult> ExecuteInternalAsync(...)
{
    await Task.CompletedTask; // Keeps compiler happy
    
    return new MyResult { Success = true };
}

// If you ARE doing async work, await it with ConfigureAwait(false):
protected override async Task<MyResult> ExecuteInternalAsync(...)
{
    var result = await SomeSlowOperationAsync().ConfigureAwait(false);
    var data = await File.ReadAllTextAsync("file.txt").ConfigureAwait(false);
    return new MyResult { Data = result };
}
```

**🚨 ConfigureAwait(false) Best Practice:**

Always use `ConfigureAwait(false)` on awaited calls in MCP framework code to prevent deadlocks:

```csharp
// ✅ Good - prevents deadlocks in sync-over-async scenarios
protected override async Task<MyResult> ExecuteInternalAsync(...)
{
    var client = new HttpClient();
    var response = await client.GetStringAsync("https://api.example.com")
        .ConfigureAwait(false);
        
    var fileContent = await File.ReadAllTextAsync("data.txt")
        .ConfigureAwait(false);
        
    return new MyResult { Data = response };
}

// ❌ Bad - can cause deadlocks if called from synchronous context
protected override async Task<MyResult> ExecuteInternalAsync(...)
{
    var response = await client.GetStringAsync("url"); // Missing ConfigureAwait(false)
    return new MyResult { Data = response };
}
```

**Why ConfigureAwait(false)?**
- Prevents continuation from running on the original synchronization context
- Essential for library code (like MCP tools) that might be called from sync contexts
- Improves performance by avoiding unnecessary context switches
- The COA MCP Framework itself uses this pattern throughout

---

## 🏗️ "Service provider / DI confusion"

**Problem:** Errors about IServiceProvider or dependency injection.

**The simple answer:** **You probably don't need DI for basic tools.**

```csharp
// ✅ Simple tool - no DI needed
public class SimpleTool : McpToolBase<SimpleParams, SimpleResult>
{
    // No constructor needed - just implement the abstract members
    public override string Name => "simple";
    public override string Description => "A simple tool";
    
    protected override async Task<SimpleResult> ExecuteInternalAsync(...)
    {
        // Your logic here
    }
}

// ✅ If you DO need services, use constructor injection
public class ComplexTool : McpToolBase<ComplexParams, ComplexResult>
{
    private readonly IMyService _service;
    
    public ComplexTool(IMyService service) // DI will provide this
    {
        _service = service;
    }
    
    // Then register the service in Program.cs:
    // builder.Services.AddScoped<IMyService, MyService>();
}
```

---

## 🔍 "JSON serialization issues"

**Problem:** Data isn't serializing/deserializing correctly.

**Common causes:**
```csharp
// ❌ Private properties won't serialize
public class MyParams
{
    private string Name { get; set; } // Won't work!
}

// ❌ No setter
public class MyResult : ToolResultBase
{
    public string Data { get; } // Won't deserialize!
}

// ❌ Constructor parameters
public class MyParams
{
    public MyParams(string name) { Name = name; } // JSON can't create this!
    public string Name { get; set; }
}
```

**✅ Solutions:**
```csharp
// Public properties with getters and setters
public class MyParams
{
    public string? Name { get; set; } // Perfect!
    public int? Count { get; set; }
}

public class MyResult : ToolResultBase
{
    public override string Operation => "my_tool";
    public string? Data { get; set; } // Perfect!
    public List<string>? Items { get; set; }
}

// Parameterless constructor (automatic for simple properties)
// No constructor needed - C# creates one automatically
```

---

## 💡 Quick Debugging Tips

### 1. Check what tools are registered
```bash
curl http://localhost:5000/tools/list
# Or add logging to see registration:
Console.WriteLine($"Registered tool: {typeof(MyTool).Name}");
```

### 2. Test parameter parsing
```csharp
// Add logging in your tool
protected override async Task<MyResult> ExecuteInternalAsync(MyParams parameters, ...)
{
    Console.WriteLine($"Received: {JsonSerializer.Serialize(parameters)}");
    // Your logic here
}
```

### 3. Verify server startup
```csharp
static async Task Main(string[] args)
{
    Console.WriteLine("🚀 Server starting...");
    
    var builder = new McpServerBuilder()
        .WithServerInfo("My Server", "1.0.0");
    
    Console.WriteLine("📝 Registering tools...");
    builder.RegisterToolType<MyTool>();
    
    Console.WriteLine("✅ Ready! Press Ctrl+C to stop");
    await builder.RunAsync();
}
```

---

## 🆘 Still Stuck?

**Check these resources:**

1. **[QUICKSTART.md](../QUICKSTART.md)** - Working example in 5 minutes
2. **[examples/1-HelloWorld/](../examples/1-HelloWorld/)** - Simplest possible server
3. **[WHICH_TRANSPORT.md](WHICH_TRANSPORT.md)** - Transport selection guide
4. **[docs/snippets/](snippets/)** - Copy-paste templates

**Common debugging sequence:**
1. Does `dotnet build` work without warnings?
2. Does `dotnet run` start without errors?
3. Can you list tools? (curl or API test)
4. Is your tool name exactly right?
5. Are your parameter types nullable where needed?

**Still having issues?** Check the GitHub issues or create a minimal reproduction case.