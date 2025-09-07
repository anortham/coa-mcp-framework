using System;
using System.Collections;
using System.Linq;

namespace COA.Mcp.Framework.Services;

/// <summary>
/// Provides helper functions for Scriban templates that can be safely imported
/// using Scriban's native Import mechanism. These functions handle common
/// template operations with proper type handling.
/// </summary>
public static class TemplateHelperFunctions
{
    /// <summary>
    /// Checks if a collection of tools contains a specific tool name.
    /// </summary>
    /// <param name="tools">Collection of available tools</param>
    /// <param name="tool">Tool name to search for</param>
    /// <returns>True if the tool is found, false otherwise</returns>
    public static bool HasTool(IEnumerable tools, string tool)
    {
        if (tools == null || string.IsNullOrEmpty(tool)) 
            return false;
        
        return tools.Cast<object>()
            .Any(t => t?.ToString()?.Equals(tool, StringComparison.OrdinalIgnoreCase) == true);
    }
    
    /// <summary>
    /// Checks if a collection of markers contains a specific marker name.
    /// </summary>
    /// <param name="markers">Collection of available markers</param>
    /// <param name="marker">Marker name to search for</param>
    /// <returns>True if the marker is found, false otherwise</returns>
    public static bool HasMarker(IEnumerable markers, string marker)
    {
        if (markers == null || string.IsNullOrEmpty(marker)) 
            return false;
        
        return markers.Cast<object>()
            .Any(m => m?.ToString()?.Equals(marker, StringComparison.OrdinalIgnoreCase) == true);
    }
    
    /// <summary>
    /// Checks if a collection of built-in tools contains a specific tool name.
    /// </summary>
    /// <param name="builtins">Collection of built-in tools</param>
    /// <param name="tool">Tool name to search for</param>
    /// <returns>True if the built-in tool is found, false otherwise</returns>
    public static bool HasBuiltin(IEnumerable builtins, string tool)
    {
        if (builtins == null || string.IsNullOrEmpty(tool)) 
            return false;
        
        return builtins.Cast<object>()
            .Any(b => b?.ToString()?.Equals(tool, StringComparison.OrdinalIgnoreCase) == true);
    }
}