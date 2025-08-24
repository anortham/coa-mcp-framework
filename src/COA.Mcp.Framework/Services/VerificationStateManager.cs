using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using COA.Mcp.Framework.Configuration;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace COA.Mcp.Framework.Services;

/// <summary>
/// Manages verification state for types and members across tool executions.
/// Maintains an in-memory cache with optional persistence and file watching.
/// </summary>
public class VerificationStateManager : IVerificationStateManager, IHostedService, IDisposable
{
    private readonly ILogger<VerificationStateManager> _logger;
    private readonly TypeVerificationOptions _options;
    private readonly ConcurrentDictionary<string, TypeVerificationState> _typeCache = new();
    private readonly ConcurrentDictionary<string, FileSystemWatcher> _fileWatchers = new();
    private readonly object _persistenceLock = new();
    
    // Statistics tracking
    private long _cacheHits = 0;
    private long _cacheMisses = 0;
    private readonly DateTime _startTime = DateTime.UtcNow;
    
    // File patterns for type extraction
    private static readonly Regex[] CSharpTypePatterns = new[]
    {
        new Regex(@"\bnew\s+([A-Z]\w*)", RegexOptions.Compiled | RegexOptions.Multiline),
        new Regex(@"\b([A-Z]\w*)\s+\w+\s*[=;]", RegexOptions.Compiled | RegexOptions.Multiline),
        new Regex(@"\b([A-Z]\w*)\?\s+\w+", RegexOptions.Compiled | RegexOptions.Multiline),
        new Regex(@":\s*([A-Z]\w*)", RegexOptions.Compiled | RegexOptions.Multiline),
        new Regex(@"<([A-Z]\w*)>", RegexOptions.Compiled | RegexOptions.Multiline),
        new Regex(@"\b([A-Z]\w*)\.(\w+)", RegexOptions.Compiled | RegexOptions.Multiline),
        new Regex(@"\bclass\s+([A-Z]\w*)", RegexOptions.Compiled | RegexOptions.Multiline),
        new Regex(@"\binterface\s+([A-Z]\w*)", RegexOptions.Compiled | RegexOptions.Multiline),
        new Regex(@"\benum\s+([A-Z]\w*)", RegexOptions.Compiled | RegexOptions.Multiline),
    };

    private static readonly Regex[] TypeScriptTypePatterns = new[]
    {
        new Regex(@":\s*([A-Z]\w*)", RegexOptions.Compiled | RegexOptions.Multiline),
        new Regex(@"\bas\s+([A-Z]\w*)", RegexOptions.Compiled | RegexOptions.Multiline),
        new Regex(@"\bnew\s+([A-Z]\w*)", RegexOptions.Compiled | RegexOptions.Multiline),
        new Regex(@"<([A-Z]\w*)>", RegexOptions.Compiled | RegexOptions.Multiline),
        new Regex(@"\b([A-Z]\w*)\.(\w+)", RegexOptions.Compiled | RegexOptions.Multiline),
        new Regex(@"\binterface\s+([A-Z]\w*)", RegexOptions.Compiled | RegexOptions.Multiline),
    };

    /// <summary>
    /// Initializes a new instance of the VerificationStateManager class.
    /// </summary>
    public VerificationStateManager(
        ILogger<VerificationStateManager> logger,
        IOptions<TypeVerificationOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
    }

    /// <inheritdoc/>
    public async Task<bool> IsTypeVerifiedAsync(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return false;

        if (_typeCache.TryGetValue(typeName, out var state))
        {
            Interlocked.Increment(ref _cacheHits);
            state.RecordAccess();

            // Check if verification is still valid
            var isValid = await IsVerificationStillValidAsync(state);
            if (isValid)
            {
                _logger.LogDebug("Type {TypeName} is verified and valid", typeName);
                return true;
            }
            else
            {
                // Remove invalid entry
                _typeCache.TryRemove(typeName, out _);
                _logger.LogDebug("Type {TypeName} verification expired, removed from cache", typeName);
            }
        }

        Interlocked.Increment(ref _cacheMisses);
        _logger.LogDebug("Type {TypeName} not found in verification cache", typeName);
        return false;
    }

