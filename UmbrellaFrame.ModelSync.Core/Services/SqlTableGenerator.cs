using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.Helpers;
using UmbrellaFrame.ModelSync.Core.Resources;

namespace UmbrellaFrame.ModelSync.Core.Services
{
    /// <summary>
    /// Abstract base class that generates provider-specific CREATE TABLE SQL from model attributes.
    /// </summary>
    public abstract class SqlTableGenerator
    {
        /// <summary>Per-instance SQL cache — thread-safe, no cross-provider pollution.</summary>
        protected readonly ConcurrentDictionary<Type, string> SqlCache =
            new ConcurrentDictionary<Type, string>();

        private readonly ILogger _logger;

        /// <summary>
        /// Returns the provider-specific identifier quote character(s).
        /// <para>MySQL → <c>`</c>, SQL Server → <c>[</c>/<c>]</c>, PostgreSQL/SQLite → <c>"</c></para>
        /// </summary>
        protected abstract string QuoteIdentifier(string identifier);

        /// <summary>
        /// Returns the provider-specific <c>IF NOT EXISTS</c> clause used in CREATE TABLE.
        /// Override and return <c>string.Empty</c> for providers that do not support it inline.
        /// </summary>
        protected virtual string IfNotExistsClause => "IF NOT EXISTS";

        protected SqlTableGenerator(ILogger? logger = null)
        {
            _logger = logger ?? NullLogger.Instance;
        }

        // ------------------------------------------------------------------ sync

        /// <summary>
        /// Generates a CREATE TABLE SQL statement for the given model type and stores it in the cache.
        /// </summary>
        /// <typeparam name="T">Model class decorated with table/column attributes.</typeparam>
        /// <param name="ifNotExists">When <c>true</c> emits <c>CREATE TABLE IF NOT EXISTS</c>.</param>
        /// <returns>The generated SQL string.</returns>
        public string GenerateSqlTable<T>(bool ifNotExists = false) where T : class, new()
        {
            var type = typeof(T);
            var sql = BuildSql<T>(ifNotExists);
            SqlCache[type] = sql;
            _logger.LogDebug(CoreResources.Get("TableGen_SqlGenerated", type.Name, sql));
            return sql;
        }

        /// <summary>
        /// Generates a DROP TABLE SQL statement for the given model type.
        /// </summary>
        /// <typeparam name="T">Model class decorated with table/column attributes.</typeparam>
        /// <returns>The generated DROP TABLE SQL string.</returns>
        public string GenerateDropTableSql<T>() where T : class, new()
        {
            var propertyManager = new DynamicPropertyManager<T>();
            var tableNameAttr = propertyManager.GetClassAttribute<DbTableNameAttribute>();
            var tableName = tableNameAttr?.TableName ?? typeof(T).Name;
            return $"DROP TABLE IF EXISTS {QuoteIdentifier(tableName)};";
        }

        /// <summary>
        /// Generates a TRUNCATE TABLE SQL statement for the given model type.
        /// </summary>
        /// <typeparam name="T">Model class decorated with table/column attributes.</typeparam>
        /// <returns>The generated TRUNCATE TABLE SQL string.</returns>
        public virtual string GenerateTruncateTableSql<T>() where T : class, new()
        {
            var propertyManager = new DynamicPropertyManager<T>();
            var tableNameAttr = propertyManager.GetClassAttribute<DbTableNameAttribute>();
            var tableName = tableNameAttr?.TableName ?? typeof(T).Name;
            return $"TRUNCATE TABLE {QuoteIdentifier(tableName)};";
        }

        // ------------------------------------------------------------------ async

        /// <summary>Async version of <see cref="GenerateSqlTable{T}"/>.</summary>
        public Task<string> GenerateSqlTableAsync<T>(bool ifNotExists = false, CancellationToken cancellationToken = default)
            where T : class, new()
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(GenerateSqlTable<T>(ifNotExists));
        }

        // ------------------------------------------------------------------ core builder

