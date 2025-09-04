using System;
using System.Collections.Generic;

namespace COA.Mcp.Framework.Models;

/// <summary>
/// Represents the verification state of a type, including when it was verified,
/// its source location, and cached member information.
/// </summary>
public class TypeVerificationState
{
    /// <summary>
    /// Gets or sets the name of the type.
    /// </summary>
    public string TypeName { get; set; } = "";

    /// <summary>
    /// Gets or sets the file path where the type is defined.
    /// </summary>
    public string FilePath { get; set; } = "";

    /// <summary>
    /// Gets or sets the timestamp when the type was verified.
    /// </summary>
    public DateTime VerifiedAt { get; set; }

    /// <summary>
    /// Gets or sets the file modification time when verification occurred.
    /// Used to invalidate cache when source files change.
    /// </summary>
    public long FileModificationTime { get; set; }

    /// <summary>
    /// Gets or sets the method used for verification (hover, definition, explicit, etc.).
    /// </summary>
    public VerificationMethod VerificationMethod { get; set; }

    /// <summary>
    /// Gets or sets the confidence score of the verification (0.0 to 1.0).
    /// </summary>
    public double ConfidenceScore { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the cached member information for this type.
    /// </summary>
    public Dictionary<string, MemberInfo> Members { get; set; } = new();

    /// <summary>
    /// Gets or sets the namespace the type belongs to.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Gets or sets the assembly or module name where the type is defined.
    /// </summary>
    public string? AssemblyName { get; set; }

    /// <summary>
    /// Gets or sets the base type name, if any.
    /// </summary>
    public string? BaseType { get; set; }

    /// <summary>
    /// Gets or sets the interfaces implemented by this type.
    /// </summary>
    public List<string> Interfaces { get; set; } = new();

    /// <summary>
    /// Gets or sets whether this is a generic type.
    /// </summary>
    public bool IsGeneric { get; set; }

    /// <summary>
    /// Gets or sets the generic type parameters if applicable.
    /// </summary>
    public List<string> GenericParameters { get; set; } = new();

    /// <summary>
    /// Gets or sets whether this type is an interface.
    /// </summary>
    public bool IsInterface { get; set; }

    /// <summary>
    /// Gets or sets whether this type is an abstract class.
    /// </summary>
    public bool IsAbstract { get; set; }

    /// <summary>
    /// Gets or sets whether this type is sealed.
    /// </summary>
    public bool IsSealed { get; set; }

    /// <summary>
    /// Gets or sets whether this type is static.
    /// </summary>
    public bool IsStatic { get; set; }

    /// <summary>
    /// Gets or sets additional metadata about the type.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the expiration time for this cache entry.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the number of times this type has been accessed.
    /// </summary>
    public int AccessCount { get; set; }

    /// <summary>
    /// Gets or sets the last time this type was accessed.
    /// </summary>
    public DateTime LastAccessedAt { get; set; }

    /// <summary>
    /// Checks if the verification is still valid based on file modification time and expiration.
    /// </summary>
    /// <param name="currentFileModificationTime">Current modification time of the source file.</param>
    /// <returns>True if the verification is still valid.</returns>
    public bool IsStillValid(long currentFileModificationTime)
    {
        // Check expiration
        if (ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value)
        {
            return false;
        }

        // Check file modification
        if (currentFileModificationTime > FileModificationTime)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if the verification is still valid with a fallback to time-based expiration.
    /// </summary>
    /// <param name="defaultExpirationHours">Default expiration in hours if file check fails.</param>
    /// <returns>True if the verification is still valid.</returns>
    public bool IsStillValid(int defaultExpirationHours = 24)
    {
        // Check expiration
        if (ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value)
        {
            return false;
        }

        // Fallback to time-based check if file modification time is not available
        if (FileModificationTime == 0)
        {
            return DateTime.UtcNow - VerifiedAt < TimeSpan.FromHours(defaultExpirationHours);
        }

        return true;
    }

    /// <summary>
    /// Records an access to this type for usage tracking.
    /// </summary>
    public void RecordAccess()
    {
        AccessCount++;
        LastAccessedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the expiration time based on the verification method and confidence.
    /// </summary>
    public void UpdateExpiration()
    {
        var baseHours = VerificationMethod switch
        {
            VerificationMethod.Hover => 24,
            VerificationMethod.GoToDefinition => 48,
            VerificationMethod.ExplicitVerification => 72,
            VerificationMethod.BulkVerification => 24,
            _ => 24
        };

        // Adjust based on confidence score
        var adjustedHours = (int)(baseHours * ConfidenceScore);
        ExpiresAt = DateTime.UtcNow.AddHours(Math.Max(adjustedHours, 1));
    }
}

/// <summary>
/// Represents information about a type member (property, method, field, etc.).
/// </summary>
public class MemberInfo
{
    /// <summary>
    /// Gets or sets the name of the member.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the type of member (Property, Method, Field, Event, etc.).
    /// </summary>
    public MemberType MemberType { get; set; }

    /// <summary>
    /// Gets or sets the return type or data type of the member.
    /// </summary>
    public string DataType { get; set; } = "";

    /// <summary>
    /// Gets or sets whether the member is public.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// Gets or sets whether the member is static.
    /// </summary>
    public bool IsStatic { get; set; }

    /// <summary>
    /// Gets or sets whether the member is read-only.
    /// </summary>
    public bool IsReadOnly { get; set; }

    /// <summary>
    /// Gets or sets the method signature for methods.
    /// </summary>
    public string? Signature { get; set; }

    /// <summary>
    /// Gets or sets the parameters for methods.
    /// </summary>
    public List<ParameterInfo> Parameters { get; set; } = new();

    /// <summary>
    /// Gets or sets documentation or summary for the member.
    /// </summary>
    public string? Documentation { get; set; }

    /// <summary>
    /// Gets or sets whether the member is deprecated.
    /// </summary>
    public bool IsDeprecated { get; set; }

    /// <summary>
    /// Gets or sets additional metadata for the member.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Represents information about a method or constructor parameter.
/// </summary>
public class ParameterInfo
{
    /// <summary>
    /// Gets or sets the parameter name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the parameter type.
    /// </summary>
    public string Type { get; set; } = "";

    /// <summary>
    /// Gets or sets whether the parameter is optional.
    /// </summary>
    public bool IsOptional { get; set; }

    /// <summary>
    /// Gets or sets the default value for optional parameters.
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// Gets or sets whether the parameter is a reference parameter (ref/out).
    /// </summary>
    public bool IsReference { get; set; }

    /// <summary>
    /// Gets or sets whether the parameter uses params keyword.
    /// </summary>
    public bool IsParams { get; set; }
}

/// <summary>
/// Comprehensive type information from LSP or similar source.
/// </summary>
public class TypeInfo
{
    /// <summary>
    /// Gets or sets the name of the type.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the full qualified name.
    /// </summary>
    public string FullName { get; set; } = "";

    /// <summary>
    /// Gets or sets the file path where the type is defined.
    /// </summary>
    public string FilePath { get; set; } = "";

    /// <summary>
    /// Gets or sets the line number where the type is defined.
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// Gets or sets the column number where the type is defined.
    /// </summary>
    public int ColumnNumber { get; set; }

    /// <summary>
    /// Gets or sets the namespace.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Gets or sets the assembly name.
    /// </summary>
    public string? AssemblyName { get; set; }

    /// <summary>
    /// Gets or sets the members of this type.
    /// </summary>
    public Dictionary<string, MemberInfo> Members { get; set; } = new();

    /// <summary>
    /// Gets or sets the base type.
    /// </summary>
    public string? BaseType { get; set; }

    /// <summary>
    /// Gets or sets the implemented interfaces.
    /// </summary>
    public List<string> Interfaces { get; set; } = new();

    /// <summary>
    /// Gets or sets type attributes and metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Enumeration of verification methods.
/// </summary>
public enum VerificationMethod
{
    /// <summary>
    /// Verified through hover information.
    /// </summary>
    Hover = 1,

    /// <summary>
    /// Verified by going to definition.
    /// </summary>
    GoToDefinition = 2,

    /// <summary>
    /// Verified through explicit verification command.
    /// </summary>
    ExplicitVerification = 3,

    /// <summary>
    /// Verified as part of bulk verification.
    /// </summary>
    BulkVerification = 4,

    /// <summary>
    /// Verified through symbol search.
    /// </summary>
    SymbolSearch = 5,

    /// <summary>
    /// Verified through type member exploration.
    /// </summary>
    MemberExploration = 6
}

/// <summary>
/// Enumeration of member types.
/// </summary>
public enum MemberType
{
    /// <summary>
    /// Property member.
    /// </summary>
    Property = 1,

    /// <summary>
    /// Method member.
    /// </summary>
    Method = 2,

    /// <summary>
    /// Field member.
    /// </summary>
    Field = 3,

    /// <summary>
    /// Event member.
    /// </summary>
    Event = 4,

    /// <summary>
    /// Constructor member.
    /// </summary>
    Constructor = 5,

    /// <summary>
    /// Indexer member.
    /// </summary>
    Indexer = 6,

    /// <summary>
    /// Operator member.
    /// </summary>
    Operator = 7,

    /// <summary>
    /// Nested type member.
    /// </summary>
    NestedType = 8
}