    /// <inheritdoc/>
    public async Task MarkTypeVerifiedAsync(string typeName, TypeInfo typeInfo)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return;

        var state = new TypeVerificationState
        {
            TypeName = typeName,
            FilePath = typeInfo.FilePath,
            VerifiedAt = DateTime.UtcNow,
            VerificationMethod = VerificationMethod.ExplicitVerification,
            Namespace = typeInfo.Namespace,
            AssemblyName = typeInfo.AssemblyName,
            BaseType = typeInfo.BaseType,
            Interfaces = typeInfo.Interfaces,
            Members = typeInfo.Members,
            Metadata = typeInfo.Metadata
        };

        // Set file modification time for cache invalidation
        if (!string.IsNullOrEmpty(typeInfo.FilePath) && File.Exists(typeInfo.FilePath))
        {
            try
            {
                state.FileModificationTime = new FileInfo(typeInfo.FilePath).LastWriteTime.Ticks;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get file modification time for {FilePath}", typeInfo.FilePath);
            }
        }

        state.UpdateExpiration();

        _typeCache.AddOrUpdate(typeName, state, (key, existing) =>
        {
            // Merge with existing if it has more recent information
            if (existing.VerifiedAt > state.VerifiedAt)
            {
                existing.RecordAccess();
                return existing;
            }
            return state;
        });

        _logger.LogDebug("Marked type {TypeName} as verified via {Method}", 
            typeName, state.VerificationMethod);

        // Persist to disk if enabled
        if (_options.CacheExpirationHours > 0)
        {
            await PersistCacheAsync();
        }

