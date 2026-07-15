using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Oracle.ManagedDataAccess.Client;

using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.Interfaces;
using UmbrellaFrame.ModelSync.Core.Services;
using UmbrellaFrame.ModelSync.Core.SqlGeneration;

namespace UmbrellaFrame.ModelSync.Oracle
{
    /// <summary>
    /// Oracle implementation of <see cref="ITableGenerator"/>.
    /// </summary>
    public class OracleTableGenerator : SqlTableGenerator, ITableGenerator
    {
        private const int OraObjectAlreadyExists = 955;
        private const int OraTableOrViewDoesNotExist = 942;
        private readonly string _connectionString;
        private static readonly ModelSyncSqlDialect Dialect = new ModelSyncSqlDialect(OracleProviderDescriptor.Create());

        protected override string QuoteValidatedIdentifier(string identifier) => $"\"{identifier}\"";

        protected override string IfNotExistsClause => string.Empty;

        public OracleTableGenerator(string connectionString, ILogger<OracleTableGenerator>? logger = null)
            : base(logger)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Oracle connection string cannot be empty.", nameof(connectionString));

            _connectionString = connectionString;
        }

        public string GenerateOracleTable<T>(bool ifNotExists = false) where T : class, new()
            => GenerateSqlTable<T>(ifNotExists);

        public new string GenerateDropTableSql<T>() where T : class, new()
        {
            var propertyManager = new Core.Helpers.DynamicPropertyManager<T>();
            var tableName = GetTableName(propertyManager);
            return $"DROP TABLE {QuoteIdentifier(tableName)} CASCADE CONSTRAINTS";
        }

        public void CreateDatabase()
        {
            // Oracle schemas are users and are normally provisioned by DBA/admin tooling.
        }

        public Task CreateDatabaseAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public void CreateTables()
        {
            foreach (var sqlCommand in SqlCache.Values)
                ExecuteIgnoring(sqlCommand, OraObjectAlreadyExists);
        }

