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
    private readonly object _evictionLock = new();
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
            LastAccessedAt = DateTime.UtcNow, // Initialize for proper LRU ordering
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

        // Enforce cache limits (both count and memory-based)
        if (ShouldTriggerEviction())
        {
            EnforceCacheLimits();
        }

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

        // Estimate memory usage with more accurate calculation
        var estimatedMemoryBytes = EstimateCacheMemoryUsage();

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
    public Task LogVerificationSuccessAsync(string toolName, string filePath, IList<string> verifiedTypes)
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
        
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task LogVerificationFailureAsync(string toolName, string filePath, 
        IList<string> unverifiedTypes, IList<string> memberIssues)
    {
        _logger.LogWarning("Verification failure: Tool={ToolName}, File={FilePath}, " +
                          "UnverifiedTypes={UnverifiedTypes}, MemberIssues={MemberIssues}",
            toolName, filePath, 
            string.Join(", ", unverifiedTypes), 
            string.Join(", ", memberIssues));
        
        return Task.CompletedTask;
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
    public Task InvalidateCacheForFileAsync(string changedFilePath)
    {
        if (string.IsNullOrWhiteSpace(changedFilePath))
            return Task.CompletedTask;

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
        
        return Task.CompletedTask;
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
    private Task<bool> IsVerificationStillValidAsync(TypeVerificationState state)
    {
        // Check expiration time
        if (state.ExpiresAt.HasValue && DateTime.UtcNow > state.ExpiresAt.Value)
        {
            return Task.FromResult(false);
        }

        // Check file modification time if available
        if (!string.IsNullOrEmpty(state.FilePath) && File.Exists(state.FilePath))
        {
            try
            {
                var currentModTime = new FileInfo(state.FilePath).LastWriteTime.Ticks;
                if (currentModTime > state.FileModificationTime)
                {
                    return Task.FromResult(false);
                }
            }
            catch
            {
                // If we can't check file modification, fall back to time-based check
                return Task.FromResult(state.IsStillValid(_options.CacheExpirationHours));
            }
        }

        return Task.FromResult(true);
    }

    /// <summary>
    /// Sets up file watching for automatic cache invalidation.
    /// </summary>
    private Task SetupFileWatchingAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return Task.CompletedTask;

        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directory) || _fileWatchers.ContainsKey(directory))
            return Task.CompletedTask;

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
        
        return Task.CompletedTask;
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

    /// <summary>
    /// Estimates the memory usage of the cache with more accurate calculation.
    /// </summary>
    private long EstimateCacheMemoryUsage()
    {
        try
        {
            if (_typeCache.IsEmpty)
                return 0;

            long totalMemory = 0;

            // Take a snapshot to avoid concurrent modification issues
            var cacheSnapshot = _typeCache.ToList();

            foreach (var kvp in cacheSnapshot)
            {
                try
                {
                    var state = kvp.Value;
                    if (state == null)
                        continue;

                    // Base object overhead and fixed fields
                    long stateMemory = 200; // Base TypeVerificationState object

                    // String fields (TypeName, FilePath, Namespace, AssemblyName, BaseType)
                    stateMemory += EstimateStringMemory(state.TypeName);
                    stateMemory += EstimateStringMemory(state.FilePath);
                    stateMemory += EstimateStringMemory(state.Namespace);
                    stateMemory += EstimateStringMemory(state.AssemblyName);
                    stateMemory += EstimateStringMemory(state.BaseType);

                    // Interface collection
                    if (state.Interfaces?.Any() == true)
                    {
                        stateMemory += 50; // Collection overhead
                        try
                        {
                            stateMemory += state.Interfaces.Sum(i => EstimateStringMemory(i));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug("Error estimating interface memory for {TypeName}: {Error}", 
                                state.TypeName, ex.Message);
                        }
                    }

                    // Members dictionary (major memory contributor)
                    if (state.Members?.Any() == true)
                    {
                        stateMemory += 100; // Dictionary overhead
                        try
                        {
                            foreach (var member in state.Members)
                            {
                                stateMemory += EstimateStringMemory(member.Key); // Member name
                                stateMemory += 150; // MemberInfo object overhead
                                stateMemory += EstimateStringMemory(member.Value?.Name);
                                stateMemory += EstimateStringMemory(member.Value?.DataType);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug("Error estimating member memory for {TypeName}: {Error}", 
                                state.TypeName, ex.Message);
                        }
                    }

                    // Metadata dictionary
                    if (state.Metadata?.Any() == true)
                    {
                        stateMemory += 50; // Dictionary overhead
                        try
                        {
                            stateMemory += state.Metadata.Sum(kvp => 
                                EstimateStringMemory(kvp.Key) + EstimateObjectMemory(kvp.Value));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug("Error estimating metadata memory for {TypeName}: {Error}", 
                                state.TypeName, ex.Message);
                        }
                    }

                    totalMemory += stateMemory;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Error estimating memory for entry {Key}: {Error}", kvp.Key, ex.Message);
                    // Continue with other entries - don't let one bad entry break everything
                }
            }

            return totalMemory;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error estimating cache memory usage, returning conservative estimate");
            // Return a conservative estimate based on entry count
            return Math.Max(0, _typeCache.Count * 2048); // 2KB per entry as fallback
        }
    }

    /// <summary>
    /// Estimates memory usage of a string.
    /// </summary>
    private static long EstimateStringMemory(string? str)
    {
        if (string.IsNullOrEmpty(str))
            return 0;
        
        // String object overhead (24 bytes on 64-bit) + character data (2 bytes per char)
        return 24 + (str.Length * 2);
    }

    /// <summary>
    /// Estimates memory usage of an object in metadata.
    /// </summary>
    private static long EstimateObjectMemory(object? obj)
    {
        if (obj == null)
            return 0;

        return obj switch
        {
            string str => EstimateStringMemory(str),
            int or long or double or float or bool => 8, // Basic primitive types
            DateTime => 16, // DateTime struct
            Guid => 16, // Guid struct
            _ => 50 // Generic object overhead for other types
        };
    }

    /// <summary>
    /// Determines if cache eviction should be triggered based on size and memory limits.
    /// </summary>
    private bool ShouldTriggerEviction()
    {
        // Count-based eviction check
        if (_typeCache.Count > _options.MaxCacheSize)
            return true;

        // Memory-based eviction check (if enabled)
        if (_options.MaxMemoryBytes > 0)
        {
            var currentMemory = EstimateCacheMemoryUsage();
            if (currentMemory > _options.MaxMemoryBytes)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Enforces cache limits using the configured eviction strategy.
    /// </summary>
    private void EnforceCacheLimits()
    {
        try
        {
            lock (_evictionLock)
            {
                try
                {
                    // Double-check after acquiring lock
                    if (!ShouldTriggerEviction())
                        return;

                    var currentCount = _typeCache.Count;
                    var currentMemory = 0L;
                    
                    // Safely get memory usage
                    try
                    {
                        currentMemory = _options.MaxMemoryBytes > 0 ? EstimateCacheMemoryUsage() : 0;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to estimate memory usage during eviction, using count-based eviction only");
                        currentMemory = 0; // Fallback to count-based eviction only
                    }
                    
                    // Calculate how many entries to evict
                    var targetEvictCount = CalculateEvictionCount(currentCount, currentMemory);
                    if (targetEvictCount <= 0)
                        return;

                    // Limit eviction count to prevent excessive removal
                    var maxSafeEviction = Math.Max(1, currentCount / 2); // Never evict more than half
                    targetEvictCount = Math.Min(targetEvictCount, maxSafeEviction);

                    // Select entries to evict based on strategy
                    List<TypeVerificationState> entriesToEvict;
                    try
                    {
                        entriesToEvict = SelectEntriesForEviction(targetEvictCount);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to select entries for eviction using {Strategy} strategy, " +
                            "falling back to LRU", _options.EvictionStrategy);
                        
                        // Fallback to LRU if configured strategy fails
                        try
                        {
                            entriesToEvict = _typeCache.Values
                                .OrderBy(state => state.LastAccessedAt)
                                .Take(targetEvictCount)
                                .ToList();
                        }
                        catch (Exception fallbackEx)
                        {
                            _logger.LogError(fallbackEx, "Fallback LRU eviction also failed, aborting eviction");
                            return;
                        }
                    }

                    if (!entriesToEvict?.Any() == true)
                        return;

                    // Perform eviction with retry logic
                    var evictedCount = PerformEvictionWithRetry(entriesToEvict!);

                    if (evictedCount > 0)
                    {
                        var finalMemory = 0L;
                        try
                        {
                            finalMemory = _options.MaxMemoryBytes > 0 ? EstimateCacheMemoryUsage() : 0;
                        }
                        catch
                        {
                            // Ignore memory estimation errors in success logging
                        }

                        _logger.LogDebug("Evicted {EvictedCount}/{TargetCount} entries using {Strategy} strategy. " +
                            "Cache size: {CurrentSize}/{MaxSize}, Memory: ~{CurrentMemory}/{MaxMemory} bytes", 
                            evictedCount, targetEvictCount, _options.EvictionStrategy, _typeCache.Count, _options.MaxCacheSize,
                            finalMemory, _options.MaxMemoryBytes);
                    }
                    else
                    {
                        _logger.LogWarning("Cache eviction failed - no entries could be removed despite exceeding limits. " +
                            "Cache size: {CurrentSize}/{MaxSize}", _typeCache.Count, _options.MaxCacheSize);
                    }
                }
                catch (Exception innerEx)
                {
                    _logger.LogError(innerEx, "Critical error during cache eviction process");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire lock for cache eviction - cache may continue growing beyond limits");
        }
    }

    /// <summary>
    /// Calculates how many entries to evict based on current cache state.
    /// </summary>
    private int CalculateEvictionCount(int currentCount, long currentMemory)
    {
        var countBasedEviction = Math.Max(0, currentCount - _options.MaxCacheSize);
        
        // Use eviction percentage for more aggressive cleanup
        var percentageBasedEviction = (int)(currentCount * _options.EvictionPercentage);
        
        // Memory-based eviction (rough estimate)
        var memoryBasedEviction = 0;
        if (_options.MaxMemoryBytes > 0 && currentMemory > _options.MaxMemoryBytes)
        {
            // Estimate entries to remove based on average entry size
            var avgEntrySize = currentCount > 0 ? currentMemory / currentCount : 1024;
            var excessMemory = currentMemory - _options.MaxMemoryBytes;
            memoryBasedEviction = (int)((excessMemory / avgEntrySize) * 1.2); // 20% extra for safety
        }

        // Use the maximum of the calculated eviction counts
        return Math.Max(countBasedEviction, Math.Max(percentageBasedEviction, memoryBasedEviction));
    }

    /// <summary>
    /// Selects entries for eviction based on the configured strategy.
    /// </summary>
    private List<TypeVerificationState> SelectEntriesForEviction(int targetCount)
    {
        if (targetCount <= 0)
            return new List<TypeVerificationState>();

        try
        {
            // Take a snapshot to avoid concurrent modification
            var allEntries = _typeCache.Values.ToList();
            
            if (!allEntries.Any())
                return new List<TypeVerificationState>();

            // Limit target count to available entries
            var actualTargetCount = Math.Min(targetCount, allEntries.Count);

            return _options.EvictionStrategy switch
            {
                CacheEvictionStrategy.LRU => allEntries
                    .Where(state => state != null)
                    .OrderBy(state => state.LastAccessedAt)
                    .Take(actualTargetCount)
                    .ToList(),

                CacheEvictionStrategy.LFU => allEntries
                    .Where(state => state != null)
                    .OrderBy(state => state.AccessCount)
                    .ThenBy(state => state.LastAccessedAt)
                    .Take(actualTargetCount)
                    .ToList(),

                CacheEvictionStrategy.FIFO => allEntries
                    .Where(state => state != null)
                    .OrderBy(state => state.VerifiedAt)
                    .Take(actualTargetCount)
                    .ToList(),

                CacheEvictionStrategy.Random => allEntries
                    .Where(state => state != null)
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(actualTargetCount)
                    .ToList(),

                _ => allEntries
                    .Where(state => state != null)
                    .OrderBy(state => state.LastAccessedAt)
                    .Take(actualTargetCount)
                    .ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error selecting entries for eviction with strategy {Strategy}", _options.EvictionStrategy);
            throw; // Let the caller handle fallback
        }
    }

    /// <summary>
    /// Performs the actual eviction of selected entries with retry logic.
    /// </summary>
    private int PerformEvictionWithRetry(List<TypeVerificationState> entriesToEvict)
    {
        if (entriesToEvict?.Any() != true)
            return 0;

        var evictedCount = 0;
        var failures = new List<string>();
        
        foreach (var state in entriesToEvict)
        {
            try
            {
                if (string.IsNullOrEmpty(state?.TypeName))
                {
                    failures.Add("null or empty type name");
                    continue;
                }

                // Attempt to remove with multiple retries for concurrent scenarios
                var attempts = 0;
                const int maxAttempts = 3;
                bool removed = false;

                while (attempts < maxAttempts && !removed)
                {
                    attempts++;
                    
                    if (_typeCache.TryRemove(state.TypeName, out var removedState))
                    {
                        removed = true;
                        evictedCount++;
                        
                        // Log successful eviction at debug level
                        if (_logger.IsEnabled(LogLevel.Trace))
                        {
                            _logger.LogTrace("Evicted type {TypeName} (last accessed: {LastAccessed}, access count: {AccessCount})",
                                state.TypeName, state.LastAccessedAt, state.AccessCount);
                        }
                    }
                    else if (attempts < maxAttempts)
                    {
                        // Brief pause before retry to handle race conditions
                        Thread.Sleep(1);
                    }
                }

                if (!removed)
                {
                    failures.Add($"{state.TypeName} (failed after {maxAttempts} attempts)");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"{state?.TypeName ?? "unknown"} ({ex.GetType().Name}: {ex.Message})");
                _logger.LogDebug(ex, "Error evicting cache entry {TypeName}", state?.TypeName);
            }
        }

        // Log failures if any occurred
        if (failures.Any())
        {
            _logger.LogDebug("Failed to evict {FailureCount} entries: {Failures}", 
                failures.Count, string.Join(", ", failures.Take(5))); // Limit logged failures
        }

        return evictedCount;
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