        private string BuildSql<T>(bool ifNotExists) where T : class, new()
        {
            var type = typeof(T);
            var propertyManager = new DynamicPropertyManager<T>();

            // preserve declaration order
            var properties = propertyManager.GetAllPropertiesOrdered();

            var tableNameAttr = propertyManager.GetClassAttribute<DbTableNameAttribute>();
            if (tableNameAttr == null)
                _logger.LogWarning(CoreResources.Get("TableGen_TableNameAttrNotFound", type.Name));
            else
                _logger.LogDebug(CoreResources.Get("TableGen_TableNameAttrFound", tableNameAttr.TableName));

            var tableName = tableNameAttr?.TableName ?? type.Name;

            var ifNotExistsSql = ifNotExists ? $" {IfNotExistsClause}" : string.Empty;
            var createTableCommand = new StringBuilder();
            createTableCommand.AppendLine($"CREATE TABLE{ifNotExistsSql} {QuoteIdentifier(tableName)} (");

            var foreignKeyConstraints = new StringBuilder();
            var primaryKeyColumns = new List<string>();

            var columnLines = new List<string>();

            foreach (var prop in properties)
            {
                var columnDef = new StringBuilder();
                var columnName = prop.Key;

                var columnTypeAttr = propertyManager.GetAttribute<DbColumnTypeAttribute>(columnName);
                if (columnTypeAttr == null)
                    throw new InvalidOperationException(CoreResources.Get("TableGen_MissingColumnTypeAttr", columnName, type.Name));

                columnDef.Append($"{QuoteIdentifier(columnName)} {columnTypeAttr.GetColumnType()}");

                var attributes = propertyManager.GetAttributes(columnName);

                var defaultAttr = attributes.OfType<DbColumnDefaultAttribute>().FirstOrDefault();
                var checkAttr = attributes.OfType<DbColumnCheckAttribute>().FirstOrDefault();

                foreach (var attr in attributes)
                {
                    switch (attr)
                    {
                        case DbColumnPrimaryKeyAttribute primaryKeyAttr:
                            primaryKeyColumns.Add(columnName);
                            columnDef.Append($" {primaryKeyAttr.GetSqlSnippet()}");
                            break;
                        case DbColumnNotNullAttribute notNullAttr:
                            columnDef.Append($" {notNullAttr.GetSqlSnippet()}");
                            break;
                        case DbColumnUniqueAttribute uniqueAttr:
                            columnDef.Append($" {uniqueAttr.GetSqlSnippet()}");
                            break;
                        case DbColumnForeignKeyAttribute foreignKeyAttr:
                            foreignKeyConstraints.AppendLine($"    {foreignKeyAttr.GetSqlSnippet()}");
                            break;
                        case DbColumnDefaultAttribute defaultAttribute:
                            columnDef.Append($" DEFAULT {defaultAttribute.DefaultValue}");
                            break;
                        case DbColumnCheckAttribute checkAttribute:
                            columnDef.Append($" CHECK ({checkAttribute.Expression})");
                            break;
                        case DbColumnIndexAttribute _:
                            // Index constraints are generated separately via GenerateIndexSql
                            break;
                    }
                }

                columnLines.Add($"    {columnDef}");
            }

            createTableCommand.Append(string.Join($",{Environment.NewLine}", columnLines));

            if (foreignKeyConstraints.Length > 0)
            {
                createTableCommand.AppendLine(",");
                createTableCommand.Append(foreignKeyConstraints.ToString().TrimEnd());
            }

            createTableCommand.AppendLine();
            createTableCommand.AppendLine(");");

            return createTableCommand.ToString();
        }

        /// <summary>
        /// Generates CREATE INDEX statements for all properties decorated with <see cref="DbColumnIndexAttribute"/>.
        /// </summary>
        /// <typeparam name="T">Model class.</typeparam>
        /// <returns>A list of CREATE INDEX SQL strings.</returns>
        public List<string> GenerateIndexSql<T>() where T : class, new()
        {
            var propertyManager = new DynamicPropertyManager<T>();
            var tableNameAttr = propertyManager.GetClassAttribute<DbTableNameAttribute>();
            var tableName = tableNameAttr?.TableName ?? typeof(T).Name;
            var results = new List<string>();

            foreach (var prop in propertyManager.GetAllPropertiesOrdered())
            {
                var indexAttr = propertyManager.GetAttribute<DbColumnIndexAttribute>(prop.Key);
                if (indexAttr == null) continue;

                var indexName = string.IsNullOrEmpty(indexAttr.IndexName)
                    ? $"idx_{tableName}_{prop.Key}"
                    : indexAttr.IndexName;

                var unique = indexAttr.IsUnique ? "UNIQUE " : string.Empty;
                results.Add($"CREATE {unique}INDEX {QuoteIdentifier(indexName)} ON {QuoteIdentifier(tableName)} ({QuoteIdentifier(prop.Key)});");
            }

            return results;
        }

        // ── ALTER TABLE helpers (provider-agnostic SQL builders) ────────────

