using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.Services;

namespace UmbrellaFrame.ModelSync.SqlServer
{
    public sealed class SqlServerModelSyncOptions : ModelSyncOptions
    {
        public SqlServerModelSyncOptions()
        {
            DefaultSchema = "app";
            HistorySchema = "sec";
        }
    }

    public sealed class SqlServerModelSynchronizer
    {
        private static readonly Regex SafeIdentifierPattern = new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
        private readonly SqlServerModelSyncOptions _options;
        private readonly List<Assembly> _modelAssemblies;
        private readonly List<Type> _modelTypes;
        private readonly List<MigrationScriptDefinition> _scripts = new List<MigrationScriptDefinition>();

        private SqlServerModelSynchronizer(SqlServerModelSyncOptions options, IEnumerable<Assembly> modelAssemblies, IEnumerable<Type> modelTypes)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(_options.ConnectionString))
                throw new ArgumentException("ConnectionString is required.", nameof(options));
            _modelAssemblies = modelAssemblies?.Where(a => a != null).Distinct().ToList() ?? new List<Assembly>();
            _modelTypes = modelTypes?.Where(t => t != null).Distinct().ToList() ?? new List<Type>();
            if (_modelAssemblies.Count == 0 && _modelTypes.Count == 0)
                throw new ArgumentException("At least one model assembly or model type is required.");
        }

        public static SqlServerModelSynchronizer FromAssemblies(SqlServerModelSyncOptions options, params Assembly[] assemblies)
            => new SqlServerModelSynchronizer(options, assemblies, Array.Empty<Type>());

        public static SqlServerModelSynchronizer FromTypes(SqlServerModelSyncOptions options, params Type[] modelTypes)
            => new SqlServerModelSynchronizer(options, Array.Empty<Assembly>(), modelTypes);

        public SqlServerModelSynchronizer AddSqlScriptsFromEmbeddedResources(Assembly assembly, string rootNamespace)
        {
            var definitions = MigrationScriptDiscovery.FromEmbeddedResources(assembly, rootNamespace);
            foreach (var definition in definitions)
                _scripts.Add(definition);
            return this;
        }

        public SqlServerModelSynchronizer AddSqlScript(MigrationScriptDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            _scripts.Add(definition);
            return this;
        }

        public async Task<ModelSyncResult> CompareAsync(CancellationToken cancellationToken = default)
        {
            ValidateIdentifier(_options.DefaultSchema, nameof(_options.DefaultSchema));
            ValidateIdentifier(_options.HistorySchema, nameof(_options.HistorySchema));

            var modelTables = _modelTypes.Count > 0
                ? ModelSchemaReader.FromTypes(_options.DefaultSchema, _modelTypes.ToArray())
                : ModelSchemaReader.FromAssemblies(_options.DefaultSchema, _modelAssemblies.ToArray());
            var databaseTables = await LoadDatabaseSchemaAsync(cancellationToken).ConfigureAwait(false);

            var builder = new ModelSyncPlanBuilder(
                Quote,
                Qualify,
                BuildCreateTableSql,
                BuildAddColumnSql,
                BuildAddDefaultConstraintSql,
                BuildAddCheckConstraintSql,
                BuildAddUniqueConstraintSql,
                BuildAddForeignKeySql,
                BuildCreateIndexSql);

            var operations = builder.Build(modelTables, databaseTables, _options).ToList();
            operations.AddRange(await BuildScriptPlansAsync(cancellationToken).ConfigureAwait(false));
            return new ModelSyncResult(operations, ExecuteSqlAsync);
        }

        private async Task<IReadOnlyList<ModelSyncPlanItem>> BuildScriptPlansAsync(CancellationToken cancellationToken)
        {
            if (_scripts.Count == 0)
                return new List<ModelSyncPlanItem>();

            var runner = CreateRunner();
            foreach (var script in _scripts)
                runner.RegisterScript(script);

            var plans = await runner.CompareRegisteredAsync(cancellationToken).ConfigureAwait(false);
            var result = new List<ModelSyncPlanItem>();
            foreach (var plan in plans.Where(p => p.HasChanges || ShouldApplyEveryRun(p.Definition.Category)))
            {
                var script = plan.Definition;
                var item = new ModelSyncPlanItem
                {
                    ChangeType = ModelSyncChangeType.ApplySqlScript,
                    Risk = ModelSyncOperationRisk.Safe,
                    Name = script.Name,
                    Sql = script.Sql,
                    Reason = plan.Reason,
                    CanApplyAutomatically = true,
                    ApplyOperationAsync = async (_, ct) =>
                    {
                        if (ShouldApplyEveryRun(script.Category))
                        {
                            foreach (var batch in SqlBatchSplitter.SplitSqlServerGoBatches(script.Sql))
                            {
                                if (!string.IsNullOrWhiteSpace(batch))
                                    await ExecuteSqlAsync(batch, ct).ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            var single = CreateRunner();
                            single.RegisterScript(script);
                            await single.RunAsync(ct).ConfigureAwait(false);
                        }
                    }
                };
                result.Add(item);
            }

            return result;
        }

        private bool ShouldApplyEveryRun(MigrationScriptCategory category)
        {
            if (category == MigrationScriptCategory.StoredProcedures)
                return _options.ApplyStoredProceduresOnEveryRun;
            if (category == MigrationScriptCategory.Triggers)
                return _options.ApplyTriggersOnEveryRun;
            return false;
        }

        private SqlServerMigrationRunner CreateRunner()
        {
            var options = new MigrationRunnerOptions
            {
                EnsureHistoryTables = true,
                AutoAddMissingColumnsFromTableScripts = true,
                DestructiveOptions = _options.AllowDestructiveChanges ? DestructiveOperationOptions.Allow() : null
            };
            options.Schemas.Add(_options.HistorySchema);
            options.Schemas.Add(_options.DefaultSchema);
            return new SqlServerMigrationRunner(_options.ConnectionString, options);
        }

        private async Task<IDictionary<string, DatabaseTableDefinition>> LoadDatabaseSchemaAsync(CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, DatabaseTableDefinition>(StringComparer.OrdinalIgnoreCase);
            using (var connection = new SqlConnection(_options.ConnectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                await EnsureSchemaAsync(connection, _options.DefaultSchema, cancellationToken).ConfigureAwait(false);
                await LoadTablesAndColumnsAsync(connection, result, cancellationToken).ConfigureAwait(false);
                await LoadIndexesAsync(connection, result, cancellationToken).ConfigureAwait(false);
                await LoadConstraintsAsync(connection, result, cancellationToken).ConfigureAwait(false);
            }

            return result;
        }

        private static async Task EnsureSchemaAsync(SqlConnection connection, string schema, CancellationToken cancellationToken)
        {
            ValidateIdentifier(schema, nameof(schema));
            using (var command = new SqlCommand($"IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'{EscapeLiteral(schema)}') EXEC('CREATE SCHEMA [{EscapeIdentifier(schema)}] AUTHORIZATION dbo;');", connection))
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task LoadTablesAndColumnsAsync(SqlConnection connection, IDictionary<string, DatabaseTableDefinition> result, CancellationToken cancellationToken)
        {
            const string sql = @"
SELECT s.name AS SchemaName, t.name AS TableName, c.name AS ColumnName,
       ty.name AS TypeName, c.max_length, c.precision, c.scale, c.is_nullable,
       CASE WHEN dc.object_id IS NULL THEN 0 ELSE 1 END AS HasDefault,
       CASE WHEN cc.object_id IS NULL THEN 0 ELSE 1 END AS HasCheck
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.columns c ON c.object_id = t.object_id
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.default_constraints dc ON dc.parent_object_id = t.object_id AND dc.parent_column_id = c.column_id
LEFT JOIN sys.check_constraints cc ON cc.parent_object_id = t.object_id AND cc.parent_column_id = c.column_id
WHERE s.name = @Schema;";

            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Schema", _options.DefaultSchema);
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        var schema = reader.GetString(0);
                        var tableName = reader.GetString(1);
                        var key = ModelSyncPlanBuilder.Key(schema, tableName);
                        if (!result.TryGetValue(key, out var table))
                        {
                            table = new DatabaseTableDefinition { Schema = schema, Name = tableName };
                            result[key] = table;
                        }

                        table.Columns[reader.GetString(2)] = new DatabaseColumnDefinition
                        {
                            Name = reader.GetString(2),
                            StoreType = BuildSqlServerStoreType(reader.GetString(3), reader.GetInt16(4), reader.GetByte(5), reader.GetByte(6)),
                            IsNullable = reader.GetBoolean(7),
                            HasDefault = reader.GetInt32(8) == 1,
                            HasCheck = reader.GetInt32(9) == 1
                        };
                    }
                }
            }
        }

        private async Task LoadIndexesAsync(SqlConnection connection, IDictionary<string, DatabaseTableDefinition> result, CancellationToken cancellationToken)
        {
            const string sql = @"
SELECT s.name, t.name, i.name
FROM sys.indexes i
JOIN sys.tables t ON t.object_id = i.object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE i.is_primary_key = 0 AND i.name IS NOT NULL AND s.name = @Schema;";
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Schema", _options.DefaultSchema);
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        if (result.TryGetValue(ModelSyncPlanBuilder.Key(reader.GetString(0), reader.GetString(1)), out var table))
                            table.Indexes.Add(reader.GetString(2));
                    }
                }
            }
        }

        private async Task LoadConstraintsAsync(SqlConnection connection, IDictionary<string, DatabaseTableDefinition> result, CancellationToken cancellationToken)
        {
            const string uniqueSql = @"
SELECT s.name, t.name, kc.name
FROM sys.key_constraints kc
JOIN sys.tables t ON t.object_id = kc.parent_object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE kc.type = 'UQ' AND s.name = @Schema;";
            using (var command = new SqlCommand(uniqueSql, connection))
            {
                command.Parameters.AddWithValue("@Schema", _options.DefaultSchema);
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        if (result.TryGetValue(ModelSyncPlanBuilder.Key(reader.GetString(0), reader.GetString(1)), out var table))
                            table.UniqueConstraints.Add(reader.GetString(2));
                    }
                }
            }

            const string fkSql = @"
