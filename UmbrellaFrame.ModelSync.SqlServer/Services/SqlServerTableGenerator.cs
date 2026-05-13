using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

using UmbrellaFrame.ModelSync.Core.Interfaces;
using UmbrellaFrame.ModelSync.Core.Services;
using UmbrellaFrame.ModelSync.SqlServer.Resources;

namespace UmbrellaFrame.ModelSync.SqlServer
{
    /// <summary>
    /// SQL Server implementation of <see cref="ITableGenerator"/>.
    /// Generates and executes CREATE TABLE statements using Microsoft.Data.SqlClient.
    /// </summary>
    public class SqlServerTableGenerator : SqlTableGenerator, ITableGenerator
    {
        private readonly string _connectionString;

        /// <inheritdoc/>
        protected override string QuoteIdentifier(string identifier) => $"[{identifier}]";

        /// <summary>
        /// SQL Server does not support <c>CREATE TABLE IF NOT EXISTS</c> inline.
        /// The guard is emitted as a surrounding <c>IF NOT EXISTS (...) BEGIN ... END</c> block.
        /// </summary>
        protected override string IfNotExistsClause => string.Empty;

        /// <param name="connectionString">A valid SQL Server connection string.</param>
        /// <param name="logger">Optional logger instance.</param>
        public SqlServerTableGenerator(string connectionString, ILogger<SqlServerTableGenerator> logger = null)
            : base(logger)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentException(SqlServerResources.Get("InvalidConnectionString"), nameof(connectionString));

            _connectionString = connectionString;
        }

        /// <summary>Generates the CREATE TABLE SQL for the given model and caches it.</summary>
        public string GenerateSqlServerTable<T>(bool ifNotExists = false) where T : class, new()
            => GenerateSqlTable<T>(ifNotExists);

        /// <summary>
        /// SQL Server does not support inline IF NOT EXISTS on CREATE TABLE.
        /// When <paramref name="ifNotExists"/> is <c>true</c>, wraps the statement with
        /// <c>IF OBJECT_ID(N'[table]', N'U') IS NULL BEGIN ... END</c>.
        /// </summary>
        public new string GenerateSqlTable<T>(bool ifNotExists = false) where T : class, new()
        {
            // Build raw CREATE TABLE (without inline IF NOT EXISTS guard)
            var sql = base.GenerateSqlTable<T>(ifNotExists: false);

            if (ifNotExists)
            {
                var pm = new Core.Helpers.DynamicPropertyManager<T>();
                var attr = pm.GetClassAttribute<Core.DbTableNameAttribute>();
                var tableName = attr?.TableName ?? typeof(T).Name;

                sql = $"IF OBJECT_ID(N'[{tableName}]', N'U') IS NULL\nBEGIN\n{sql}\nEND";

                // Update the cache with the guarded SQL
                SqlCache[typeof(T)] = sql;
            }

            return sql;
        }

        // ── DDL execution ───────────────────────────────────────────────────

        /// <inheritdoc/>
        public void CreateDatabase()
        {
            var builder = new SqlConnectionStringBuilder(_connectionString);
            string databaseName = builder.InitialCatalog;

            if (string.IsNullOrEmpty(databaseName))
                return;

            builder.InitialCatalog = "master";
            builder.ConnectTimeout = 10;

            using var masterConnection = new SqlConnection(builder.ConnectionString);
            masterConnection.Open();

            var sql = $"IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = N'{databaseName}') CREATE DATABASE [{databaseName}];";
            using var command = new SqlCommand(sql, masterConnection);
            command.ExecuteNonQuery();
        }

