# COA.Mcp.Framework.Migration

Automated migration tools for converting existing MCP projects to use the COA MCP Framework.

## Features

### Migration Analyzer
- Scans projects for legacy MCP patterns
- Identifies migration opportunities
- Estimates effort and provides recommendations
- Generates detailed reports in Markdown or JSON

### Code Migrators
- **Tool Registration**: Converts manual registration to attribute-based
- **Response Format**: Migrates to AI-optimized response format
- **Token Management**: Replaces manual token counting with framework utilities

### Safe Transformations
- Uses Roslyn for AST-based code modifications
- Preserves code formatting and comments
- Creates backups before applying changes
- Supports dry-run mode for preview

## Usage

### Via CLI Tool

```bash
# Analyze a project
coa-mcp migrate --project MyMcpServer.csproj --dry-run

# Apply migrations
coa-mcp migrate --project MyMcpServer.csproj

# Skip backup creation (not recommended)
coa-mcp migrate --project MyMcpServer.csproj --no-backup
```

### Programmatic Usage

```csharp
// Create orchestrator
var logger = loggerFactory.CreateLogger<MigrationOrchestrator>();
var orchestrator = new MigrationOrchestrator(logger);

// Configure options
var options = new MigrationOptions
{
    DryRun = false,
    SaveChanges = true,
    UpdateProjectFile = true,
    CreateBackups = true
};

// Run migration
var summary = await orchestrator.MigrateProjectAsync(
    "path/to/project.csproj", 
    options);

// Check results
if (summary.Success)
{
    Console.WriteLine($"Migration completed in {summary.Duration}");
    Console.WriteLine($"Migrated: {summary.SuccessfulMigrations.Count}");
}
```

## Pattern Detection

The analyzer detects various legacy patterns:

### Tool Registration
- Manual `RegisterTool()` calls
- Legacy service registration
- Missing `[McpServerToolType]` attributes

### Response Formats
- Direct object returns without metadata
- Missing insights and actions
- Manual JSON serialization

### Token Management
- String length division (`text.Length / 4`)
- Manual result truncation
- Hard-coded token limits

## Migration Safety

1. **Backup Creation**: All modified files are backed up
2. **Validation**: Changes are validated before applying
3. **Atomic Operations**: Either all migrations succeed or none are applied
4. **Preview Mode**: Dry-run shows what would change

## Gradual Migration

For large projects, migrations can be applied incrementally:

1. Start with tool registration (lowest risk)
2. Update response formats (medium risk)
3. Migrate token management (highest complexity)

## Troubleshooting

### Common Issues

**Issue**: "No migrator found for pattern type"
- **Solution**: Pattern requires manual migration

**Issue**: "Document not found in project"
- **Solution**: Ensure project is fully built before migration

**Issue**: "Hard-coded token limit detected"
- **Solution**: Manually review and update to use configuration

### Manual Review Required

Some patterns require manual intervention:
- Complex JSON serialization logic
- Custom validation patterns
- Non-standard tool implementations

## Best Practices

1. Always run in dry-run mode first
2. Review the migration report carefully
3. Test thoroughly after migration
4. Commit changes incrementally
5. Keep backups until testing is complete

## Architecture

```
MigrationOrchestrator
├── MigrationAnalyzer
│   ├── ToolRegistrationPatternAnalyzer
│   ├── ResponseFormatPatternAnalyzer
│   ├── TokenManagementPatternAnalyzer
│   ├── AttributePatternAnalyzer
│   └── BaseClassPatternAnalyzer
└── Migrators
    ├── ToolRegistrationMigrator
    ├── ResponseFormatMigrator
    └── TokenManagementMigrator
```

## Future Enhancements

- Support for VB.NET projects
- Custom pattern analyzers via plugins
- Integration with CI/CD pipelines
- Bulk migration for multiple projects
- Rollback functionality