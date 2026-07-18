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
using UmbrellaFrame.ModelSync.Core.SqlGeneration;

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
        private static readonly ModelSyncSqlDialect Dialect = new ModelSyncSqlDialect(SQLiteProviderDescriptor.Create());
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
            var attributes = new ProviderAttributeSet(
                typeof(SQLiteTableNameAttribute),
                typeof(SQLiteColumnTypeAttribute),
                typeof(SQLiteColumnPrimaryKeyAttribute),
                typeof(SQLiteColumnNotNullAttribute),
                typeof(SQLiteColumnUniqueAttribute),
                typeof(SQLiteColumnForeignKeyAttribute),
                (pk, column) =>
                    string.Equals(column.StoreType, "INTEGER", StringComparison.OrdinalIgnoreCase)
                        ? DbValueGenerationKind.RowIdAlias
                        : DbValueGenerationKind.None);

            var modelTables = _modelTypes.Count > 0
                ? ModelSchemaReader.FromTypes(_options.DefaultSchema, attributes, _modelTypes.ToArray())
                : ModelSchemaReader.FromAssemblies(_options.DefaultSchema, attributes, _modelAssemblies.ToArray());
            var databaseTables = await LoadDatabaseSchemaAsync(cancellationToken).ConfigureAwait(false);
            var builder = new ModelSyncPlanBuilder(Dialect.Quote, Dialect.Qualify, Dialect.BuildCreateTableSql, Dialect.BuildAddColumnSql, Dialect.BuildAddDefaultConstraintSql, Dialect.BuildAddCheckConstraintSql, Dialect.BuildAddUniqueConstraintSql, Dialect.BuildAddForeignKeySql, Dialect.BuildCreateIndexSql);
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
                HistorySchema = _options.HistorySchema,
                EnsureHistoryTables = true,
                AutoAddMissingColumnsFromTableScripts = false,
                DestructiveOptions = _options.AllowDestructiveChanges ? DestructiveOperationOptions.Allow() : null
            });
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
                            var single = new SQLiteMigrationRunner(_options.ConnectionString);
                            single.RegisterScript(script);
                            await single.RunAsync(ct).ConfigureAwait(false);
                        }
                    }
                };
            }).ToList();
        }

        private bool ShouldApplyEveryRun(MigrationScriptCategory category)
            => category == MigrationScriptCategory.Triggers && _options.ApplyTriggersOnEveryRun
               || category == MigrationScriptCategory.Seeds && !_options.ApplySeedsWithHashTracking
               || category == MigrationScriptCategory.CustomSql && !_options.ApplyCustomSqlWithHashTracking;

        private async Task<IDictionary<string, DatabaseTableDefinition>> LoadDatabaseSchemaAsync(CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, DatabaseTableDefinition>(StringComparer.OrdinalIgnoreCase);
            using (var connection = SQLiteConnectionFactory.Create(_options.ConnectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                var tables = new List<string>();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = Dialect.BuildReadFileCatalogTablesPlan().CommandText;
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
                        command.CommandText = Dialect.BuildReadFileCatalogTableInfoPlan(tableName).CommandText;
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
                        var indexes = new List<DatabaseIndexDefinition>();
                        command.CommandText = Dialect.BuildReadFileCatalogIndexListPlan(tableName).CommandText;
                        using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                            {
                                var indexName = reader.GetString(1);
                                table.Indexes.Add(indexName);
                                indexes.Add(new DatabaseIndexDefinition
                                {
                                    Name = indexName,
                                    IsUnique = reader.GetInt32(2) == 1
                                });
                            }
                        }

                        foreach (var index in indexes)
                        {
                            await LoadSQLiteIndexColumnsAsync(connection, index.Name, index, cancellationToken).ConfigureAwait(false);
                            if (index.Columns.Count > 0)
                                table.SemanticIndexes.Add(index);
                        }
                    }

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = Dialect.BuildReadFileCatalogForeignKeysPlan(tableName).CommandText;
                        using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                            {
                                var fk = new DatabaseForeignKeyDefinition
                                {
                                    Name = $"FK_{tableName}_{reader.GetString(3)}_{reader.GetString(2)}",
                                    ReferencedSchema = _options.DefaultSchema,
                                    ReferencedTable = reader.GetString(2)
                                };
                                fk.LocalColumns.Add(reader.GetString(3));
                                fk.ReferencedColumns.Add(reader.GetString(4));
                                table.ForeignKeys.Add(fk.Name);
                                table.SemanticForeignKeys.Add(fk);
                            }
                        }
                    }
                }
            }
            return result;
        }

        private static async Task LoadSQLiteIndexColumnsAsync(SqliteConnection connection, string indexName, DatabaseIndexDefinition index, CancellationToken cancellationToken)
        {
            ValidateIdentifier(indexName, nameof(indexName));
            using (var command = connection.CreateCommand())
            {
                command.CommandText = Dialect.BuildReadFileCatalogIndexInfoPlan(indexName).CommandText;
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        index.Columns.Add(reader.GetString(2));
                }
            }
        }

        private async Task ExecuteSqlAsync(string sql, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new NotSupportedException("SQLite cannot add this constraint after table creation. Use a reviewed migration script with create-copy-rename strategy.");
            using (var connection = SQLiteConnectionFactory.Create(_options.ConnectionString))
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
            => Dialect.Qualify(schema, table);

        private static string Quote(string identifier)
            => Dialect.Quote(identifier);

        private static void ValidateIdentifier(string identifier, string parameterName)
            => SqlIdentifierValidator.Validate(identifier, parameterName);
    }
}
