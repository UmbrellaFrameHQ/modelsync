using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.Services;

namespace UmbrellaFrame.ModelSync.SQLite
{
    public sealed class SQLiteModelSyncOptions : ModelSyncOptions
    {
        public SQLiteModelSyncOptions()
        {
            DefaultSchema = "main";
            HistorySchema = "main";
            AddMissingConstraints = false;
        }
    }

    public sealed class SQLiteModelSynchronizer
    {
        private static readonly Regex SafeIdentifierPattern = new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
        private readonly SQLiteModelSyncOptions _options;
        private readonly List<Assembly> _modelAssemblies;
        private readonly List<Type> _modelTypes;
        private readonly List<MigrationScriptDefinition> _scripts = new List<MigrationScriptDefinition>();

        private SQLiteModelSynchronizer(SQLiteModelSyncOptions options, IEnumerable<Assembly> modelAssemblies, IEnumerable<Type> modelTypes)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(_options.ConnectionString))
                throw new ArgumentException("ConnectionString is required.", nameof(options));
            _modelAssemblies = modelAssemblies?.Where(a => a != null).Distinct().ToList() ?? new List<Assembly>();
            _modelTypes = modelTypes?.Where(t => t != null).Distinct().ToList() ?? new List<Type>();
            if (_modelAssemblies.Count == 0 && _modelTypes.Count == 0)
                throw new ArgumentException("At least one model assembly or model type is required.");
        }

        public static SQLiteModelSynchronizer FromAssemblies(SQLiteModelSyncOptions options, params Assembly[] assemblies)
            => new SQLiteModelSynchronizer(options, assemblies, Array.Empty<Type>());

        public static SQLiteModelSynchronizer FromTypes(SQLiteModelSyncOptions options, params Type[] modelTypes)
            => new SQLiteModelSynchronizer(options, Array.Empty<Assembly>(), modelTypes);

        public SQLiteModelSynchronizer AddSqlScriptsFromEmbeddedResources(Assembly assembly, string rootNamespace)
        {
            foreach (var definition in MigrationScriptDiscovery.FromEmbeddedResources(assembly, rootNamespace))
                _scripts.Add(definition);
            return this;
        }

        public SQLiteModelSynchronizer AddSqlScript(MigrationScriptDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            _scripts.Add(definition);
            return this;
        }

        public async Task<ModelSyncResult> CompareAsync(CancellationToken cancellationToken = default)
        {
            var modelTables = _modelTypes.Count > 0
                ? ModelSchemaReader.FromTypes(_options.DefaultSchema, _modelTypes.ToArray())
                : ModelSchemaReader.FromAssemblies(_options.DefaultSchema, _modelAssemblies.ToArray());
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

            if (_scripts.Any(s => s.Category == MigrationScriptCategory.StoredProcedures))
            {
                return _scripts.Where(s => s.Category == MigrationScriptCategory.StoredProcedures)
                    .Select(s => new ModelSyncPlanItem
                    {
                        ChangeType = ModelSyncChangeType.Unsupported,
                        Risk = ModelSyncOperationRisk.Unsupported,
                        Name = s.Name,
                        Reason = "SQLite does not support stored procedures.",
                        CanApplyAutomatically = false
                    })
                    .ToList();
            }

            var runner = new SQLiteMigrationRunner(_options.ConnectionString, new MigrationRunnerOptions
            {
                EnsureHistoryTables = true,
                AutoAddMissingColumnsFromTableScripts = true,
                DestructiveOptions = _options.AllowDestructiveChanges ? DestructiveOperationOptions.Allow() : null
            });
            foreach (var script in _scripts)
                runner.RegisterScript(script);
            var plans = await runner.CompareRegisteredAsync(cancellationToken).ConfigureAwait(false);
            return plans.Where(p => p.HasChanges || p.Definition.Category == MigrationScriptCategory.Triggers && _options.ApplyTriggersOnEveryRun).Select(plan =>
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
                        if (script.Category == MigrationScriptCategory.Triggers && _options.ApplyTriggersOnEveryRun)
                            await ExecuteSqlAsync(script.Sql, ct).ConfigureAwait(false);
                        else
                        {
                            var single = new SQLiteMigrationRunner(_options.ConnectionString);
                            single.RegisterScript(script);
                            await single.RunAsync(ct).ConfigureAwait(false);
                        }
                    }
                };
            }).ToList();
        }

        private async Task<IDictionary<string, DatabaseTableDefinition>> LoadDatabaseSchemaAsync(CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, DatabaseTableDefinition>(StringComparer.OrdinalIgnoreCase);
            using (var connection = new SqliteConnection(_options.ConnectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                var tables = new List<string>();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';";
                    using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                            tables.Add(reader.GetString(0));
                }

                foreach (var tableName in tables)
                {
                    ValidateIdentifier(tableName, nameof(tableName));
                    var table = new DatabaseTableDefinition { Schema = _options.DefaultSchema, Name = tableName };
                    result[ModelSyncPlanBuilder.Key(_options.DefaultSchema, tableName)] = table;

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = $"PRAGMA table_info({Quote(tableName)});";
                        using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                            {
                                table.Columns[reader.GetString(1)] = new DatabaseColumnDefinition
                                {
                                    Name = reader.GetString(1),
                                    StoreType = reader.IsDBNull(2) ? string.Empty : reader.GetString(2).ToUpperInvariant(),
                                    IsNullable = reader.GetInt32(3) == 0,
                                    HasDefault = !reader.IsDBNull(4)
                                };
                            }
                        }
                    }

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = $"PRAGMA index_list({Quote(tableName)});";
                        using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                                table.Indexes.Add(reader.GetString(1));
                    }
                }
            }
            return result;
        }

        private string BuildCreateTableSql(ModelTableDefinition table)
        {
            var lines = new List<string>();
            var primaryKeys = table.Columns.Where(c => c.IsPrimaryKey).Select(c => c.Name).ToList();
            foreach (var column in table.Columns)
                lines.Add("    " + BuildColumnDefinition(column, primaryKeys.Count <= 1));
            if (primaryKeys.Count > 1)
                lines.Add("    PRIMARY KEY (" + string.Join(", ", primaryKeys.Select(Quote)) + ")");
            return $"CREATE TABLE {Quote(table.Name)} ({Environment.NewLine}{string.Join("," + Environment.NewLine, lines)}{Environment.NewLine});";
        }

        private string BuildAddColumnSql(ModelTableDefinition table, ModelColumnDefinition column)
            => $"ALTER TABLE {Quote(table.Name)} ADD COLUMN {BuildColumnDefinition(column, true)};";

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
            => string.Empty;

        private string BuildAddCheckConstraintSql(ModelTableDefinition table, ModelColumnDefinition column)
            => string.Empty;

        private string BuildAddUniqueConstraintSql(ModelTableDefinition table, ModelColumnDefinition column)
            => $"CREATE UNIQUE INDEX {Quote($"UQ_{table.Name}_{column.Name}")} ON {Quote(table.Name)} ({Quote(column.Name)});";

        private string BuildAddForeignKeySql(ModelTableDefinition table, ModelColumnDefinition column)
            => string.Empty;

        private string BuildCreateIndexSql(ModelTableDefinition table, ModelColumnDefinition column)
        {
            var indexName = string.IsNullOrWhiteSpace(column.IndexName) ? $"idx_{table.Name}_{column.Name}" : column.IndexName;
            return $"CREATE {(column.IsUniqueIndex ? "UNIQUE " : string.Empty)}INDEX {Quote(indexName)} ON {Quote(table.Name)} ({Quote(column.Name)});";
        }

        private async Task ExecuteSqlAsync(string sql, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new NotSupportedException("SQLite cannot add this constraint after table creation. Use a reviewed migration script with create-copy-rename strategy.");
            using (var connection = new SqliteConnection(_options.ConnectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private static string Qualify(string schema, string table)
            => Quote(table);

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
    }
}
