using System.CommandLine;
using COA.Mcp.Framework.CLI.Commands;
using Spectre.Console;

// Create root command
var rootCommand = new RootCommand("COA MCP Framework CLI - Tools for MCP server development");

// Add commands
rootCommand.AddCommand(new NewCommand());
rootCommand.AddCommand(new ValidateCommand());
rootCommand.AddCommand(new TestTokensCommand());
rootCommand.AddCommand(new MigrateCommand());

// Show banner
AnsiConsole.Write(
    new FigletText("COA MCP CLI")
        .LeftJustified()
        .Color(Color.Blue));

// Execute command
return await rootCommand.InvokeAsync(args);