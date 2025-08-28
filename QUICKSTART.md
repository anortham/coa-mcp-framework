# 5-Minute MCP Server Quickstart

Get a working MCP server running in **5 minutes or less**. No prior knowledge needed.

## Step 1: Create a New Project (1 minute)

```bash
# Create a new console app
dotnet new console -n MyMcpServer
cd MyMcpServer

# Add the MCP framework
dotnet add package COA.Mcp.Framework
```

## Step 2: Replace Program.cs (2 minutes)

Delete everything in `Program.cs` and paste this:

```csharp
using COA.Mcp.Framework.Server;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;

// Your MCP tool - does one simple thing
public class EchoTool : McpToolBase<EchoParams, EchoResult>
{
    public override string Name => "echo";
    public override string Description => "Echoes back whatever you send";
    
    protected override async Task<EchoResult> ExecuteInternalAsync(
        EchoParams parameters, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Keep compiler happy
        
        return new EchoResult 
        { 
            Success = true, 
            Response = $"You said: {parameters.Text}" 
        };
    }
}

public class EchoParams { public string Text { get; set; } = ""; }
public class EchoResult : ToolResultBase 
{ 
    public override string Operation => "echo";
    public string Response { get; set; } = "";
}

// Run the server
class Program
{
    static async Task Main(string[] args)
    {
        var builder = new McpServerBuilder()
            .WithServerInfo("My First MCP Server", "1.0.0");
        
        builder.RegisterToolType<EchoTool>();
        
        Console.WriteLine("🚀 MCP Server starting... Press Ctrl+C to stop");
        await builder.RunAsync();
    }
}
```

## Step 3: Run It (1 minute)

```bash
dotnet run
```

You should see:
```
🚀 MCP Server starting... Press Ctrl+C to stop
[MCP server is now listening for requests]
```

**🎉 Congratulations!** You have a working MCP server.

## Step 4: What Just Happened? (1 minute)

You created:
- **One tool** called "echo" that repeats back what you send it
- **Input parameters** (EchoParams) - what data the tool needs
- **Output result** (EchoResult) - what the tool returns
- **A server** that listens for MCP requests and runs your tool

## Step 5: Next Steps

**Want to integrate with Claude Desktop?** 
- Add your server to Claude's config - see [Claude Desktop Integration](#claude-desktop-integration) below

**Want to add more tools?**
- Copy the `EchoTool` pattern and change the logic
- Register it with `builder.RegisterToolType<YourNewTool>()`

**Want to see more examples?**
- Check `examples/1-HelloWorld/` - Even simpler example
- Check `examples/2-BasicTools/` - Multiple tools with validation
- Check `examples/SimpleMcpServer/` - Full-featured server

**Having issues?**
- See [COMMON_PITFALLS.md](docs/COMMON_PITFALLS.md) for solutions to common problems

## Claude Desktop Integration

To use your server with Claude Desktop, add this to Claude's configuration file:

**Windows:** `%APPDATA%\Claude\claude_desktop_config.json`
**Mac:** `~/Library/Application Support/Claude/claude_desktop_config.json`

```json
{
  "mcpServers": {
    "my-mcp-server": {
      "command": "dotnet",
      "args": ["run", "--project", "C:/path/to/MyMcpServer"],
      "env": {}
    }
  }
}
```

Replace `C:/path/to/MyMcpServer` with your actual project path.

## HTTP Mode (Optional)

Want to test with HTTP instead of STDIO? Change your Program.cs:

```csharp
var builder = new McpServerBuilder()
    .WithServerInfo("My First MCP Server", "1.0.0")
    .UseHttpTransport(options =>
    {
        options.Port = 5000;
        options.EnableCors = true;
    });
```

Then test with curl:
```bash
curl -X POST http://localhost:5000/tools/echo \
  -H "Content-Type: application/json" \
  -d '{"text": "Hello World"}'
```

---

**That's it!** You now have a working MCP server. Time to build something amazing! 🚀

**Questions?** Check out:
- [Full Documentation](docs/README.md)
- [Example Projects](examples/)
- [Troubleshooting Guide](docs/COMMON_PITFALLS.md)