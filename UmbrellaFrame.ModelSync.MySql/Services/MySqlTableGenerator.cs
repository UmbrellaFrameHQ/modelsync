using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using MySqlConnector;

using UmbrellaFrame.ModelSync.Core.Interfaces;
using UmbrellaFrame.ModelSync.Core.Services;
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

        /// <inheritdoc/>
        protected override string QuoteIdentifier(string identifier) => $"`{identifier}`";

        /// <inheritdoc/>
        protected override string IfNotExistsClause => "IF NOT EXISTS";

        /// <param name="connectionString">A valid MySQL connection string.</param>
        /// <param name="logger">Optional logger instance.</param>
        public MySqlTableGenerator(string connectionString, ILogger<MySqlTableGenerator> logger = null)
            : base(logger)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentException(MySqlResources.Get("InvalidConnectionString"), nameof(connectionString));

            _connectionString = connectionString;
        }

        /// <summary>Generates the CREATE TABLE SQL for the given model and caches it.</summary>
        public string GenerateMySqlTable<T>(bool ifNotExists = false) where T : class, new()
            => GenerateSqlTable<T>(ifNotExists);

        // ── DDL execution ───────────────────────────────────────────────────

        /// <inheritdoc/>
        public void CreateDatabase()
        {
            var builder = new MySqlConnectionStringBuilder(_connectionString);
            string databaseName = builder.Database;

            if (string.IsNullOrEmpty(databaseName))
                return;

            builder.Database = string.Empty;

            using var connection = new MySqlConnection(builder.ConnectionString);
            connection.Open();
            using var command = new MySqlCommand($"CREATE DATABASE IF NOT EXISTS `{databaseName}`;", connection);
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

            using var connection = new MySqlConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var command = new MySqlCommand($"CREATE DATABASE IF NOT EXISTS `{databaseName}`;", connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public void CreateTables()
        {
            foreach (var sqlCommand in SqlCache.Values)
            {
                using var connection = new MySqlConnection(_connectionString);
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
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using var command = new MySqlCommand(sqlCommand, connection);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public void DropTables()
        {
            foreach (var type in SqlCache.Keys)
            {
                var sql = InvokeGenerateDropSql(type);
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                using var command = new MySqlCommand(sql, connection);
                command.ExecuteNonQuery();
            }
        }

        /// <inheritdoc/>
        public async Task DropTablesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var type in SqlCache.Keys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sql = InvokeGenerateDropSql(type);
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using var command = new MySqlCommand(sql, connection);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private string InvokeGenerateDropSql(Type type)
            => $"DROP TABLE IF EXISTS {QuoteIdentifier(type.Name)};";

        // ── ALTER TABLE ─────────────────────────────────────────────────────

        /// <inheritdoc/>
        public void AddColumn<T>(string columnName) where T : class, new()
        {
            var sql = BuildAddColumnSql<T>(columnName);
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();
            using var command = new MySqlCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        /// <inheritdoc/>
        public async Task AddColumnAsync<T>(string columnName, CancellationToken cancellationToken = default) where T : class, new()
        {
            var sql = BuildAddColumnSql<T>(columnName);
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var command = new MySqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public void DropColumn<T>(string columnName) where T : class, new()
        {
            var sql = BuildDropColumnSql<T>(columnName);
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();
            using var command = new MySqlCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        /// <inheritdoc/>
        public async Task DropColumnAsync<T>(string columnName, CancellationToken cancellationToken = default) where T : class, new()
        {
            var sql = BuildDropColumnSql<T>(columnName);
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var command = new MySqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public void RenameColumn<T>(string oldColumnName, string newColumnName) where T : class, new()
        {
            var sql = BuildRenameColumnSql<T>(oldColumnName, newColumnName);
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();
            using var command = new MySqlCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        /// <inheritdoc/>
        public async Task RenameColumnAsync<T>(string oldColumnName, string newColumnName, CancellationToken cancellationToken = default) where T : class, new()
        {
            var sql = BuildRenameColumnSql<T>(oldColumnName, newColumnName);
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var command = new MySqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>MySQL uses MODIFY COLUMN instead of ALTER COLUMN.</summary>
        protected override string BuildAlterColumnTypeSql<T>(string columnName)
        {
            var propertyManager = new Core.Helpers.DynamicPropertyManager<T>();
            var tableNameAttr = propertyManager.GetClassAttribute<Core.DbTableNameAttribute>();
            var tableName = tableNameAttr?.TableName ?? typeof(T).Name;
            var columnTypeAttr = propertyManager.GetAttribute<Core.DbColumnTypeAttribute>(columnName);
            if (columnTypeAttr == null)
                throw new InvalidOperationException($"Column '{columnName}' has no type attribute on {typeof(T).Name}.");
            return $"ALTER TABLE `{tableName}` MODIFY COLUMN `{columnName}` {columnTypeAttr.GetColumnType()};";
        }

        /// <inheritdoc/>
        public void AlterColumnType<T>(string columnName) where T : class, new()
        {
            var sql = BuildAlterColumnTypeSql<T>(columnName);
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();
            using var command = new MySqlCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        /// <inheritdoc/>
        public async Task AlterColumnTypeAsync<T>(string columnName, CancellationToken cancellationToken = default) where T : class, new()
        {
            var sql = BuildAlterColumnTypeSql<T>(columnName);
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var command = new MySqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}

