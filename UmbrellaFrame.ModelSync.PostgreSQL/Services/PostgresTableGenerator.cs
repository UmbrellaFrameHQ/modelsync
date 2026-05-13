using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Npgsql;

using UmbrellaFrame.ModelSync.Core.Interfaces;
using UmbrellaFrame.ModelSync.Core.Services;
using UmbrellaFrame.ModelSync.PostgreSQL.Resources;

namespace UmbrellaFrame.ModelSync.PostgreSQL
{
    /// <summary>
    /// PostgreSQL implementation of <see cref="ITableGenerator"/>.
    /// Generates and executes CREATE TABLE statements using Npgsql.
    /// </summary>
    public class PostgresTableGenerator : SqlTableGenerator, ITableGenerator
    {
        private readonly string _connectionString;

        /// <inheritdoc/>
        protected override string QuoteIdentifier(string identifier) => $"\"{identifier}\"";

        /// <inheritdoc/>
        protected override string IfNotExistsClause => "IF NOT EXISTS";

        /// <param name="connectionString">A valid PostgreSQL connection string.</param>
        /// <param name="logger">Optional logger instance.</param>
        public PostgresTableGenerator(string connectionString, ILogger<PostgresTableGenerator> logger = null)
            : base(logger)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentException(PostgresResources.Get("InvalidConnectionString"), nameof(connectionString));

            _connectionString = connectionString;
        }

        /// <summary>Generates the CREATE TABLE SQL for the given model and caches it.</summary>
        public string GeneratePostgresTable<T>(bool ifNotExists = false) where T : class, new()
            => GenerateSqlTable<T>(ifNotExists);

        // ── DDL execution ───────────────────────────────────────────────────

        /// <inheritdoc/>
        public void CreateDatabase()
        {
            var builder = new NpgsqlConnectionStringBuilder(_connectionString);
            string databaseName = builder.Database;

            if (string.IsNullOrEmpty(databaseName))
                return;

            builder.Database = "postgres";

            using var connection = new NpgsqlConnection(builder.ConnectionString);
            connection.Open();

            using var checkCmd = new NpgsqlCommand($"SELECT 1 FROM pg_database WHERE datname = '{databaseName}';", connection);
            var exists = checkCmd.ExecuteScalar() != null;
            if (!exists)
            {
                using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\";", connection);
                createCmd.ExecuteNonQuery();
            }
        }

        /// <inheritdoc/>
        public async Task CreateDatabaseAsync(CancellationToken cancellationToken = default)
        {
            var builder = new NpgsqlConnectionStringBuilder(_connectionString);
            string databaseName = builder.Database;

            if (string.IsNullOrEmpty(databaseName))
                return;

            builder.Database = "postgres";

            using var connection = new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var checkCmd = new NpgsqlCommand($"SELECT 1 FROM pg_database WHERE datname = '{databaseName}';", connection);
            var exists = await checkCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) != null;
            if (!exists)
            {
                using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\";", connection);
                await createCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public void CreateTables()
        {
            foreach (var sqlCommand in SqlCache.Values)
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();
                using var command = new NpgsqlCommand(sqlCommand, connection);
                command.ExecuteNonQuery();
            }
        }

        /// <inheritdoc/>
        public async Task CreateTablesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var sqlCommand in SqlCache.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using var command = new NpgsqlCommand(sqlCommand, connection);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public void DropTables()
        {
            foreach (var type in SqlCache.Keys)
            {
                var sql = $"DROP TABLE IF EXISTS \"{type.Name}\";";
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();
                using var command = new NpgsqlCommand(sql, connection);
                command.ExecuteNonQuery();
            }
        }

        /// <inheritdoc/>
        public async Task DropTablesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var type in SqlCache.Keys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sql = $"DROP TABLE IF EXISTS \"{type.Name}\";";
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using var command = new NpgsqlCommand(sql, connection);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        // ── ALTER TABLE ─────────────────────────────────────────────────────

        /// <summary>
        /// PostgreSQL requires the <c>TYPE</c> keyword:
        /// <c>ALTER TABLE "t" ALTER COLUMN "col" TYPE datatype;</c>
        /// </summary>
        protected override string BuildAlterColumnTypeSql<T>(string columnName)
        {
            var propertyManager = new Core.Helpers.DynamicPropertyManager<T>();
            var tableNameAttr = propertyManager.GetClassAttribute<Core.DbTableNameAttribute>();
            var tableName = tableNameAttr?.TableName ?? typeof(T).Name;
            var columnTypeAttr = propertyManager.GetAttribute<Core.DbColumnTypeAttribute>(columnName);
            if (columnTypeAttr == null)
                throw new InvalidOperationException($"Column '{columnName}' has no type attribute on {typeof(T).Name}.");
            return $"ALTER TABLE \"{tableName}\" ALTER COLUMN \"{columnName}\" TYPE {columnTypeAttr.GetColumnType()};";
        }

        /// <inheritdoc/>
        public void AddColumn<T>(string columnName) where T : class, new()
        {
            var sql = BuildAddColumnSql<T>(columnName);
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            using var command = new NpgsqlCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        /// <inheritdoc/>
        public async Task AddColumnAsync<T>(string columnName, CancellationToken cancellationToken = default) where T : class, new()
        {
            var sql = BuildAddColumnSql<T>(columnName);
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public void DropColumn<T>(string columnName) where T : class, new()
        {
            var sql = BuildDropColumnSql<T>(columnName);
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            using var command = new NpgsqlCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        /// <inheritdoc/>
        public async Task DropColumnAsync<T>(string columnName, CancellationToken cancellationToken = default) where T : class, new()
        {
            var sql = BuildDropColumnSql<T>(columnName);
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public void RenameColumn<T>(string oldColumnName, string newColumnName) where T : class, new()
        {
            var sql = BuildRenameColumnSql<T>(oldColumnName, newColumnName);
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            using var command = new NpgsqlCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        /// <inheritdoc/>
        public async Task RenameColumnAsync<T>(string oldColumnName, string newColumnName, CancellationToken cancellationToken = default) where T : class, new()
        {
            var sql = BuildRenameColumnSql<T>(oldColumnName, newColumnName);
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public void AlterColumnType<T>(string columnName) where T : class, new()
        {
            var sql = BuildAlterColumnTypeSql<T>(columnName);
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            using var command = new NpgsqlCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        /// <inheritdoc/>
        public async Task AlterColumnTypeAsync<T>(string columnName, CancellationToken cancellationToken = default) where T : class, new()
        {
            var sql = BuildAlterColumnTypeSql<T>(columnName);
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}

