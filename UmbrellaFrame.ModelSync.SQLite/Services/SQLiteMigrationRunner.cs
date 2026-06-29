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
using UmbrellaFrame.ModelSync.Core.SqlGeneration;
using UmbrellaFrame.ModelSync.Core.Services;

namespace UmbrellaFrame.ModelSync.SQLite
{
    /// <summary>Runs ordered SQLite migration scripts with history tracking. Stored procedures are not supported by SQLite.</summary>
    public sealed class SQLiteMigrationRunner : SqlMigrationRunnerBase
    {
        private static readonly Regex SafeIdentifierPattern = new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
        private static readonly ModelSyncSqlDialect Dialect = new ModelSyncSqlDialect(SQLiteProviderDescriptor.Create());
        private readonly string _connectionString;

        public SQLiteMigrationRunner(string connectionString, MigrationRunnerOptions options = null, ILogger<SQLiteMigrationRunner> logger = null)
            : base(ConfigureDefaults(options), logger ?? NullLogger<SQLiteMigrationRunner>.Instance, new ProviderNativeMigrationLockStrategy(Dialect))
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));
            _connectionString = connectionString;
        }

        private static MigrationRunnerOptions ConfigureDefaults(MigrationRunnerOptions options)
        {
            var configured = options ?? MigrationRunnerOptions.Default();
            configured.LockOptions.Mode = MigrationLockMode.Disabled;
            return configured;
        }

        protected override Task<System.Data.Common.DbConnection?> CreateLockConnectionAsync(CancellationToken cancellationToken)
            => Task.FromResult<System.Data.Common.DbConnection?>(SQLiteConnectionFactory.Create(_connectionString));

        protected override Task ResetDatabaseAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException("SQLite database reset is not supported by SQLiteMigrationRunner. Delete the database file explicitly if you need a reset.");

        protected override Task EnsureSchemasAsync(IEnumerable<string> schemas, CancellationToken cancellationToken)
            => Task.CompletedTask;

        protected override async Task EnsureHistoryTablesAsync(CancellationToken cancellationToken)
        {
            await ExecuteSqlAsync(Dialect.BuildEnsureHistoryInfrastructurePlan(string.Empty).CommandText, cancellationToken).ConfigureAwait(false);
        }

        protected override async Task<IDictionary<string, string>> ReadHistoryAsync(CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var connection = SQLiteConnectionFactory.Create(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                foreach (MigrationScriptCategory category in Enum.GetValues(typeof(MigrationScriptCategory)))
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = Dialect.BuildReadHistoryPlan(string.Empty, category).CommandText;
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
            using (var connection = SQLiteConnectionFactory.Create(_connectionString))
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
            var plan = Dialect.BuildRecordHistoryPlan(string.Empty, definition.Category, definition.Id, definition.Name, hash);
            using (var connection = SQLiteConnectionFactory.Create(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = plan.CommandText;
                    AddParameters(command, plan);
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

        protected override Task<MigrationExecutionItemResult> ApplyPlanWithResultAsync(MigrationSyncPlan plan, CancellationToken cancellationToken)
        {
            if (plan.Definition.Category != MigrationScriptCategory.StoredProcedures)
                return ApplyPlanWithSQLiteTransactionAsync(plan, cancellationToken);

            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new MigrationExecutionItemResult
            {
                Category = plan.Definition.Category,
                ScriptId = plan.Definition.Id,
                Name = plan.Definition.Name,
                Source = plan.Definition.Source,
                Action = MigrationExecutionAction.Failed,
                ExistingHash = plan.CurrentHash,
                TargetHash = plan.TargetHash,
                StartedAt = now,
                CompletedAt = now,
                FailureStage = "ProviderCapability",
                ErrorCode = nameof(NotSupportedException)
            });
        }

        protected override bool SupportsTransactions => true;

        protected override bool SupportsTransactionalDdl => true;

        private async Task<MigrationExecutionItemResult> ApplyPlanWithSQLiteTransactionAsync(MigrationSyncPlan plan, CancellationToken cancellationToken)
        {
            if (Options.TransactionPolicy == MigrationTransactionPolicy.Required || Options.TransactionPolicy == MigrationTransactionPolicy.Auto)
                return await ApplyPlanWithTransactionAsync(plan, cancellationToken).ConfigureAwait(false);

            return await base.ApplyPlanWithResultAsync(plan, cancellationToken).ConfigureAwait(false);
        }

        private async Task<MigrationExecutionItemResult> ApplyPlanWithTransactionAsync(MigrationSyncPlan plan, CancellationToken cancellationToken)
        {
            var startedAt = DateTimeOffset.UtcNow;
            var result = new MigrationExecutionItemResult
            {
                Category = plan.Definition.Category,
                ScriptId = plan.Definition.Id,
                Name = plan.Definition.Name,
                Source = plan.Definition.Source,
                Action = plan.ChangeType == MigrationChangeType.Reapply ? MigrationExecutionAction.Reapplied : MigrationExecutionAction.Applied,
                ExistingHash = plan.CurrentHash,
                TargetHash = plan.TargetHash,
                StartedAt = startedAt
            };

            var scripts = new List<string>();
            if (plan.Definition.Category == MigrationScriptCategory.Tables &&
                plan.ChangeType == MigrationChangeType.Reapply &&
                Options.AutoAddMissingColumnsFromTableScripts)
            {
                scripts.AddRange(await BuildMissingColumnScriptsAsync(plan.Definition, cancellationToken).ConfigureAwait(false));
            }
            else
            {
                scripts.Add(plan.SqlToApply);
            }

            using (var connection = SQLiteConnectionFactory.Create(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                await ExecuteNonQueryAsync(connection, "BEGIN IMMEDIATE;", cancellationToken).ConfigureAwait(false);
                var transactionOpen = true;
                try
                {
                    foreach (var sql in scripts)
                    {
                        var batches = SplitBatches(sql).Where(batch => !string.IsNullOrWhiteSpace(batch)).ToList();
                        result.BatchCount += batches.Count;
                        foreach (var batch in batches)
                        {
                            await ExecuteNonQueryAsync(connection, batch, cancellationToken).ConfigureAwait(false);
                            result.CompletedBatchCount++;
                        }
                    }

                    if (scripts.Count > 0)
                    {
                        var historyPlan = Dialect.BuildRecordHistoryPlan(string.Empty, plan.Definition.Category, plan.Definition.Id, plan.Definition.Name, plan.TargetHash);
                        await ExecuteCommandAsync(connection, historyPlan, cancellationToken).ConfigureAwait(false);
                    }

                    await ExecuteNonQueryAsync(connection, "COMMIT;", cancellationToken).ConfigureAwait(false);
                    transactionOpen = false;
                    result.CompletedAt = DateTimeOffset.UtcNow;
                    return result;
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    if (transactionOpen)
                        await RollbackQuietlyAsync(connection).ConfigureAwait(false);
                    result.Action = MigrationExecutionAction.Failed;
                    result.FailureStage = result.CompletedBatchCount < result.BatchCount ? "ExecuteBatch" : "RecordHistory";
                    result.ErrorCode = ex.GetType().Name;
                    result.CompletedAt = DateTimeOffset.UtcNow;
                    return result;
                }
                catch
                {
                    if (transactionOpen)
                        await RollbackQuietlyAsync(connection).ConfigureAwait(false);
                    throw;
                }
            }
        }

        private static async Task ExecuteNonQueryAsync(SqliteConnection connection, string batch, CancellationToken cancellationToken)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = batch;
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task ExecuteCommandAsync(SqliteConnection connection, ModelSyncSqlCommand plan, CancellationToken cancellationToken)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = plan.CommandText;
                AddParameters(command, plan);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task RollbackQuietlyAsync(SqliteConnection connection)
        {
            try
            {
                var batch = "ROLLBACK;";
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = batch;
                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
            catch
            {
            }
        }

        protected override async Task<IReadOnlyList<string>> BuildMissingColumnScriptsAsync(MigrationScriptDefinition definition, CancellationToken cancellationToken)
        {
            var columns = TableScriptColumnParser.Parse(definition.Sql, "main");
            var result = new List<string>();
            using (var connection = SQLiteConnectionFactory.Create(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                foreach (var column in columns)
                {
                    ValidateIdentifier(column.Table, nameof(column.Table));
                    ValidateIdentifier(column.Column, nameof(column.Column));
                    var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = Dialect.BuildParsedColumnExistsPlan(column).CommandText;
                        using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                                existing.Add(reader.GetString(1));
                        }
                    }

                    if (!existing.Contains(column.Column))
                        result.Add(Dialect.BuildAddParsedColumnSql(column));
                }
            }

            return result;
        }

        protected override bool IsMissingInfrastructureException(Exception exception)
        {
            var sqlite = exception as SqliteException;
            return sqlite != null && sqlite.SqliteErrorCode == 1;
        }

        private static void ValidateIdentifier(string identifier, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(identifier) || !SafeIdentifierPattern.IsMatch(identifier))
                throw new ArgumentException($"Invalid SQL identifier '{identifier}'.", parameterName);
        }

        private static string EscapeIdentifier(string identifier)
            => identifier.Replace("\"", "\"\"");

        private static void AddParameters(SqliteCommand command, ModelSyncSqlCommand plan)
        {
            foreach (var parameter in plan.Parameters)
                command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }
    }
}
