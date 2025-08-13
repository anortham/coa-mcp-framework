# SQL Server MCP Technical Specification

## Overview

The SQL Server MCP provides comprehensive database interrogation, documentation, and query capabilities with intelligent token management and Windows Authentication support.

## Architecture

### Core Components

```
SQL Server MCP
├── Tools/
│   ├── SchemaExplorerTool.cs         # Database/table/column discovery
│   ├── QueryExecutorTool.cs          # Query execution with limits
│   ├── DocumentationTool.cs          # Auto-documentation generation
│   ├── PerformanceAnalyzerTool.cs    # Query performance analysis
│   └── DataDictionaryTool.cs         # Data dictionary generation
├── Services/
│   ├── SqlConnectionService.cs       # Connection management
│   ├── SchemaService.cs              # Schema discovery and caching
│   ├── QueryService.cs               # Query execution with safety limits
│   └── DocumentationService.cs      # Documentation generation
├── Models/
│   ├── SchemaObjects.cs              # Database, Table, Column models
│   ├── QueryResult.cs                # Query execution results
│   └── DocumentationModels.cs       # Documentation structures
└── Configuration/
    ├── ConnectionSettings.cs         # Connection configuration
    └── QueryLimits.cs               # Safety limits and timeouts
```

## Authentication Strategy

### Windows Authentication (Primary)

```csharp
public class WindowsAuthConnectionService : ISqlConnectionService
{
    private readonly ILogger<WindowsAuthConnectionService> _logger;
    private readonly ConnectionPoolManager _poolManager;
    
    public WindowsAuthConnectionService(
        ILogger<WindowsAuthConnectionService> logger,
        IOptions<SqlConnectionSettings> settings)
    {
        _logger = logger;
        _poolManager = new ConnectionPoolManager(settings.Value);
    }
    
    public async Task<SqlConnection> GetConnectionAsync(string database = null)
    {
        var connectionString = BuildWindowsAuthConnectionString(database);
        
        var connection = await _poolManager.GetConnectionAsync(connectionString);
        
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
            _logger.LogInformation($"Connected to SQL Server as {GetCurrentUser(connection)}");
        }
        
        return connection;
    }
    
    private string BuildWindowsAuthConnectionString(string database)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = _settings.ServerName,
            IntegratedSecurity = true,
            TrustServerCertificate = _settings.TrustServerCertificate,
            ConnectTimeout = _settings.ConnectionTimeout,
            CommandTimeout = _settings.CommandTimeout,
            ApplicationName = "COA MCP SQL Server Tool",
            Pooling = true,
            MaxPoolSize = _settings.MaxPoolSize
        };
        
        if (!string.IsNullOrEmpty(database))
        {
            builder.InitialCatalog = database;
        }
        
        return builder.ConnectionString;
    }
    
    private string GetCurrentUser(SqlConnection connection)
    {
        using var command = new SqlCommand("SELECT SYSTEM_USER", connection);
        return command.ExecuteScalar()?.ToString() ?? "Unknown";
    }
}
```

### SQL Authentication (Fallback)

```csharp
public class SqlAuthConnectionService : ISqlConnectionService
{
    public async Task<SqlConnection> GetConnectionAsync(string database = null)
    {
        var connectionString = BuildSqlAuthConnectionString(database);
        var connection = new SqlConnection(connectionString);
        
        try
        {
            await connection.OpenAsync();
            return connection;
        }
        catch (SqlException ex) when (ex.Number == 18456) // Login failed
        {
            _logger.LogError("SQL Authentication failed. Check username/password.");
            throw new AuthenticationException("SQL Server authentication failed", ex);
        }
    }
    
    private string BuildSqlAuthConnectionString(string database)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = _settings.ServerName,
            UserID = _settings.Username,
            Password = _settings.Password,
            IntegratedSecurity = false,
            TrustServerCertificate = _settings.TrustServerCertificate,
            ConnectTimeout = _settings.ConnectionTimeout,
            CommandTimeout = _settings.CommandTimeout,
            ApplicationName = "COA MCP SQL Server Tool"
        };
        
        if (!string.IsNullOrEmpty(database))
        {
            builder.InitialCatalog = database;
        }
        
        return builder.ConnectionString;
    }
}
```

## Schema Discovery Tools

### Schema Explorer Tool

