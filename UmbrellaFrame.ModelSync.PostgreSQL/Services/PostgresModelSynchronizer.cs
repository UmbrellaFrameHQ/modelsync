using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.Services;

namespace UmbrellaFrame.ModelSync.PostgreSQL
{
    public sealed class PostgresModelSyncOptions : ModelSyncOptions
    {
        public PostgresModelSyncOptions()
        {
            DefaultSchema = "public";
            HistorySchema = "sec";
        }
    }

    public sealed class PostgresModelSynchronizer
    {
        private static readonly Regex SafeIdentifierPattern = new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
        private readonly PostgresModelSyncOptions _options;
        private readonly List<Assembly> _modelAssemblies;
        private readonly List<Type> _modelTypes;
        private readonly List<MigrationScriptDefinition> _scripts = new List<MigrationScriptDefinition>();

        private PostgresModelSynchronizer(PostgresModelSyncOptions options, IEnumerable<Assembly> modelAssemblies, IEnumerable<Type> modelTypes)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(_options.ConnectionString))
                throw new ArgumentException("ConnectionString is required.", nameof(options));
            _modelAssemblies = modelAssemblies?.Where(a => a != null).Distinct().ToList() ?? new List<Assembly>();
            _modelTypes = modelTypes?.Where(t => t != null).Distinct().ToList() ?? new List<Type>();
            if (_modelAssemblies.Count == 0 && _modelTypes.Count == 0)
                throw new ArgumentException("At least one model assembly or model type is required.");
        }

        public static PostgresModelSynchronizer FromAssemblies(PostgresModelSyncOptions options, params Assembly[] assemblies)
            => new PostgresModelSynchronizer(options, assemblies, Array.Empty<Type>());

        public static PostgresModelSynchronizer FromTypes(PostgresModelSyncOptions options, params Type[] modelTypes)
            => new PostgresModelSynchronizer(options, Array.Empty<Assembly>(), modelTypes);

        public PostgresModelSynchronizer AddSqlScriptsFromEmbeddedResources(Assembly assembly, string rootNamespace)
        {
            foreach (var definition in MigrationScriptDiscovery.FromEmbeddedResources(assembly, rootNamespace))
                _scripts.Add(definition);
            return this;
        }

        public PostgresModelSynchronizer AddSqlScript(MigrationScriptDefinition definition)
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
                ? ModelSchemaReader.FromTypes(_options.DefaultSchema, typeof(PostgresColumnTypeAttribute), typeof(PostgresTableName), _modelTypes.ToArray())
                : ModelSchemaReader.FromAssemblies(_options.DefaultSchema, typeof(PostgresColumnTypeAttribute), typeof(PostgresTableName), _modelAssemblies.ToArray());
            var databaseTables = await LoadDatabaseSchemaAsync(cancellationToken).ConfigureAwait(false);
            var builder = new ModelSyncPlanBuilder(Quote, Qualify, BuildCreateTableSql, BuildAddColumnSql, BuildAddDefaultConstraintSql, BuildAddCheckConstraintSql, BuildAddUniqueConstraintSql, BuildAddForeignKeySql, BuildCreateIndexSql);
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
            return plans.Where(p => p.HasChanges || ShouldApplyEveryRun(p.Definition.Category)).Select(plan =>
            {
                var script = plan.Definition;
                return new ModelSyncPlanItem
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
                            await ExecuteSqlAsync(script.Sql, ct).ConfigureAwait(false);
                        else
                        {
                            var single = CreateRunner();
                            single.RegisterScript(script);
                            await single.RunAsync(ct).ConfigureAwait(false);
                        }
                    }
                };
            }).ToList();
        }

        private bool ShouldApplyEveryRun(MigrationScriptCategory category)
            => category == MigrationScriptCategory.StoredProcedures && _options.ApplyStoredProceduresOnEveryRun
               || category == MigrationScriptCategory.Triggers && _options.ApplyTriggersOnEveryRun
               || category == MigrationScriptCategory.Seeds && !_options.ApplySeedsWithHashTracking
               || category == MigrationScriptCategory.CustomSql && !_options.ApplyCustomSqlWithHashTracking;

        private PostgresMigrationRunner CreateRunner()
        {
            var options = new MigrationRunnerOptions
            {
                HistorySchema = _options.HistorySchema,
                EnsureHistoryTables = true,
                AutoAddMissingColumnsFromTableScripts = true,
                DestructiveOptions = _options.AllowDestructiveChanges ? DestructiveOperationOptions.Allow() : null
            };
            options.Schemas.Add(_options.HistorySchema);
            options.Schemas.Add(_options.DefaultSchema);
            return new PostgresMigrationRunner(_options.ConnectionString, options);
        }

        private async Task<IDictionary<string, DatabaseTableDefinition>> LoadDatabaseSchemaAsync(CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, DatabaseTableDefinition>(StringComparer.OrdinalIgnoreCase);
            using (var connection = new NpgsqlConnection(_options.ConnectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var ensure = new NpgsqlCommand($"CREATE SCHEMA IF NOT EXISTS {Quote(_options.DefaultSchema)};", connection))
                    await ensure.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

                const string columnsSql = @"
SELECT table_schema, table_name, column_name, data_type, character_maximum_length, numeric_precision, numeric_scale, is_nullable,
       CASE WHEN column_default IS NULL THEN 0 ELSE 1 END AS has_default
FROM information_schema.columns
WHERE table_schema = @Schema;";
                using (var command = new NpgsqlCommand(columnsSql, connection))
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
                                StoreType = BuildPostgresStoreType(reader.GetString(3), reader.IsDBNull(4) ? 0 : Convert.ToInt64(reader.GetValue(4)), reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetValue(5)), reader.IsDBNull(6) ? 0 : Convert.ToInt32(reader.GetValue(6))),
                                IsNullable = string.Equals(reader.GetString(7), "YES", StringComparison.OrdinalIgnoreCase),
                                HasDefault = Convert.ToInt32(reader.GetValue(8)) == 1
                            };
                        }
                    }
                }
                await LoadIndexesAndConstraintsAsync(connection, result, cancellationToken).ConfigureAwait(false);
            }
            return result;
        }

        private async Task LoadIndexesAndConstraintsAsync(NpgsqlConnection connection, IDictionary<string, DatabaseTableDefinition> result, CancellationToken cancellationToken)
        {
            const string indexesSql = @"
SELECT schemaname, tablename, indexname FROM pg_indexes WHERE schemaname = @Schema;";
            using (var command = new NpgsqlCommand(indexesSql, connection))
            {
                command.Parameters.AddWithValue("@Schema", _options.DefaultSchema);
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        if (result.TryGetValue(ModelSyncPlanBuilder.Key(reader.GetString(0), reader.GetString(1)), out var table))
                            table.Indexes.Add(reader.GetString(2));
            }

            const string constraintsSql = @"
SELECT n.nspname, t.relname, c.conname, c.contype
FROM pg_constraint c
JOIN pg_class t ON t.oid = c.conrelid
JOIN pg_namespace n ON n.oid = t.relnamespace
WHERE n.nspname = @Schema;";
            using (var command = new NpgsqlCommand(constraintsSql, connection))
            {
                command.Parameters.AddWithValue("@Schema", _options.DefaultSchema);
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        if (!result.TryGetValue(ModelSyncPlanBuilder.Key(reader.GetString(0), reader.GetString(1)), out var table))
                            continue;
                        var type = reader.GetChar(3);
                        if (type == 'u')
                            table.UniqueConstraints.Add(reader.GetString(2));
                        if (type == 'f')
                            table.ForeignKeys.Add(reader.GetString(2));
                        if (type == 'c')
                            foreach (var column in table.Columns.Values)
                                column.HasCheck = true;
                    }
                }
            }

            const string constraintColumnsSql = @"
