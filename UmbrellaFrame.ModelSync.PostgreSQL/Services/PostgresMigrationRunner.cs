using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.SqlGeneration;
using UmbrellaFrame.ModelSync.Core.Services;

namespace UmbrellaFrame.ModelSync.PostgreSQL
{
    /// <summary>Runs ordered PostgreSQL migration scripts with history tracking.</summary>
    public sealed class PostgresMigrationRunner : SqlMigrationRunnerBase
    {
        private static readonly Regex SafeIdentifierPattern = new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
        private static readonly ModelSyncSqlDialect Dialect = new ModelSyncSqlDialect(PostgresProviderDescriptor.Create());
        private readonly string _connectionString;

        public PostgresMigrationRunner(string connectionString, MigrationRunnerOptions options = null, ILogger<PostgresMigrationRunner> logger = null)
            : base(ConfigureDefaults(options), logger ?? NullLogger<PostgresMigrationRunner>.Instance, new ProviderNativeMigrationLockStrategy(Dialect))
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));
            _connectionString = connectionString;
        }

        protected override Task<System.Data.Common.DbConnection?> CreateLockConnectionAsync(CancellationToken cancellationToken)
            => Task.FromResult<System.Data.Common.DbConnection?>(PostgresConnectionFactory.Create(_connectionString));

        protected override async Task ResetDatabaseAsync(CancellationToken cancellationToken)
        {
            var builder = new NpgsqlConnectionStringBuilder(_connectionString);
            var database = builder.Database;
            ValidateIdentifier(database, nameof(database));
            ValidateResetDatabaseName(database);
            builder.Database = "postgres";
            using (var connection = PostgresConnectionFactory.Create(builder.ConnectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var terminate = new NpgsqlCommand("SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @db AND pid <> pg_backend_pid();", connection))
                {
                    terminate.Parameters.AddWithValue("@db", database);
                    await terminate.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
                using (var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{EscapeIdentifier(database)}\";", connection))
                    await drop.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                using (var create = new NpgsqlCommand($"CREATE DATABASE \"{EscapeIdentifier(database)}\";", connection))
                    await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        protected override async Task EnsureSchemasAsync(IEnumerable<string> schemas, CancellationToken cancellationToken)
        {
            using (var connection = PostgresConnectionFactory.Create(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                foreach (var schema in schemas.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    ValidateIdentifier(schema, nameof(schema));
                    var plan = Dialect.BuildEnsureSchemaPlan(schema);
                    using (var command = new NpgsqlCommand(plan.CommandText, connection))
                        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        protected override async Task EnsureHistoryTablesAsync(CancellationToken cancellationToken)
        {
            await ExecuteSqlAsync(Dialect.BuildEnsureHistoryInfrastructurePlan(HistorySchema()).CommandText, cancellationToken).ConfigureAwait(false);
            var upgrade = Dialect.BuildEnsureHistoryHashColumnsPlan(HistorySchema()).CommandText;
            if (!string.IsNullOrWhiteSpace(upgrade))
                await ExecuteSqlAsync(upgrade, cancellationToken).ConfigureAwait(false);
        }

        protected override async Task<IDictionary<string, string>> ReadHistoryAsync(CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var connection = PostgresConnectionFactory.Create(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                foreach (MigrationScriptCategory category in Enum.GetValues(typeof(MigrationScriptCategory)))
                {
                    var plan = Dialect.BuildReadHistoryPlan(HistorySchema(), category);
                    try
                    {
                        using (var command = new NpgsqlCommand(plan.CommandText, connection))
                        using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                                result[CreateHistoryKey(category, reader.GetString(0))] = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        }
                    }
                    catch (PostgresException ex) when (ex.SqlState == "42P01" || ex.SqlState == "3F000")
                    {
                    }
                    catch (PostgresException ex) when (ex.SqlState == "42703")
                    {
                        await ReadLegacyHistoryAsync(connection, category, result, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            return result;
        }

        protected override async Task ExecuteSqlAsync(string sql, CancellationToken cancellationToken)
        {
            using (var connection = PostgresConnectionFactory.Create(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = new NpgsqlCommand(sql, connection))
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        protected override async Task RecordHistoryAsync(MigrationScriptDefinition definition, string hash, CancellationToken cancellationToken)
        {
            var plan = Dialect.BuildRecordHistoryPlan(HistorySchema(), definition.Category, definition.Id, definition.Name, hash);
            using (var connection = PostgresConnectionFactory.Create(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = new NpgsqlCommand(plan.CommandText, connection))
                {
                    AddParameters(command, plan);
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        protected override async Task<IReadOnlyList<string>> BuildMissingColumnScriptsAsync(MigrationScriptDefinition definition, CancellationToken cancellationToken)
        {
            var columns = TableScriptColumnParser.Parse(definition.Sql, "public");
            var result = new List<string>();
            using (var connection = PostgresConnectionFactory.Create(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                foreach (var column in columns)
                {
                    ValidateIdentifier(column.Schema, nameof(column.Schema));
                    ValidateIdentifier(column.Table, nameof(column.Table));
                    ValidateIdentifier(column.Column, nameof(column.Column));
                    var plan = Dialect.BuildParsedColumnExistsPlan(column);
                    using (var command = new NpgsqlCommand(plan.CommandText, connection))
                    {
                        AddParameters(command, plan);
                        var exists = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) > 0;
                        if (!exists)
                            result.Add(Dialect.BuildAddParsedColumnSql(column));
                    }
                }
            }
            return result;
        }

        protected override bool IsMissingInfrastructureException(Exception exception)
        {
            var postgres = exception as PostgresException;
            return postgres != null && (postgres.SqlState == "42P01" || postgres.SqlState == "3F000");
        }

        private async Task ReadLegacyHistoryAsync(NpgsqlConnection connection, MigrationScriptCategory category, IDictionary<string, string> result, CancellationToken cancellationToken)
        {
            var plan = Dialect.BuildReadLegacyHistoryPlan(HistorySchema(), category);
            using (var command = new NpgsqlCommand(plan.CommandText, connection))
            using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    result[CreateHistoryKey(category, reader.GetString(0))] = string.Empty;
            }
        }

        private static MigrationRunnerOptions ConfigureDefaults(MigrationRunnerOptions options)
        {
            var configured = options ?? MigrationRunnerOptions.Default();
            if (string.IsNullOrWhiteSpace(configured.HistorySchema))
                configured.HistorySchema = "sec";
            ValidateIdentifier(configured.HistorySchema, nameof(configured.HistorySchema));
            if (configured.Schemas.Count == 0)
            {
                foreach (var schema in new[] { "app", "ref", "sec", "auth", "log", "crm", "exp", "veh", "fin" })
                    configured.Schemas.Add(schema);
            }
            if (!configured.Schemas.Contains(configured.HistorySchema, StringComparer.OrdinalIgnoreCase))
                configured.Schemas.Add(configured.HistorySchema);
            return configured;
        }

        private string HistorySchema()
        {
            var schema = string.IsNullOrWhiteSpace(Options.HistorySchema) ? "sec" : Options.HistorySchema;
            ValidateIdentifier(schema, nameof(Options.HistorySchema));
            return schema;
        }

        private static void ValidateIdentifier(string identifier, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(identifier) || !SafeIdentifierPattern.IsMatch(identifier))
                throw new ArgumentException($"Invalid SQL identifier '{identifier}'.", parameterName);
        }

        private static string EscapeIdentifier(string identifier)
            => identifier.Replace("\"", "\"\"");

        private static void AddParameters(NpgsqlCommand command, ModelSyncSqlCommand plan)
        {
            foreach (var parameter in plan.Parameters)
                command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }

        protected override void ValidateResetDatabaseName(string databaseName)
        {
            base.ValidateResetDatabaseName(databaseName);
            var blocked = new[] { "postgres", "template0", "template1" };
            if (blocked.Contains(databaseName, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException($"PostgreSQL system database '{databaseName}' cannot be reset.");

            var expected = Options.ResetOptions?.ExpectedDatabaseName;
            if (!string.IsNullOrWhiteSpace(expected) && !string.Equals(expected, databaseName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Expected database name does not match the PostgreSQL target database.");
        }
    }
}