```csharp
[Tool("explore_schema")]
public class SchemaExplorerTool : AdaptiveResponseBuilder<SchemaExploreParams, SchemaResult>
{
    private readonly ISchemaService _schemaService;
    
    public SchemaExplorerTool(ISchemaService schemaService, ILogger<SchemaExplorerTool> logger) 
        : base(logger)
    {
        _schemaService = schemaService;
    }
    
    protected override string GetOperationName() => "schema_exploration";
    
    protected override async Task<SchemaResult> ExecuteInternalAsync(
        SchemaExploreParams parameters, 
        CancellationToken cancellationToken)
    {
        ValidateRequired(parameters.Database, nameof(parameters.Database));
        
        var schema = await _schemaService.GetDatabaseSchemaAsync(
            parameters.Database, 
            parameters.IncludeSystemObjects,
            cancellationToken);
        
        var context = new ResponseContext
        {
            ResponseMode = parameters.ResponseMode ?? "summary",
            TokenLimit = parameters.MaxTokens ?? 8000
        };
        
        return await BuildResponseAsync(schema, context);
    }
    
    protected override async Task ApplyAdaptiveFormattingAsync(
        SchemaResult result, 
        DatabaseSchema schema, 
        ResponseContext context)
    {
        var formatter = _formatterFactory.CreateInlineFormatter(_environment);
        
        result.Success = true;
        result.Summary = $"Schema for database '{schema.Name}' ({schema.Tables.Count} tables, {schema.Views.Count} views)";
        
        // Create hierarchical tree view
        if (_environment.SupportsHTML && context.ResponseMode == "full")
        {
            result.ResourceUri = await CreateInteractiveSchemaTree(schema);
            result.IDEDisplayHint = "tree";
            result.Message = $"Interactive schema explorer available: [Open Schema Tree]({result.ResourceUri})";
        }
        else
        {
            result.Message = FormatSchemaText(schema, context.ResponseMode);
            result.IDEDisplayHint = "markdown";
        }
        
        // Add schema statistics
        result.Metadata = new Dictionary<string, object>
        {
            ["database"] = schema.Name,
            ["tables"] = schema.Tables.Count,
            ["views"] = schema.Views.Count,
            ["procedures"] = schema.StoredProcedures.Count,
            ["functions"] = schema.Functions.Count,
            ["totalColumns"] = schema.Tables.Sum(t => t.Columns.Count),
            ["lastRefreshed"] = DateTime.UtcNow
        };
        
        // Add contextual actions
        var actions = new List<ActionItem>
        {
            new ActionItem
            {
                Title = "Generate Documentation",
                Command = "sql.generateDocumentation",
                Parameters = new { database = schema.Name }
            },
            new ActionItem
            {
                Title = "Export Schema",
                Command = "sql.exportSchema", 
                Parameters = new { database = schema.Name, format = "json" }
            }
        };
        
        if (schema.Tables.Any())
        {
            actions.Add(new ActionItem
            {
                Title = "Find Table Relationships",
                Command = "sql.findRelationships",
                Parameters = new { database = schema.Name }
            });
        }
        
        result.Actions = actions;
    }
    
    private string FormatSchemaText(DatabaseSchema schema, string mode)
    {
        var sb = new StringBuilder();
        
        // Database info
        sb.AppendLine($"## 🗄️ Database: {schema.Name}");
        sb.AppendLine();
        
        // Tables section
        if (schema.Tables.Any())
        {
            sb.AppendLine($"### 📊 Tables ({schema.Tables.Count})");
            
            if (mode == "summary")
            {
                // Summary: just table names with row counts
                foreach (var table in schema.Tables.Take(20))
                {
                    var rowInfo = table.RowCount.HasValue ? $" ({table.RowCount:N0} rows)" : "";
                    sb.AppendLine($"- **{table.Schema}.{table.Name}**{rowInfo} - {table.Columns.Count} columns");
                }
                
                if (schema.Tables.Count > 20)
                {
                    sb.AppendLine($"- *...and {schema.Tables.Count - 20} more tables*");
                }
            }
            else
            {
                // Full: show tables with key columns
                foreach (var table in schema.Tables)
                {
                    sb.AppendLine($"#### {table.Schema}.{table.Name}");
                    sb.AppendLine($"*{table.Description ?? "No description"}*");
                    sb.AppendLine();
                    
                    // Show primary key and first few columns
                    var keyColumns = table.Columns.Where(c => c.IsPrimaryKey).ToList();
                    var otherColumns = table.Columns.Where(c => !c.IsPrimaryKey).Take(5).ToList();
                    
                    if (keyColumns.Any())
                    {
                        sb.AppendLine("**Primary Key:**");
                        foreach (var col in keyColumns)
                        {
                            sb.AppendLine($"- 🔑 `{col.Name}` ({col.DataType})");
                        }
                    }
                    
                    if (otherColumns.Any())
                    {
                        sb.AppendLine("**Key Columns:**");
                        foreach (var col in otherColumns)
                        {
                            var nullable = col.IsNullable ? "?" : "";
                            sb.AppendLine($"- `{col.Name}` ({col.DataType}{nullable}) - {col.Description ?? "No description"}");
                        }
                    }
                    
                    if (table.Columns.Count > keyColumns.Count + otherColumns.Count)
                    {
                        sb.AppendLine($"- *...and {table.Columns.Count - keyColumns.Count - otherColumns.Count} more columns*");
                    }
                    
                    sb.AppendLine();
                }
            }
        }
        
        // Views section
        if (schema.Views.Any())
        {
            sb.AppendLine($"### 👁️ Views ({schema.Views.Count})");
            foreach (var view in schema.Views.Take(10))
            {
                sb.AppendLine($"- **{view.Schema}.{view.Name}** - {view.Description ?? "No description"}");
            }
            
            if (schema.Views.Count > 10)
            {
                sb.AppendLine($"- *...and {schema.Views.Count - 10} more views*");
            }
            sb.AppendLine();
        }
        
        // Stored procedures
        if (schema.StoredProcedures.Any())
        {
            sb.AppendLine($"### ⚙️ Stored Procedures ({schema.StoredProcedures.Count})");
            foreach (var proc in schema.StoredProcedures.Take(10))
            {
                sb.AppendLine($"- **{proc.Schema}.{proc.Name}** - {proc.Description ?? "No description"}");
            }
            
            if (schema.StoredProcedures.Count > 10)
            {
                sb.AppendLine($"- *...and {schema.StoredProcedures.Count - 10} more procedures*");
            }
        }
        
        return sb.ToString();
    }
    
    private async Task<string> CreateInteractiveSchemaTree(DatabaseSchema schema)
    {
        var html = GenerateSchemaTreeHTML(schema);
        var resourceId = Guid.NewGuid().ToString("N")[..8];
        await _resourceProvider.StoreAsync($"schema/{resourceId}.html", html);
        return $"mcp://schema/{resourceId}.html";
    }
}
```

