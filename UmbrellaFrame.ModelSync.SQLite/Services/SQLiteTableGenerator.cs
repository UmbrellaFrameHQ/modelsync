using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

using UmbrellaFrame.ModelSync.Core.Interfaces;
using UmbrellaFrame.ModelSync.Core.Services;
using UmbrellaFrame.ModelSync.SQLite.Resources;

namespace UmbrellaFrame.ModelSync.SQLite
{
    /// <summary>
    /// SQLite implementation of <see cref="ITableGenerator"/>.
    /// Generates and executes CREATE TABLE statements using Microsoft.Data.Sqlite.
    /// </summary>
    public class SQLiteTableGenerator : SqlTableGenerator, ITableGenerator
    {
        private readonly string _connectionString;

        /// <inheritdoc/>
        protected override string QuoteIdentifier(string identifier) => $"\"{identifier}\"";

        /// <inheritdoc/>
        protected override string IfNotExistsClause => "IF NOT EXISTS";

        /// <param name="connectionString">A valid SQLite connection string.</param>
        /// <param name="logger">Optional logger instance.</param>
        public SQLiteTableGenerator(string connectionString, ILogger<SQLiteTableGenerator> logger = null)
            : base(logger)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentException(SQLiteResources.Get("InvalidConnectionString"), nameof(connectionString));

            _connectionString = connectionString;
        }

        /// <summary>Generates the CREATE TABLE SQL for the given model and caches it.</summary>
        public string GenerateSQLiteTable<T>(bool ifNotExists = false) where T : class, new()
            => GenerateSqlTable<T>(ifNotExists);

        // ── DDL execution ───────────────────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks>SQLite automatically creates the database file on first connection. This is a no-op.</remarks>
        public void CreateDatabase() { }

        /// <inheritdoc/>
        /// <remarks>SQLite automatically creates the database file on first connection. This is a no-op.</remarks>
        public Task CreateDatabaseAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        /// <inheritdoc/>
        public void CreateTables()
        {
            foreach (var sqlCommand in SqlCache.Values)
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                using var command = new SqliteCommand(sqlCommand, connection);
                command.ExecuteNonQuery();
            }
        }

        /// <inheritdoc/>
        public async Task CreateTablesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var sqlCommand in SqlCache.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using var command = new SqliteCommand(sqlCommand, connection);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public void DropTables()
        {
            foreach (var type in SqlCache.Keys)
            {
                var sql = $"DROP TABLE IF EXISTS \"{type.Name}\";";
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                using var command = new SqliteCommand(sql, connection);
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
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using var command = new SqliteCommand(sql, connection);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        // ── ALTER TABLE ─────────────────────────────────────────────────────

        /// <inheritdoc/>
        public void AddColumn<T>(string columnName) where T : class, new()
        {
            var sql = BuildAddColumnSql<T>(columnName);
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = new SqliteCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        /// <inheritdoc/>
        public async Task AddColumnAsync<T>(string columnName, CancellationToken cancellationToken = default) where T : class, new()
        {
            var sql = BuildAddColumnSql<T>(columnName);
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var command = new SqliteCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public void DropColumn<T>(string columnName) where T : class, new()
        {
            var sql = BuildDropColumnSql<T>(columnName);
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = new SqliteCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        /// <inheritdoc/>
        public async Task DropColumnAsync<T>(string columnName, CancellationToken cancellationToken = default) where T : class, new()
        {
            var sql = BuildDropColumnSql<T>(columnName);
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var command = new SqliteCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public void RenameColumn<T>(string oldColumnName, string newColumnName) where T : class, new()
        {
            var sql = BuildRenameColumnSql<T>(oldColumnName, newColumnName);
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = new SqliteCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        /// <inheritdoc/>
        public async Task RenameColumnAsync<T>(string oldColumnName, string newColumnName, CancellationToken cancellationToken = default) where T : class, new()
        {
            var sql = BuildRenameColumnSql<T>(oldColumnName, newColumnName);
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var command = new SqliteCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public void AlterColumnType<T>(string columnName) where T : class, new()
        {
            // SQLite does not support ALTER COLUMN TYPE natively.
            // A full table rebuild is required; this throws to make the limitation explicit.
            throw new NotSupportedException("SQLite does not support altering a column type directly. Recreate the table instead.");
        }

        /// <inheritdoc/>
        public Task AlterColumnTypeAsync<T>(string columnName, CancellationToken cancellationToken = default) where T : class, new()
        {
            throw new NotSupportedException("SQLite does not support altering a column type directly. Recreate the table instead.");
        }
    }
}

