using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.Services;

namespace UmbrellaFrame.ModelSync.SqlServer
{
    /// <summary>Runs ordered SQL Server migration scripts with history tracking.</summary>
    public sealed class SqlServerMigrationRunner : SqlMigrationRunnerBase
    {
        private static readonly Regex SafeIdentifierPattern = new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
        private readonly string _connectionString;

        public SqlServerMigrationRunner(
            string connectionString,
            MigrationRunnerOptions options = null,
            ILogger<SqlServerMigrationRunner> logger = null)
            : base(ConfigureDefaults(options), logger ?? NullLogger<SqlServerMigrationRunner>.Instance)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));
            _connectionString = connectionString;
        }

        protected override IReadOnlyList<string> SplitBatches(string sql)
            => SqlBatchSplitter.SplitSqlServerGoBatches(sql);

        protected override async Task ResetDatabaseAsync(CancellationToken cancellationToken)
        {
            var builder = new SqlConnectionStringBuilder(_connectionString);
            var targetDb = builder.InitialCatalog;
            if (string.IsNullOrWhiteSpace(targetDb))
                throw new InvalidOperationException("SQL Server reset requires Initial Catalog in the connection string.");

            ValidateIdentifier(targetDb, nameof(targetDb));
            builder.InitialCatalog = "master";

            using (var connection = new SqlConnection(builder.ConnectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                var sql = $@"
IF EXISTS (SELECT 1 FROM sys.databases WHERE name = N'{EscapeLiteral(targetDb)}')
BEGIN
    ALTER DATABASE [{EscapeIdentifier(targetDb)}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{EscapeIdentifier(targetDb)}];
END
CREATE DATABASE [{EscapeIdentifier(targetDb)}];";
                using (var command = new SqlCommand(sql, connection))
                {
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        protected override async Task EnsureSchemasAsync(IEnumerable<string> schemas, CancellationToken cancellationToken)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                foreach (var schema in schemas.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    ValidateIdentifier(schema, nameof(schema));
                    var sql = $"IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'{EscapeLiteral(schema)}') EXEC('CREATE SCHEMA [{EscapeIdentifier(schema)}] AUTHORIZATION dbo;');";
                    using (var command = new SqlCommand(sql, connection))
                    {
                        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        protected override async Task EnsureHistoryTablesAsync(CancellationToken cancellationToken)
        {
            var schema = HistorySchema();
            var escapedSchema = EscapeIdentifier(schema);
            var literalSchema = EscapeLiteral(schema);
            var sql = $@"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'{literalSchema}') EXEC('CREATE SCHEMA [{escapedSchema}] AUTHORIZATION dbo;');

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SchemaMigration_Tables' AND schema_id = SCHEMA_ID('{literalSchema}'))
CREATE TABLE [{escapedSchema}].[SchemaMigration_Tables](
    [Id] NVARCHAR(128) NOT NULL PRIMARY KEY,
    [Name] NVARCHAR(256) NOT NULL,
    [SqlHash] NVARCHAR(128) NULL,
    [AppliedAt] DATETIME2 NOT NULL CONSTRAINT DF_ModelSync_Tables_AppliedAt DEFAULT SYSUTCDATETIME(),
    [UpdateAt] DATETIME2 NULL
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SchemaMigration_StoredProcedures' AND schema_id = SCHEMA_ID('{literalSchema}'))
CREATE TABLE [{escapedSchema}].[SchemaMigration_StoredProcedures](
    [Id] NVARCHAR(128) NOT NULL PRIMARY KEY,
    [Name] NVARCHAR(256) NOT NULL,
    [SqlHash] NVARCHAR(128) NULL,
    [AppliedAt] DATETIME2 NOT NULL CONSTRAINT DF_ModelSync_StoredProcedures_AppliedAt DEFAULT SYSUTCDATETIME(),
    [UpdateAt] DATETIME2 NULL
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SchemaMigration_Triggers' AND schema_id = SCHEMA_ID('{literalSchema}'))
CREATE TABLE [{escapedSchema}].[SchemaMigration_Triggers](
    [Id] NVARCHAR(128) NOT NULL PRIMARY KEY,
    [Name] NVARCHAR(256) NOT NULL,
    [SqlHash] NVARCHAR(128) NULL,
    [AppliedAt] DATETIME2 NOT NULL CONSTRAINT DF_ModelSync_Triggers_AppliedAt DEFAULT SYSUTCDATETIME(),
    [UpdateAt] DATETIME2 NULL
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SchemaMigration_Seeds' AND schema_id = SCHEMA_ID('{literalSchema}'))
CREATE TABLE [{escapedSchema}].[SchemaMigration_Seeds](
    [Id] NVARCHAR(128) NOT NULL PRIMARY KEY,
    [Name] NVARCHAR(256) NOT NULL,
    [SqlHash] NVARCHAR(128) NULL,
    [AppliedAt] DATETIME2 NOT NULL CONSTRAINT DF_ModelSync_Seeds_AppliedAt DEFAULT SYSUTCDATETIME(),
    [UpdateAt] DATETIME2 NULL
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SchemaMigration_CustomSql' AND schema_id = SCHEMA_ID('{literalSchema}'))
CREATE TABLE [{escapedSchema}].[SchemaMigration_CustomSql](
    [Id] NVARCHAR(128) NOT NULL PRIMARY KEY,
    [Name] NVARCHAR(256) NOT NULL,
    [SqlHash] NVARCHAR(128) NULL,
    [AppliedAt] DATETIME2 NOT NULL CONSTRAINT DF_ModelSync_CustomSql_AppliedAt DEFAULT SYSUTCDATETIME(),
    [UpdateAt] DATETIME2 NULL
);";

            await ExecuteSqlAsync(sql, cancellationToken).ConfigureAwait(false);
        }

        protected override async Task<IDictionary<string, string>> ReadHistoryAsync(CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                foreach (MigrationScriptCategory category in Enum.GetValues(typeof(MigrationScriptCategory)))
                {
                    var table = HistoryTable(category);
                    using (var command = new SqlCommand($"SELECT [Id], [SqlHash] FROM [{EscapeIdentifier(HistorySchema())}].[{table}]", connection))
                    using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            var id = reader.GetString(0);
                            var hash = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                            result[CreateHistoryKey(category, id)] = hash;
                        }
                    }
                }
            }

            return result;
        }

        protected override async Task ExecuteSqlAsync(string sql, CancellationToken cancellationToken)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = new SqlCommand(sql, connection))
                {
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        protected override async Task RecordHistoryAsync(MigrationScriptDefinition definition, string hash, CancellationToken cancellationToken)
        {
            var table = HistoryTable(definition.Category);
            var schema = EscapeIdentifier(HistorySchema());
            var sql = $@"
MERGE [{schema}].[{table}] AS target
USING (SELECT @Id AS Id, @Name AS Name, @SqlHash AS SqlHash) AS source
ON target.Id = source.Id
WHEN MATCHED THEN UPDATE SET [Name] = source.Name, [SqlHash] = source.SqlHash, [UpdateAt] = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT ([Id], [Name], [SqlHash]) VALUES (source.Id, source.Name, source.SqlHash);";

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@Id", definition.Id);
                    command.Parameters.AddWithValue("@Name", definition.Name);
                    command.Parameters.AddWithValue("@SqlHash", hash);
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        protected override async Task<IReadOnlyList<string>> BuildMissingColumnScriptsAsync(MigrationScriptDefinition definition, CancellationToken cancellationToken)
        {
            var columns = TableScriptColumnParser.Parse(definition.Sql, "dbo");
            var result = new List<string>();
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                foreach (var column in columns)
                {
                    ValidateIdentifier(column.Schema, nameof(column.Schema));
                    ValidateIdentifier(column.Table, nameof(column.Table));
                    ValidateIdentifier(column.Column, nameof(column.Column));

                    using (var command = new SqlCommand("SELECT COL_LENGTH(@ObjectName, @ColumnName)", connection))
                    {
                        command.Parameters.AddWithValue("@ObjectName", $"{column.Schema}.{column.Table}");
                        command.Parameters.AddWithValue("@ColumnName", column.Column);
                        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                        var exists = value != null && value != DBNull.Value;
                        if (!exists)
                        {
                            result.Add($"ALTER TABLE [{EscapeIdentifier(column.Schema)}].[{EscapeIdentifier(column.Table)}] ADD [{EscapeIdentifier(column.Column)}] {column.Definition};");
                        }
                    }
                }
            }

            return result;
        }

        private static MigrationRunnerOptions ConfigureDefaults(MigrationRunnerOptions options)
        {
            var configured = options ?? MigrationRunnerOptions.Default();
            if (string.IsNullOrWhiteSpace(configured.HistorySchema))
                configured.HistorySchema = "sec";
            ValidateIdentifier(configured.HistorySchema, nameof(configured.HistorySchema));
            if (configured.Schemas.Count == 0)
            {
                foreach (var schema in new[] { "app", "ref", "sec", "auth", "log", "crm", "exp", "veh", "fin" })
                    configured.Schemas.Add(schema);
            }
            if (!configured.Schemas.Contains(configured.HistorySchema, StringComparer.OrdinalIgnoreCase))
                configured.Schemas.Add(configured.HistorySchema);
            return configured;
        }

        protected override async Task ApplyPlanAsync(MigrationSyncPlan plan, CancellationToken cancellationToken)
        {
            if (plan.Definition.Category != MigrationScriptCategory.StoredProcedures)
            {
                await base.ApplyPlanAsync(plan, cancellationToken).ConfigureAwait(false);
                return;
            }

            var sql = SqlServerStoredProcedureSynchronizer.ToCreateOrAlterSql(plan.SqlToApply);
            foreach (var batch in SplitBatches(sql))
            {
                if (!string.IsNullOrWhiteSpace(batch))
                    await ExecuteSqlAsync(batch, cancellationToken).ConfigureAwait(false);
            }

            await RecordHistoryAsync(plan.Definition, plan.TargetHash, cancellationToken).ConfigureAwait(false);
        }

        private string HistorySchema()
        {
            var schema = string.IsNullOrWhiteSpace(Options.HistorySchema) ? "sec" : Options.HistorySchema;
            ValidateIdentifier(schema, nameof(Options.HistorySchema));
            return schema;
        }

        private static string HistoryTable(MigrationScriptCategory category)
        {
            switch (category)
            {
                case MigrationScriptCategory.StoredProcedures: return "SchemaMigration_StoredProcedures";
                case MigrationScriptCategory.Triggers: return "SchemaMigration_Triggers";
                case MigrationScriptCategory.Seeds: return "SchemaMigration_Seeds";
                case MigrationScriptCategory.CustomSql: return "SchemaMigration_CustomSql";
                default: return "SchemaMigration_Tables";
            }
        }

        private static void ValidateIdentifier(string identifier, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(identifier) || !SafeIdentifierPattern.IsMatch(identifier))
                throw new ArgumentException($"Invalid SQL identifier '{identifier}'.", parameterName);
        }

        private static string EscapeIdentifier(string identifier)
            => identifier.Replace("]", "]]");

        private static string EscapeLiteral(string value)
            => value.Replace("'", "''");
    }
}