SELECT n.nspname, t.relname, c.contype, a.attname, rt.relname
FROM pg_constraint c
JOIN pg_class t ON t.oid = c.conrelid
JOIN pg_namespace n ON n.oid = t.relnamespace
JOIN LATERAL unnest(c.conkey) AS ck(attnum) ON true
JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = ck.attnum
LEFT JOIN pg_class rt ON rt.oid = c.confrelid
WHERE n.nspname = @Schema
  AND c.contype IN ('u', 'f');";
            using (var command = new NpgsqlCommand(constraintColumnsSql, connection))
            {
                command.Parameters.AddWithValue("@Schema", _options.DefaultSchema);
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        if (!result.TryGetValue(ModelSyncPlanBuilder.Key(reader.GetString(0), reader.GetString(1)), out var table))
                            continue;

                        var type = reader.GetChar(2);
                        if (type == 'u')
                            table.UniqueConstraints.Add($"UQ_{reader.GetString(1)}_{reader.GetString(3)}");
                        if (type == 'f' && !reader.IsDBNull(4))
                            table.ForeignKeys.Add($"FK_{reader.GetString(1)}_{reader.GetString(3)}_{reader.GetString(4)}");
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
            => $"ALTER TABLE {Qualify(table.Schema, table.Name)} ADD COLUMN {BuildColumnDefinition(column, true)};";

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
            => $"ALTER TABLE {Qualify(table.Schema, table.Name)} ALTER COLUMN {Quote(column.Name)} SET DEFAULT {column.DefaultSql};";

        private string BuildAddCheckConstraintSql(ModelTableDefinition table, ModelColumnDefinition column)
            => $"ALTER TABLE {Qualify(table.Schema, table.Name)} ADD CONSTRAINT {Quote($"CK_{table.Name}_{column.Name}")} CHECK ({column.CheckSql});";

        private string BuildAddUniqueConstraintSql(ModelTableDefinition table, ModelColumnDefinition column)
            => $"ALTER TABLE {Qualify(table.Schema, table.Name)} ADD CONSTRAINT {Quote($"UQ_{table.Name}_{column.Name}")} UNIQUE ({Quote(column.Name)});";

        private string BuildAddForeignKeySql(ModelTableDefinition table, ModelColumnDefinition column)
            => $"ALTER TABLE {Qualify(table.Schema, table.Name)} ADD CONSTRAINT {Quote($"FK_{table.Name}_{ForeignKeyColumn(column)}_{column.ForeignKeyTable}")} FOREIGN KEY ({Quote(ForeignKeyColumn(column))}) REFERENCES {Qualify(table.Schema, column.ForeignKeyTable)} ({Quote(column.ForeignKeyReferenceColumn)});";

        private static string ForeignKeyColumn(ModelColumnDefinition column)
            => string.IsNullOrWhiteSpace(column.ForeignKeyColumn) ? column.Name : column.ForeignKeyColumn;

        private string BuildCreateIndexSql(ModelTableDefinition table, ModelColumnDefinition column)
        {
            var indexName = string.IsNullOrWhiteSpace(column.IndexName) ? $"idx_{table.Name}_{column.Name}" : column.IndexName;
            return $"CREATE {(column.IsUniqueIndex ? "UNIQUE " : string.Empty)}INDEX {Quote(indexName)} ON {Qualify(table.Schema, table.Name)} ({Quote(column.Name)});";
        }

        private async Task ExecuteSqlAsync(string sql, CancellationToken cancellationToken)
        {
            using (var connection = new NpgsqlConnection(_options.ConnectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = new NpgsqlCommand(sql, connection))
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private static string Qualify(string schema, string table)
            => $"{Quote(schema)}.{Quote(table)}";

        private static string Quote(string identifier)
        {
            ValidateIdentifier(identifier, nameof(identifier));
            return "\"" + identifier.Replace("\"", "\"\"") + "\"";
        }

        private static void ValidateIdentifier(string identifier, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(identifier) || !SafeIdentifierPattern.IsMatch(identifier))
                throw new ArgumentException($"Invalid SQL identifier '{identifier}'.", parameterName);
        }

        private static string BuildPostgresStoreType(string type, long length, int precision, int scale)
        {
            var lower = type.ToLowerInvariant();
            if ((lower == "character varying" || lower == "varchar") && length > 0)
                return $"VARCHAR({length})";
            if ((lower == "numeric" || lower == "decimal") && precision > 0)
                return $"NUMERIC({precision},{scale})";
            if (lower == "integer")
                return "INTEGER";
            if (lower == "bigint")
                return "BIGINT";
            if (lower == "timestamp with time zone")
                return "TIMESTAMPTZ";
            return type.ToUpperInvariant();
        }
    }
}