### Schema Service Implementation

```csharp
public class SchemaService : ISchemaService
{
    private readonly ISqlConnectionService _connectionService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SchemaService> _logger;
    
    public async Task<DatabaseSchema> GetDatabaseSchemaAsync(
        string database, 
        bool includeSystemObjects = false,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"schema:{database}:{includeSystemObjects}";
        
        if (_cache.TryGetValue(cacheKey, out DatabaseSchema cachedSchema))
        {
            _logger.LogDebug($"Returning cached schema for database '{database}'");
            return cachedSchema;
        }
        
        using var connection = await _connectionService.GetConnectionAsync(database);
        
        var schema = new DatabaseSchema
        {
            Name = database,
            RefreshTime = DateTime.UtcNow,
            Tables = await GetTablesAsync(connection, includeSystemObjects, cancellationToken),
            Views = await GetViewsAsync(connection, includeSystemObjects, cancellationToken),
            StoredProcedures = await GetStoredProceduresAsync(connection, includeSystemObjects, cancellationToken),
            Functions = await GetFunctionsAsync(connection, includeSystemObjects, cancellationToken)
        };
        
        // Cache for 5 minutes
        _cache.Set(cacheKey, schema, TimeSpan.FromMinutes(5));
        
        return schema;
    }
    
    private async Task<List<TableInfo>> GetTablesAsync(
        SqlConnection connection, 
        bool includeSystemObjects,
        CancellationToken cancellationToken)
    {
        var sql = @"
            SELECT 
                t.TABLE_SCHEMA,
                t.TABLE_NAME,
                ep.value as TABLE_DESCRIPTION,
                ps.row_count
            FROM INFORMATION_SCHEMA.TABLES t
            LEFT JOIN sys.extended_properties ep 
                ON ep.major_id = OBJECT_ID(t.TABLE_SCHEMA + '.' + t.TABLE_NAME) 
                AND ep.minor_id = 0 
                AND ep.name = 'MS_Description'
            LEFT JOIN (
                SELECT 
                    SCHEMA_NAME(o.schema_id) as schema_name,
                    o.name as table_name,
                    SUM(p.rows) as row_count
                FROM sys.objects o
                JOIN sys.partitions p ON o.object_id = p.object_id
                WHERE o.type = 'U' AND p.index_id IN (0,1)
                GROUP BY o.schema_id, o.name
            ) ps ON ps.schema_name = t.TABLE_SCHEMA AND ps.table_name = t.TABLE_NAME
            WHERE t.TABLE_TYPE = 'BASE TABLE'";
            
        if (!includeSystemObjects)
        {
            sql += " AND t.TABLE_SCHEMA NOT IN ('sys', 'INFORMATION_SCHEMA')";
        }
        
        sql += " ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME";
        
        var tables = new List<TableInfo>();
        
        using var command = new SqlCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        
        while (await reader.ReadAsync(cancellationToken))
        {
            var table = new TableInfo
            {
                Schema = reader.GetString("TABLE_SCHEMA"),
                Name = reader.GetString("TABLE_NAME"),
                Description = reader.IsDBNull("TABLE_DESCRIPTION") ? null : reader.GetString("TABLE_DESCRIPTION"),
                RowCount = reader.IsDBNull("row_count") ? null : reader.GetInt64("row_count")
            };
            
            tables.Add(table);
        }
        
        // Get columns for each table
        foreach (var table in tables)
        {
            table.Columns = await GetTableColumnsAsync(connection, table.Schema, table.Name, cancellationToken);
        }
        
        return tables;
    }
    
    private async Task<List<ColumnInfo>> GetTableColumnsAsync(
        SqlConnection connection, 
        string schema, 
        string tableName,
        CancellationToken cancellationToken)
    {
        var sql = @"
            SELECT 
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.IS_NULLABLE,
                c.COLUMN_DEFAULT,
                c.CHARACTER_MAXIMUM_LENGTH,
                c.NUMERIC_PRECISION,
                c.NUMERIC_SCALE,
                c.ORDINAL_POSITION,
                ep.value as COLUMN_DESCRIPTION,
                CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END as IS_PRIMARY_KEY,
                CASE WHEN fk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END as IS_FOREIGN_KEY,
                fk.REFERENCED_TABLE_SCHEMA,
                fk.REFERENCED_TABLE_NAME,
                fk.REFERENCED_COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN sys.extended_properties ep 
                ON ep.major_id = OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME) 
                AND ep.minor_id = c.ORDINAL_POSITION
                AND ep.name = 'MS_Description'
            LEFT JOIN (
                SELECT ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                    ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
            ) pk ON pk.TABLE_SCHEMA = c.TABLE_SCHEMA 
                AND pk.TABLE_NAME = c.TABLE_NAME 
                AND pk.COLUMN_NAME = c.COLUMN_NAME
            LEFT JOIN (
                SELECT 
                    ku.TABLE_SCHEMA, 
                    ku.TABLE_NAME, 
                    ku.COLUMN_NAME,
                    rc.UNIQUE_CONSTRAINT_SCHEMA as REFERENCED_TABLE_SCHEMA,
                    ku2.TABLE_NAME as REFERENCED_TABLE_NAME,
                    ku2.COLUMN_NAME as REFERENCED_COLUMN_NAME
                FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                    ON rc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku2
                    ON rc.UNIQUE_CONSTRAINT_NAME = ku2.CONSTRAINT_NAME
            ) fk ON fk.TABLE_SCHEMA = c.TABLE_SCHEMA 
                AND fk.TABLE_NAME = c.TABLE_NAME 
                AND fk.COLUMN_NAME = c.COLUMN_NAME
            WHERE c.TABLE_SCHEMA = @Schema AND c.TABLE_NAME = @TableName
            ORDER BY c.ORDINAL_POSITION";
        
        var columns = new List<ColumnInfo>();
        
        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Schema", schema);
        command.Parameters.AddWithValue("@TableName", tableName);
        
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        
        while (await reader.ReadAsync(cancellationToken))
        {
            var column = new ColumnInfo
            {
                Name = reader.GetString("COLUMN_NAME"),
                DataType = BuildDataTypeString(reader),
                IsNullable = reader.GetString("IS_NULLABLE") == "YES",
                DefaultValue = reader.IsDBNull("COLUMN_DEFAULT") ? null : reader.GetString("COLUMN_DEFAULT"),
                Description = reader.IsDBNull("COLUMN_DESCRIPTION") ? null : reader.GetString("COLUMN_DESCRIPTION"),
                IsPrimaryKey = reader.GetInt32("IS_PRIMARY_KEY") == 1,
                IsForeignKey = reader.GetInt32("IS_FOREIGN_KEY") == 1,
                OrdinalPosition = reader.GetInt32("ORDINAL_POSITION")
            };
            
            if (column.IsForeignKey)
            {
                column.ReferencedTable = $"{reader.GetString("REFERENCED_TABLE_SCHEMA")}.{reader.GetString("REFERENCED_TABLE_NAME")}";
                column.ReferencedColumn = reader.GetString("REFERENCED_COLUMN_NAME");
            }
            
            columns.Add(column);
        }
        
        return columns;
    }
    
    private string BuildDataTypeString(SqlDataReader reader)
    {
        var dataType = reader.GetString("DATA_TYPE");
        
        if (reader.IsDBNull("CHARACTER_MAXIMUM_LENGTH") == false)
        {
            var maxLength = reader.GetInt32("CHARACTER_MAXIMUM_LENGTH");
            return maxLength == -1 ? $"{dataType}(max)" : $"{dataType}({maxLength})";
        }
        
        if (reader.IsDBNull("NUMERIC_PRECISION") == false)
        {
            var precision = reader.GetByte("NUMERIC_PRECISION");
            if (reader.IsDBNull("NUMERIC_SCALE") == false)
            {
                var scale = reader.GetByte("NUMERIC_SCALE");
                return $"{dataType}({precision},{scale})";
            }
            return $"{dataType}({precision})";
        }
        
        return dataType;
    }
}
```