        /// <summary>
        /// Builds an ADD COLUMN SQL fragment for the given column on model <typeparamref name="T"/>.
        /// </summary>
        protected string BuildAddColumnSql<T>(string columnName) where T : class, new()
        {
            var propertyManager = new DynamicPropertyManager<T>();
            var tableName = GetTableName<T>(propertyManager);

            var columnTypeAttr = propertyManager.GetAttribute<DbColumnTypeAttribute>(columnName);
            if (columnTypeAttr == null)
                throw new InvalidOperationException(CoreResources.Get("TableGen_MissingColumnTypeAttr", columnName, typeof(T).Name));

            var colDef = BuildColumnDefinition(propertyManager, columnName, columnTypeAttr);
            return $"ALTER TABLE {QuoteIdentifier(tableName)} ADD {colDef};";
        }

        /// <summary>
        /// Builds a DROP COLUMN SQL fragment for the given column on model <typeparamref name="T"/>.
        /// </summary>
        protected string BuildDropColumnSql<T>(string columnName) where T : class, new()
        {
            var propertyManager = new DynamicPropertyManager<T>();
            var tableName = GetTableName<T>(propertyManager);
            return $"ALTER TABLE {QuoteIdentifier(tableName)} DROP COLUMN {QuoteIdentifier(columnName)};";
        }

        /// <summary>
        /// Builds a RENAME COLUMN SQL fragment. Providers that use a different syntax
        /// (e.g. SQL Server sp_rename) should override <see cref="BuildRenameColumnSql{T}"/>.
        /// </summary>
        protected virtual string BuildRenameColumnSql<T>(string oldColumnName, string newColumnName) where T : class, new()
        {
            var propertyManager = new DynamicPropertyManager<T>();
            var tableName = GetTableName<T>(propertyManager);
            // Standard SQL:2003 — supported by PostgreSQL 10+, MySQL 8+, SQLite 3.25+
            return $"ALTER TABLE {QuoteIdentifier(tableName)} RENAME COLUMN {QuoteIdentifier(oldColumnName)} TO {QuoteIdentifier(newColumnName)};";
        }

        /// <summary>
        /// Builds an ALTER COLUMN TYPE SQL fragment for the given column on model <typeparamref name="T"/>.
        /// Providers that use a different syntax (e.g. SQL Server ALTER COLUMN) should override this.
        /// </summary>
        protected virtual string BuildAlterColumnTypeSql<T>(string columnName) where T : class, new()
        {
            var propertyManager = new DynamicPropertyManager<T>();
            var tableName = GetTableName<T>(propertyManager);

            var columnTypeAttr = propertyManager.GetAttribute<DbColumnTypeAttribute>(columnName);
            if (columnTypeAttr == null)
                throw new InvalidOperationException(CoreResources.Get("TableGen_MissingColumnTypeAttr", columnName, typeof(T).Name));

            // Standard SQL — MySQL uses MODIFY COLUMN, SQL Server uses ALTER COLUMN
            return $"ALTER TABLE {QuoteIdentifier(tableName)} ALTER COLUMN {QuoteIdentifier(columnName)} {columnTypeAttr.GetColumnType()};";
        }

        // ── shared helpers ──────────────────────────────────────────────────

        private string GetTableName<T>(DynamicPropertyManager<T> propertyManager) where T : class, new()
        {
            var tableNameAttr = propertyManager.GetClassAttribute<DbTableNameAttribute>();
            return tableNameAttr?.TableName ?? typeof(T).Name;
        }

        private string BuildColumnDefinition<T>(DynamicPropertyManager<T> propertyManager, string columnName, DbColumnTypeAttribute columnTypeAttr) where T : class, new()
        {
            var colDef = new System.Text.StringBuilder();
            colDef.Append($"{QuoteIdentifier(columnName)} {columnTypeAttr.GetColumnType()}");

            var attributes = propertyManager.GetAttributes(columnName);
            foreach (var attr in attributes)
            {
                switch (attr)
                {
                    case DbColumnNotNullAttribute notNull:
                        colDef.Append($" {notNull.GetSqlSnippet()}");
                        break;
                    case DbColumnUniqueAttribute unique:
                        colDef.Append($" {unique.GetSqlSnippet()}");
                        break;
                    case DbColumnDefaultAttribute def:
                        colDef.Append($" DEFAULT {def.DefaultValue}");
                        break;
                    case DbColumnCheckAttribute check:
                        colDef.Append($" CHECK ({check.Expression})");
                        break;
                }
            }

            return colDef.ToString();
        }
    }
}
