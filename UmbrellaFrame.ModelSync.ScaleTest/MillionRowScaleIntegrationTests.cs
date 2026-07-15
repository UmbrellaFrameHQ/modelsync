using System;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
using NUnit.Framework;
using Oracle.ManagedDataAccess.Client;
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.Interfaces;
using UmbrellaFrame.ModelSync.MySql;
using UmbrellaFrame.ModelSync.Oracle;
using UmbrellaFrame.ModelSync.PostgreSQL;
using UmbrellaFrame.ModelSync.SQLite;
using UmbrellaFrame.ModelSync.SqlServer;

namespace UmbrellaFrame.ModelSync.ScaleTest;

[TestFixture]
[NonParallelizable]
[Category("Integration")]
[Category("Scale")]
public sealed class MillionRowScaleIntegrationTests
{
    private const int ExpectedRows = 1_000_000;
    private const string RunVariable = "MODELSYNC_RUN_SCALE_INTEGRATION";

    [Test]
    [Timeout(600_000)]
    public Task SqlServer_OneMillionRows_ShouldRemainSafeAndIdempotent()
    {
        RequireScaleTests();
        var connectionString = Environment.GetEnvironmentVariable("MODELSYNC_SQLSERVER_CONNECTION_STRING")
            ?? "Server=127.0.0.1,14333;Database=appdb;User Id=sa;Password=ModelSync_Pass123;Encrypt=False;TrustServerCertificate=True;";

        new SqlServerTableGenerator(connectionString).CreateDatabase();

        return RunScenarioAsync(new ScaleScenario(
            "SQL Server",
            () => new SqlConnection(connectionString),
            "IF OBJECT_ID(N'dbo.MS_SCALE_ROWS', N'U') IS NOT NULL DROP TABLE [dbo].[MS_SCALE_ROWS];",
            SqlServerSeedSql,
            "SELECT COUNT_BIG(*) FROM [dbo].[MS_SCALE_ROWS];",
            "UPDATE [dbo].[MS_SCALE_ROWS] SET [Tag] = N'migrated' WHERE [Id] = 1;",
            type => SqlServerModelSynchronizer.FromTypes(new SqlServerModelSyncOptions
            {
                ConnectionString = connectionString,
                DefaultSchema = "dbo",
                HistorySchema = "sec"
            }, type).CompareAsync(),
            typeof(SqlServerScaleBase),
            typeof(SqlServerScaleExpanded),
            typeof(SqlServerScaleRisky),
            () => new SqlServerMigrationRunner(connectionString)));
    }

    [TestCase("MySQL", "MODELSYNC_MYSQL_CONNECTION_STRING", "Server=127.0.0.1;Port=13306;Database=appdb;User ID=root;Password=ModelSync_Pass123;Allow User Variables=true;")]
    [TestCase("MariaDB", "MODELSYNC_MARIADB_CONNECTION_STRING", "Server=127.0.0.1;Port=13307;Database=appdb;User ID=root;Password=ModelSync_Pass123;Allow User Variables=true;")]
    [Timeout(600_000)]
    public Task MySqlFamily_OneMillionRows_ShouldRemainSafeAndIdempotent(
        string provider,
        string environmentVariable,
        string fallbackConnectionString)
    {
        RequireScaleTests();
        var connectionString = Environment.GetEnvironmentVariable(environmentVariable) ?? fallbackConnectionString;

        return RunScenarioAsync(new ScaleScenario(
            provider,
            () => new MySqlConnection(connectionString),
            "DROP TABLE IF EXISTS `MS_SCALE_ROWS`;",
            MySqlSeedSql,
            "SELECT COUNT(*) FROM `MS_SCALE_ROWS`;",
            "UPDATE `MS_SCALE_ROWS` SET `Tag` = 'migrated' WHERE `Id` = 1;",
            type => MySqlModelSynchronizer.FromTypes(new MySqlModelSyncOptions
            {
                ConnectionString = connectionString
            }, type).CompareAsync(),
            typeof(MySqlScaleBase),
            typeof(MySqlScaleExpanded),
            typeof(MySqlScaleRisky),
            () => new MySqlMigrationRunner(connectionString)));
    }