## Query Execution Tools

### Query Executor Tool

```csharp
[Tool("execute_query")]
public class QueryExecutorTool : AdaptiveResponseBuilder<QueryParams, QueryResult>
{
    private readonly IQueryService _queryService;
    private readonly IQueryValidator _validator;
    
    protected override async Task<QueryResult> ExecuteInternalAsync(
        QueryParams parameters, 
        CancellationToken cancellationToken)
    {
        ValidateRequired(parameters.Query, nameof(parameters.Query));
        ValidateRequired(parameters.Database, nameof(parameters.Database));
        
        // Validate query safety
        var validation = await _validator.ValidateQueryAsync(parameters.Query);
        if (!validation.IsValid)
        {
            return new QueryResult
            {
                Success = false,
                Error = new ErrorInfo
                {
                    Code = "QUERY_VALIDATION_FAILED",
                    Message = $"Query validation failed: {string.Join(", ", validation.Errors)}",
                    Recovery = new RecoveryInfo
                    {
                        Steps = new[]
                        {
                            "Review query for unsafe operations",
                            "Ensure query has appropriate WHERE clauses",
                            "Consider using query limits"
                        }
                    }
                }
            };
        }
        
        var context = new ResponseContext
        {
            ResponseMode = parameters.ResponseMode ?? "summary",
            TokenLimit = parameters.MaxTokens ?? 8000
        };
        
        var queryResult = await _queryService.ExecuteQueryAsync(
            parameters.Database,
            parameters.Query,
            parameters.MaxRows ?? 1000,
            parameters.TimeoutSeconds ?? 30,
            cancellationToken);
        
        return await BuildResponseAsync(queryResult, context);
    }
    
    protected override async Task ApplyAdaptiveFormattingAsync(
        QueryResult result, 
        SqlQueryResult queryResult, 
        ResponseContext context)
    {
        result.Success = queryResult.Success;
        result.Summary = $"Query executed - {queryResult.RowCount:N0} rows returned in {queryResult.ExecutionTimeMs}ms";
        
        if (queryResult.Success)
        {
            if (queryResult.RowCount <= 50 && context.ResponseMode == "full")
            {
                // Small result set - show inline
                result.Message = FormatQueryResultInline(queryResult);
                result.IDEDisplayHint = "table";
            }
            else
            {
                // Large result set - create resource
                result.ResourceUri = await CreateQueryResultResource(queryResult);
                result.Message = FormatQuerySummary(queryResult);
                result.IDEDisplayHint = "markdown";
            }
            
            result.Metadata = new Dictionary<string, object>
            {
                ["rowCount"] = queryResult.RowCount,
                ["executionTime"] = queryResult.ExecutionTimeMs,
                ["columnCount"] = queryResult.Data?.Columns.Count ?? 0,
                ["database"] = queryResult.Database,
                ["truncated"] = queryResult.Truncated
            };
        }
        else
        {
            result.Error = new ErrorInfo
            {
                Code = "QUERY_EXECUTION_ERROR",
                Message = queryResult.ErrorMessage,
                Recovery = new RecoveryInfo
                {
                    Steps = GetQueryErrorRecoverySteps(queryResult.ErrorMessage)
                }
            };
        }
        
        // Add contextual actions
        result.Actions = CreateQueryActions(queryResult, parameters);
    }
    
    private string FormatQueryResultInline(SqlQueryResult queryResult)
    {
        var sb = new StringBuilder();
        
        // Query header
        sb.AppendLine("```sql");
        sb.AppendLine(queryResult.Query);
        sb.AppendLine("```");
        sb.AppendLine();
        
        // Execution info
        sb.AppendLine($"✅ **Executed successfully**");
        sb.AppendLine($"- Database: `{queryResult.Database}`");
        sb.AppendLine($"- Rows: **{queryResult.RowCount:N0}**");
        sb.AppendLine($"- Time: **{queryResult.ExecutionTimeMs}ms**");
        sb.AppendLine();
        
        // Data preview
        if (queryResult.Data != null && queryResult.Data.Rows.Count > 0)
        {
            sb.AppendLine("**Results:**");
            sb.AppendLine();
            
            if (_environment.SupportsHTML)
            {
                sb.AppendLine("[Interactive table will be displayed]");
            }
            else
            {
                sb.AppendLine(FormatDataAsMarkdownTable(queryResult.Data));
            }
        }
        
        return sb.ToString();
    }
    
    private string FormatQuerySummary(SqlQueryResult queryResult)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("```sql");
        sb.AppendLine(TruncateQuery(queryResult.Query, 200));
        if (queryResult.Query.Length > 200) sb.AppendLine("...");
        sb.AppendLine("```");
        sb.AppendLine();
        
        sb.AppendLine($"✅ **Query executed successfully**");
        sb.AppendLine($"- Rows returned: **{queryResult.RowCount:N0}**");
        sb.AppendLine($"- Execution time: **{queryResult.ExecutionTimeMs}ms**");
        sb.AppendLine($"- Columns: **{queryResult.Data?.Columns.Count ?? 0}**");
        
        if (queryResult.Truncated)
        {
            sb.AppendLine($"- ⚠️ Results truncated at {queryResult.MaxRows:N0} rows");
        }
        
        sb.AppendLine();
        sb.AppendLine($"📊 **Full results available**: [View Complete Results]({result.ResourceUri})");
        
        return sb.ToString();
    }
    
    private List<ActionItem> CreateQueryActions(SqlQueryResult queryResult, QueryParams parameters)
    {
        var actions = new List<ActionItem>();
        
        if (queryResult.Success)
        {
            actions.Add(new ActionItem
            {
                Title = "Export to CSV",
                Command = "sql.exportResults",
                Parameters = new { format = "csv", resultId = queryResult.Id }
            });
            
            if (queryResult.Data != null)
            {
                actions.Add(new ActionItem
                {
                    Title = "Analyze Columns",
                    Command = "sql.analyzeColumns", 
                    Parameters = new { resultId = queryResult.Id }
                });
            }
            
            if (queryResult.ExecutionTimeMs > 1000)
            {
                actions.Add(new ActionItem
                {
                    Title = "Analyze Query Performance",
                    Command = "sql.analyzePerformance",
                    Parameters = new { query = parameters.Query, database = parameters.Database }
                });
            }
        }
        
        actions.Add(new ActionItem
        {
            Title = "Save Query",
            Command = "sql.saveQuery",
            Parameters = new { query = parameters.Query, name = GenerateQueryName(parameters.Query) }
        });
        
        return actions;
    }
}
```

### Query Validator

```csharp
public class QueryValidator : IQueryValidator
{
    private readonly ILogger<QueryValidator> _logger;
    private static readonly string[] DangerousOperations = 
    {
        "DROP", "DELETE", "UPDATE", "INSERT", "ALTER", "CREATE", "TRUNCATE", 
        "EXEC", "EXECUTE", "xp_", "sp_configure", "SHUTDOWN", "RESTORE", "BACKUP"
    };
    
