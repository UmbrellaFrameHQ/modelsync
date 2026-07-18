using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.SqlGeneration;
using UmbrellaFrame.ModelSync.Core.Services;

namespace UmbrellaFrame.ModelSync.SqlServer
{
    /// <summary>Runs ordered SQL Server migration scripts with history tracking.</summary>
    public sealed class SqlServerMigrationRunner : SqlMigrationRunnerBase
    {
        private static readonly Regex SafeIdentifierPattern = new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
        private static readonly ModelSyncSqlDialect Dialect = new ModelSyncSqlDialect(SqlServerProviderDescriptor.Create());
        private readonly string _connectionString;

        public SqlServerMigrationRunner(
            string connectionString,
            MigrationRunnerOptions? options = null,
            ILogger<SqlServerMigrationRunner>? logger = null)
            : base(ConfigureDefaults(options), logger ?? NullLogger<SqlServerMigrationRunner>.Instance, new ProviderNativeMigrationLockStrategy(Dialect))
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));
            _connectionString = connectionString;
        }

        protected override IReadOnlyList<string> SplitBatches(string sql)
            => SqlBatchSplitter.SplitGoBatches(sql);

        protected override Task<System.Data.Common.DbConnection?> CreateLockConnectionAsync(CancellationToken cancellationToken)
            => Task.FromResult<System.Data.Common.DbConnection?>(SqlServerConnectionFactory.Create(_connectionString));

        protected override Task<System.Data.Common.DbConnection?> CreateReadinessConnectionAsync(CancellationToken cancellationToken)
            => Task.FromResult<System.Data.Common.DbConnection?>(SqlServerConnectionFactory.Create(_connectionString));

        protected override async Task ResetDatabaseAsync(CancellationToken cancellationToken)
        {
            var builder = new SqlConnectionStringBuilder(_connectionString);
            var targetDb = builder.InitialCatalog;
            if (string.IsNullOrWhiteSpace(targetDb))
                throw new InvalidOperationException("SQL Server reset requires Initial Catalog in the connection string.");

            ValidateIdentifier(targetDb, nameof(targetDb));
            ValidateResetDatabaseName(targetDb);
            builder.InitialCatalog = "master";

            using (var connection = SqlServerConnectionFactory.Create(builder.ConnectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                var backupSql = BuildBackupSql(targetDb);
                var sql = $@"
IF EXISTS (SELECT 1 FROM sys.databases WHERE name = N'{EscapeLiteral(targetDb)}')
BEGIN
{backupSql}
    ALTER DATABASE [{EscapeIdentifier(targetDb)}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{EscapeIdentifier(targetDb)}];
END
CREATE DATABASE [{EscapeIdentifier(targetDb)}];";
                using (var command = new SqlCommand(sql, connection))
                {
                    command.CommandTimeout = ResetCommandTimeoutSeconds;
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        protected override async Task EnsureSchemasAsync(IEnumerable<string> schemas, CancellationToken cancellationToken)
        {
            using (var connection = SqlServerConnectionFactory.Create(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                foreach (var schema in schemas.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    ValidateIdentifier(schema, nameof(schema));
                    var plan = Dialect.BuildEnsureSchemaPlan(schema);
                    using (var command = new SqlCommand(plan.CommandText, connection))
                    {
                        AddParameters(command, plan);
                        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        protected override async Task EnsureHistoryTablesAsync(CancellationToken cancellationToken)
        {
            await ExecuteCommandAsync(Dialect.BuildEnsureHistoryInfrastructurePlan(HistorySchema()), cancellationToken).ConfigureAwait(false);
            await ExecuteCommandAsync(Dialect.BuildEnsureHistoryHashColumnsPlan(HistorySchema()), cancellationToken).ConfigureAwait(false);
        }

        protected override async Task<IDictionary<string, string>> ReadHistoryAsync(CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var connection = SqlServerConnectionFactory.Create(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                foreach (MigrationScriptCategory category in Enum.GetValues(typeof(MigrationScriptCategory)))
                {
                    var plan = Dialect.BuildReadHistoryPlan(HistorySchema(), category);
                    try
                    {
                        using (var command = new SqlCommand(plan.CommandText, connection))
                        using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                            {
                                var id = reader.GetString(0);
                                var hash = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                                result[CreateHistoryKey(category, id)] = hash;
                            }
                        }
                    }
                    catch (SqlException ex) when (IsMissingHistoryTable(ex, category))
                    {
                    }
                    catch (SqlException ex) when (IsMissingHistoryHashColumn(ex))
                    {
                        await ReadLegacyHistoryAsync(connection, category, result, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            return result;
        }

        protected override async Task ExecuteSqlAsync(string sql, CancellationToken cancellationToken)
        {
            using (var connection = SqlServerConnectionFactory.Create(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = new SqlCommand(sql, connection))
                {
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        protected override async Task RecordHistoryAsync(MigrationScriptDefinition definition, string hash, CancellationToken cancellationToken)
        {
            var plan = Dialect.BuildRecordHistoryPlan(HistorySchema(), definition.Category, definition.Id, definition.Name, hash);
            using (var connection = SqlServerConnectionFactory.Create(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = new SqlCommand(plan.CommandText, connection))
                {
                    AddParameters(command, plan);
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        protected override async Task<IMigrationExecutionScope> OpenExecutionScopeAsync(MigrationTransactionPolicy transactionPolicy, CancellationToken cancellationToken)
        {
            var connection = SqlServerConnectionFactory.Create(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            var transaction = TransactionPolicyStartsTransaction(transactionPolicy)
                ? connection.BeginTransaction()
                : null;
            return new SqlServerExecutionScope(connection, transaction);
        }

        protected override async Task RecordHistoryAsync(IMigrationExecutionScope scope, MigrationScriptDefinition definition, string hash, CancellationToken cancellationToken)
        {
            var sqlScope = (SqlServerExecutionScope)scope;
            var plan = Dialect.BuildRecordHistoryPlan(HistorySchema(), definition.Category, definition.Id, definition.Name, hash);
            using (var command = new SqlCommand(plan.CommandText, sqlScope.Connection))
            {
                command.Transaction = sqlScope.Transaction;
                AddParameters(command, plan);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        protected override async Task<IReadOnlyList<string>> BuildMissingColumnScriptsAsync(MigrationScriptDefinition definition, CancellationToken cancellationToken)
        {
            var columns = TableScriptColumnParser.Parse(definition.Sql, "dbo");
            var result = new List<string>();
            using (var connection = SqlServerConnectionFactory.Create(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                foreach (var column in columns)
                {
                    ValidateIdentifier(column.Schema, nameof(column.Schema));
                    ValidateIdentifier(column.Table, nameof(column.Table));
                    ValidateIdentifier(column.Column, nameof(column.Column));

                    var plan = Dialect.BuildParsedColumnExistsPlan(column);
                    using (var command = new SqlCommand(plan.CommandText, connection))
                    {
                        AddParameters(command, plan);
                        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                        var exists = value != null && value != DBNull.Value;
                        if (!exists)
                        {
                            result.Add(Dialect.BuildAddParsedColumnSql(column));
                        }
                    }
                }
            }

            return result;
        }

        protected override bool IsMissingInfrastructureException(Exception exception)
            => false;

        private async Task ReadLegacyHistoryAsync(SqlConnection connection, MigrationScriptCategory category, IDictionary<string, string> result, CancellationToken cancellationToken)
        {
            var plan = Dialect.BuildReadLegacyHistoryPlan(HistorySchema(), category);
            using (var command = new SqlCommand(plan.CommandText, connection))
            using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    result[CreateHistoryKey(category, reader.GetString(0))] = string.Empty;
            }
        }

        private bool IsMissingHistoryTable(SqlException exception, MigrationScriptCategory category)
        {
            var expectedTable = Dialect.HistoryTableName(category);
            return exception.Errors.Cast<SqlError>().Any(error =>
                IsExpectedMissingHistoryObject(error.Number, error.Message, expectedTable));
        }

        private static bool IsExpectedMissingHistoryObject(int number, string message, string expectedTable)
            => number == 208 &&
               !string.IsNullOrWhiteSpace(expectedTable) &&
               (message ?? string.Empty).IndexOf(expectedTable, StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool IsMissingHistoryHashColumn(SqlException exception)
            => exception.Errors.Cast<SqlError>().Any(error => error.Number == 207);

        private async Task ExecuteCommandAsync(ModelSyncSqlCommand plan, CancellationToken cancellationToken)
        {
            using (var connection = SqlServerConnectionFactory.Create(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = new SqlCommand(plan.CommandText, connection))
                {
                    AddParameters(command, plan);
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private static void AddParameters(SqlCommand command, ModelSyncSqlCommand plan)
        {
            foreach (var parameter in plan.Parameters)
                command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }

        private static MigrationRunnerOptions ConfigureDefaults(MigrationRunnerOptions? options)
        {
            var configured = options ?? MigrationRunnerOptions.Default();
            if (string.IsNullOrWhiteSpace(configured.HistorySchema))
                configured.HistorySchema = "sec";
            ValidateIdentifier(configured.HistorySchema, nameof(configured.HistorySchema));
            if (!configured.Schemas.Contains(configured.HistorySchema, StringComparer.OrdinalIgnoreCase))
                configured.Schemas.Add(configured.HistorySchema);
            return configured;
        }

        protected override string PrepareScriptSql(MigrationScriptDefinition definition, string sql)
        {
            if (definition.Category != MigrationScriptCategory.StoredProcedures)
                return sql;

            var routineSql = IsLegacyEmbeddedSqlEnabled()
                ? SqlServerLegacyRoutineNormalizer.Normalize(sql)
                : sql;

            return SqlServerStoredProcedureSynchronizer.ToCreateOrAlterSql(routineSql);
        }

        protected override void PopulateProviderError(MigrationExecutionItemResult result, Exception exception)
        {
            base.PopulateProviderError(result, exception);
            var sql = exception as SqlException;
            if (sql == null || sql.Errors.Count == 0)
                return;

            var error = sql.Errors[0];
            result.ProviderErrorCode = error.Number.ToString();
            result.ProviderErrorNumber = error.Number;
            result.ProviderErrorState = error.State.ToString();
            result.ProviderErrorSeverity = error.Class.ToString();
            result.ErrorLineNumber = error.LineNumber > 0 ? error.LineNumber : (int?)null;
            result.ErrorObjectName = Redact(error.Procedure ?? string.Empty);
            result.ErrorMessage = Redact(error.Message);
        }

        protected override bool SupportsTransactions => true;

        protected override bool SupportsTransactionalDdl => true;

        protected override bool SupportsDatabaseBackupBeforeReset => true;

        protected override bool IsTransactionCompatible(MigrationScriptDefinition definition)
        {
            foreach (var batch in SplitBatches(definition.Sql))
            {
                var sql = SqlDefinitionNormalizer.Normalize(batch);
                if (Regex.IsMatch(sql, @"(?:^|;)\s*(?:(?:CREATE|ALTER|DROP)\s+DATABASE|BACKUP\s+(?:DATABASE|LOG)|RESTORE\s+(?:DATABASE|LOG)|CREATE\s+FULLTEXT\s+INDEX|ALTER\s+FULLTEXT\s+INDEX|RECONFIGURE)\b", RegexOptions.IgnoreCase))
                    return false;
            }

            return true;
        }

        protected override string ProviderName => "SQL Server";

        protected override void ValidateResetDatabaseName(string databaseName)
        {
            base.ValidateResetDatabaseName(databaseName);
            var blocked = new[] { "master", "model", "msdb", "tempdb" };
            if (blocked.Contains(databaseName, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException($"SQL Server system database '{databaseName}' cannot be reset.");

            var expected = Options.ResetOptions?.ExpectedDatabaseName;
            if (!string.IsNullOrWhiteSpace(expected) && !string.Equals(expected, databaseName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Expected database name does not match the SQL Server target database.");
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
            => identifier.Replace("]", "]]");

        private static string EscapeLiteral(string value)
            => value.Replace("'", "''");

        private string BuildBackupSql(string databaseName)
        {
            var reset = Options.ResetOptions;
            if (reset == null || !reset.BackupBeforeReset)
                return string.Empty;

            var backupPath = ResolveBackupPath(reset, databaseName);
            return $"    BACKUP DATABASE [{EscapeIdentifier(databaseName)}] TO DISK = N'{EscapeLiteral(backupPath)}' WITH INIT, COPY_ONLY;\r\n";
        }

        private static string ResolveBackupPath(DatabaseResetOptions reset, string databaseName)
        {
            if (!string.IsNullOrWhiteSpace(reset.BackupFilePath))
                return reset.BackupFilePath!;

            if (string.IsNullOrWhiteSpace(reset.BackupDirectory))
                throw new InvalidOperationException("BackupDirectory or BackupFilePath is required when BackupBeforeReset is enabled.");

            var fileName = string.IsNullOrWhiteSpace(reset.BackupFileName)
                ? $"{databaseName}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.bak"
                : reset.BackupFileName!;

            return Path.Combine(reset.BackupDirectory!, fileName);
        }

        private bool IsLegacyEmbeddedSqlEnabled()
            => Options.AppliedCompatibilityProfiles.Contains(MigrationCompatibilityProfiles.LegacyEmbeddedSql);

        private sealed class SqlServerExecutionScope : IMigrationExecutionScope
        {
            private bool _completed;

            public SqlServerExecutionScope(SqlConnection connection, SqlTransaction? transaction)
            {
                Connection = connection;
                Transaction = transaction;
            }

            public SqlConnection Connection { get; }
            public SqlTransaction? Transaction { get; }
            public bool TransactionStarted => Transaction != null;

            public async Task ExecuteSqlAsync(string sql, CancellationToken cancellationToken)
            {
                using (var command = new SqlCommand(sql, Connection))
                {
                    command.Transaction = Transaction;
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            public async Task CompleteAsync(CancellationToken cancellationToken)
            {
                if (Transaction != null)
                    Transaction.Commit();
                _completed = true;
            }

            public Task<bool> RollbackAsync(CancellationToken cancellationToken)
            {
                if (Transaction == null || _completed)
                    return Task.FromResult(false);
                try
                {
                    Transaction.Rollback();
                    _completed = true;
                    return Task.FromResult(true);
                }
                catch
                {
                    return Task.FromResult(false);
                }
            }

            public void Dispose()
            {
                if (!_completed && Transaction != null)
                {
                    try
                    {
                        Transaction.Rollback();
                    }
                    catch
                    {
                    }
                }

                Transaction?.Dispose();
                Connection.Dispose();
            }
        }

    }
}
