using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using MySqlConnector;

using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.Interfaces;
using UmbrellaFrame.ModelSync.Core.Services;
using UmbrellaFrame.ModelSync.Core.SqlGeneration;
using UmbrellaFrame.ModelSync.MySql.Resources;

namespace UmbrellaFrame.ModelSync.MySql
{
    /// <summary>
    /// MySQL/MariaDB implementation of <see cref="ITableGenerator"/>.
    /// Generates and executes CREATE TABLE statements using MySqlConnector.
    /// </summary>
    public class MySqlTableGenerator : SqlTableGenerator, ITableGenerator
    {
        private readonly string _connectionString;
        private static readonly ModelSyncSqlDialect Dialect = new ModelSyncSqlDialect(MySqlProviderDescriptor.Create());

        /// <inheritdoc/>
        protected override string QuoteValidatedIdentifier(string identifier) => $"`{identifier}`";

        /// <inheritdoc/>
        protected override string IfNotExistsClause => "IF NOT EXISTS";

        /// <param name="connectionString">A valid MySQL connection string.</param>
        /// <param name="logger">Optional logger instance.</param>
        public MySqlTableGenerator(string connectionString, ILogger<MySqlTableGenerator>? logger = null)
            : base(logger)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentException(MySqlResources.Get("InvalidConnectionString"), nameof(connectionString));

            _connectionString = connectionString;
        }

        /// <summary>Generates the CREATE TABLE SQL for the given model and caches it.</summary>
        public string GenerateMySqlTable<T>(bool ifNotExists = false) where T : class, new()
            => GenerateSqlTable<T>(ifNotExists);

        // �� DDL execution ���������������������������������������������������

        /// <inheritdoc/>
        public void CreateDatabase()
        {
            var builder = new MySqlConnectionStringBuilder(_connectionString);
            string databaseName = builder.Database;

            if (string.IsNullOrEmpty(databaseName))
                return;

            builder.Database = string.Empty;

            using var connection = MySqlConnectionFactory.Create(builder.ConnectionString);
            connection.Open();
            using var command = new MySqlCommand($"CREATE DATABASE IF NOT EXISTS {QuoteIdentifier(databaseName)};", connection);
            command.ExecuteNonQuery();
        }