    public async Task<QueryValidationResult> ValidateQueryAsync(string query)
    {
        var result = new QueryValidationResult { IsValid = true };
        var upperQuery = query.ToUpper();
        
        // Check for dangerous operations
        foreach (var operation in DangerousOperations)
        {
            if (upperQuery.Contains(operation))
            {
                result.IsValid = false;
                result.Errors.Add($"Operation '{operation}' is not allowed for safety reasons");
            }
        }
        
        // Check for missing WHERE clauses on potential data modification
        if (upperQuery.Contains("DELETE") && !upperQuery.Contains("WHERE"))
        {
            result.IsValid = false;
            result.Errors.Add("DELETE statements must include WHERE clauses");
        }
        
        // Check for SELECT * without limits
        if (upperQuery.Contains("SELECT *") && !upperQuery.Contains("TOP") && !upperQuery.Contains("LIMIT"))
        {
            result.Warnings.Add("Consider using specific column names instead of SELECT *");
            result.Warnings.Add("Consider adding TOP N to limit results");
        }
        
        // Check query length
        if (query.Length > 10000)
        {
            result.Warnings.Add("Query is very long and may be difficult to maintain");
        }
        
        _logger.LogDebug($"Query validation result: Valid={result.IsValid}, Errors={result.Errors.Count}, Warnings={result.Warnings.Count}");
        
        return result;
    }
}
```

## Documentation Generation

### Documentation Tool

```csharp
[Tool("generate_documentation")]
public class DocumentationTool : AdaptiveResponseBuilder<DocumentationParams, DocumentationResult>
{
    private readonly IDocumentationService _docService;
    private readonly ISchemaService _schemaService;
    private readonly IProjectKnowledgeClient _knowledgeClient;
    