    [Test]
    [Timeout(600_000)]
    public Task PostgreSql_OneMillionRows_ShouldRemainSafeAndIdempotent()
    {
        RequireScaleTests();
        var connectionString = Environment.GetEnvironmentVariable("MODELSYNC_POSTGRES_CONNECTION_STRING")
            ?? "Host=127.0.0.1;Port=15432;Database=modelsync_integration;Username=modelsync_test;Password=ModelSync_Pass123;";

        return RunScenarioAsync(new ScaleScenario(
            "PostgreSQL",
            () => new NpgsqlConnection(connectionString),
            "DROP TABLE IF EXISTS \"public\".\"MS_SCALE_ROWS\" CASCADE;",
            PostgreSqlSeedSql,
            "SELECT COUNT(*) FROM \"public\".\"MS_SCALE_ROWS\";",
            "UPDATE \"public\".\"MS_SCALE_ROWS\" SET \"Tag\" = 'migrated' WHERE \"Id\" = 1;",
            type => PostgresModelSynchronizer.FromTypes(new PostgresModelSyncOptions
            {
                ConnectionString = connectionString,
                DefaultSchema = "public",
                HistorySchema = "sec"
            }, type).CompareAsync(),
            typeof(PostgresScaleBase),
            typeof(PostgresScaleExpanded),
            typeof(PostgresScaleRisky),
            () => new PostgresMigrationRunner(connectionString)));
    }