        // Set up file watching if enabled
        if (_options.EnableFileWatching && !string.IsNullOrEmpty(typeInfo.FilePath))
        {
            await SetupFileWatchingAsync(typeInfo.FilePath);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> HasVerifiedMemberAsync(string typeName, string memberName)
    {
        if (string.IsNullOrWhiteSpace(typeName) || string.IsNullOrWhiteSpace(memberName))
            return false;

        if (_typeCache.TryGetValue(typeName, out var state))
        {
            state.RecordAccess();
            
            // Check if verification is still valid
            if (await IsVerificationStillValidAsync(state))
            {
                var hasMember = state.Members.ContainsKey(memberName);
                _logger.LogDebug("Type {TypeName} member {MemberName} verification: {HasMember}", 
                    typeName, memberName, hasMember);
                return hasMember;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<string>?> GetAvailableMembersAsync(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return null;

        if (_typeCache.TryGetValue(typeName, out var state))
        {
            state.RecordAccess();
            
            // Check if verification is still valid
            if (await IsVerificationStillValidAsync(state))
            {
                return state.Members.Keys.ToList();
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<TypeVerificationState?> GetVerificationStatusAsync(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return null;

        if (_typeCache.TryGetValue(typeName, out var state))
        {
            state.RecordAccess();
            
            // Check if verification is still valid
            if (await IsVerificationStillValidAsync(state))
            {
                return state;
            }
            else
            {
                // Remove expired entry
                _typeCache.TryRemove(typeName, out _);
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task ClearCacheAsync(string? pattern = null)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            var count = _typeCache.Count;
            _typeCache.Clear();
            _logger.LogInformation("Cleared verification cache ({Count} entries)", count);
        }
        else
        {
            var regex = new Regex(pattern.Replace("*", ".*"), RegexOptions.IgnoreCase);
            var keysToRemove = _typeCache.Keys.Where(key => regex.IsMatch(key)).ToList();
            
            foreach (var key in keysToRemove)
            {
                _typeCache.TryRemove(key, out _);
            }
            
            _logger.LogInformation("Cleared {Count} verification cache entries matching pattern {Pattern}", 
                keysToRemove.Count, pattern);
        }

        await PersistCacheAsync();
    }

    /// <inheritdoc/>
    public async Task<VerificationCacheStatistics> GetCacheStatisticsAsync()
    {
        var now = DateTime.UtcNow;
        var validStates = new List<TypeVerificationState>();
        var expiredCount = 0;

        foreach (var state in _typeCache.Values)
        {
            if (await IsVerificationStillValidAsync(state))
            {
                validStates.Add(state);
            }
            else
            {
                expiredCount++;
            }
        }

        var totalAge = validStates.Sum(s => (now - s.VerifiedAt).TotalHours);
        var averageAge = validStates.Count > 0 ? totalAge / validStates.Count : 0;

        // Estimate memory usage
        var estimatedMemoryBytes = _typeCache.Count * 1024; // Rough estimate per entry

        return new VerificationCacheStatistics
        {
            TotalTypes = validStates.Count,
            CacheHits = Interlocked.Read(ref _cacheHits),
            CacheMisses = Interlocked.Read(ref _cacheMisses),
            MemoryUsageBytes = estimatedMemoryBytes,
            ExpiredTypes = expiredCount,
            AverageAgeHours = averageAge,
            LastUpdated = now
        };
    }

    /// <inheritdoc/>
    public async Task LogVerificationSuccessAsync(string toolName, string filePath, IList<string> verifiedTypes)
    {
        _logger.LogInformation("Verification success: Tool={ToolName}, File={FilePath}, Types={Types}",
            toolName, filePath, string.Join(", ", verifiedTypes));

        // Update access times for verified types
        foreach (var typeName in verifiedTypes)
        {
            if (_typeCache.TryGetValue(typeName, out var state))
            {
                state.RecordAccess();
            }
        }
    }

    /// <inheritdoc/>
    public async Task LogVerificationFailureAsync(string toolName, string filePath, 
        IList<string> unverifiedTypes, IList<string> memberIssues)
    {
        _logger.LogWarning("Verification failure: Tool={ToolName}, File={FilePath}, " +
                          "UnverifiedTypes={UnverifiedTypes}, MemberIssues={MemberIssues}",
            toolName, filePath, 
            string.Join(", ", unverifiedTypes), 
            string.Join(", ", memberIssues));
    }

    /// <inheritdoc/>
    public async Task WarmCacheAsync(string filePath, bool includeReferences = false)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return;

        _logger.LogDebug("Warming cache for file: {FilePath}", filePath);

        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            var typeRefs = await GetUnverifiedTypesAsync(content, filePath);
            
            _logger.LogDebug("Found {Count} type references in {FilePath} for cache warming", 
                typeRefs.Count, filePath);
            
            // This would typically integrate with LSP to actually verify the types
            // For now, we just log the intent
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error warming cache for file {FilePath}", filePath);
        }
    }

    /// <inheritdoc/>
    public async Task InvalidateCacheForFileAsync(string changedFilePath)
    {
        if (string.IsNullOrWhiteSpace(changedFilePath))
            return;

        var invalidatedTypes = new List<string>();

        foreach (var kvp in _typeCache)
        {
            if (string.Equals(kvp.Value.FilePath, changedFilePath, StringComparison.OrdinalIgnoreCase))
            {
                if (_typeCache.TryRemove(kvp.Key, out _))
                {
                    invalidatedTypes.Add(kvp.Key);
                }
            }
        }

        if (invalidatedTypes.Any())
        {
            _logger.LogDebug("Invalidated {Count} types due to file change: {FilePath}", 
                invalidatedTypes.Count, changedFilePath);
        }
    }

    /// <inheritdoc/>
    public async Task<IList<TypeReference>> GetUnverifiedTypesAsync(string code, string filePath)
    {
        if (string.IsNullOrWhiteSpace(code))
            return new List<TypeReference>();

        var typeRefs = ExtractTypeReferences(code, filePath);
        var unverifiedRefs = new List<TypeReference>();

        foreach (var typeRef in typeRefs)
        {
            if (!await IsTypeVerifiedAsync(typeRef.TypeName))
            {
                unverifiedRefs.Add(typeRef);
            }
        }

        return unverifiedRefs;
    }

    /// <inheritdoc/>
    public async Task<IDictionary<string, bool>> BulkVerifyTypesAsync(IList<string> typeNames, string workspaceRoot)
    {
        var results = new Dictionary<string, bool>();

        foreach (var typeName in typeNames)
        {
            results[typeName] = await IsTypeVerifiedAsync(typeName);
        }

        return results;
    }

    /// <summary>
    /// Extracts type references from code based on language patterns.
    /// </summary>
    private List<TypeReference> ExtractTypeReferences(string code, string filePath)
    {
        var typeRefs = new List<TypeReference>();
        var isCSharp = IsCSharpFile(filePath);
        var patterns = isCSharp ? CSharpTypePatterns : TypeScriptTypePatterns;

        foreach (var pattern in patterns)
        {
            var matches = pattern.Matches(code);
            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 2)
                {
                    var typeName = match.Groups[1].Value;
                    var memberName = match.Groups.Count >= 3 ? match.Groups[2].Value : null;

                    // Skip common language keywords and primitives
                    if (IsCommonKeyword(typeName))
                        continue;

                    typeRefs.Add(new TypeReference
                    {
                        TypeName = typeName,
                        MemberName = memberName,
                        Context = $"Pattern: {pattern}",
                        LineNumber = GetLineNumber(code, match.Index),
                        ColumnNumber = GetColumnNumber(code, match.Index)
                    });
                }
            }
        }

        // Remove duplicates
        return typeRefs.GroupBy(t => new { t.TypeName, t.MemberName })
                      .Select(g => g.First())
                      .ToList();
    }

    /// <summary>
    /// Checks if the verification state is still valid.
    /// </summary>
    private async Task<bool> IsVerificationStillValidAsync(TypeVerificationState state)
    {
        // Check expiration time
        if (state.ExpiresAt.HasValue && DateTime.UtcNow > state.ExpiresAt.Value)
        {
            return false;
        }

        // Check file modification time if available
        if (!string.IsNullOrEmpty(state.FilePath) && File.Exists(state.FilePath))
        {
            try
            {
                var currentModTime = new FileInfo(state.FilePath).LastWriteTime.Ticks;
                if (currentModTime > state.FileModificationTime)
                {
                    return false;
                }
            }
            catch
            {
                // If we can't check file modification, fall back to time-based check
                return state.IsStillValid(_options.CacheExpirationHours);
            }
        }

        return true;
    }

    /// <summary>
    /// Sets up file watching for automatic cache invalidation.
    /// </summary>
    private async Task SetupFileWatchingAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return;

        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directory) || _fileWatchers.ContainsKey(directory))
            return;

        try
        {
            var watcher = new FileSystemWatcher(directory)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            watcher.Changed += async (sender, e) => await InvalidateCacheForFileAsync(e.FullPath);
            watcher.Deleted += async (sender, e) => await InvalidateCacheForFileAsync(e.FullPath);

            _fileWatchers.TryAdd(directory, watcher);
            _logger.LogDebug("Set up file watching for directory: {Directory}", directory);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set up file watching for directory: {Directory}", directory);
        }
    }

    /// <summary>
    /// Persists the cache to disk for recovery across sessions.
    /// </summary>
    private async Task PersistCacheAsync()
    {
        // This would implement cache persistence to disk
        // Skipping implementation for now as it's not critical for initial functionality
        await Task.CompletedTask;
    }

    /// <summary>
    /// Helper methods for code analysis.
    /// </summary>
    private static bool IsCSharpFile(string filePath) =>
        filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
        filePath.EndsWith(".csx", StringComparison.OrdinalIgnoreCase);

    private static bool IsCommonKeyword(string word)
    {
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "string", "int", "bool", "double", "float", "decimal", "object", "var",
            "number", "boolean", "undefined", "null", "any", "void"
        };
        return keywords.Contains(word);
    }

    private static int GetLineNumber(string text, int index)
    {
        return text.Take(index).Count(c => c == '\n') + 1;
    }

    private static int GetColumnNumber(string text, int index)
    {
        if (index <= 0) return 1;
        var lastNewline = text.LastIndexOf('\n', index - 1);
        return index - lastNewline;
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting VerificationStateManager");
        
        // Load persisted cache if available
        // Implementation would go here
        
        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping VerificationStateManager");
        
        // Persist cache before shutdown
        await PersistCacheAsync();
        
        // Clean up file watchers
        foreach (var watcher in _fileWatchers.Values)
        {
            watcher?.Dispose();
        }
        _fileWatchers.Clear();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var watcher in _fileWatchers.Values)
        {
            watcher?.Dispose();
        }
        _fileWatchers.Clear();
    }
}