    protected override async Task<DocumentationResult> ExecuteInternalAsync(
        DocumentationParams parameters, 
        CancellationToken cancellationToken)
    {
        ValidateRequired(parameters.Database, nameof(parameters.Database));
        
        var schema = await _schemaService.GetDatabaseSchemaAsync(parameters.Database, false, cancellationToken);
        var documentation = await _docService.GenerateDocumentationAsync(
            schema, 
            parameters.IncludeDataDictionary,
            parameters.IncludeRelationships,
            cancellationToken);
        
        // Store in ProjectKnowledge if enabled
        if (parameters.StoreInKnowledge)
        {
            await StoreInProjectKnowledge(documentation, parameters.Database);
        }
        
        var context = new ResponseContext
        {
            ResponseMode = parameters.ResponseMode ?? "summary",
            TokenLimit = parameters.MaxTokens ?? 8000
        };
        
        return await BuildResponseAsync(documentation, context);
    }
    
    private async Task StoreInProjectKnowledge(DatabaseDocumentation doc, string database)
    {
        var knowledgeItem = new ProjectKnowledge.Models.KnowledgeItem
        {
            Type = "TechnicalDocumentation",
            Title = $"Database Documentation: {database}",
            Content = doc.MarkdownContent,
            Tags = new[] { "database", "documentation", database.ToLower() },
            Metadata = new Dictionary<string, object>
            {
                ["database"] = database,
                ["tableCount"] = doc.Tables.Count,
                ["generatedAt"] = DateTime.UtcNow,
                ["generator"] = "SQL MCP Documentation Tool"
            }
        };
        
        await _knowledgeClient.StoreKnowledgeAsync(knowledgeItem);
    }
}
```

### Documentation Service

```csharp
public class DocumentationService : IDocumentationService
{
    public async Task<DatabaseDocumentation> GenerateDocumentationAsync(
        DatabaseSchema schema,
        bool includeDataDictionary,
        bool includeRelationships,
        CancellationToken cancellationToken)
    {
        var doc = new DatabaseDocumentation
        {
            DatabaseName = schema.Name,
            GeneratedAt = DateTime.UtcNow,
            Tables = new List<TableDocumentation>()
        };
        
        foreach (var table in schema.Tables)
        {
            var tableDoc = new TableDocumentation
            {
                Schema = table.Schema,
                Name = table.Name,
                Description = table.Description ?? "No description available",
                RowCount = table.RowCount,
                Columns = table.Columns.Select(c => new ColumnDocumentation
                {
                    Name = c.Name,
                    DataType = c.DataType,
                    IsNullable = c.IsNullable,
                    IsPrimaryKey = c.IsPrimaryKey,
                    IsForeignKey = c.IsForeignKey,
                    DefaultValue = c.DefaultValue,
                    Description = c.Description ?? "No description available",
                    ReferencedTable = c.ReferencedTable,
                    ReferencedColumn = c.ReferencedColumn
                }).ToList()
            };
            
            // Generate business rules if patterns detected
            tableDoc.BusinessRules = InferBusinessRules(table);
            
            doc.Tables.Add(tableDoc);
        }
        
        if (includeRelationships)
        {
            doc.Relationships = AnalyzeRelationships(schema);
        }
        
        if (includeDataDictionary)
        {
            doc.DataDictionary = GenerateDataDictionary(schema);
        }
        
        // Generate markdown content
        doc.MarkdownContent = GenerateMarkdownDocumentation(doc);
        
        return doc;
    }
    
