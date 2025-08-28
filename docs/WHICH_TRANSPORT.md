# Which Transport Should I Use?

**TL;DR:** Not sure? Use the default (STDIO). It works with Claude Desktop and most MCP clients.

## Quick Decision Tree

```
┌─ Building for Claude Desktop? ──── YES ──→ STDIO ✓
│
├─ Need a web API? ──────────────── YES ──→ HTTP
│
├─ Need real-time updates? ──────── YES ──→ WebSocket
│
└─ Just experimenting? ──────────── ??? ──→ STDIO (default)
```

## Transport Options Explained

### 🔌 STDIO Transport (Default - Recommended)

**What it is:** Your MCP server communicates through standard input/output (like a command-line program).

**✅ Use STDIO when:**
- Integrating with Claude Desktop
- Running your server as a subprocess
- Simple request/response pattern is enough
- You want the easiest setup

**❌ Don't use STDIO when:**
- You need direct HTTP access
- Multiple clients need to connect
- You're building a web service

**Code example:**
```csharp
// This is the default - no extra code needed!
var builder = new McpServerBuilder()
    .WithServerInfo("My Server", "1.0.0");
// Uses STDIO automatically
```

**How to test:**
```bash
# Run your server
dotnet run

# Test with echo (Linux/Mac) or type (Windows)
echo '{"jsonrpc":"2.0","method":"tools/list","id":1}' | dotnet run
```

### 🌐 HTTP Transport

**What it is:** Your MCP server runs as a web API that accepts HTTP requests.

**✅ Use HTTP when:**
- Building a REST-like API
- Need authentication/authorization
- Want to test with curl/Postman
- Multiple clients need access
- Integrating with web applications

**❌ Don't use HTTP when:**
- You only need Claude Desktop integration
- You want the simplest possible setup

**Code example:**
```csharp
var builder = new McpServerBuilder()
    .WithServerInfo("My Server", "1.0.0")
    .UseHttpTransport(options =>
    {
        options.Port = 5000;
        options.EnableCors = true; // Allow web browser access
        options.EnableWebSocket = false; // HTTP only
    });
```

**How to test:**
```bash
# List available tools
curl http://localhost:5000/tools/list

# Call a specific tool
curl -X POST http://localhost:5000/tools/echo \
  -H "Content-Type: application/json" \
  -d '{"text": "Hello World"}'
```

### ⚡ WebSocket Transport

**What it is:** Real-time bidirectional communication between client and server.

**✅ Use WebSocket when:**
- You need real-time updates
- Server needs to push data to clients
- Long-running connections
- Building interactive applications

**❌ Don't use WebSocket when:**
- Simple request/response is enough
- You want to keep it simple
- One-way communication is sufficient

**Code example:**
```csharp
var builder = new McpServerBuilder()
    .WithServerInfo("My Server", "1.0.0")
    .UseWebSocketTransport(options =>
    {
        options.Port = 8080;
        options.Host = "localhost";
        options.UseHttps = false; // Set true for production
    });
```

**How to test:**
Use a WebSocket client tool or JavaScript:
```javascript
const ws = new WebSocket('ws://localhost:8080');
ws.send('{"jsonrpc":"2.0","method":"tools/list","id":1}');
```

## Migration Between Transports

**Good news:** You can change transports without changing your tools! Just modify the server builder:

```csharp
// From STDIO (default)
var builder = new McpServerBuilder()
    .WithServerInfo("My Server", "1.0.0");

// To HTTP
var builder = new McpServerBuilder()
    .WithServerInfo("My Server", "1.0.0")
    .UseHttpTransport(options => options.Port = 5000);

// Your tools stay exactly the same!
builder.RegisterToolType<MyTool>();
```

## Common Scenarios

### Scenario: "I want to use this with Claude Desktop"
**Answer:** Use STDIO (default). Add your server to Claude's config file.

### Scenario: "I want to test my tools with curl"
**Answer:** Use HTTP transport with `options.Port = 5000`.

### Scenario: "I need both Claude Desktop AND web access"
**Answer:** Create two servers or use HTTP with a wrapper script for Claude.

### Scenario: "My server needs to send updates to clients"
**Answer:** Use WebSocket transport for real-time communication.

### Scenario: "I'm not sure what I need"
**Answer:** Start with STDIO (default). You can change later without touching your tools.

## Configuration Examples

### Minimal STDIO (Default)
```csharp
var builder = new McpServerBuilder()
    .WithServerInfo("My Server", "1.0.0");
// No transport configuration needed
```

### Production HTTP
```csharp
var builder = new McpServerBuilder()
    .WithServerInfo("My Server", "1.0.0")
    .UseHttpTransport(options =>
    {
        options.Port = 443;
        options.UseHttps = true;
        options.EnableCors = true;
        options.Authentication = AuthenticationType.ApiKey;
        options.ApiKey = Environment.GetEnvironmentVariable("API_KEY");
    });
```

### Development WebSocket
```csharp
var builder = new McpServerBuilder()
    .WithServerInfo("My Server", "1.0.0")
    .UseWebSocketTransport(options =>
    {
        options.Port = 8080;
        options.Host = "localhost";
        options.UseHttps = false;
        options.EnableHeartbeat = true;
    });
```

## Troubleshooting

**"Connection refused" errors:**
- Check the port isn't already in use
- Verify firewall settings
- Make sure you're connecting to the right transport type

**"Server not responding":**
- STDIO: Check your client is sending proper JSON-RPC
- HTTP: Test with curl first
- WebSocket: Verify the WebSocket handshake

**"Can't integrate with Claude Desktop":**
- Must use STDIO transport
- Check Claude's config file syntax
- Verify the command path is correct

---

**Still confused?** Start with STDIO (the default) - it's the most widely supported and easiest to set up. You can always change later!