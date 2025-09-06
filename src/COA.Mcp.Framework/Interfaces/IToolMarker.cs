using System;

namespace COA.Mcp.Framework.Interfaces;

/// <summary>
/// Base marker interface for categorizing MCP tools by their capabilities.
/// Tool markers enable sophisticated conditional instruction generation based on
/// what capabilities are available in the current server instance.
/// </summary>
/// <remarks>
/// - Professional guidance
/// - Transparent capability advertisement
/// - User choice and control maintained
/// </remarks>
public interface IToolMarker
{
    // Marker interfaces are just tags - no methods needed
    // They serve as compile-time and runtime capability indicators
}

/// <summary>
/// Marker interface indicating that a tool can perform editing operations on files.
/// Used for conditional instruction generation about code modification workflows.
/// </summary>
public interface ICanEdit : IToolMarker
{
    // No additional methods - this is purely a capability marker
}

/// <summary>
/// Marker interface indicating that a tool performs symbolic read operations
/// (reading specific symbols, methods, classes rather than entire files).
/// Used to recommend symbol-based workflows over file-based approaches.
/// </summary>
public interface ISymbolicRead : IToolMarker
{
    // No additional methods - this is purely a capability marker
}

/// <summary>
/// Marker interface indicating that a tool performs symbolic editing operations
/// (modifying specific symbols, methods, classes with precision).
/// Combines both ICanEdit and ISymbolicRead capabilities.
/// </summary>
public interface ISymbolicEdit : ICanEdit, ISymbolicRead
{
    // No additional methods - inherits capabilities from both parent interfaces
}

/// <summary>
/// Marker interface indicating that a tool is optional and disabled by default.
/// Used in conditional instructions to mention advanced capabilities when available.
/// </summary>
public interface IOptional : IToolMarker
{
    // No additional methods - this is purely a capability marker
}

/// <summary>
/// Marker interface indicating that a tool does not require an active project context.
/// Used for workspace-independent tools like system information or general utilities.
/// </summary>
public interface INoActiveProject : IToolMarker
{
    // No additional methods - this is purely a capability marker
}

/// <summary>
/// Marker interface indicating that a tool provides type-aware operations
/// (understanding language-specific type systems and relationships).
/// Used to promote type-safe development workflows.
/// </summary>
public interface ITypeAware : IToolMarker
{
    // No additional methods - this is purely a capability marker
}

/// <summary>
/// Marker interface indicating that a tool can perform bulk operations
/// on multiple files or large datasets efficiently.
/// Used to recommend batch processing workflows for performance.
/// </summary>
public interface IBulkOperation : IToolMarker
{
    // No additional methods - this is purely a capability marker
}

/// <summary>
/// Marker interface indicating that a tool provides real-time or interactive capabilities
/// (progress updates, cancellation support, streaming results).
/// Used in instructions about long-running operations.
/// </summary>
public interface IInteractive : IToolMarker
{
    // No additional methods - this is purely a capability marker
}

/// <summary>
/// Marker interface indicating that a tool requires workspace indexing
/// or other preparation steps before optimal performance.
/// Used to generate "essential first step" guidance.
/// </summary>
public interface IRequiresPreparation : IToolMarker
{
    // No additional methods - this is purely a capability marker
}