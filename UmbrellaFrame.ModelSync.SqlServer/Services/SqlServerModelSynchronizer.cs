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
using UmbrellaFrame.ModelSync.Core.SqlGeneration;

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
        private static readonly ModelSyncSqlDialect Dialect = new ModelSyncSqlDialect(SqlServerProviderDescriptor.Create());
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

            var attributes = new ProviderAttributeSet(
                typeof(SqlServerTableNameAttribute),
                typeof(SqlServerColumnTypeAttribute),
                typeof(SqlServerColumnPrimaryKeyAttribute),
                typeof(SqlServerColumnNotNullAttribute),
                typeof(SqlServerColumnUniqueAttribute),
                typeof(SqlServerColumnForeignKey),
                (pk, column) =>
                {
                    if (IsAutoIncrement(pk))
                    {
                        column.IdentitySeed = 1;
                        column.IdentityIncrement = 1;
                        return DbValueGenerationKind.Identity;
                    }
                    return DbValueGenerationKind.None;
                });

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
                            var sql = script.Category == MigrationScriptCategory.StoredProcedures
                                ? SqlServerStoredProcedureSynchronizer.ToCreateOrAlterSql(script.Sql)
                                : script.Sql;
                            foreach (var batch in SqlBatchSplitter.SplitGoBatches(sql))
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
            if (category == MigrationScriptCategory.Seeds)
                return !_options.ApplySeedsWithHashTracking;
            if (category == MigrationScriptCategory.CustomSql)
                return !_options.ApplyCustomSqlWithHashTracking;
            return false;
        }

        private SqlServerMigrationRunner CreateRunner()
        {
            var options = new MigrationRunnerOptions
            {
                HistorySchema = _options.HistorySchema,
                EnsureHistoryTables = true,
                AutoAddMissingColumnsFromTableScripts = false,
                DestructiveOptions = _options.AllowDestructiveChanges ? DestructiveOperationOptions.Allow() : null
            };
            options.Schemas.Add(_options.HistorySchema);
            options.Schemas.Add(_options.DefaultSchema);
            return new SqlServerMigrationRunner(_options.ConnectionString, options);
        }

        private async Task<IDictionary<string, DatabaseTableDefinition>> LoadDatabaseSchemaAsync(CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, DatabaseTableDefinition>(StringComparer.OrdinalIgnoreCase);
            using (var connection = SqlServerConnectionFactory.Create(_options.ConnectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                await LoadTablesAndColumnsAsync(connection, result, cancellationToken).ConfigureAwait(false);
                await LoadIndexesAsync(connection, result, cancellationToken).ConfigureAwait(false);
                await LoadConstraintsAsync(connection, result, cancellationToken).ConfigureAwait(false);
            }

            return result;
        }

        private async Task LoadTablesAndColumnsAsync(SqlConnection connection, IDictionary<string, DatabaseTableDefinition> result, CancellationToken cancellationToken)
        {
            using (var command = new SqlCommand(Dialect.BuildReadColumnsPlan().CommandText, connection))
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
            using (var command = new SqlCommand(Dialect.BuildReadIndexesPlan().CommandText, connection))
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
                                index = new DatabaseIndexDefinition { Name = indexName, IsUnique = reader.GetBoolean(3) };
                                semanticIndexes[key] = index;
                                table.SemanticIndexes.Add(index);
                            }
                            index.Columns.Add(reader.GetString(4));
                        }
                    }
                }
            }
        }

        private async Task LoadConstraintsAsync(SqlConnection connection, IDictionary<string, DatabaseTableDefinition> result, CancellationToken cancellationToken)
        {
            using (var command = new SqlCommand(Dialect.BuildReadConstraintsPlan().CommandText, connection))
            {
                command.Parameters.AddWithValue("@Schema", _options.DefaultSchema);
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        if (result.TryGetValue(ModelSyncPlanBuilder.Key(reader.GetString(0), reader.GetString(1)), out var table))
                        {
                            table.UniqueConstraints.Add(reader.GetString(2));
                            table.UniqueConstraints.Add($"UQ_{reader.GetString(1)}_{reader.GetString(3)}");
                        }
                    }
                }
            }

            using (var command = new SqlCommand(Dialect.BuildReadForeignKeysPlan().CommandText, connection))
            {
                command.Parameters.AddWithValue("@Schema", _options.DefaultSchema);
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    var semanticForeignKeys = new Dictionary<string, DatabaseForeignKeyDefinition>(StringComparer.OrdinalIgnoreCase);
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        if (result.TryGetValue(ModelSyncPlanBuilder.Key(reader.GetString(0), reader.GetString(1)), out var table))
                        {
                            var fkName = reader.GetString(2);
                            table.ForeignKeys.Add(fkName);
                            table.ForeignKeys.Add($"FK_{reader.GetString(1)}_{reader.GetString(3)}_{reader.GetString(5)}");
                            var key = ModelSyncPlanBuilder.Key(reader.GetString(1), fkName);
                            if (!semanticForeignKeys.TryGetValue(key, out var foreignKey))
                            {
                                foreignKey = new DatabaseForeignKeyDefinition
                                {
                                    Name = fkName,
                                    ReferencedSchema = reader.GetString(4),
                                    ReferencedTable = reader.GetString(5)
                                };
                                semanticForeignKeys[key] = foreignKey;
                                table.SemanticForeignKeys.Add(foreignKey);
                            }
                            foreignKey.LocalColumns.Add(reader.GetString(3));
                            foreignKey.ReferencedColumns.Add(reader.GetString(6));
                        }
                    }
                }
            }
        }

        private static bool IsAutoIncrement(DbColumnPrimaryKeyAttribute attribute)
        {
            var property = attribute.GetType().GetProperty("IsAutoIncrement");
            return property != null && property.PropertyType == typeof(bool) && (bool)property.GetValue(attribute, null);
        }

        private async Task ExecuteSqlAsync(string sql, CancellationToken cancellationToken)
        {
            using (var connection = SqlServerConnectionFactory.Create(_options.ConnectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = new SqlCommand(sql, connection))
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private static string Qualify(string schema, string table)
            => Dialect.Qualify(schema, table);

        private static string Quote(string identifier)
            => Dialect.Quote(identifier);

        private static void ValidateIdentifier(string identifier, string parameterName)
            => SqlIdentifierValidator.Validate(identifier, parameterName);

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
