using System;
using System.Collections.Generic;

namespace COA.Mcp.Framework.Configuration;

/// <summary>
/// Configuration options for type verification middleware.
/// </summary>
public class TypeVerificationOptions
{
    /// <summary>
    /// Gets or sets whether type verification is enabled.
    /// Default: false (opt-in for better out-of-box experience)
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the verification enforcement mode.
    /// Default: Warning (non-blocking for better out-of-box experience)
    /// </summary>
    public TypeVerificationMode Mode { get; set; } = TypeVerificationMode.Warning;

    /// <summary>
    /// Gets or sets the cache expiration time in hours.
    /// Default: 24 hours
    /// </summary>
    public int CacheExpirationHours { get; set; } = 24;

    /// <summary>
    /// Gets or sets whether to automatically mark types as verified when hover succeeds.
    /// Default: true
    /// </summary>
    public bool AutoVerifyOnHover { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to verify member access beyond just type existence.
    /// Default: true
    /// </summary>
    public bool RequireMemberVerification { get; set; } = true;

    /// <summary>
    /// Gets or sets additional whitelisted types that don't require verification.
    /// These are added to the built-in whitelist of common BCL types.
    /// </summary>
    public HashSet<string> WhitelistedTypes { get; set; } = new();

    /// <summary>
    /// Gets or sets the maximum cache size in number of entries.
    /// Default: 10000 entries
    /// </summary>
    public int MaxCacheSize { get; set; } = 10000;

    /// <summary>
    /// Gets or sets the cache eviction strategy to use when MaxCacheSize is exceeded.
    /// Default: LRU (Least Recently Used)
    /// </summary>
    public CacheEvictionStrategy EvictionStrategy { get; set; } = CacheEvictionStrategy.LRU;

    /// <summary>
    /// Gets or sets the percentage of entries to evict when cache limit is exceeded.
    /// For example, 0.25 means evict 25% of entries when limit is reached.
    /// Default: 0.20 (20%)
    /// </summary>
    public double EvictionPercentage { get; set; } = 0.20;

    /// <summary>
    /// Gets or sets the maximum memory usage in bytes before triggering eviction.
    /// Set to 0 to disable memory-based eviction (count-based only).
    /// Default: 50MB (52,428,800 bytes)
    /// </summary>
    public long MaxMemoryBytes { get; set; } = 50 * 1024 * 1024;

    /// <summary>
    /// Gets or sets whether to enable file watching for automatic cache invalidation.
    /// Default: true
    /// </summary>
    public bool EnableFileWatching { get; set; } = true;

    /// <summary>
    /// Gets or sets the file patterns to watch for changes (supports wildcards).
    /// Default: *.cs, *.ts, *.tsx
    /// </summary>
    public List<string> WatchedFilePatterns { get; set; } = new() 
    { 
        "*.cs", 
        "*.csx", 
        "*.ts", 
        "*.tsx", 
        "*.js", 
        "*.jsx" 
    };

    /// <summary>
    /// Gets or sets whether to preload types during solution/project loading.
    /// Default: false (on-demand loading)
    /// </summary>
    public bool PreloadTypes { get; set; } = false;

    /// <summary>
    /// Gets or sets the confidence threshold for type verification (0.0 to 1.0).
    /// Types with confidence below this threshold will require re-verification.
    /// Default: 0.8
    /// </summary>
    public double ConfidenceThreshold { get; set; } = 0.8;

    /// <summary>
    /// Gets or sets whether to log detailed verification events.
    /// Default: false
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Gets or sets the file paths or patterns to exclude from verification.
    /// Supports wildcards and regex patterns.
    /// </summary>
    public List<string> ExcludedPaths { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to verify types in generated code files.
    /// Default: false (skip generated files)
    /// </summary>
    public bool VerifyGeneratedCode { get; set; } = false;

    /// <summary>
    /// Gets or sets the timeout for verification operations in milliseconds.
    /// Default: 5000ms (5 seconds)
    /// </summary>
    public int VerificationTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Gets or sets whether to fall back to namespace-based verification when exact type verification fails.
    /// Default: true
    /// </summary>
    public bool EnableNamespaceFallback { get; set; } = true;

    /// <summary>
    /// Gets or sets custom verification rules by file extension.
    /// Key: file extension (e.g., ".cs", ".ts")
    /// Value: verification configuration
    /// </summary>
    public Dictionary<string, LanguageVerificationConfig> LanguageConfigs { get; set; } = new();

    /// <summary>
    /// Validates the configuration and sets defaults for missing values.
    /// </summary>
    public void Validate()
    {
        if (CacheExpirationHours <= 0)
        {
            CacheExpirationHours = 24;
        }

        if (MaxCacheSize <= 0)
        {
            MaxCacheSize = 10000;
        }

        if (EvictionPercentage <= 0.0 || EvictionPercentage >= 1.0)
        {
            EvictionPercentage = 0.20;
        }

        if (MaxMemoryBytes < 0)
        {
            MaxMemoryBytes = 50 * 1024 * 1024; // 50MB default
        }

        if (ConfidenceThreshold < 0.0 || ConfidenceThreshold > 1.0)
        {
            ConfidenceThreshold = 0.8;
        }

        if (VerificationTimeoutMs <= 0)
        {
            VerificationTimeoutMs = 5000;
        }

        // Ensure we have default language configs
        if (!LanguageConfigs.ContainsKey(".cs"))
        {
            LanguageConfigs[".cs"] = new LanguageVerificationConfig
            {
                RequireExactMatch = true,
                CaseSensitive = true,
                EnableGenericTypeInference = true
            };
        }

        if (!LanguageConfigs.ContainsKey(".ts"))
        {
            LanguageConfigs[".ts"] = new LanguageVerificationConfig
            {
                RequireExactMatch = false,
                CaseSensitive = true,
                EnableGenericTypeInference = true
            };
        }
    }
}

/// <summary>
/// Language-specific verification configuration.
/// </summary>
public class LanguageVerificationConfig
{
    /// <summary>
    /// Gets or sets whether to require exact type name matches.
    /// Default: true
    /// </summary>
    public bool RequireExactMatch { get; set; } = true;

    /// <summary>
    /// Gets or sets whether type matching is case sensitive.
    /// Default: true
    /// </summary>
    public bool CaseSensitive { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable generic type inference.
    /// Default: true
    /// </summary>
    public bool EnableGenericTypeInference { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to verify imported/using statements.
    /// Default: true
    /// </summary>
    public bool VerifyImports { get; set; } = true;

    /// <summary>
    /// Gets or sets custom type patterns to ignore for this language.
    /// </summary>
    public List<string> IgnorePatterns { get; set; } = new();

    /// <summary>
    /// Gets or sets additional whitelisted types specific to this language.
    /// </summary>
    public HashSet<string> LanguageSpecificWhitelist { get; set; } = new();
}

/// <summary>
/// Enumeration of type verification modes.
/// </summary>
public enum TypeVerificationMode
{
    /// <summary>
    /// Type verification is disabled.
    /// </summary>
    Disabled = 0,

    /// <summary>
    /// Show warnings for unverified types but allow operations to continue.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Block operations that use unverified types (default).
    /// </summary>
    Strict = 2
}

/// <summary>
/// Enumeration of cache eviction strategies.
/// </summary>
public enum CacheEvictionStrategy
{
    /// <summary>
    /// Least Recently Used - evict entries that haven't been accessed recently.
    /// </summary>
    LRU = 1,

    /// <summary>
    /// Least Frequently Used - evict entries with the lowest access count.
    /// </summary>
    LFU = 2,

    /// <summary>
    /// First In, First Out - evict the oldest entries by creation time.
    /// </summary>
    FIFO = 3,

    /// <summary>
    /// Random - evict random entries (fastest, least optimal).
    /// </summary>
    Random = 4
}