    [Test]
    [Timeout(600_000)]
    public async Task SQLite_OneMillionRows_ShouldRemainSafeAndIdempotent()
    {
        RequireScaleTests();
        var databasePath = Path.Combine(Path.GetTempPath(), $"modelsync-scale-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";

        try
        {
            await RunScenarioAsync(new ScaleScenario(
                "SQLite",
                () => new SqliteConnection(connectionString),
                "DROP TABLE IF EXISTS \"MS_SCALE_ROWS\";",
                SQLiteSeedSql,
                "SELECT COUNT(*) FROM \"MS_SCALE_ROWS\";",
                "UPDATE \"MS_SCALE_ROWS\" SET \"Tag\" = 'migrated' WHERE \"Id\" = 1;",
                type => SQLiteModelSynchronizer.FromTypes(new SQLiteModelSyncOptions
                {
                    ConnectionString = connectionString
                }, type).CompareAsync(),
                typeof(SQLiteScaleBase),
                typeof(SQLiteScaleExpanded),
                typeof(SQLiteScaleRisky),
                () => new SQLiteMigrationRunner(connectionString)));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
                File.Delete(databasePath);
        }
    }

    [Test]
    [Timeout(600_000)]
    public Task Oracle_OneMillionRows_ShouldRemainSafeAndIdempotent()
    {
        RequireScaleTests();
        var connectionString = Environment.GetEnvironmentVariable("MODELSYNC_ORACLE_CONNECTION")
            ?? "User Id=MODELSYNC_TEST;Password=ModelSync_Pass123;Data Source=127.0.0.1:11521/FREEPDB1";

        return RunScenarioAsync(new ScaleScenario(
            "Oracle",
            () => new OracleConnection(connectionString),
            "BEGIN EXECUTE IMMEDIATE 'DROP TABLE \"MS_SCALE_ROWS\" CASCADE CONSTRAINTS'; EXCEPTION WHEN OTHERS THEN IF SQLCODE != -942 THEN RAISE; END IF; END;",
            OracleSeedSql,
            "SELECT COUNT(*) FROM \"MS_SCALE_ROWS\"",
            null,
            type => OracleModelSynchronizer.FromTypes(new OracleModelSyncOptions
            {
                ConnectionString = connectionString
            }, type).CompareAsync(),
            typeof(OracleScaleBase),
            typeof(OracleScaleExpanded),
            typeof(OracleScaleRisky),
            null));
    }

    private static async Task RunScenarioAsync(ScaleScenario scenario)
    {
        var total = Stopwatch.StartNew();
        await ExecuteAsync(scenario.ConnectionFactory, scenario.CleanupSql);

        try
        {
            await MeasureAsync(scenario.Provider, "CreateTable", async () =>
            {
                var result = await scenario.Compare(scenario.BaseModel);
                Assert.That(result.BlockedOperations, Is.Empty, Dump(result));
                Assert.That(result.AutomaticOperations.Any(x => x.ChangeType == ModelSyncChangeType.CreateTable), Is.True, Dump(result));
                await result.ApplyAsync();
            });

            await MeasureAsync(scenario.Provider, "Insert1M", () => ExecuteAsync(scenario.ConnectionFactory, scenario.SeedSql, 300));
            Assert.That(await ScalarInt64Async(scenario.ConnectionFactory, scenario.CountSql), Is.EqualTo(ExpectedRows));

            await MeasureAsync(scenario.Provider, "StableCompare1M", async () =>
            {
                var result = await scenario.Compare(scenario.BaseModel);
                Assert.That(result.AutomaticOperations, Is.Empty, Dump(result));
                Assert.That(result.BlockedOperations, Is.Empty, Dump(result));
            });

            await MeasureAsync(scenario.Provider, "AddColumnAndIndex1M", async () =>
            {
                var result = await scenario.Compare(scenario.ExpandedModel);
                Assert.That(result.BlockedOperations, Is.Empty, Dump(result));
                Assert.That(result.AutomaticOperations.Any(x => x.ChangeType == ModelSyncChangeType.AddColumn), Is.True, Dump(result));
                Assert.That(result.AutomaticOperations.Any(x => x.ChangeType == ModelSyncChangeType.AddIndex), Is.True, Dump(result));
                await result.ApplyAsync();
            });

            await MeasureAsync(scenario.Provider, "IdempotentCompare1M", async () =>
            {
                var result = await scenario.Compare(scenario.ExpandedModel);
                Assert.That(result.AutomaticOperations, Is.Empty, Dump(result));
                Assert.That(result.BlockedOperations, Is.Empty, Dump(result));
            });

            var risky = await scenario.Compare(scenario.RiskyModel);
            Assert.That(risky.BlockedOperations.Any(x => x.ChangeType == ModelSyncChangeType.AddColumn), Is.True, Dump(risky));

            if (scenario.RunnerFactory != null && scenario.MigrationSql != null)
            {
                await MeasureAsync(scenario.Provider, "MigrationHistory", async () =>
                {
                    var runner = scenario.RunnerFactory();
                    var migrationId = "scale-" + Guid.NewGuid().ToString("N");
                    runner.RegisterScript(MigrationScriptDefinition.Create(
                        migrationId,
                        "Update one row in the million-row fixture",
                        MigrationScriptCategory.CustomSql,
                        scenario.MigrationSql,
                        "scale-test"));

                    var first = await runner.RunWithResultAsync();
                    Assert.That(first.Succeeded, Is.True);
                    Assert.That(first.Items.Any(x => x.Action == MigrationExecutionAction.Applied), Is.True);

                    var second = await runner.RunWithResultAsync();
                    Assert.That(second.Succeeded, Is.True);
                    Assert.That(second.Items.Any(x => x.Action == MigrationExecutionAction.Skipped), Is.True);
                });
            }

            Assert.That(await ScalarInt64Async(scenario.ConnectionFactory, scenario.CountSql), Is.EqualTo(ExpectedRows));
            Assert.That(total.Elapsed, Is.LessThan(TimeSpan.FromMinutes(8)), $"{scenario.Provider} scale scenario exceeded the eight-minute safety budget.");
            TestContext.Progress.WriteLine($"SCALE|{scenario.Provider}|Total|{total.Elapsed.TotalMilliseconds:0}|Rows={ExpectedRows}");
        }
        finally
        {
            await ExecuteAsync(scenario.ConnectionFactory, scenario.CleanupSql);
        }
    }

    private static async Task MeasureAsync(string provider, string stage, Func<Task> action)
    {
        var timer = Stopwatch.StartNew();
        await action();
        TestContext.Progress.WriteLine($"SCALE|{provider}|{stage}|{timer.Elapsed.TotalMilliseconds:0}");
    }

    private static async Task ExecuteAsync(Func<DbConnection> connectionFactory, string sql, int timeoutSeconds = 120)
    {
        await using var connection = connectionFactory();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = timeoutSeconds;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ScalarInt64Async(Func<DbConnection> connectionFactory, string sql)
    {
        await using var connection = connectionFactory();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 120;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static string Dump(ModelSyncResult result)
        => string.Join(" | ", result.Operations.Select(x => $"{x.Disposition}:{x.Risk}:{x.ChangeType}:{x.Table}:{x.Column}:{x.Reason}"));

    private static void RequireScaleTests()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(RunVariable), "1", StringComparison.Ordinal))
            Assert.Ignore($"Set {RunVariable}=1 to run the million-row integration tests.");
    }

    private sealed record ScaleScenario(
        string Provider,
        Func<DbConnection> ConnectionFactory,
        string CleanupSql,
        string SeedSql,
        string CountSql,
        string? MigrationSql,
        Func<Type, Task<ModelSyncResult>> Compare,
        Type BaseModel,
        Type ExpandedModel,
        Type RiskyModel,
        Func<IMigrationRunner>? RunnerFactory);

