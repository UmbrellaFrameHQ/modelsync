using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.Interfaces;
using UmbrellaFrame.ModelSync.Core.Services;
using UmbrellaFrame.ModelSync.Core.SqlGeneration;

namespace UmbrellaFrame.ModelSync.SqlServer
{
    public sealed class SqlServerStoredProcedureSynchronizer : IStoredProcedureSynchronizer
    {
        private readonly string _connectionString;
        private readonly ILogger<SqlServerStoredProcedureSynchronizer> _logger;
        private readonly StoredProcedureSqlPlanner _planner = new StoredProcedureSqlPlanner(SqlServerProviderDescriptor.Create());
        private readonly List<StoredProcedureDefinition> _definitions = new List<StoredProcedureDefinition>();

        public SqlServerStoredProcedureSynchronizer(string connectionString, ILogger<SqlServerStoredProcedureSynchronizer>? logger = null)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));
            _connectionString = connectionString;
            _logger = logger ?? NullLogger<SqlServerStoredProcedureSynchronizer>.Instance;
        }

        public void RegisterProcedure(StoredProcedureDefinition definition)
        {
            _planner.BuildApplySql(definition);
            _definitions.Add(definition);
        }

        public StoredProcedureDefinition RegisterProcedureFile(string path, string? name = null, string schema = "dbo")
        {
            var definition = StoredProcedureDefinition.FromFile(path, name, schema);
            RegisterProcedure(definition);
            return definition;
        }

        public async Task<StoredProcedureSyncPlan> CompareAsync(StoredProcedureDefinition definition, CancellationToken cancellationToken = default)
            => _planner.BuildPlan(definition, await ReadCurrentDefinitionAsync(definition, cancellationToken).ConfigureAwait(false));

        public async Task<IReadOnlyList<StoredProcedureSyncPlan>> CompareRegisteredAsync(CancellationToken cancellationToken = default)
        {
            var plans = new List<StoredProcedureSyncPlan>();
            foreach (var definition in _definitions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                plans.Add(await CompareAsync(definition, cancellationToken).ConfigureAwait(false));
            }
            return plans;
        }

        public async Task ApplyAsync(StoredProcedureSyncPlan plan, CancellationToken cancellationToken = default)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (!plan.HasChanges)
                return;
            using (var connection = SqlServerConnectionFactory.Create(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = new SqlCommand(plan.SqlToApply, connection))
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            _logger.LogInformation("Stored procedure synchronized: {Schema}.{Name} ({ChangeType})", plan.Definition.Schema, plan.Definition.Name, plan.ChangeType);
        }

        public async Task<IReadOnlyList<StoredProcedureSyncPlan>> SyncRegisteredAsync(CancellationToken cancellationToken = default)
        {
            var plans = await CompareRegisteredAsync(cancellationToken).ConfigureAwait(false);
            foreach (var plan in plans)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ApplyAsync(plan, cancellationToken).ConfigureAwait(false);
            }
            return plans;
        }

        public StoredProcedureSyncPlan BuildPlan(StoredProcedureDefinition definition, string? currentSql)
            => _planner.BuildPlan(definition, currentSql);

        public string BuildCreateOrAlterSql(StoredProcedureDefinition definition)
            => _planner.BuildApplySql(definition);

        public static string ToCreateOrAlterSql(string sql)
            => new StoredProcedureSqlPlanner(SqlServerProviderDescriptor.Create()).BuildComparableSql(sql);

        private async Task<string> ReadCurrentDefinitionAsync(StoredProcedureDefinition definition, CancellationToken cancellationToken)
        {
            var plan = _planner.BuildReadCurrentDefinitionPlan(definition);
            using (var connection = SqlServerConnectionFactory.Create(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = new SqlCommand(plan.CommandText, connection))
                {
                    AddParameters(command, plan);
                    return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string ?? string.Empty;
                }
            }
        }

        private static void AddParameters(SqlCommand command, ModelSyncSqlCommand plan)
        {
            foreach (var parameter in plan.Parameters)
                command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }
    }
}