        /// <inheritdoc/>
        public async Task CreateDatabaseAsync(CancellationToken cancellationToken = default)
        {
            var builder = new MySqlConnectionStringBuilder(_connectionString);
            string databaseName = builder.Database;

            if (string.IsNullOrEmpty(databaseName))
                return;

            builder.Database = string.Empty;

            using var connection = MySqlConnectionFactory.Create(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var command = new MySqlCommand($"CREATE DATABASE IF NOT EXISTS {QuoteIdentifier(databaseName)};", connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public void CreateTables()
        {
            foreach (var sqlCommand in SqlCache.Values)
            {
                using var connection = MySqlConnectionFactory.Create(_connectionString);
                connection.Open();
                using var command = new MySqlCommand(sqlCommand, connection);
                command.ExecuteNonQuery();
            }
        }

        /// <inheritdoc/>
        public async Task CreateTablesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var sqlCommand in SqlCache.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var connection = MySqlConnectionFactory.Create(_connectionString);
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using var command = new MySqlCommand(sqlCommand, connection);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public void DropTables()
            => RequireDestructivePermission(null, nameof(DropTables));

        /// <inheritdoc/>
        public void DropTables(DestructiveOperationOptions options)
        {
            RequireDestructivePermission(options, nameof(DropTables));

            foreach (var type in SqlCache.Keys)
            {
                var sql = BuildDropTableSql(type);
                using var connection = MySqlConnectionFactory.Create(_connectionString);
                connection.Open();
                using var command = new MySqlCommand(sql, connection);
                command.ExecuteNonQuery();
            }
        }

        /// <inheritdoc/>
        public async Task DropTablesAsync(CancellationToken cancellationToken = default)
            => await DropTablesAsync(null, cancellationToken).ConfigureAwait(false);

        /// <inheritdoc/>
        public async Task DropTablesAsync(DestructiveOperationOptions? options, CancellationToken cancellationToken = default)
        {
            RequireDestructivePermission(options, nameof(DropTablesAsync));

            foreach (var type in SqlCache.Keys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sql = BuildDropTableSql(type);
                using var connection = MySqlConnectionFactory.Create(_connectionString);
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using var command = new MySqlCommand(sql, connection);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        // �� ALTER TABLE �����������������������������������������������������

        /// <inheritdoc/>
        public void AddColumn<T>(string columnName) where T : class, new()
        {
            var sql = BuildAddColumnSql<T>(columnName);
            using var connection = MySqlConnectionFactory.Create(_connectionString);
            connection.Open();
            using var command = new MySqlCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        /// <inheritdoc/>
        public async Task AddColumnAsync<T>(string columnName, CancellationToken cancellationToken = default) where T : class, new()
        {
            var sql = BuildAddColumnSql<T>(columnName);
            using var connection = MySqlConnectionFactory.Create(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var command = new MySqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public void DropColumn<T>(string columnName) where T : class, new()
            => RequireDestructivePermission(null, nameof(DropColumn));

        /// <inheritdoc/>
        public void DropColumn<T>(string columnName, DestructiveOperationOptions options) where T : class, new()
        {
            RequireDestructivePermission(options, nameof(DropColumn));

            var sql = BuildDropColumnSql<T>(columnName);
            using var connection = MySqlConnectionFactory.Create(_connectionString);
            connection.Open();
            using var command = new MySqlCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        /// <inheritdoc/>
        public async Task DropColumnAsync<T>(string columnName, CancellationToken cancellationToken = default) where T : class, new()
            => await DropColumnAsync<T>(columnName, null, cancellationToken).ConfigureAwait(false);

        /// <inheritdoc/>
        public async Task DropColumnAsync<T>(string columnName, DestructiveOperationOptions? options, CancellationToken cancellationToken = default) where T : class, new()
        {
            RequireDestructivePermission(options, nameof(DropColumnAsync));

            var sql = BuildDropColumnSql<T>(columnName);
            using var connection = MySqlConnectionFactory.Create(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var command = new MySqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public void RenameColumn<T>(string oldColumnName, string newColumnName) where T : class, new()
        {
            var sql = BuildRenameColumnSql<T>(oldColumnName, newColumnName);
            using var connection = MySqlConnectionFactory.Create(_connectionString);
            connection.Open();
            using var command = new MySqlCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        /// <inheritdoc/>
        public async Task RenameColumnAsync<T>(string oldColumnName, string newColumnName, CancellationToken cancellationToken = default) where T : class, new()
        {
            var sql = BuildRenameColumnSql<T>(oldColumnName, newColumnName);
            using var connection = MySqlConnectionFactory.Create(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var command = new MySqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>MySQL uses MODIFY COLUMN instead of ALTER COLUMN.</summary>
        protected override string BuildAlterColumnTypeSql<T>(string columnName)
        {
            var propertyManager = new Core.Helpers.DynamicPropertyManager<T>();
            var tableName = GetTableName(propertyManager);
            var columnTypeAttr = propertyManager.GetAttribute<Core.DbColumnTypeAttribute>(columnName);
            if (columnTypeAttr == null)
                throw new InvalidOperationException($"Column '{columnName}' has no type attribute on {typeof(T).Name}.");
            return Dialect.BuildAlterColumnTypeSql(string.Empty, tableName, columnName, columnTypeAttr.GetColumnType());
        }

        /// <inheritdoc/>
        public void AlterColumnType<T>(string columnName) where T : class, new()
            => RequireDestructivePermission(null, nameof(AlterColumnType));

        /// <inheritdoc/>
        public void AlterColumnType<T>(string columnName, DestructiveOperationOptions options) where T : class, new()
        {
            RequireDestructivePermission(options, nameof(AlterColumnType));

            var sql = BuildAlterColumnTypeSql<T>(columnName);
            using var connection = MySqlConnectionFactory.Create(_connectionString);
            connection.Open();
            using var command = new MySqlCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        /// <inheritdoc/>
        public async Task AlterColumnTypeAsync<T>(string columnName, CancellationToken cancellationToken = default) where T : class, new()
            => await AlterColumnTypeAsync<T>(columnName, null, cancellationToken).ConfigureAwait(false);

        /// <inheritdoc/>
        public async Task AlterColumnTypeAsync<T>(string columnName, DestructiveOperationOptions? options, CancellationToken cancellationToken = default) where T : class, new()
        {
            RequireDestructivePermission(options, nameof(AlterColumnTypeAsync));

            var sql = BuildAlterColumnTypeSql<T>(columnName);
            using var connection = MySqlConnectionFactory.Create(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var command = new MySqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
