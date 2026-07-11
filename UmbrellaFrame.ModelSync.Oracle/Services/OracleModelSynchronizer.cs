using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Oracle.ManagedDataAccess.Client;

using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.Services;
using UmbrellaFrame.ModelSync.Core.SqlGeneration;

namespace UmbrellaFrame.ModelSync.Oracle
{
    public sealed class OracleModelSyncOptions : ModelSyncOptions
    {
        public OracleModelSyncOptions()
        {
            DefaultSchema = string.Empty;
            HistorySchema = string.Empty;
        }
    }

    public sealed class OracleModelSynchronizer
    {
        private static readonly Regex SafeIdentifierPattern = new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
        private static readonly ModelSyncSqlDialect Dialect = new ModelSyncSqlDialect(OracleProviderDescriptor.Create());
        private readonly OracleModelSyncOptions _options;
        private readonly List<Assembly> _modelAssemblies;
        private readonly List<Type> _modelTypes;

        private OracleModelSynchronizer(OracleModelSyncOptions options, IEnumerable<Assembly> modelAssemblies, IEnumerable<Type> modelTypes)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(_options.ConnectionString))
                throw new ArgumentException("ConnectionString is required.", nameof(options));

            if (string.IsNullOrWhiteSpace(_options.DefaultSchema))
                _options.DefaultSchema = ResolveUserName(_options.ConnectionString);

            _modelAssemblies = modelAssemblies?.Where(a => a != null).Distinct().ToList() ?? new List<Assembly>();
            _modelTypes = modelTypes?.Where(t => t != null).Distinct().ToList() ?? new List<Type>();
            if (_modelAssemblies.Count == 0 && _modelTypes.Count == 0)
                throw new ArgumentException("At least one model assembly or model type is required.");
        }

        public static OracleModelSynchronizer FromAssemblies(OracleModelSyncOptions options, params Assembly[] assemblies)
            => new OracleModelSynchronizer(options, assemblies, Array.Empty<Type>());

        public static OracleModelSynchronizer FromTypes(OracleModelSyncOptions options, params Type[] modelTypes)
            => new OracleModelSynchronizer(options, Array.Empty<Assembly>(), modelTypes);

        public OracleModelSynchronizer AddSqlScriptsFromEmbeddedResources(Assembly assembly, string rootNamespace)
            => throw new NotSupportedException("Oracle model synchronizer does not support migration script execution yet.");

        public OracleModelSynchronizer AddSqlScript(MigrationScriptDefinition definition)
            => throw new NotSupportedException("Oracle model synchronizer does not support migration script execution yet.");

        public async Task<ModelSyncResult> CompareAsync(CancellationToken cancellationToken = default)
        {
            ValidateIdentifier(_options.DefaultSchema, nameof(_options.DefaultSchema));
            var attributes = new ProviderAttributeSet(
                typeof(OracleTableNameAttribute),
                typeof(OracleColumnTypeAttribute),
                typeof(OracleColumnPrimaryKeyAttribute),
                typeof(OracleColumnNotNullAttribute),
                typeof(OracleColumnUniqueAttribute),
                typeof(OracleForeignKeyAttribute),
                (pk, column) => IsAutoIncrement(pk) ? DbValueGenerationKind.Identity : DbValueGenerationKind.None);

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

            return new ModelSyncResult(builder.Build(modelTables, databaseTables, _options), ExecuteSqlAsync);
        }

        private async Task<IDictionary<string, DatabaseTableDefinition>> LoadDatabaseSchemaAsync(CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, DatabaseTableDefinition>(StringComparer.OrdinalIgnoreCase);
            using (var connection = OracleConnectionFactory.Create(_options.ConnectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = new OracleCommand(Dialect.BuildReadColumnsPlan().CommandText, connection))
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
                            StoreType = BuildOracleStoreType(reader.GetString(3), reader.IsDBNull(4) ? 0 : Convert.ToInt64(reader.GetValue(4)), reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetValue(5)), reader.IsDBNull(6) ? 0 : Convert.ToInt32(reader.GetValue(6))),
                            IsNullable = string.Equals(reader.GetString(7), "Y", StringComparison.OrdinalIgnoreCase),
                            HasDefault = Convert.ToInt32(reader.GetValue(8)) == 1
                        };
                    }
                }

                await LoadIndexesAsync(connection, result, cancellationToken).ConfigureAwait(false);
                await LoadConstraintsAsync(connection, result, cancellationToken).ConfigureAwait(false);
            }

            return result;
        }

        private static async Task LoadIndexesAsync(OracleConnection connection, IDictionary<string, DatabaseTableDefinition> result, CancellationToken cancellationToken)
        {
            using (var command = new OracleCommand(Dialect.BuildReadIndexesPlan().CommandText, connection))
            using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                var semanticIndexes = new Dictionary<string, DatabaseIndexDefinition>(StringComparer.OrdinalIgnoreCase);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (!result.TryGetValue(ModelSyncPlanBuilder.Key(reader.GetString(0), reader.GetString(1)), out var table))
                        continue;

                    var indexName = reader.GetString(2);
                    table.Indexes.Add(indexName);
                    var key = ModelSyncPlanBuilder.Key(reader.GetString(1), indexName);
                    if (!semanticIndexes.TryGetValue(key, out var index))
                    {
                        index = new DatabaseIndexDefinition { Name = indexName, IsUnique = Convert.ToInt32(reader.GetValue(3)) == 1 };
                        semanticIndexes[key] = index;
                        table.SemanticIndexes.Add(index);
                    }
                    index.Columns.Add(reader.GetString(4));
                }
            }
        }

        private static async Task LoadConstraintsAsync(OracleConnection connection, IDictionary<string, DatabaseTableDefinition> result, CancellationToken cancellationToken)
        {
            using (var command = new OracleCommand(Dialect.BuildReadConstraintsPlan().CommandText, connection))
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
                    if (type == "CHECK")
                        foreach (var column in table.Columns.Values)
                            column.HasCheck = true;
                }
            }

            using (var command = new OracleCommand(Dialect.BuildReadConstraintColumnsPlan().CommandText, connection))
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

        private async Task ExecuteSqlAsync(string sql, CancellationToken cancellationToken)
        {
            using (var connection = OracleConnectionFactory.Create(_options.ConnectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = new OracleCommand(NormalizeCommandText(sql), connection))
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private static string NormalizeCommandText(string sql)
            => (sql ?? string.Empty).Trim().TrimEnd(';');

        private static bool IsAutoIncrement(DbColumnPrimaryKeyAttribute attribute)
        {
            var property = attribute.GetType().GetProperty("IsAutoIncrement");
            return property != null && property.PropertyType == typeof(bool) && (bool)property.GetValue(attribute, null);
        }

        private static string ResolveUserName(string connectionString)
            => (new OracleConnectionStringBuilder(connectionString).UserID ?? string.Empty).ToUpperInvariant();

        private static void ValidateIdentifier(string identifier, string parameterName)
            => SqlIdentifierValidator.Validate(identifier, parameterName);

        private static string BuildOracleStoreType(string type, long length, int precision, int scale)
        {
            var upper = type.ToUpperInvariant();
            if ((upper == "VARCHAR2" || upper == "NVARCHAR2" || upper == "CHAR" || upper == "NCHAR" || upper == "RAW") && length > 0)
                return $"{upper}({length})";
            if (upper == "NUMBER" && precision > 0)
                return scale > 0 ? $"NUMBER({precision},{scale})" : $"NUMBER({precision})";
            return upper;
        }
    }
}
