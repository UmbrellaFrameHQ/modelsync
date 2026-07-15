using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.Interfaces;
using UmbrellaFrame.ModelSync.Core.Services;
using UmbrellaFrame.ModelSync.Core.SqlGeneration;

namespace UmbrellaFrame.ModelSync.MySql
{
    public sealed class MySqlStoredProcedureSynchronizer : IStoredProcedureSynchronizer
    {
        private readonly string _connectionString;
        private readonly ILogger<MySqlStoredProcedureSynchronizer> _logger;
        private readonly StoredProcedureSqlPlanner _planner = new StoredProcedureSqlPlanner(MySqlProviderDescriptor.Create());
        private readonly List<StoredProcedureDefinition> _definitions = new List<StoredProcedureDefinition>();

        public MySqlStoredProcedureSynchronizer(string connectionString, ILogger<MySqlStoredProcedureSynchronizer>? logger = null)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));
            _connectionString = connectionString;
            _logger = logger ?? NullLogger<MySqlStoredProcedureSynchronizer>.Instance;
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
            foreach (var statement in new[] { _planner.BuildDropSql(plan.Definition), plan.Definition.Sql.Trim() })
            {
                using (var connection = MySqlConnectionFactory.Create(_connectionString))
                {
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                    using (var command = new MySqlCommand(statement, connection))
                        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
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

        public string BuildApplySql(StoredProcedureDefinition definition)
            => _planner.BuildApplySql(definition);

        private async Task<string> ReadCurrentDefinitionAsync(StoredProcedureDefinition definition, CancellationToken cancellationToken)
        {
            var plan = _planner.BuildReadCurrentDefinitionPlan(definition);
            using (var connection = MySqlConnectionFactory.Create(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    using (var command = new MySqlCommand(plan.CommandText, connection))
                    using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                    {
                        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                            return string.Empty;
                        var ordinal = reader.GetOrdinal("Create Procedure");
                        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
                    }
                }
                catch (MySqlException ex) when (ex.Number == 1305)
                {
                    return string.Empty;
                }
            }
        }
    }
}
