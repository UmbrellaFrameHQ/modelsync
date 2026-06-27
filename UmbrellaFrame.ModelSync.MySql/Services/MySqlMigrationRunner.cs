using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.Services;

namespace UmbrellaFrame.ModelSync.MySql
{
    /// <summary>Runs ordered MySQL/MariaDB migration scripts with history tracking.</summary>
    public sealed class MySqlMigrationRunner : SqlMigrationRunnerBase
    {
        private static readonly Regex SafeIdentifierPattern = new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
        private readonly string _connectionString;

        public MySqlMigrationRunner(string connectionString, MigrationRunnerOptions options = null, ILogger<MySqlMigrationRunner> logger = null)
            : base(options, logger ?? NullLogger<MySqlMigrationRunner>.Instance)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));
            _connectionString = connectionString;
        }

        protected override async Task ResetDatabaseAsync(CancellationToken cancellationToken)
        {
            var builder = new MySqlConnectionStringBuilder(_connectionString);
            var database = builder.Database;
            ValidateIdentifier(database, nameof(database));
            builder.Database = string.Empty;

            using (var connection = new MySqlConnection(builder.ConnectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var drop = new MySqlCommand($"DROP DATABASE IF EXISTS `{EscapeIdentifier(database)}`;", connection))
                    await drop.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                using (var create = new MySqlCommand($"CREATE DATABASE `{EscapeIdentifier(database)}`;", connection))
                    await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        protected override Task EnsureSchemasAsync(IEnumerable<string> schemas, CancellationToken cancellationToken)
            => Task.CompletedTask;

        protected override async Task EnsureHistoryTablesAsync(CancellationToken cancellationToken)
        {
            const string sql = @"
CREATE TABLE IF NOT EXISTS `SchemaMigration_Tables`(
    `Id` VARCHAR(128) NOT NULL PRIMARY KEY,
    `Name` VARCHAR(256) NOT NULL,
    `SqlHash` VARCHAR(128) NULL,
    `AppliedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdateAt` DATETIME NULL
);
CREATE TABLE IF NOT EXISTS `SchemaMigration_StoredProcedures`(
    `Id` VARCHAR(128) NOT NULL PRIMARY KEY,
    `Name` VARCHAR(256) NOT NULL,
    `SqlHash` VARCHAR(128) NULL,
    `AppliedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdateAt` DATETIME NULL
);
CREATE TABLE IF NOT EXISTS `SchemaMigration_Triggers`(
    `Id` VARCHAR(128) NOT NULL PRIMARY KEY,
    `Name` VARCHAR(256) NOT NULL,
    `SqlHash` VARCHAR(128) NULL,
    `AppliedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdateAt` DATETIME NULL
);
CREATE TABLE IF NOT EXISTS `SchemaMigration_Seeds`(
    `Id` VARCHAR(128) NOT NULL PRIMARY KEY,
    `Name` VARCHAR(256) NOT NULL,
    `SqlHash` VARCHAR(128) NULL,
    `AppliedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdateAt` DATETIME NULL
);
CREATE TABLE IF NOT EXISTS `SchemaMigration_CustomSql`(
    `Id` VARCHAR(128) NOT NULL PRIMARY KEY,
    `Name` VARCHAR(256) NOT NULL,
    `SqlHash` VARCHAR(128) NULL,
    `AppliedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdateAt` DATETIME NULL
);";
            foreach (var statement in sql.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                await ExecuteSqlAsync(statement, cancellationToken).ConfigureAwait(false);
        }

        protected override async Task<IDictionary<string, string>> ReadHistoryAsync(CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                foreach (MigrationScriptCategory category in Enum.GetValues(typeof(MigrationScriptCategory)))
                {
                    using (var command = new MySqlCommand($"SELECT `Id`, `SqlHash` FROM `{HistoryTable(category)}`;", connection))
                    using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            result[CreateHistoryKey(category, reader.GetString(0))] = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        }
                    }
                }
            }
            return result;
        }

        protected override async Task ExecuteSqlAsync(string sql, CancellationToken cancellationToken)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = new MySqlCommand(sql, connection))
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        protected override async Task RecordHistoryAsync(MigrationScriptDefinition definition, string hash, CancellationToken cancellationToken)
        {
            var sql = $@"
INSERT INTO `{HistoryTable(definition.Category)}`(`Id`, `Name`, `SqlHash`)
VALUES (@Id, @Name, @SqlHash)
ON DUPLICATE KEY UPDATE `Name` = VALUES(`Name`), `SqlHash` = VALUES(`SqlHash`), `UpdateAt` = CURRENT_TIMESTAMP;";
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = new MySqlCommand(sql, connection))
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
            var database = new MySqlConnectionStringBuilder(_connectionString).Database;
            var columns = TableScriptColumnParser.Parse(definition.Sql, database);
            var result = new List<string>();
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                foreach (var column in columns)
                {
                    ValidateIdentifier(column.Schema, nameof(column.Schema));
                    ValidateIdentifier(column.Table, nameof(column.Table));
                    ValidateIdentifier(column.Column, nameof(column.Column));
                    const string existsSql = @"SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = @Schema AND table_name = @Table AND column_name = @Column;";
                    using (var command = new MySqlCommand(existsSql, connection))
                    {
                        command.Parameters.AddWithValue("@Schema", column.Schema);
                        command.Parameters.AddWithValue("@Table", column.Table);
                        command.Parameters.AddWithValue("@Column", column.Column);
                        var exists = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) > 0;
                        if (!exists)
                            result.Add($"ALTER TABLE `{EscapeIdentifier(column.Schema)}`.`{EscapeIdentifier(column.Table)}` ADD COLUMN `{EscapeIdentifier(column.Column)}` {column.Definition};");
                    }
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
            => identifier.Replace("`", "``");
    }
}
