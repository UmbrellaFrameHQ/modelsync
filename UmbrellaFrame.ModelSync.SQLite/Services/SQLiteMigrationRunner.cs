using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.Services;

namespace UmbrellaFrame.ModelSync.SQLite
{
    /// <summary>Runs ordered SQLite migration scripts with history tracking. Stored procedures are not supported by SQLite.</summary>
    public sealed class SQLiteMigrationRunner : SqlMigrationRunnerBase
    {
        private static readonly Regex SafeIdentifierPattern = new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
        private readonly string _connectionString;

        public SQLiteMigrationRunner(string connectionString, MigrationRunnerOptions options = null, ILogger<SQLiteMigrationRunner> logger = null)
            : base(options, logger ?? NullLogger<SQLiteMigrationRunner>.Instance)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));
            _connectionString = connectionString;
        }

        protected override Task ResetDatabaseAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException("SQLite database reset is not supported by SQLiteMigrationRunner. Delete the database file explicitly if you need a reset.");

        protected override Task EnsureSchemasAsync(IEnumerable<string> schemas, CancellationToken cancellationToken)
            => Task.CompletedTask;

        protected override async Task EnsureHistoryTablesAsync(CancellationToken cancellationToken)
        {
            const string sql = @"
CREATE TABLE IF NOT EXISTS SchemaMigration_Tables(Id TEXT NOT NULL PRIMARY KEY, Name TEXT NOT NULL, SqlHash TEXT NULL, AppliedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, UpdateAt TEXT NULL);
CREATE TABLE IF NOT EXISTS SchemaMigration_StoredProcedures(Id TEXT NOT NULL PRIMARY KEY, Name TEXT NOT NULL, SqlHash TEXT NULL, AppliedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, UpdateAt TEXT NULL);
CREATE TABLE IF NOT EXISTS SchemaMigration_Triggers(Id TEXT NOT NULL PRIMARY KEY, Name TEXT NOT NULL, SqlHash TEXT NULL, AppliedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, UpdateAt TEXT NULL);
CREATE TABLE IF NOT EXISTS SchemaMigration_Seeds(Id TEXT NOT NULL PRIMARY KEY, Name TEXT NOT NULL, SqlHash TEXT NULL, AppliedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, UpdateAt TEXT NULL);";
            await ExecuteSqlAsync(sql, cancellationToken).ConfigureAwait(false);
        }

        protected override async Task<IDictionary<string, string>> ReadHistoryAsync(CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                foreach (MigrationScriptCategory category in Enum.GetValues(typeof(MigrationScriptCategory)))
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = $"SELECT Id, SqlHash FROM {HistoryTable(category)};";
                        using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                                result[CreateHistoryKey(category, reader.GetString(0))] = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        }
                    }
                }
            }
            return result;
        }

        protected override async Task ExecuteSqlAsync(string sql, CancellationToken cancellationToken)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        protected override async Task RecordHistoryAsync(MigrationScriptDefinition definition, string hash, CancellationToken cancellationToken)
        {
            var sql = $@"
INSERT INTO {HistoryTable(definition.Category)}(Id, Name, SqlHash)
VALUES (@Id, @Name, @SqlHash)
ON CONFLICT(Id) DO UPDATE SET Name = excluded.Name, SqlHash = excluded.SqlHash, UpdateAt = CURRENT_TIMESTAMP;";
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    command.Parameters.AddWithValue("@Id", definition.Id);
                    command.Parameters.AddWithValue("@Name", definition.Name);
                    command.Parameters.AddWithValue("@SqlHash", hash);
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        protected override async Task ApplyPlanAsync(MigrationSyncPlan plan, CancellationToken cancellationToken)
        {
            if (plan.Definition.Category == MigrationScriptCategory.StoredProcedures)
                throw new NotSupportedException("SQLite does not support stored procedures.");
            await base.ApplyPlanAsync(plan, cancellationToken).ConfigureAwait(false);
        }

        protected override async Task<IReadOnlyList<string>> BuildMissingColumnScriptsAsync(MigrationScriptDefinition definition, CancellationToken cancellationToken)
        {
            var columns = TableScriptColumnParser.Parse(definition.Sql, "main");
            var result = new List<string>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                foreach (var column in columns)
                {
                    ValidateIdentifier(column.Table, nameof(column.Table));
                    ValidateIdentifier(column.Column, nameof(column.Column));
                    var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = $"PRAGMA table_info(\"{EscapeIdentifier(column.Table)}\");";
                        using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                                existing.Add(reader.GetString(1));
                        }
                    }

                    if (!existing.Contains(column.Column))
                        result.Add($"ALTER TABLE \"{EscapeIdentifier(column.Table)}\" ADD COLUMN \"{EscapeIdentifier(column.Column)}\" {column.Definition};");
                }
            }

            return result;
        }

        private static string HistoryTable(MigrationScriptCategory category)
        {
            switch (category)
            {
                case MigrationScriptCategory.StoredProcedures: return "SchemaMigration_StoredProcedures";
                case MigrationScriptCategory.Triggers: return "SchemaMigration_Triggers";
                case MigrationScriptCategory.Seeds: return "SchemaMigration_Seeds";
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
