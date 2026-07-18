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
using UmbrellaFrame.ModelSync.Core.SqlGeneration;

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
        private static readonly ModelSyncSqlDialect Dialect = new ModelSyncSqlDialect(MySqlProviderDescriptor.Create());
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
            var attributes = new ProviderAttributeSet(
                typeof(MySqlTableNameAttribute),
                typeof(MySqlColumnTypeAttribute),
                typeof(MySqlColumnPrimaryKeyAttribute),
                typeof(MySqlColumnNotNullAttribute),
                typeof(MySqlColumnUniqueAttribute),
                typeof(MySqlForeignKeyAttribute),
                (pk, column) => IsAutoIncrement(pk) ? DbValueGenerationKind.AutoIncrement : DbValueGenerationKind.None);

            var modelTables = _modelTypes.Count > 0
                ? ModelSchemaReader.FromTypes(_options.DefaultSchema, attributes, _modelTypes.ToArray())
                : ModelSchemaReader.FromAssemblies(_options.DefaultSchema, attributes, _modelAssemblies.ToArray());
            var databaseTables = await LoadDatabaseSchemaAsync(cancellationToken).ConfigureAwait(false);
            var builder = new ModelSyncPlanBuilder(
                Dialect.Quote,
                Dialect.Qualify,
                Dialect.BuildCreateTableSql,
                Dialect.BuildAddColumnSql,
                Dialect.BuildAddDefaultConstraintSql,
                Dialect.BuildAddCheckConstraintSql,
                Dialect.BuildAddUniqueConstraintSql,
                Dialect.BuildAddForeignKeySql,
                Dialect.BuildCreateIndexSql);
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
                AutoAddMissingColumnsFromTableScripts = false,
                DestructiveOptions = _options.AllowDestructiveChanges ? DestructiveOperationOptions.Allow() : null
            });

        private async Task<IDictionary<string, DatabaseTableDefinition>> LoadDatabaseSchemaAsync(CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, DatabaseTableDefinition>(StringComparer.OrdinalIgnoreCase);
            using (var connection = MySqlConnectionFactory.Create(_options.ConnectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = new MySqlCommand(Dialect.BuildReadColumnsPlan().CommandText, connection))
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
            using (var command = new MySqlCommand(Dialect.BuildReadIndexesPlan().CommandText, connection))
            {
                command.Parameters.AddWithValue("@Schema", _options.DefaultSchema);
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    var semanticIndexes = new Dictionary<string, DatabaseIndexDefinition>(StringComparer.OrdinalIgnoreCase);
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        if (result.TryGetValue(ModelSyncPlanBuilder.Key(reader.GetString(0), reader.GetString(1)), out var table))
                        {
                            var indexName = reader.GetString(2);
                            table.Indexes.Add(indexName);
                            var key = ModelSyncPlanBuilder.Key(reader.GetString(1), indexName);
                            if (!semanticIndexes.TryGetValue(key, out var index))
                            {
                                index = new DatabaseIndexDefinition { Name = indexName, IsUnique = Convert.ToInt32(reader.GetValue(3)) == 0 };
                                semanticIndexes[key] = index;
                                table.SemanticIndexes.Add(index);
                            }
                            index.Columns.Add(reader.GetString(4));
                        }
                    }
                }
            }
        }

        private async Task LoadConstraintsAsync(MySqlConnection connection, IDictionary<string, DatabaseTableDefinition> result, CancellationToken cancellationToken)
        {
            using (var command = new MySqlCommand(Dialect.BuildReadConstraintsPlan().CommandText, connection))
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

            using (var command = new MySqlCommand(Dialect.BuildReadConstraintColumnsPlan().CommandText, connection))
            {
                command.Parameters.AddWithValue("@Schema", _options.DefaultSchema);
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    var semanticForeignKeys = new Dictionary<string, DatabaseForeignKeyDefinition>(StringComparer.OrdinalIgnoreCase);
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        if (!result.TryGetValue(ModelSyncPlanBuilder.Key(reader.GetString(0), reader.GetString(1)), out var table))
                            continue;

                        var type = reader.GetString(3);
                        if (type == "UNIQUE")
                            table.UniqueConstraints.Add($"UQ_{reader.GetString(1)}_{reader.GetString(4)}");
                        if (type == "FOREIGN KEY" && !reader.IsDBNull(6))
                        {
                            table.ForeignKeys.Add($"FK_{reader.GetString(1)}_{reader.GetString(4)}_{reader.GetString(6)}");
                            var key = ModelSyncPlanBuilder.Key(reader.GetString(1), reader.GetString(2));
                            if (!semanticForeignKeys.TryGetValue(key, out var foreignKey))
                            {
                                foreignKey = new DatabaseForeignKeyDefinition
                                {
                                    Name = reader.GetString(2),
                                    ReferencedSchema = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                                    ReferencedTable = reader.GetString(6)
                                };
                                semanticForeignKeys[key] = foreignKey;
                                table.SemanticForeignKeys.Add(foreignKey);
                            }
                            foreignKey.LocalColumns.Add(reader.GetString(4));
                            foreignKey.ReferencedColumns.Add(reader.GetString(7));
                        }
                    }
                }
            }

            using (var command = new MySqlCommand(Dialect.BuildReadChecksPlan().CommandText, connection))
            {
                command.Parameters.AddWithValue("@Schema", _options.DefaultSchema);
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        if (result.TryGetValue(ModelSyncPlanBuilder.Key(reader.GetString(0), reader.GetString(1)), out var table))
                            foreach (var column in table.Columns.Values)
                                column.HasCheck = true;
            }
        }

        private static bool IsAutoIncrement(DbColumnPrimaryKeyAttribute attribute)
        {
            var property = attribute.GetType().GetProperty("IsAutoIncrement");
            return property != null && property.PropertyType == typeof(bool) && (bool)property.GetValue(attribute, null);
        }

        private async Task ExecuteSqlAsync(string sql, CancellationToken cancellationToken)
        {
            using (var connection = MySqlConnectionFactory.Create(_options.ConnectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = new MySqlCommand(sql, connection))
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private static string Qualify(string schema, string table)
            => Dialect.Qualify(schema, table);

        private static string Quote(string identifier)
            => Dialect.Quote(identifier);

        private static void ValidateIdentifier(string identifier, string parameterName)
            => SqlIdentifierValidator.Validate(identifier, parameterName);

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
