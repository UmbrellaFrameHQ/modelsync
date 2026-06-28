using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.Services;

namespace UmbrellaFrame.ModelSync.MySql
{
    public sealed class MySqlModelSyncOptions : ModelSyncOptions
    {
        public MySqlModelSyncOptions()
        {
            DefaultSchema = string.Empty;
            HistorySchema = string.Empty;
        }
    }

    public sealed class MySqlModelSynchronizer
    {
        private static readonly Regex SafeIdentifierPattern = new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
        private readonly MySqlModelSyncOptions _options;
        private readonly List<Assembly> _modelAssemblies;
        private readonly List<Type> _modelTypes;
        private readonly List<MigrationScriptDefinition> _scripts = new List<MigrationScriptDefinition>();

        private MySqlModelSynchronizer(MySqlModelSyncOptions options, IEnumerable<Assembly> modelAssemblies, IEnumerable<Type> modelTypes)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(_options.ConnectionString))
                throw new ArgumentException("ConnectionString is required.", nameof(options));
            _modelAssemblies = modelAssemblies?.Where(a => a != null).Distinct().ToList() ?? new List<Assembly>();
            _modelTypes = modelTypes?.Where(t => t != null).Distinct().ToList() ?? new List<Type>();
            if (_modelAssemblies.Count == 0 && _modelTypes.Count == 0)
                throw new ArgumentException("At least one model assembly or model type is required.");
            if (string.IsNullOrWhiteSpace(_options.DefaultSchema))
                _options.DefaultSchema = new MySqlConnectionStringBuilder(_options.ConnectionString).Database;
        }

        public static MySqlModelSynchronizer FromAssemblies(MySqlModelSyncOptions options, params Assembly[] assemblies)
            => new MySqlModelSynchronizer(options, assemblies, Array.Empty<Type>());

        public static MySqlModelSynchronizer FromTypes(MySqlModelSyncOptions options, params Type[] modelTypes)
            => new MySqlModelSynchronizer(options, Array.Empty<Assembly>(), modelTypes);

        public MySqlModelSynchronizer AddSqlScriptsFromEmbeddedResources(Assembly assembly, string rootNamespace)
        {
            foreach (var definition in MigrationScriptDiscovery.FromEmbeddedResources(assembly, rootNamespace))
                _scripts.Add(definition);
            return this;
        }

        public MySqlModelSynchronizer AddSqlScript(MigrationScriptDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            _scripts.Add(definition);
            return this;
        }

        public async Task<ModelSyncResult> CompareAsync(CancellationToken cancellationToken = default)
        {
            ValidateIdentifier(_options.DefaultSchema, nameof(_options.DefaultSchema));
            var modelTables = _modelTypes.Count > 0
                ? ModelSchemaReader.FromTypes(_options.DefaultSchema, typeof(MySqlColumnTypeAttribute), typeof(MySqlTableNameAttribute), _modelTypes.ToArray())
                : ModelSchemaReader.FromAssemblies(_options.DefaultSchema, typeof(MySqlColumnTypeAttribute), typeof(MySqlTableNameAttribute), _modelAssemblies.ToArray());
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
                        {
                            await ExecuteSqlAsync(script.Sql, ct).ConfigureAwait(false);
                        }
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

        private MySqlMigrationRunner CreateRunner()
            => new MySqlMigrationRunner(_options.ConnectionString, new MigrationRunnerOptions
            {
                HistorySchema = _options.HistorySchema,
                EnsureHistoryTables = true,
                AutoAddMissingColumnsFromTableScripts = true,
                DestructiveOptions = _options.AllowDestructiveChanges ? DestructiveOperationOptions.Allow() : null
            });

        private async Task<IDictionary<string, DatabaseTableDefinition>> LoadDatabaseSchemaAsync(CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, DatabaseTableDefinition>(StringComparer.OrdinalIgnoreCase);
            using (var connection = new MySqlConnection(_options.ConnectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                const string columnsSql = @"
SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE, IS_NULLABLE,
       CASE WHEN COLUMN_DEFAULT IS NULL THEN 0 ELSE 1 END AS HasDefault
FROM information_schema.columns
WHERE TABLE_SCHEMA = @Schema;";
                using (var command = new MySqlCommand(columnsSql, connection))
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
                                StoreType = BuildMySqlStoreType(reader.GetString(3), reader.IsDBNull(4) ? 0 : Convert.ToInt64(reader.GetValue(4)), reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetValue(5)), reader.IsDBNull(6) ? 0 : Convert.ToInt32(reader.GetValue(6))),
                                IsNullable = string.Equals(reader.GetString(7), "YES", StringComparison.OrdinalIgnoreCase),
                                HasDefault = Convert.ToInt32(reader.GetValue(8)) == 1
                            };
                        }
                    }
                }

                await LoadIndexesAsync(connection, result, cancellationToken).ConfigureAwait(false);
                await LoadConstraintsAsync(connection, result, cancellationToken).ConfigureAwait(false);
            }
            return result;
        }

        private async Task LoadIndexesAsync(MySqlConnection connection, IDictionary<string, DatabaseTableDefinition> result, CancellationToken cancellationToken)
        {
            const string sql = @"SELECT TABLE_SCHEMA, TABLE_NAME, INDEX_NAME FROM information_schema.statistics WHERE TABLE_SCHEMA = @Schema AND INDEX_NAME <> 'PRIMARY';";
            using (var command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Schema", _options.DefaultSchema);
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        if (result.TryGetValue(ModelSyncPlanBuilder.Key(reader.GetString(0), reader.GetString(1)), out var table))
                            table.Indexes.Add(reader.GetString(2));
            }
        }

        private async Task LoadConstraintsAsync(MySqlConnection connection, IDictionary<string, DatabaseTableDefinition> result, CancellationToken cancellationToken)
        {
            const string sql = @"SELECT TABLE_SCHEMA, TABLE_NAME, CONSTRAINT_NAME, CONSTRAINT_TYPE FROM information_schema.table_constraints WHERE TABLE_SCHEMA = @Schema;";
            using (var command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Schema", _options.DefaultSchema);
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        if (!result.TryGetValue(ModelSyncPlanBuilder.Key(reader.GetString(0), reader.GetString(1)), out var table))
                            continue;
                        var type = reader.GetString(3);
                        if (type == "UNIQUE")
                            table.UniqueConstraints.Add(reader.GetString(2));
                        if (type == "FOREIGN KEY")
                            table.ForeignKeys.Add(reader.GetString(2));
                    }
                }
            }

            const string keyColumns = @"