    private const string SqlServerSeedSql = """
        INSERT INTO [dbo].[MS_SCALE_ROWS] ([Id], [Code], [Payload], [CreatedAt])
        SELECT TOP (1000000)
            ROW_NUMBER() OVER (ORDER BY (SELECT NULL)),
            RIGHT(REPLICATE('0', 12) + CONVERT(varchar(12), ROW_NUMBER() OVER (ORDER BY (SELECT NULL))), 12),
            REPLICATE('x', 80),
            SYSUTCDATETIME()
        FROM sys.all_objects a CROSS JOIN sys.all_objects b;
        """;

    private const string MySqlSeedSql = """
        INSERT INTO `MS_SCALE_ROWS` (`Id`, `Code`, `Payload`, `CreatedAt`)
        SELECT n + 1, LPAD(n + 1, 12, '0'), REPEAT('x', 80), UTC_TIMESTAMP()
        FROM (
            SELECT a.d + 10*b.d + 100*c.d + 1000*d.d + 10000*e.d + 100000*f.d AS n
            FROM (SELECT 0 d UNION ALL SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4 UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8 UNION ALL SELECT 9) a
            CROSS JOIN (SELECT 0 d UNION ALL SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4 UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8 UNION ALL SELECT 9) b
            CROSS JOIN (SELECT 0 d UNION ALL SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4 UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8 UNION ALL SELECT 9) c
            CROSS JOIN (SELECT 0 d UNION ALL SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4 UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8 UNION ALL SELECT 9) d
            CROSS JOIN (SELECT 0 d UNION ALL SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4 UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8 UNION ALL SELECT 9) e
            CROSS JOIN (SELECT 0 d UNION ALL SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4 UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8 UNION ALL SELECT 9) f
        ) numbers;
        """;

    private const string PostgreSqlSeedSql = """
        INSERT INTO "public"."MS_SCALE_ROWS" ("Id", "Code", "Payload", "CreatedAt")
        SELECT n, LPAD(n::text, 12, '0'), REPEAT('x', 80), CURRENT_TIMESTAMP
        FROM generate_series(1, 1000000) AS n;
        """;

    private const string SQLiteSeedSql = """
        WITH RECURSIVE numbers(n) AS (
            SELECT 1
            UNION ALL
            SELECT n + 1 FROM numbers WHERE n < 1000000
        )
        INSERT INTO "MS_SCALE_ROWS" ("Id", "Code", "Payload", "CreatedAt")
        SELECT n, printf('%012d', n), printf('%080s', 'x'), CURRENT_TIMESTAMP FROM numbers;
        """;

    private const string OracleSeedSql = """
        INSERT INTO "MS_SCALE_ROWS" ("Id", "Code", "Payload", "CreatedAt")
        SELECT LEVEL, LPAD(TO_CHAR(LEVEL), 12, '0'), RPAD('x', 80, 'x'), SYSTIMESTAMP
        FROM dual CONNECT BY LEVEL <= 1000000
        """;

    [SqlServerTableName("MS_SCALE_ROWS")]
    private class SqlServerScaleBase
    {
        [SqlServerColumnType(SqlServerColumnType.BIGINT), SqlServerColumnPrimaryKey] public long Id { get; set; }
        [SqlServerColumnType(SqlServerColumnType.NVARCHAR, "32"), SqlServerColumnNotNull] public string Code { get; set; } = string.Empty;
        [SqlServerColumnType(SqlServerColumnType.NVARCHAR, "128")] public string Payload { get; set; } = string.Empty;
        [SqlServerColumnType(SqlServerColumnType.DATETIME2)] public DateTime CreatedAt { get; set; }
    }

    [SqlServerTableName("MS_SCALE_ROWS")]
    private class SqlServerScaleExpanded : SqlServerScaleBase
    {
        [SqlServerColumnType(SqlServerColumnType.NVARCHAR, "40"), DbColumnIndex] public string? Tag { get; set; }
    }

    [SqlServerTableName("MS_SCALE_ROWS")]
    private sealed class SqlServerScaleRisky : SqlServerScaleExpanded
    {
        [SqlServerColumnType(SqlServerColumnType.NVARCHAR, "40"), SqlServerColumnNotNull] public string RequiredCode { get; set; } = string.Empty;
    }

    [MySqlTableName("MS_SCALE_ROWS")]
    private class MySqlScaleBase
    {
        [MySqlColumnType(MySqlColumnType.BIGINT), MySqlColumnPrimaryKey] public long Id { get; set; }
        [MySqlColumnType(MySqlColumnType.VARCHAR, "32"), MySqlColumnNotNull] public string Code { get; set; } = string.Empty;
        [MySqlColumnType(MySqlColumnType.VARCHAR, "128")] public string Payload { get; set; } = string.Empty;
        [MySqlColumnType(MySqlColumnType.DATETIME)] public DateTime CreatedAt { get; set; }
    }