SELECT s.name, t.name, fk.name
FROM sys.foreign_keys fk
JOIN sys.tables t ON t.object_id = fk.parent_object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name = @Schema;";
            using (var command = new SqlCommand(fkSql, connection))
            {
                command.Parameters.AddWithValue("@Schema", _options.DefaultSchema);
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        if (result.TryGetValue(ModelSyncPlanBuilder.Key(reader.GetString(0), reader.GetString(1)), out var table))
                            table.ForeignKeys.Add(reader.GetString(2));
                    }
                }
            }
        }

        private string BuildCreateTableSql(ModelTableDefinition table)
        {
            var lines = new List<string>();
            var primaryKeys = table.Columns.Where(c => c.IsPrimaryKey).Select(c => c.Name).ToList();
            foreach (var column in table.Columns)
                lines.Add("    " + BuildColumnDefinition(column, primaryKeys.Count <= 1));
            if (primaryKeys.Count > 1)
                lines.Add("    PRIMARY KEY (" + string.Join(", ", primaryKeys.Select(Quote)) + ")");
            return $"CREATE TABLE {Qualify(table.Schema, table.Name)} ({Environment.NewLine}{string.Join("," + Environment.NewLine, lines)}{Environment.NewLine});";
        }

        private string BuildAddColumnSql(ModelTableDefinition table, ModelColumnDefinition column)
            => $"ALTER TABLE {Qualify(table.Schema, table.Name)} ADD {BuildColumnDefinition(column, true)};";

        private string BuildColumnDefinition(ModelColumnDefinition column, bool allowInlinePrimaryKey)
        {
            var sql = new StringBuilder();
            sql.Append($"{Quote(column.Name)} {column.StoreType}");
            if (column.IsPrimaryKey && allowInlinePrimaryKey)
                sql.Append(" PRIMARY KEY");
            if (column.IsRequired)
                sql.Append(" NOT NULL");
            if (column.IsUnique)
                sql.Append(" UNIQUE");
            if (!string.IsNullOrWhiteSpace(column.DefaultSql))
                sql.Append(" DEFAULT " + column.DefaultSql);
            if (!string.IsNullOrWhiteSpace(column.CheckSql))
                sql.Append(" CHECK (" + column.CheckSql + ")");
            return sql.ToString();
        }

        private string BuildAddDefaultConstraintSql(ModelTableDefinition table, ModelColumnDefinition column)
            => $"ALTER TABLE {Qualify(table.Schema, table.Name)} ADD CONSTRAINT {Quote($"DF_{table.Name}_{column.Name}")} DEFAULT {column.DefaultSql} FOR {Quote(column.Name)};";

        private string BuildAddCheckConstraintSql(ModelTableDefinition table, ModelColumnDefinition column)
            => $"ALTER TABLE {Qualify(table.Schema, table.Name)} ADD CONSTRAINT {Quote($"CK_{table.Name}_{column.Name}")} CHECK ({column.CheckSql});";

        private string BuildAddUniqueConstraintSql(ModelTableDefinition table, ModelColumnDefinition column)
            => $"ALTER TABLE {Qualify(table.Schema, table.Name)} ADD CONSTRAINT {Quote($"UQ_{table.Name}_{column.Name}")} UNIQUE ({Quote(column.Name)});";

        private string BuildAddForeignKeySql(ModelTableDefinition table, ModelColumnDefinition column)
            => $"ALTER TABLE {Qualify(table.Schema, table.Name)} ADD CONSTRAINT {Quote($"FK_{table.Name}_{column.Name}_{column.ForeignKeyTable}")} FOREIGN KEY ({Quote(column.Name)}) REFERENCES {Qualify(table.Schema, column.ForeignKeyTable)} ({Quote(column.ForeignKeyReferenceColumn)});";

        private string BuildCreateIndexSql(ModelTableDefinition table, ModelColumnDefinition column)
        {
            var indexName = string.IsNullOrWhiteSpace(column.IndexName) ? $"idx_{table.Name}_{column.Name}" : column.IndexName;
            return $"CREATE {(column.IsUniqueIndex ? "UNIQUE " : string.Empty)}INDEX {Quote(indexName)} ON {Qualify(table.Schema, table.Name)} ({Quote(column.Name)});";
        }

        private async Task ExecuteSqlAsync(string sql, CancellationToken cancellationToken)
        {
            using (var connection = new SqlConnection(_options.ConnectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = new SqlCommand(sql, connection))
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private static string Qualify(string schema, string table)
            => $"{Quote(schema)}.{Quote(table)}";

        private static string Quote(string identifier)
        {
            ValidateIdentifier(identifier, nameof(identifier));
            return "[" + EscapeIdentifier(identifier) + "]";
        }

        private static void ValidateIdentifier(string identifier, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(identifier) || !SafeIdentifierPattern.IsMatch(identifier))
                throw new ArgumentException($"Invalid SQL identifier '{identifier}'.", parameterName);
        }

        private static string EscapeIdentifier(string identifier)
            => identifier.Replace("]", "]]");

        private static string EscapeLiteral(string value)
            => value.Replace("'", "''");

        private static string BuildSqlServerStoreType(string typeName, short maxLength, byte precision, byte scale)
        {
            var lower = typeName.ToLowerInvariant();
            if (lower == "nvarchar" || lower == "nchar")
                return $"{typeName.ToUpperInvariant()}({(maxLength < 0 ? "MAX" : (maxLength / 2).ToString())})";
            if (lower == "varchar" || lower == "char" || lower == "varbinary" || lower == "binary")
                return $"{typeName.ToUpperInvariant()}({(maxLength < 0 ? "MAX" : maxLength.ToString())})";
            if (lower == "decimal" || lower == "numeric")
                return $"{typeName.ToUpperInvariant()}({precision},{scale})";
            return typeName.ToUpperInvariant();
        }
    }
}