    private List<string> InferBusinessRules(TableInfo table)
    {
        var rules = new List<string>();
        
        // Look for common patterns
        var auditColumns = table.Columns.Where(c => 
            c.Name.Contains("Created") || c.Name.Contains("Modified") || 
            c.Name.Contains("Updated") || c.Name.Contains("Deleted")).ToList();
            
        if (auditColumns.Count >= 2)
        {
            rules.Add("Table includes audit trail columns for change tracking");
        }
        
        var statusColumn = table.Columns.FirstOrDefault(c => 
            c.Name.ToLower().Contains("status") || c.Name.ToLower().Contains("state"));
        if (statusColumn != null)
        {
            rules.Add($"Record lifecycle managed through {statusColumn.Name} column");
        }
        
        var softDeleteColumn = table.Columns.FirstOrDefault(c =>
            c.Name.ToLower().Contains("deleted") || c.Name.ToLower().Contains("isactive"));
        if (softDeleteColumn != null)
        {
            rules.Add("Implements soft delete pattern - records are marked as deleted rather than physically removed");
        }
        
        return rules;
    }
    
    private string GenerateMarkdownDocumentation(DatabaseDocumentation doc)
    {
        var md = new StringBuilder();
        
        md.AppendLine($"# Database Documentation: {doc.DatabaseName}");
        md.AppendLine();
        md.AppendLine($"**Generated:** {doc.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        md.AppendLine($"**Tables:** {doc.Tables.Count}");
        md.AppendLine();
        
        md.AppendLine("## Table of Contents");
        md.AppendLine();
        foreach (var table in doc.Tables.OrderBy(t => t.Schema).ThenBy(t => t.Name))
        {
            md.AppendLine($"- [{table.Schema}.{table.Name}](#{table.Schema.ToLower()}{table.Name.ToLower()})");
        }
        md.AppendLine();
        
        // Tables section
        md.AppendLine("## Tables");
        md.AppendLine();
        
        foreach (var table in doc.Tables.OrderBy(t => t.Schema).ThenBy(t => t.Name))
        {
            md.AppendLine($"### {table.Schema}.{table.Name}");
            md.AppendLine();
            md.AppendLine(table.Description);
            md.AppendLine();
            
            if (table.RowCount.HasValue)
            {
                md.AppendLine($"**Estimated Rows:** {table.RowCount:N0}");
                md.AppendLine();
            }
            
            // Columns table
            md.AppendLine("| Column | Type | Nullable | Key | Default | Description |");
            md.AppendLine("|--------|------|----------|-----|---------|-------------|");
            
            foreach (var column in table.Columns.OrderBy(c => c.Name))
            {
                var keyInfo = column.IsPrimaryKey ? "PK" : column.IsForeignKey ? "FK" : "";
                var nullable = column.IsNullable ? "Yes" : "No";
                var defaultValue = column.DefaultValue ?? "";
                var description = column.Description ?? "";
                
                md.AppendLine($"| {column.Name} | {column.DataType} | {nullable} | {keyInfo} | {defaultValue} | {description} |");
            }
            
            md.AppendLine();
            
            // Foreign key relationships
            var foreignKeys = table.Columns.Where(c => c.IsForeignKey).ToList();
            if (foreignKeys.Any())
            {
                md.AppendLine("**Relationships:**");
                md.AppendLine();
                foreach (var fk in foreignKeys)
                {
                    md.AppendLine($"- `{fk.Name}` → `{fk.ReferencedTable}.{fk.ReferencedColumn}`");
                }
                md.AppendLine();
            }
            
            // Business rules
            if (table.BusinessRules.Any())
            {
                md.AppendLine("**Business Rules:**");
                md.AppendLine();
                foreach (var rule in table.BusinessRules)
                {
                    md.AppendLine($"- {rule}");
                }
                md.AppendLine();
            }
            
            md.AppendLine("---");
            md.AppendLine();
        }
        
        return md.ToString();
    }
}
```

## Configuration and Setup

### Configuration Model

```csharp
public class SqlConnectionSettings
{
    public string ServerName { get; set; } = "localhost";
    public string DefaultDatabase { get; set; } = "master";
    public bool UseWindowsAuthentication { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public bool TrustServerCertificate { get; set; } = true;
    public int ConnectionTimeout { get; set; } = 30;
    public int CommandTimeout { get; set; } = 30;
    public int MaxPoolSize { get; set; } = 100;
}

public class QueryLimits
{
    public int MaxRows { get; set; } = 10000;
    public int DefaultTimeout { get; set; } = 30;
    public int MaxQueryLength { get; set; } = 10000;
    public bool AllowDataModification { get; set; } = false;
    public List<string> RestrictedOperations { get; set; } = new()
    {
        "DROP", "DELETE", "UPDATE", "INSERT", "ALTER", "CREATE", "TRUNCATE"
    };
}
```

### Program.cs Setup

```csharp
var builder = new McpServerBuilder()
    .WithServerInfo("SQL Server MCP", "1.0.0")
    .ConfigureLogging(logging =>
    {
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
    });

// Configuration
builder.Services.Configure<SqlConnectionSettings>(builder.Configuration.GetSection("SqlConnection"));
builder.Services.Configure<QueryLimits>(builder.Configuration.GetSection("QueryLimits"));

// Services
builder.Services.AddSingleton<ISqlConnectionService, WindowsAuthConnectionService>();
builder.Services.AddScoped<ISchemaService, SchemaService>();
builder.Services.AddScoped<IQueryService, QueryService>();
builder.Services.AddScoped<IDocumentationService, DocumentationService>();
builder.Services.AddScoped<IQueryValidator, QueryValidator>();

// HTTP client for ProjectKnowledge integration
builder.Services.AddHttpClient<IProjectKnowledgeClient, ProjectKnowledgeClient>();

// Memory cache for schema caching
builder.Services.AddMemoryCache();

// Tools
builder.RegisterToolType<SchemaExplorerTool>();
builder.RegisterToolType<QueryExecutorTool>();
builder.RegisterToolType<DocumentationTool>();
builder.RegisterToolType<PerformanceAnalyzerTool>();
builder.RegisterToolType<DataDictionaryTool>();

// Use HTTP transport for better integration
builder.UseHttpTransport(options =>
{
    options.Port = 5001;
    options.AllowedOrigins = new[] { "*" };
});

await builder.RunAsync();
```

## Security Considerations

### Query Validation
- Whitelist approach for allowed operations
- Mandatory WHERE clauses for DELETE/UPDATE
- Row count limits for SELECT statements
- Query timeout enforcement
- SQL injection pattern detection

### Authentication
- Windows Authentication preferred for internal systems
- SQL Authentication as fallback with secure credential storage
- Connection pooling with proper cleanup
- Audit logging of all database operations

### Data Protection
- Large result sets stored as resources, not in chat
- Automatic PII detection and masking (future enhancement)
- Query logging with sensitive data redaction
- Resource cleanup and expiration

This SQL Server MCP specification provides a comprehensive, secure, and token-efficient approach to database interaction while maintaining the rich visualization capabilities required for the multi-IDE environment.