    [MySqlTableName("MS_SCALE_ROWS")]
    private class MySqlScaleExpanded : MySqlScaleBase
    {
        [MySqlColumnType(MySqlColumnType.VARCHAR, "40"), DbColumnIndex] public string? Tag { get; set; }
    }

    [MySqlTableName("MS_SCALE_ROWS")]
    private sealed class MySqlScaleRisky : MySqlScaleExpanded
    {
        [MySqlColumnType(MySqlColumnType.VARCHAR, "40"), MySqlColumnNotNull] public string RequiredCode { get; set; } = string.Empty;
    }

    [PostgresTableName("MS_SCALE_ROWS")]
    private class PostgresScaleBase
    {
        [PostgresColumnType(PostgresColumnType.BIGINT), PostgresColumnPrimaryKey] public long Id { get; set; }
        [PostgresColumnType(PostgresColumnType.VARCHAR, "32"), PostgresColumnNotNull] public string Code { get; set; } = string.Empty;
        [PostgresColumnType(PostgresColumnType.VARCHAR, "128")] public string Payload { get; set; } = string.Empty;
        [PostgresColumnType(PostgresColumnType.TIMESTAMP)] public DateTime CreatedAt { get; set; }
    }

    [PostgresTableName("MS_SCALE_ROWS")]
    private class PostgresScaleExpanded : PostgresScaleBase
    {
        [PostgresColumnType(PostgresColumnType.VARCHAR, "40"), DbColumnIndex] public string? Tag { get; set; }
    }

    [PostgresTableName("MS_SCALE_ROWS")]
    private sealed class PostgresScaleRisky : PostgresScaleExpanded
    {
        [PostgresColumnType(PostgresColumnType.VARCHAR, "40"), PostgresColumnNotNull] public string RequiredCode { get; set; } = string.Empty;
    }

    [SQLiteTableName("MS_SCALE_ROWS")]
    private class SQLiteScaleBase
    {
        [SQLiteColumnType(SQLiteColumnType.INTEGER), SQLiteColumnPrimaryKey] public long Id { get; set; }
        [SQLiteColumnType(SQLiteColumnType.TEXT), SQLiteColumnNotNull] public string Code { get; set; } = string.Empty;
        [SQLiteColumnType(SQLiteColumnType.TEXT)] public string Payload { get; set; } = string.Empty;
        [SQLiteColumnType(SQLiteColumnType.TEXT)] public string CreatedAt { get; set; } = string.Empty;
    }

    [SQLiteTableName("MS_SCALE_ROWS")]
    private class SQLiteScaleExpanded : SQLiteScaleBase
    {
        [SQLiteColumnType(SQLiteColumnType.TEXT), DbColumnIndex] public string? Tag { get; set; }
    }

    [SQLiteTableName("MS_SCALE_ROWS")]
    private sealed class SQLiteScaleRisky : SQLiteScaleExpanded
    {
        [SQLiteColumnType(SQLiteColumnType.TEXT), SQLiteColumnNotNull] public string RequiredCode { get; set; } = string.Empty;
    }

    [OracleTableName("MS_SCALE_ROWS")]
    private class OracleScaleBase
    {
        [OracleColumnType(OracleColumnType.NUMBER, "19"), OracleColumnPrimaryKey] public long Id { get; set; }
        [OracleColumnType(OracleColumnType.VARCHAR2, "32"), OracleColumnNotNull] public string Code { get; set; } = string.Empty;
        [OracleColumnType(OracleColumnType.VARCHAR2, "128")] public string Payload { get; set; } = string.Empty;
        [OracleColumnType(OracleColumnType.TIMESTAMP)] public DateTime CreatedAt { get; set; }
    }

    [OracleTableName("MS_SCALE_ROWS")]
    private class OracleScaleExpanded : OracleScaleBase
    {
        [OracleColumnType(OracleColumnType.VARCHAR2, "40"), DbColumnIndex] public string? Tag { get; set; }
    }

    [OracleTableName("MS_SCALE_ROWS")]
    private sealed class OracleScaleRisky : OracleScaleExpanded
    {
        [OracleColumnType(OracleColumnType.VARCHAR2, "40"), OracleColumnNotNull] public string RequiredCode { get; set; } = string.Empty;
    }
}