        /// <inheritdoc/>
        public async Task CreateDatabaseAsync(CancellationToken cancellationToken = default)
        {
            var builder = new SqlConnectionStringBuilder(_connectionString);
            string databaseName = builder.InitialCatalog;

            if (string.IsNullOrEmpty(databaseName))
                return;

            builder.InitialCatalog = "master";
            builder.ConnectTimeout = 10;

            using var masterConnection = new SqlConnection(builder.ConnectionString);
            await masterConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var sql = $"IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = N'{databaseName}') CREATE DATABASE [{databaseName}];";
            using var command = new SqlCommand(sql, masterConnection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public void CreateTables()
        {
            CreateDatabase();

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            foreach (var sqlCommand in SqlCache.Values)
            {
                // SQL Server doesn't support CREATE TABLE IF NOT EXISTS inline.
                // Wrap in TRY/CATCH: silently ignore error 2714 (object already exists).
                var guardedSql =
                    $"BEGIN TRY\n{sqlCommand}\nEND TRY\n" +
                    $"BEGIN CATCH\n  IF ERROR_NUMBER() <> 2714 THROW;\nEND CATCH";

                using var command = new SqlCommand(guardedSql, connection);
                command.ExecuteNonQuery();
            }
        }

        /// <inheritdoc/>
        public async Task CreateTablesAsync(CancellationToken cancellationToken = default)
        {
            await CreateDatabaseAsync(cancellationToken).ConfigureAwait(false);

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            foreach (var sqlCommand in SqlCache.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var guardedSql =
                    $"BEGIN TRY\n{sqlCommand}\nEND TRY\n" +
                    $"BEGIN CATCH\n  IF ERROR_NUMBER() <> 2714 THROW;\nEND CATCH";
                using var command = new SqlCommand(guardedSql, connection);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public void DropTables()
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            foreach (var type in SqlCache.Keys)
            {
                var sql = $"IF OBJECT_ID(N'{type.Name}', N'U') IS NOT NULL DROP TABLE [{type.Name}];";
                using var command = new SqlCommand(sql, connection);
                command.ExecuteNonQuery();
            }
        }

        /// <inheritdoc/>
        public async Task DropTablesAsync(CancellationToken cancellationToken = default)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            foreach (var type in SqlCache.Keys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sql = $"IF OBJECT_ID(N'{type.Name}', N'U') IS NOT NULL DROP TABLE [{type.Name}];";
                using var command = new SqlCommand(sql, connection);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        // ── ALTER TABLE ─────────────────────────────────────────────────────

        /// <inheritdoc/>
        public void AddColumn<T>(string columnName) where T : class, new()
        {
            var sql = BuildAddColumnSql<T>(columnName);
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        /// <inheritdoc/>
        public async Task AddColumnAsync<T>(string columnName, CancellationToken cancellationToken = default) where T : class, new()
        {
            var sql = BuildAddColumnSql<T>(columnName);
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var command = new SqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public void DropColumn<T>(string columnName) where T : class, new()
        {
            var sql = BuildDropColumnSql<T>(columnName);
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        /// <inheritdoc/>
        public async Task DropColumnAsync<T>(string columnName, CancellationToken cancellationToken = default) where T : class, new()
        {
            var sql = BuildDropColumnSql<T>(columnName);
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var command = new SqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// SQL Server uses <c>sp_rename</c> to rename a column.
        /// </summary>
        protected override string BuildRenameColumnSql<T>(string oldColumnName, string newColumnName)
        {
            var propertyManager = new Core.Helpers.DynamicPropertyManager<T>();
            var tableNameAttr = propertyManager.GetClassAttribute<Core.DbTableNameAttribute>();
            var tableName = tableNameAttr?.TableName ?? typeof(T).Name;
            return $"EXEC sp_rename N'{tableName}.{oldColumnName}', N'{newColumnName}', N'COLUMN';";
        }

        /// <inheritdoc/>
        public void RenameColumn<T>(string oldColumnName, string newColumnName) where T : class, new()
        {
            var sql = BuildRenameColumnSql<T>(oldColumnName, newColumnName);
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        /// <inheritdoc/>
        public async Task RenameColumnAsync<T>(string oldColumnName, string newColumnName, CancellationToken cancellationToken = default) where T : class, new()
        {
            var sql = BuildRenameColumnSql<T>(oldColumnName, newColumnName);
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var command = new SqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// SQL Server uses <c>ALTER TABLE ... ALTER COLUMN col TYPE</c>.
        /// </summary>
        protected override string BuildAlterColumnTypeSql<T>(string columnName)
        {
            var propertyManager = new Core.Helpers.DynamicPropertyManager<T>();
            var tableNameAttr = propertyManager.GetClassAttribute<Core.DbTableNameAttribute>();
            var tableName = tableNameAttr?.TableName ?? typeof(T).Name;
            var columnTypeAttr = propertyManager.GetAttribute<Core.DbColumnTypeAttribute>(columnName);
            if (columnTypeAttr == null)
                throw new InvalidOperationException($"Column '{columnName}' has no type attribute on {typeof(T).Name}.");
            return $"ALTER TABLE [{tableName}] ALTER COLUMN [{columnName}] {columnTypeAttr.GetColumnType()};";
        }

        /// <inheritdoc/>
        public void AlterColumnType<T>(string columnName) where T : class, new()
        {
            var sql = BuildAlterColumnTypeSql<T>(columnName);
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        /// <inheritdoc/>
        public async Task AlterColumnTypeAsync<T>(string columnName, CancellationToken cancellationToken = default) where T : class, new()
        {
            var sql = BuildAlterColumnTypeSql<T>(columnName);
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var command = new SqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}

