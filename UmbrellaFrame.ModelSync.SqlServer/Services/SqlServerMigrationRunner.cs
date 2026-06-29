using System;
using System.Collections.Generic;
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
            MigrationRunnerOptions options = null,
            ILogger<SqlServerMigrationRunner> logger = null)
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
                var sql = $@"
IF EXISTS (SELECT 1 FROM sys.databases WHERE name = N'{EscapeLiteral(targetDb)}')
BEGIN
    ALTER DATABASE [{EscapeIdentifier(targetDb)}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{EscapeIdentifier(targetDb)}];
END
CREATE DATABASE [{EscapeIdentifier(targetDb)}];";
                using (var command = new SqlCommand(sql, connection))
                {
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
        {
            var sql = exception as SqlException;
            if (sql == null)
                return false;

            foreach (SqlError error in sql.Errors)
            {
                if (error.Number == 208 || error.Number == 2760 || error.Number == 15151)
                    return true;
            }

            return false;
        }

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

        protected override async Task ApplyPlanAsync(MigrationSyncPlan plan, CancellationToken cancellationToken)
        {
            if (plan.Definition.Category != MigrationScriptCategory.StoredProcedures)
            {
                await base.ApplyPlanAsync(plan, cancellationToken).ConfigureAwait(false);
                return;
            }

            var sql = SqlServerStoredProcedureSynchronizer.ToCreateOrAlterSql(plan.SqlToApply);
            foreach (var batch in SplitBatches(sql))
            {
                if (!string.IsNullOrWhiteSpace(batch))
                    await ExecuteSqlAsync(batch, cancellationToken).ConfigureAwait(false);
            }

            await RecordHistoryAsync(plan.Definition, plan.TargetHash, cancellationToken).ConfigureAwait(false);
        }

        protected override async Task<MigrationExecutionItemResult> ApplyPlanWithResultAsync(MigrationSyncPlan plan, CancellationToken cancellationToken)
        {
            if (plan.Definition.Category != MigrationScriptCategory.StoredProcedures)
                return await base.ApplyPlanWithResultAsync(plan, cancellationToken).ConfigureAwait(false);

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

            try
            {
                var batches = SplitBatches(SqlServerStoredProcedureSynchronizer.ToCreateOrAlterSql(plan.SqlToApply))
                    .Where(batch => !string.IsNullOrWhiteSpace(batch))
                    .ToList();
                result.BatchCount = batches.Count;
                foreach (var batch in batches)
                {
                    await ExecuteSqlAsync(batch, cancellationToken).ConfigureAwait(false);
                    result.CompletedBatchCount++;
                }

                await RecordHistoryAsync(plan.Definition, plan.TargetHash, cancellationToken).ConfigureAwait(false);
                result.CompletedAt = DateTimeOffset.UtcNow;
                return result;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                result.Action = MigrationExecutionAction.Failed;
                result.FailureStage = result.CompletedBatchCount < result.BatchCount ? "ExecuteBatch" : "RecordHistory";
                result.ErrorCode = ex.GetType().Name;
                result.CompletedAt = DateTimeOffset.UtcNow;
                return result;
            }
        }

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

    }
}
