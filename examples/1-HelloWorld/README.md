# Hello World MCP Server

The **simplest possible MCP server** that actually works. Everything is in one file with no complexity.

## What This Does

This server has one tool called `hello` that takes a name and says hello to them. That's it!

## Quick Start

1. **Run the server:**
   ```bash
   dotnet run
   ```

2. **Test it works** (in another terminal):
   ```bash
   # The server is now listening for MCP requests on stdin/stdout
   # You can integrate it with Claude Desktop or test with curl if using HTTP
   ```

## The Code Explained

**Everything is in Program.cs:**

- `HelloTool` - Your MCP tool that does the work
- `HelloParams` - What data the tool needs (just a name)
- `HelloResult` - What the tool returns (a greeting message)
- `Main()` - Creates and runs the server

**That's literally it!** No dependency injection, no middleware, no configuration files. Just working code.

## Next Steps

Once this works, check out:
- `../2-BasicTools/` - Multiple tools with validation
- `../3-WithServices/` - When you actually need dependency injection
- `../SimpleMcpServer/` - Full-featured example with all the bells and whistles

## Troubleshooting

**Server won't start?**
- Make sure you have .NET 8+ installed: `dotnet --version`
- Check the project builds: `dotnet build`

**Tool not working?**
- The tool name is "hello" - make sure you're calling it correctly
- Check the server logs in the console

**Want HTTP instead of STDIO?**
See `../docs/WHICH_TRANSPORT.md` for transport selection guide.