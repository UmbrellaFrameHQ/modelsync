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
using UmbrellaFrame.ModelSync.Core.Services;

namespace UmbrellaFrame.ModelSync.PostgreSQL
{
    /// <summary>Runs ordered PostgreSQL migration scripts with history tracking.</summary>
    public sealed class PostgresMigrationRunner : SqlMigrationRunnerBase
    {
        private static readonly Regex SafeIdentifierPattern = new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
        private readonly string _connectionString;

        public PostgresMigrationRunner(string connectionString, MigrationRunnerOptions options = null, ILogger<PostgresMigrationRunner> logger = null)
            : base(ConfigureDefaults(options), logger ?? NullLogger<PostgresMigrationRunner>.Instance)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));
            _connectionString = connectionString;
        }

        protected override async Task ResetDatabaseAsync(CancellationToken cancellationToken)
        {
            var builder = new NpgsqlConnectionStringBuilder(_connectionString);
            var database = builder.Database;
            ValidateIdentifier(database, nameof(database));
            builder.Database = "postgres";
            using (var connection = new NpgsqlConnection(builder.ConnectionString))
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
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                foreach (var schema in schemas.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    ValidateIdentifier(schema, nameof(schema));
                    using (var command = new NpgsqlCommand($"CREATE SCHEMA IF NOT EXISTS \"{EscapeIdentifier(schema)}\";", connection))
                        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        protected override async Task EnsureHistoryTablesAsync(CancellationToken cancellationToken)
        {
            const string sql = @"
CREATE SCHEMA IF NOT EXISTS ""sec"";
CREATE TABLE IF NOT EXISTS ""sec"".""SchemaMigration_Tables""(""Id"" VARCHAR(128) PRIMARY KEY, ""Name"" VARCHAR(256) NOT NULL, ""SqlHash"" VARCHAR(128) NULL, ""AppliedAt"" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP, ""UpdateAt"" TIMESTAMP NULL);
CREATE TABLE IF NOT EXISTS ""sec"".""SchemaMigration_StoredProcedures""(""Id"" VARCHAR(128) PRIMARY KEY, ""Name"" VARCHAR(256) NOT NULL, ""SqlHash"" VARCHAR(128) NULL, ""AppliedAt"" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP, ""UpdateAt"" TIMESTAMP NULL);
CREATE TABLE IF NOT EXISTS ""sec"".""SchemaMigration_Triggers""(""Id"" VARCHAR(128) PRIMARY KEY, ""Name"" VARCHAR(256) NOT NULL, ""SqlHash"" VARCHAR(128) NULL, ""AppliedAt"" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP, ""UpdateAt"" TIMESTAMP NULL);
CREATE TABLE IF NOT EXISTS ""sec"".""SchemaMigration_Seeds""(""Id"" VARCHAR(128) PRIMARY KEY, ""Name"" VARCHAR(256) NOT NULL, ""SqlHash"" VARCHAR(128) NULL, ""AppliedAt"" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP, ""UpdateAt"" TIMESTAMP NULL);
CREATE TABLE IF NOT EXISTS ""sec"".""SchemaMigration_CustomSql""(""Id"" VARCHAR(128) PRIMARY KEY, ""Name"" VARCHAR(256) NOT NULL, ""SqlHash"" VARCHAR(128) NULL, ""AppliedAt"" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP, ""UpdateAt"" TIMESTAMP NULL);";
            await ExecuteSqlAsync(sql, cancellationToken).ConfigureAwait(false);
        }

        protected override async Task<IDictionary<string, string>> ReadHistoryAsync(CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                foreach (MigrationScriptCategory category in Enum.GetValues(typeof(MigrationScriptCategory)))
                {
                    using (var command = new NpgsqlCommand($"SELECT \"Id\", \"SqlHash\" FROM \"sec\".\"{HistoryTable(category)}\";", connection))
                    using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                            result[CreateHistoryKey(category, reader.GetString(0))] = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    }
                }
            }
            return result;
        }

        protected override async Task ExecuteSqlAsync(string sql, CancellationToken cancellationToken)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = new NpgsqlCommand(sql, connection))
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        protected override async Task RecordHistoryAsync(MigrationScriptDefinition definition, string hash, CancellationToken cancellationToken)
        {
            var sql = $@"
INSERT INTO ""sec"".""{HistoryTable(definition.Category)}""(""Id"", ""Name"", ""SqlHash"")
VALUES (@Id, @Name, @SqlHash)
ON CONFLICT (""Id"") DO UPDATE SET ""Name"" = EXCLUDED.""Name"", ""SqlHash"" = EXCLUDED.""SqlHash"", ""UpdateAt"" = CURRENT_TIMESTAMP;";
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@Id", definition.Id);
                    command.Parameters.AddWithValue("@Name", definition.Name);
                    command.Parameters.AddWithValue("@SqlHash", hash);
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        protected override async Task<IReadOnlyList<string>> BuildMissingColumnScriptsAsync(MigrationScriptDefinition definition, CancellationToken cancellationToken)
        {
            var columns = TableScriptColumnParser.Parse(definition.Sql, "public");
            var result = new List<string>();
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                foreach (var column in columns)
                {
                    ValidateIdentifier(column.Schema, nameof(column.Schema));
                    ValidateIdentifier(column.Table, nameof(column.Table));
                    ValidateIdentifier(column.Column, nameof(column.Column));
                    const string existsSql = @"SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = @Schema AND table_name = @Table AND column_name = @Column;";
                    using (var command = new NpgsqlCommand(existsSql, connection))
                    {
                        command.Parameters.AddWithValue("@Schema", column.Schema);
                        command.Parameters.AddWithValue("@Table", column.Table);
                        command.Parameters.AddWithValue("@Column", column.Column);
                        var exists = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) > 0;
                        if (!exists)
                            result.Add($"ALTER TABLE \"{EscapeIdentifier(column.Schema)}\".\"{EscapeIdentifier(column.Table)}\" ADD COLUMN \"{EscapeIdentifier(column.Column)}\" {column.Definition};");
                    }
                }
            }
            return result;
        }

        private static MigrationRunnerOptions ConfigureDefaults(MigrationRunnerOptions options)
        {
            var configured = options ?? MigrationRunnerOptions.Default();
            if (configured.Schemas.Count == 0)
            {
                foreach (var schema in new[] { "app", "ref", "sec", "auth", "log", "crm", "exp", "veh", "fin" })
                    configured.Schemas.Add(schema);
            }
            return configured;
        }

        private static string HistoryTable(MigrationScriptCategory category)
        {
            switch (category)
            {
                case MigrationScriptCategory.StoredProcedures: return "SchemaMigration_StoredProcedures";
                case MigrationScriptCategory.Triggers: return "SchemaMigration_Triggers";
                case MigrationScriptCategory.Seeds: return "SchemaMigration_Seeds";
                case MigrationScriptCategory.CustomSql: return "SchemaMigration_CustomSql";
                default: return "SchemaMigration_Tables";
            }
        }

        private static void ValidateIdentifier(string identifier, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(identifier) || !SafeIdentifierPattern.IsMatch(identifier))
                throw new ArgumentException($"Invalid SQL identifier '{identifier}'.", parameterName);
        }

        private static string EscapeIdentifier(string identifier)
            => identifier.Replace("\"", "\"\"");
    }
}
