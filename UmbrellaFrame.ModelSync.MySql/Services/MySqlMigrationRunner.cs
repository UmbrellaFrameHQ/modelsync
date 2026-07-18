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
using UmbrellaFrame.ModelSync.Core.SqlGeneration;
using UmbrellaFrame.ModelSync.Core.Services;

namespace UmbrellaFrame.ModelSync.MySql
{
    /// <summary>Runs ordered MySQL/MariaDB migration scripts with history tracking.</summary>
    public sealed class MySqlMigrationRunner : SqlMigrationRunnerBase
    {
        private static readonly Regex SafeIdentifierPattern = new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
        private static readonly ModelSyncSqlDialect Dialect = new ModelSyncSqlDialect(MySqlProviderDescriptor.Create());
        private readonly string _connectionString;

        public MySqlMigrationRunner(string connectionString, MigrationRunnerOptions? options = null, ILogger<MySqlMigrationRunner>? logger = null)
            : base(options, logger ?? NullLogger<MySqlMigrationRunner>.Instance, new ProviderNativeMigrationLockStrategy(Dialect))
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));
            _connectionString = connectionString;
        }

        protected override Task<System.Data.Common.DbConnection?> CreateLockConnectionAsync(CancellationToken cancellationToken)
            => Task.FromResult<System.Data.Common.DbConnection?>(MySqlConnectionFactory.Create(_connectionString));

        protected override Task<System.Data.Common.DbConnection?> CreateReadinessConnectionAsync(CancellationToken cancellationToken)
            => Task.FromResult<System.Data.Common.DbConnection?>(MySqlConnectionFactory.Create(_connectionString));

        protected override async Task ResetDatabaseAsync(CancellationToken cancellationToken)
        {
            var builder = new MySqlConnectionStringBuilder(_connectionString);
            var database = builder.Database;
            ValidateIdentifier(database, nameof(database));
            ValidateResetDatabaseName(database);
            builder.Database = string.Empty;

            using (var connection = MySqlConnectionFactory.Create(builder.ConnectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var drop = new MySqlCommand($"DROP DATABASE IF EXISTS `{EscapeIdentifier(database)}`;", connection))
                {
                    drop.CommandTimeout = ResetCommandTimeoutSeconds;
                    await drop.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
                using (var create = new MySqlCommand($"CREATE DATABASE `{EscapeIdentifier(database)}`;", connection))
                {
                    create.CommandTimeout = ResetCommandTimeoutSeconds;
                    await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        protected override Task EnsureSchemasAsync(IEnumerable<string> schemas, CancellationToken cancellationToken)
            => Task.CompletedTask;

        protected override async Task EnsureHistoryTablesAsync(CancellationToken cancellationToken)
        {
            var plan = Dialect.BuildEnsureHistoryInfrastructurePlan(string.Empty);
            foreach (var statement in plan.CommandText.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                await ExecuteSqlAsync(statement, cancellationToken).ConfigureAwait(false);
            var upgrade = Dialect.BuildEnsureHistoryHashColumnsPlan(string.Empty);
            if (!string.IsNullOrWhiteSpace(upgrade.CommandText))
                await ExecuteSqlAsync(upgrade.CommandText, cancellationToken).ConfigureAwait(false);
        }

        protected override async Task<IDictionary<string, string>> ReadHistoryAsync(CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var connection = MySqlConnectionFactory.Create(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                foreach (MigrationScriptCategory category in Enum.GetValues(typeof(MigrationScriptCategory)))
                {
                    var plan = Dialect.BuildReadHistoryPlan(string.Empty, category);
                    try
                    {
                        using (var command = new MySqlCommand(plan.CommandText, connection))
                        using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                                result[CreateHistoryKey(category, reader.GetString(0))] = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        }
                    }
                    catch (MySqlException ex) when (ex.Number == 1146)
                    {
                    }
                    catch (MySqlException ex) when (ex.Number == 1054)
                    {
                        await ReadLegacyHistoryAsync(connection, category, result, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            return result;
        }

        protected override async Task ExecuteSqlAsync(string sql, CancellationToken cancellationToken)
        {
            using (var connection = MySqlConnectionFactory.Create(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = new MySqlCommand(sql, connection))
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        protected override async Task RecordHistoryAsync(MigrationScriptDefinition definition, string hash, CancellationToken cancellationToken)
        {
            var plan = Dialect.BuildRecordHistoryPlan(string.Empty, definition.Category, definition.Id, definition.Name, hash);
            using (var connection = MySqlConnectionFactory.Create(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = new MySqlCommand(plan.CommandText, connection))
                {
                    AddParameters(command, plan);
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        protected override async Task<IReadOnlyList<string>> BuildMissingColumnScriptsAsync(MigrationScriptDefinition definition, CancellationToken cancellationToken)
        {
            var database = new MySqlConnectionStringBuilder(_connectionString).Database;
            var columns = TableScriptColumnParser.Parse(definition.Sql, database);
            var result = new List<string>();
            using (var connection = MySqlConnectionFactory.Create(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                foreach (var column in columns)
                {
                    ValidateIdentifier(column.Schema, nameof(column.Schema));
                    ValidateIdentifier(column.Table, nameof(column.Table));
                    ValidateIdentifier(column.Column, nameof(column.Column));
                    var plan = Dialect.BuildParsedColumnExistsPlan(column);
                    using (var command = new MySqlCommand(plan.CommandText, connection))
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
            var mysql = exception as MySqlException;
            return mysql != null && (mysql.Number == 1146 || mysql.Number == 1049);
        }

        private static async Task ReadLegacyHistoryAsync(MySqlConnection connection, MigrationScriptCategory category, IDictionary<string, string> result, CancellationToken cancellationToken)
        {
            var plan = Dialect.BuildReadLegacyHistoryPlan(string.Empty, category);
            using (var command = new MySqlCommand(plan.CommandText, connection))
            using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    result[CreateHistoryKey(category, reader.GetString(0))] = string.Empty;
            }
        }

        protected override async Task<IMigrationExecutionScope> OpenExecutionScopeAsync(MigrationTransactionPolicy transactionPolicy, CancellationToken cancellationToken)
        {
            _ = TransactionPolicyStartsTransaction(transactionPolicy);
            var connection = MySqlConnectionFactory.Create(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return new MySqlExecutionScope(connection);
        }

        protected override async Task RecordHistoryAsync(IMigrationExecutionScope scope, MigrationScriptDefinition definition, string hash, CancellationToken cancellationToken)
        {
            var mysqlScope = (MySqlExecutionScope)scope;
            var plan = Dialect.BuildRecordHistoryPlan(string.Empty, definition.Category, definition.Id, definition.Name, hash);
            using (var command = new MySqlCommand(plan.CommandText, mysqlScope.Connection))
            {
                AddParameters(command, plan);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        protected override void PopulateProviderError(MigrationExecutionItemResult result, Exception exception)
        {
            base.PopulateProviderError(result, exception);
            var mysql = exception as MySqlException;
            if (mysql == null)
                return;

            result.ProviderErrorCode = mysql.SqlState ?? string.Empty;
            result.ProviderErrorNumber = mysql.Number;
            result.ProviderErrorState = mysql.SqlState ?? string.Empty;
            result.ErrorMessage = Redact(mysql.Message);
        }

        protected override string ProviderName => "MySQL/MariaDB";

        private static void ValidateIdentifier(string identifier, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(identifier) || !SafeIdentifierPattern.IsMatch(identifier))
                throw new ArgumentException($"Invalid SQL identifier '{identifier}'.", parameterName);
        }

        private static string EscapeIdentifier(string identifier)
            => identifier.Replace("`", "``");

        private static void AddParameters(MySqlCommand command, ModelSyncSqlCommand plan)
        {
            foreach (var parameter in plan.Parameters)
                command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }

        protected override void ValidateResetDatabaseName(string databaseName)
        {
            base.ValidateResetDatabaseName(databaseName);
            if (Dialect.SystemDatabaseNames.Contains(databaseName, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException($"MySQL/MariaDB system database '{databaseName}' cannot be reset.");

            var expected = Options.ResetOptions?.ExpectedDatabaseName;
            if (!string.IsNullOrWhiteSpace(expected) && !string.Equals(expected, databaseName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Expected database name does not match the MySQL/MariaDB target database.");
        }

        private sealed class MySqlExecutionScope : IMigrationExecutionScope
        {
            public MySqlExecutionScope(MySqlConnection connection)
            {
                Connection = connection;
            }

            public MySqlConnection Connection { get; }
            public bool TransactionStarted => false;

            public async Task ExecuteSqlAsync(string sql, CancellationToken cancellationToken)
            {
                using (var command = new MySqlCommand(sql, Connection))
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            public Task CompleteAsync(CancellationToken cancellationToken)
                => Task.CompletedTask;

            public Task<bool> RollbackAsync(CancellationToken cancellationToken)
                => Task.FromResult(false);

            public void Dispose()
            {
                Connection.Dispose();
            }
        }
    }
}
