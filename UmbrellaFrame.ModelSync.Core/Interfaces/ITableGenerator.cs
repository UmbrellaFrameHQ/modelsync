using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UmbrellaFrame.ModelSync.Core.Interfaces
{
    /// <summary>
    /// Contract for provider-specific table generators.
    /// Register as a scoped/transient service in your DI container.
    /// </summary>
    public interface ITableGenerator
    {
        // ── SQL generation ──────────────────────────────────────────────────

        /// <summary>Generates a CREATE TABLE SQL statement and stores it in the internal cache.</summary>
        /// <param name="ifNotExists">Emit IF NOT EXISTS guard.</param>
        string GenerateSqlTable<T>(bool ifNotExists = false) where T : class, new();

        /// <summary>Async version of <see cref="GenerateSqlTable{T}"/>.</summary>
        Task<string> GenerateSqlTableAsync<T>(bool ifNotExists = false, CancellationToken cancellationToken = default)
            where T : class, new();

        /// <summary>Generates a DROP TABLE SQL statement.</summary>
        string GenerateDropTableSql<T>() where T : class, new();

        /// <summary>Generates a TRUNCATE TABLE SQL statement.</summary>
        string GenerateTruncateTableSql<T>() where T : class, new();

        /// <summary>Generates CREATE INDEX statements for all indexed properties.</summary>
        List<string> GenerateIndexSql<T>() where T : class, new();

        // ── DDL execution ───────────────────────────────────────────────────

        /// <summary>
        /// Creates the target database if it does not already exist.
        /// For file-based providers (e.g. SQLite) this is a no-op.
        /// </summary>
        void CreateDatabase();

        /// <summary>Async version of <see cref="CreateDatabase"/>.</summary>
        Task CreateDatabaseAsync(CancellationToken cancellationToken = default);

        /// <summary>Executes all cached CREATE TABLE statements against the target database.</summary>
        void CreateTables();

        /// <summary>Async version of <see cref="CreateTables"/>.</summary>
        Task CreateTablesAsync(CancellationToken cancellationToken = default);

        /// <summary>Drops all tables whose SQL is cached in this generator.</summary>
        void DropTables();

        /// <summary>Async version of <see cref="DropTables"/>.</summary>
        Task DropTablesAsync(CancellationToken cancellationToken = default);

        // ── ALTER TABLE ─────────────────────────────────────────────────────

        /// <summary>
        /// Adds a new column to the table mapped from <typeparamref name="T"/>.
        /// Column definition is read from the property's attributes.
        /// </summary>
        void AddColumn<T>(string columnName) where T : class, new();

        /// <summary>Async version of <see cref="AddColumn{T}"/>.</summary>
        Task AddColumnAsync<T>(string columnName, CancellationToken cancellationToken = default) where T : class, new();

        /// <summary>
        /// Drops an existing column from the table mapped from <typeparamref name="T"/>.
        /// </summary>
        void DropColumn<T>(string columnName) where T : class, new();

        /// <summary>Async version of <see cref="DropColumn{T}"/>.</summary>
        Task DropColumnAsync<T>(string columnName, CancellationToken cancellationToken = default) where T : class, new();

        /// <summary>
        /// Renames an existing column in the table mapped from <typeparamref name="T"/>.
        /// </summary>
        void RenameColumn<T>(string oldColumnName, string newColumnName) where T : class, new();

        /// <summary>Async version of <see cref="RenameColumn{T}"/>.</summary>
        Task RenameColumnAsync<T>(string oldColumnName, string newColumnName, CancellationToken cancellationToken = default) where T : class, new();

        /// <summary>
        /// Alters the type of an existing column in the table mapped from <typeparamref name="T"/>.
        /// New type definition is read from the property's attributes.
        /// </summary>
        void AlterColumnType<T>(string columnName) where T : class, new();

        /// <summary>Async version of <see cref="AlterColumnType{T}"/>.</summary>
        Task AlterColumnTypeAsync<T>(string columnName, CancellationToken cancellationToken = default) where T : class, new();
    }
}