        public async Task CreateTablesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var sqlCommand in SqlCache.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ExecuteIgnoringAsync(sqlCommand, OraObjectAlreadyExists, cancellationToken).ConfigureAwait(false);
            }
        }

        public void DropTables()
            => RequireDestructivePermission(null, nameof(DropTables));

        public void DropTables(DestructiveOperationOptions options)
        {
            RequireDestructivePermission(options, nameof(DropTables));

            foreach (var type in SqlCache.Keys)
                ExecuteIgnoring($"DROP TABLE {QuoteIdentifier(GetCachedTableName(type))} CASCADE CONSTRAINTS", OraTableOrViewDoesNotExist);
        }

        public async Task DropTablesAsync(CancellationToken cancellationToken = default)
            => await DropTablesAsync(null, cancellationToken).ConfigureAwait(false);

        public async Task DropTablesAsync(DestructiveOperationOptions? options, CancellationToken cancellationToken = default)
        {
            RequireDestructivePermission(options, nameof(DropTablesAsync));

            foreach (var type in SqlCache.Keys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ExecuteIgnoringAsync($"DROP TABLE {QuoteIdentifier(GetCachedTableName(type))} CASCADE CONSTRAINTS", OraTableOrViewDoesNotExist, cancellationToken).ConfigureAwait(false);
            }
        }

        public void AddColumn<T>(string columnName) where T : class, new()
            => Execute(BuildAddColumnSql<T>(columnName));

        public async Task AddColumnAsync<T>(string columnName, CancellationToken cancellationToken = default) where T : class, new()
            => await ExecuteAsync(BuildAddColumnSql<T>(columnName), cancellationToken).ConfigureAwait(false);

        public void DropColumn<T>(string columnName) where T : class, new()
            => RequireDestructivePermission(null, nameof(DropColumn));

        public void DropColumn<T>(string columnName, DestructiveOperationOptions options) where T : class, new()
        {
            RequireDestructivePermission(options, nameof(DropColumn));
            Execute(BuildDropColumnSql<T>(columnName));
        }

        public async Task DropColumnAsync<T>(string columnName, CancellationToken cancellationToken = default) where T : class, new()
            => await DropColumnAsync<T>(columnName, null, cancellationToken).ConfigureAwait(false);

        public async Task DropColumnAsync<T>(string columnName, DestructiveOperationOptions? options, CancellationToken cancellationToken = default) where T : class, new()
        {
            RequireDestructivePermission(options, nameof(DropColumnAsync));
            await ExecuteAsync(BuildDropColumnSql<T>(columnName), cancellationToken).ConfigureAwait(false);
        }

        public void RenameColumn<T>(string oldColumnName, string newColumnName) where T : class, new()
            => Execute(BuildRenameColumnSql<T>(oldColumnName, newColumnName));

        public async Task RenameColumnAsync<T>(string oldColumnName, string newColumnName, CancellationToken cancellationToken = default) where T : class, new()
            => await ExecuteAsync(BuildRenameColumnSql<T>(oldColumnName, newColumnName), cancellationToken).ConfigureAwait(false);

        protected override string BuildAlterColumnTypeSql<T>(string columnName)
        {
            var propertyManager = new Core.Helpers.DynamicPropertyManager<T>();
            var tableName = GetTableName(propertyManager);
            var columnTypeAttr = propertyManager.GetAttribute<DbColumnTypeAttribute>(columnName);
            if (columnTypeAttr == null)
                throw new InvalidOperationException($"Column '{columnName}' has no type attribute on {typeof(T).Name}.");
            return Dialect.BuildAlterColumnTypeSql(string.Empty, tableName, columnName, columnTypeAttr.GetColumnType());
        }

        public void AlterColumnType<T>(string columnName) where T : class, new()
            => RequireDestructivePermission(null, nameof(AlterColumnType));

        public void AlterColumnType<T>(string columnName, DestructiveOperationOptions options) where T : class, new()
        {
            RequireDestructivePermission(options, nameof(AlterColumnType));
            Execute(BuildAlterColumnTypeSql<T>(columnName));
        }

        public async Task AlterColumnTypeAsync<T>(string columnName, CancellationToken cancellationToken = default) where T : class, new()
            => await AlterColumnTypeAsync<T>(columnName, null, cancellationToken).ConfigureAwait(false);

        public async Task AlterColumnTypeAsync<T>(string columnName, DestructiveOperationOptions? options, CancellationToken cancellationToken = default) where T : class, new()
        {
            RequireDestructivePermission(options, nameof(AlterColumnTypeAsync));
            await ExecuteAsync(BuildAlterColumnTypeSql<T>(columnName), cancellationToken).ConfigureAwait(false);
        }

        private void Execute(string sql)
        {
            using var connection = OracleConnectionFactory.Create(_connectionString);
            connection.Open();
            using var command = new OracleCommand(NormalizeCommandText(sql), connection);
            command.ExecuteNonQuery();
        }

        private async Task ExecuteAsync(string sql, CancellationToken cancellationToken)
        {
            using var connection = OracleConnectionFactory.Create(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var command = new OracleCommand(NormalizeCommandText(sql), connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static string NormalizeCommandText(string sql)
            => (sql ?? string.Empty).Trim().TrimEnd(';');

        private void ExecuteIgnoring(string sql, int oracleErrorNumber)
        {
            try
            {
                Execute(sql);
            }
            catch (OracleException ex) when (ex.Number == oracleErrorNumber)
            {
            }
        }

        private async Task ExecuteIgnoringAsync(string sql, int oracleErrorNumber, CancellationToken cancellationToken)
        {
            try
            {
                await ExecuteAsync(sql, cancellationToken).ConfigureAwait(false);
            }
            catch (OracleException ex) when (ex.Number == oracleErrorNumber)
            {
            }
        }
    }
}
