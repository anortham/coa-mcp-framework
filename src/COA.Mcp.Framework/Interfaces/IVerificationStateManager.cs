using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using COA.Mcp.Framework.Models;

namespace COA.Mcp.Framework.Interfaces;

/// <summary>
/// Manages the verification state of types and members across tool executions.
/// Maintains a session-scoped cache of verified types and their members,
/// with automatic invalidation based on file modifications.
/// </summary>
public interface IVerificationStateManager
{
    /// <summary>
    /// Checks if a type has been verified in the current session.
    /// </summary>
    /// <param name="typeName">The name of the type to check.</param>
    /// <returns>True if the type has been verified and verification is still valid.</returns>
    Task<bool> IsTypeVerifiedAsync(string typeName);

    /// <summary>
    /// Marks a type as verified with detailed type information.
    /// </summary>
    /// <param name="typeName">The name of the type.</param>
    /// <param name="typeInfo">Detailed information about the type.</param>
    Task MarkTypeVerifiedAsync(string typeName, TypeInfo typeInfo);

    /// <summary>
    /// Checks if a specific member of a type has been verified.
    /// </summary>
    /// <param name="typeName">The name of the type.</param>
    /// <param name="memberName">The name of the member.</param>
    /// <returns>True if the member has been verified.</returns>
    Task<bool> HasVerifiedMemberAsync(string typeName, string memberName);

    /// <summary>
    /// Gets all available members for a verified type.
    /// </summary>
    /// <param name="typeName">The name of the type.</param>
    /// <returns>Collection of available member names, or null if type not verified.</returns>
    Task<IEnumerable<string>?> GetAvailableMembersAsync(string typeName);

    /// <summary>
    /// Gets the current verification status for a type.
    /// </summary>
    /// <param name="typeName">The name of the type.</param>
    /// <returns>The verification state, or null if not found.</returns>
    Task<TypeVerificationState?> GetVerificationStatusAsync(string typeName);

    /// <summary>
    /// Clears the verification cache, optionally filtering by pattern.
    /// </summary>
    /// <param name="pattern">Optional pattern to match type names (supports wildcards).</param>
    Task ClearCacheAsync(string? pattern = null);

    /// <summary>
    /// Gets statistics about the current cache state.
    /// </summary>
    /// <returns>Cache statistics including size, hit rate, etc.</returns>
    Task<VerificationCacheStatistics> GetCacheStatisticsAsync();

    /// <summary>
    /// Logs a successful verification operation.
    /// </summary>
    /// <param name="toolName">The name of the tool that performed verification.</param>
    /// <param name="filePath">The file path being operated on.</param>
    /// <param name="verifiedTypes">The types that were successfully verified.</param>
    Task LogVerificationSuccessAsync(string toolName, string filePath, IList<string> verifiedTypes);

    /// <summary>
    /// Logs a failed verification operation.
    /// </summary>
    /// <param name="toolName">The name of the tool that attempted verification.</param>
    /// <param name="filePath">The file path being operated on.</param>
    /// <param name="unverifiedTypes">The types that could not be verified.</param>
    /// <param name="memberIssues">The types with member access issues.</param>
    Task LogVerificationFailureAsync(string toolName, string filePath, IList<string> unverifiedTypes, IList<string> memberIssues);

    /// <summary>
    /// Preloads type information for a file or project to warm the cache.
    /// </summary>
    /// <param name="filePath">The file path to analyze and preload types for.</param>
    /// <param name="includeReferences">Whether to include referenced types.</param>
    Task WarmCacheAsync(string filePath, bool includeReferences = false);

    /// <summary>
    /// Invalidates cached types based on file modification events.
    /// </summary>
    /// <param name="changedFilePath">The file that was modified.</param>
    Task InvalidateCacheForFileAsync(string changedFilePath);

    /// <summary>
    /// Gets all types that need verification for the given code content.
    /// </summary>
    /// <param name="code">The code to analyze.</param>
    /// <param name="filePath">The file path for context.</param>
    /// <returns>List of type references that need verification.</returns>
    Task<IList<TypeReference>> GetUnverifiedTypesAsync(string code, string filePath);

    /// <summary>
    /// Bulk verification of multiple types at once.
    /// </summary>
    /// <param name="typeNames">The types to verify.</param>
    /// <param name="workspaceRoot">The workspace root for context.</param>
    /// <returns>Dictionary mapping type names to their verification success status.</returns>
    Task<IDictionary<string, bool>> BulkVerifyTypesAsync(IList<string> typeNames, string workspaceRoot);
}

/// <summary>
/// Represents a type reference found in code that needs verification.
/// </summary>
public class TypeReference
{
    /// <summary>
    /// Gets or sets the name of the type.
    /// </summary>
    public string TypeName { get; set; } = "";

    /// <summary>
    /// Gets or sets the name of the member being accessed, if any.
    /// </summary>
    public string? MemberName { get; set; }

    /// <summary>
    /// Gets or sets the context in which the type was found.
    /// </summary>
    public string Context { get; set; } = "";

    /// <summary>
    /// Gets or sets the line number where the type was found.
    /// </summary>
    public int? LineNumber { get; set; }

    /// <summary>
    /// Gets or sets the column number where the type was found.
    /// </summary>
    public int? ColumnNumber { get; set; }
}

/// <summary>
/// Provides statistics about the verification cache.
/// </summary>
public class VerificationCacheStatistics
{
    /// <summary>
    /// Gets or sets the total number of cached types.
    /// </summary>
    public int TotalTypes { get; set; }

    /// <summary>
    /// Gets or sets the number of cache hits.
    /// </summary>
    public long CacheHits { get; set; }

    /// <summary>
    /// Gets or sets the number of cache misses.
    /// </summary>
    public long CacheMisses { get; set; }

    /// <summary>
    /// Gets or sets the cache hit rate as a percentage.
    /// </summary>
    public double HitRate => CacheHits + CacheMisses > 0 
        ? (double)CacheHits / (CacheHits + CacheMisses) * 100 
        : 0;

    /// <summary>
    /// Gets or sets the memory usage in bytes.
    /// </summary>
    public long MemoryUsageBytes { get; set; }

    /// <summary>
    /// Gets or sets the time when statistics were last updated.
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the number of types that have expired and need re-verification.
    /// </summary>
    public int ExpiredTypes { get; set; }

    /// <summary>
    /// Gets or sets the average age of cached types in hours.
    /// </summary>
    public double AverageAgeHours { get; set; }
}