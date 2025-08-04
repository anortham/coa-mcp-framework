using Microsoft.CodeAnalysis;
using COA.Mcp.Framework.Migration.Analyzers;

namespace COA.Mcp.Framework.Migration.Migrators;

/// <summary>
/// Interface for code migrators that transform legacy patterns to framework patterns
/// </summary>
public interface IMigrator
{
    /// <summary>
    /// Gets the type of pattern this migrator handles
    /// </summary>
    string PatternType { get; }

    /// <summary>
    /// Determines if this migrator can handle the given pattern
    /// </summary>
    bool CanMigrate(PatternMatch pattern);

    /// <summary>
    /// Migrates the pattern to use framework patterns
    /// </summary>
    Task<MigrationResult> MigrateAsync(PatternMatch pattern, Document document);
}

public class MigrationResult
{
    public bool Success { get; set; }
    public Document? ModifiedDocument { get; set; }
    public string? Error { get; set; }
    public List<string> Warnings { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}