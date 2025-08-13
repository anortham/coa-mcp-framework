using System;

namespace COA.Mcp.Framework.TokenOptimization.AdaptiveResponse.Models;

/// <summary>
/// Enumeration of supported IDE types for adaptive response formatting.
/// </summary>
public enum IDEType
{
    /// <summary>
    /// Unknown or unsupported IDE environment.
    /// </summary>
    Unknown,
    
    /// <summary>
    /// Visual Studio Code.
    /// </summary>
    VSCode,
    
    /// <summary>
    /// Visual Studio 2022.
    /// </summary>
    VS2022,
    
    /// <summary>
    /// Terminal or command-line environment.
    /// </summary>
    Terminal,
    
    /// <summary>
    /// Browser-based environment.
    /// </summary>
    Browser
}

/// <summary>
/// Represents the detected IDE environment and its capabilities for adaptive response formatting.
/// </summary>
public class IDEEnvironment
{
    /// <summary>
    /// Gets or sets the detected IDE type.
    /// </summary>
    public IDEType IDE { get; set; } = IDEType.Unknown;
    
    /// <summary>
    /// Gets or sets whether the environment supports HTML rendering.
    /// </summary>
    public bool SupportsHTML { get; set; }
    
    /// <summary>
    /// Gets or sets whether the environment supports Markdown rendering.
    /// </summary>
    public bool SupportsMarkdown { get; set; }
    
    /// <summary>
    /// Gets or sets whether the environment supports interactive elements.
    /// </summary>
    public bool SupportsInteractive { get; set; }
    
    /// <summary>
    /// Gets or sets whether the environment supports WebView components.
    /// </summary>
    public bool SupportsWebView { get; set; }
    
    /// <summary>
    /// Gets or sets the version of the IDE.
    /// </summary>
    public string Version { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets additional environment information.
    /// </summary>
    public Dictionary<string, string> AdditionalInfo { get; set; } = new();
    
    /// <summary>
    /// Detects the current IDE environment based on environment variables and process information.
    /// </summary>
    /// <returns>A new IDEEnvironment instance representing the detected environment.</returns>
    public static IDEEnvironment Detect()
    {
        var vsCodePid = Environment.GetEnvironmentVariable("VSCODE_PID");
        var vsVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");
        var termProgram = Environment.GetEnvironmentVariable("TERM_PROGRAM");
        var ghCopilotChat = Environment.GetEnvironmentVariable("GITHUB_COPILOT_CHAT");
        
        // VS Code detection
        if (!string.IsNullOrEmpty(vsCodePid))
        {
            return new IDEEnvironment
            {
                IDE = IDEType.VSCode,
                SupportsHTML = true,
                SupportsMarkdown = true,
                SupportsInteractive = true,
                SupportsWebView = true,
                Version = Environment.GetEnvironmentVariable("VSCODE_VERSION") ?? "unknown",
                AdditionalInfo = new Dictionary<string, string>
                {
                    ["PID"] = vsCodePid,
                    ["CopilotChat"] = ghCopilotChat ?? "unknown"
                }
            };
        }
        
        // Visual Studio 2022 detection
        if (!string.IsNullOrEmpty(vsVersion))
        {
            return new IDEEnvironment
            {
                IDE = IDEType.VS2022,
                SupportsHTML = true,
                SupportsMarkdown = true,
                SupportsInteractive = true,
                SupportsWebView = false, // Limited webview support in VS 2022
                Version = vsVersion,
                AdditionalInfo = new Dictionary<string, string>
                {
                    ["VSVersion"] = vsVersion,
                    ["CopilotChat"] = ghCopilotChat ?? "unknown"
                }
            };
        }
        
        // Terminal/Claude Code detection
        return new IDEEnvironment
        {
            IDE = IDEType.Terminal,
            SupportsHTML = false,
            SupportsMarkdown = true,
            SupportsInteractive = false,
            SupportsWebView = false,
            Version = termProgram ?? "unknown",
            AdditionalInfo = new Dictionary<string, string>
            {
                ["TermProgram"] = termProgram ?? "unknown",
                ["Shell"] = Environment.GetEnvironmentVariable("SHELL") ?? "unknown"
            }
        };
    }
    
    /// <summary>
    /// Gets a display-friendly name for the IDE environment.
    /// </summary>
    public string GetDisplayName()
    {
        return IDE switch
        {
            IDEType.VSCode => $"VS Code {Version}",
            IDEType.VS2022 => $"Visual Studio {Version}",
            IDEType.Terminal => $"Terminal ({Version})",
            IDEType.Browser => $"Browser ({Version})",
            _ => "Unknown Environment"
        };
    }
    
    /// <summary>
    /// Determines if the environment should use rich visual formatting.
    /// </summary>
    public bool ShouldUseRichFormatting() => SupportsHTML && SupportsInteractive;
    
    /// <summary>
    /// Gets the preferred resource format for large data sets.
    /// </summary>
    public string GetPreferredResourceFormat()
    {
        return IDE switch
        {
            IDEType.VSCode => "html",
            IDEType.VS2022 => "html",
            IDEType.Browser => "html",
            _ => "json"
        };
    }
}