SELECT k.TABLE_SCHEMA, k.TABLE_NAME, k.CONSTRAINT_NAME, tc.CONSTRAINT_TYPE, k.COLUMN_NAME, k.REFERENCED_TABLE_NAME
FROM information_schema.key_column_usage k
JOIN information_schema.table_constraints tc
  ON tc.CONSTRAINT_SCHEMA = k.CONSTRAINT_SCHEMA
 AND tc.TABLE_NAME = k.TABLE_NAME
 AND tc.CONSTRAINT_NAME = k.CONSTRAINT_NAME
WHERE k.TABLE_SCHEMA = @Schema
  AND tc.CONSTRAINT_TYPE IN ('UNIQUE', 'FOREIGN KEY');";
            using (var command = new MySqlCommand(keyColumns, connection))
            {
                command.Parameters.AddWithValue("@Schema", _options.DefaultSchema);
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        if (!result.TryGetValue(ModelSyncPlanBuilder.Key(reader.GetString(0), reader.GetString(1)), out var table))
                            continue;

                        var type = reader.GetString(3);
                        if (type == "UNIQUE")
                            table.UniqueConstraints.Add($"UQ_{reader.GetString(1)}_{reader.GetString(4)}");
                        if (type == "FOREIGN KEY" && !reader.IsDBNull(5))
                            table.ForeignKeys.Add($"FK_{reader.GetString(1)}_{reader.GetString(4)}_{reader.GetString(5)}");
                    }
                }
            }

            const string checks = @"SELECT CONSTRAINT_SCHEMA, TABLE_NAME, CONSTRAINT_NAME FROM information_schema.check_constraints WHERE CONSTRAINT_SCHEMA = @Schema;";
            using (var command = new MySqlCommand(checks, connection))
            {
                command.Parameters.AddWithValue("@Schema", _options.DefaultSchema);
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        if (result.TryGetValue(ModelSyncPlanBuilder.Key(reader.GetString(0), reader.GetString(1)), out var table))
                            foreach (var column in table.Columns.Values)
                                column.HasCheck = true;
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
            => $"ALTER TABLE {Qualify(table.Schema, table.Name)} ALTER {Quote(column.Name)} SET DEFAULT {column.DefaultSql};";

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
            using (var connection = new MySqlConnection(_options.ConnectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = new MySqlCommand(sql, connection))
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private static string Qualify(string schema, string table)
            => string.IsNullOrWhiteSpace(schema) ? Quote(table) : $"{Quote(schema)}.{Quote(table)}";

        private static string Quote(string identifier)
        {
            ValidateIdentifier(identifier, nameof(identifier));
            return "`" + identifier.Replace("`", "``") + "`";
        }

        private static void ValidateIdentifier(string identifier, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(identifier) || !SafeIdentifierPattern.IsMatch(identifier))
                throw new ArgumentException($"Invalid SQL identifier '{identifier}'.", parameterName);
        }

        private static string BuildMySqlStoreType(string type, long length, int precision, int scale)
        {
            var lower = type.ToLowerInvariant();
            if ((lower == "varchar" || lower == "char") && length > 0)
                return $"{type.ToUpperInvariant()}({length})";
            if ((lower == "decimal" || lower == "numeric") && precision > 0)
                return $"{type.ToUpperInvariant()}({precision},{scale})";
            return type.ToUpperInvariant();
        }
    }
}
