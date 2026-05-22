using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.Interfaces;
using UmbrellaFrame.ModelSync.Core.Services;
using UmbrellaFrame.ModelSync.PostgreSQL;

/// <summary>No-op generator that skips real DDL execution for unit tests.</summary>
internal class FakePostgresTestGenerator : PostgresTableGenerator, ITableGenerator
{
    public FakePostgresTestGenerator() : base("Host=fake;Database=fake;Username=fake;Password=fake;") { }
    public new void CreateTables() { /* no-op */ }
    public new Task CreateTablesAsync(CancellationToken ct = default) => Task.CompletedTask;
    public new void DropTables() { /* no-op */ }
    public new Task DropTablesAsync(CancellationToken ct = default) => Task.CompletedTask;

    // ── ALTER TABLE SQL üretimini dışarıya açan wrapper'lar (unit test için) ──
    public string GenerateAddColumnSql<T>(string columnName) where T : class, new()
        => BuildAddColumnSql<T>(columnName);

    public string GenerateDropColumnSql<T>(string columnName) where T : class, new()
        => BuildDropColumnSql<T>(columnName);

    public string GenerateRenameColumnSql<T>(string oldName, string newName) where T : class, new()
        => BuildRenameColumnSql<T>(oldName, newName);

    public string GenerateAlterColumnTypeSql<T>(string columnName) where T : class, new()
        => BuildAlterColumnTypeSql<T>(columnName);
}

[TestFixture]
public class PostgresTableGeneratorTests
{
    private const string RunPostgresIntegrationVariable = "MODELSYNC_RUN_POSTGRES_INTEGRATION";
    private const string PostgresConnectionStringVariable = "MODELSYNC_POSTGRES_CONNECTION_STRING";

    private enum StatusEnum
    {
        Active,
        Inactive,
        Pending
    }

    [PostgresTableName("PostgresMockTable")]
    private class MockModel
    {
        [PostgresColumnType(PostgresColumnType.INTEGER)]
        [PostgresColumnPrimaryKey()]
        public int Id { get; set; }

        [PostgresColumnType(PostgresColumnType.VARCHAR, "255")]
        [PostgresColumnNotNull]
        public string Name { get; set; }

        [PostgresColumnType(PostgresColumnType.TEXT)]
        public string Description { get; set; }

        [PostgresColumnType(PostgresColumnType.TIMESTAMP)]
        public DateTime CreatedAt { get; set; }

        [PostgresColumnType(PostgresColumnType.DECIMAL, "10,2")]
        public decimal Price { get; set; }

        //[PostgresColumnType(PostgresColumnType.BIT)]
        //public bool IsActive { get; set; }

        [PostgresColumnType(PostgresColumnType.REAL)]
        public float Rating { get; set; }

        [PostgresColumnType(PostgresColumnType.DOUBLE_PRECISION)]
        public double Score { get; set; }

        [PostgresColumnType(PostgresColumnType.BYTEA)]
        public byte[] Data { get; set; }

        [PostgresColumnType(PostgresColumnType.CHAR, "1")]
        public char Initial { get; set; }

        [PostgresColumnType(PostgresColumnType.BIGINT)]
        public long BigNumber { get; set; }

        [PostgresColumnType(PostgresColumnType.SMALLINT)]
        public short SmallNumber { get; set; }

        [PostgresColumnType(PostgresColumnType.SMALLINT)]
        public sbyte TinyNumber { get; set; }

        [PostgresColumnType(PostgresColumnType.INTEGER)]
        public int MediumNumber { get; set; }

        [PostgresColumnType(PostgresColumnType.VARCHAR, "50")]
        public string EnumValue { get; set; }

        [PostgresColumnType(PostgresColumnType.VARCHAR, "50")]
        public string SetValue { get; set; }

        [PostgresColumnType(PostgresColumnType.VARCHAR, "50")]
        public StatusEnum Status { get; set; }

        [PostgresColumnType(PostgresColumnType.INTEGER)]
        public int ForeignKeyId { get; set; }
    }

    [PostgresTableName("PostgresMockTable2")]
    private class MockModel2
    {
        [PostgresColumnType(PostgresColumnType.INTEGER)]
        [PostgresColumnPrimaryKey()]
        public int Id { get; set; }

        [PostgresColumnType(PostgresColumnType.VARCHAR, "255")]
        [PostgresColumnNotNull]
        public string Name { get; set; }

        [PostgresColumnType(PostgresColumnType.TIMESTAMP)]
        public DateTime CreatedAt { get; set; }

        [PostgresColumnType(PostgresColumnType.DECIMAL, "10,2")]
        public decimal Price { get; set; }
    }

    [PostgresTableName("PostgresMockTable3")]
    private class MockModel3
    {
        [PostgresColumnType(PostgresColumnType.INTEGER)]
        [PostgresColumnPrimaryKey]
        public int Id { get; set; }

        [PostgresColumnType(PostgresColumnType.VARCHAR, "255")]
        [PostgresColumnNotNull]
        public string Name { get; set; }

        [PostgresColumnType(PostgresColumnType.TIMESTAMP)]
        public DateTime CreatedAt { get; set; }

        [PostgresColumnType(PostgresColumnType.DECIMAL, "10,2")]
        public decimal Price { get; set; }

        [PostgresColumnType(PostgresColumnType.BOOLEAN)]
        public bool IsActive { get; set; }
    }

    [Test]
    public void GenerateCreateTableCommand1_ShouldGenerateSql_WithoutExecuting()
    {
        var sqlGenerator = new FakePostgresTestGenerator();
        var sql  = sqlGenerator.GenerateSqlTable<MockModel>();
        var sql1 = sqlGenerator.GenerateSqlTable<MockModel2>();
        var sql2 = sqlGenerator.GenerateSqlTable<MockModel3>();

        Assert.That(sql,  Does.Contain("\"PostgresMockTable\""));
        Assert.That(sql1, Does.Contain("\"PostgresMockTable2\""));
        Assert.That(sql2, Does.Contain("\"PostgresMockTable3\""));
    }

    [Test]
    [Category("Integration")]
    public void GenerateCreateTableCommand1_Integration_CreateTables()
    {
        RequirePostgresIntegration();

        var sqlGenerator = new PostgresTableGenerator(GetPostgresConnectionString());
        sqlGenerator.GenerateSqlTable<MockModel>(ifNotExists: true);
        sqlGenerator.GenerateSqlTable<MockModel2>(ifNotExists: true);
        sqlGenerator.GenerateSqlTable<MockModel3>(ifNotExists: true);
        sqlGenerator.CreateDatabase(); // DB yoksa oluşturur
        sqlGenerator.CreateTables();
    }

    [Test]
    [Category("Integration")]
    public void CreateDatabase_Integration_ShouldBeIdempotent()
    {
        RequirePostgresIntegration();

        var sqlGenerator = new PostgresTableGenerator(GetPostgresConnectionString());

        Assert.DoesNotThrow(() => sqlGenerator.CreateDatabase()); // DB yoksa oluşturur
        Assert.DoesNotThrow(() => sqlGenerator.CreateDatabase()); // DB varsa hiçbir şey yapmaz
    }

    // ── ALTER TABLE — SQL üretim testleri (DB bağlantısı yok) ───────────────

    [Test]
    public void AlterTable_AddColumn_ShouldGenerateCorrectSql()
    {
        var generator = new FakePostgresTestGenerator();
        var sql = generator.GenerateAddColumnSql<MockModel3>("IsActive");

        // ALTER TABLE "PostgresMockTable3" ADD "IsActive" BOOLEAN;
        Assert.That(sql, Does.Contain("ALTER TABLE"));
        Assert.That(sql, Does.Contain("\"PostgresMockTable3\""));
        Assert.That(sql, Does.Contain("ADD"));
        Assert.That(sql, Does.Contain("\"IsActive\""));
        Assert.That(sql, Does.Contain("BOOLEAN"));
    }

    [Test]
    public void AlterTable_DropColumn_ShouldGenerateCorrectSql()
    {
        var generator = new FakePostgresTestGenerator();
        var sql = generator.GenerateDropColumnSql<MockModel3>("IsActive");

        Assert.That(sql, Does.Contain("ALTER TABLE"));
        Assert.That(sql, Does.Contain("\"PostgresMockTable3\""));
        Assert.That(sql, Does.Contain("DROP COLUMN"));
        Assert.That(sql, Does.Contain("\"IsActive\""));
    }

    [Test]
    public void AlterTable_RenameColumn_ShouldGenerateCorrectSql()
    {
        var generator = new FakePostgresTestGenerator();
        var sql = generator.GenerateRenameColumnSql<MockModel3>("Name", "FullName");

        // PostgreSQL standard RENAME COLUMN syntax
        Assert.That(sql, Does.Contain("ALTER TABLE"));
        Assert.That(sql, Does.Contain("\"PostgresMockTable3\""));
        Assert.That(sql, Does.Contain("RENAME COLUMN"));
        Assert.That(sql, Does.Contain("\"Name\""));
        Assert.That(sql, Does.Contain("\"FullName\""));
    }

    [Test]
    public void AlterTable_AlterColumnType_ShouldGenerateCorrectSql()
    {
        var generator = new FakePostgresTestGenerator();
        var sql = generator.GenerateAlterColumnTypeSql<MockModel3>("Price");

        // PostgreSQL: ALTER TABLE "..." ALTER COLUMN "Price" TYPE DECIMAL(10,2);
        Assert.That(sql, Does.Contain("ALTER TABLE"));
        Assert.That(sql, Does.Contain("\"PostgresMockTable3\""));
        Assert.That(sql, Does.Contain("ALTER COLUMN"));
        Assert.That(sql, Does.Contain("TYPE"));
        Assert.That(sql, Does.Contain("\"Price\""));
        Assert.That(sql, Does.Contain("DECIMAL(10,2)"));
    }

    [Test]
    public void AlterTable_AddColumn_WithNotNull_ShouldIncludeConstraint()
    {
        var generator = new FakePostgresTestGenerator();
        var sql = generator.GenerateAddColumnSql<MockModel3>("Name");

        Assert.That(sql, Does.Contain("NOT NULL"));
        Assert.That(sql, Does.Contain("VARCHAR(255)"));
    }

    [Test]
    public void AlterTable_AddColumn_UnknownColumn_ShouldThrow()
    {
        var generator = new FakePostgresTestGenerator();
        Assert.Catch<Exception>(() => generator.GenerateAddColumnSql<MockModel3>("NonExistent"));
    }

    // ── ALTER TABLE — Integration testleri (PostgreSQL) ─────────────────────────

    private static PostgresTableGenerator CreateFreshPostgresMockTable3()
    {
        RequirePostgresIntegration();

        var cs = GetPostgresConnectionString();
        var gen = new PostgresTableGenerator(cs);
        gen.CreateDatabase();

        using var conn = new Npgsql.NpgsqlConnection(cs);
        conn.Open();
        using (var cmd = new Npgsql.NpgsqlCommand("DROP TABLE IF EXISTS \"PostgresMockTable3\";", conn))
            cmd.ExecuteNonQuery();

        gen.GenerateSqlTable<MockModel3>(ifNotExists: false);
        gen.CreateTables();
        return gen;
    }

    [Test]
    [Category("Integration")]
    public void AlterTable_Integration_Postgres_AddColumn()
    {
        var gen = CreateFreshPostgresMockTable3();
        Assert.DoesNotThrow(() => gen.DropColumn<MockModel3>("IsActive", DestructiveOperationOptions.Allow()), "DropColumn (hazırlık)");
        Assert.DoesNotThrow(() => gen.AddColumn<MockModel3>("IsActive"),  "AddColumn");
    }

    [Test]
    [Category("Integration")]
    public void AlterTable_Integration_Postgres_RenameColumn()
    {
        var gen = CreateFreshPostgresMockTable3();
        Assert.DoesNotThrow(() => gen.RenameColumn<MockModel3>("Name", "FullName"), "RenameColumn");
        Assert.DoesNotThrow(() => gen.RenameColumn<MockModel3>("FullName", "Name"),  "RenameColumn (geri al)");
    }

    [Test]
    [Category("Integration")]
    public void AlterTable_Integration_Postgres_DropColumn()
    {
        var gen = CreateFreshPostgresMockTable3();
        Assert.DoesNotThrow(() => gen.DropColumn<MockModel3>("IsActive", DestructiveOperationOptions.Allow()), "DropColumn");
        Assert.DoesNotThrow(() => gen.AddColumn<MockModel3>("IsActive"),  "AddColumn (geri al)");
    }

    [Test]
    [Category("Integration")]
    public void AlterTable_Integration_Postgres_AlterColumnType()
    {
        var gen = CreateFreshPostgresMockTable3();
        Assert.DoesNotThrow(() => gen.AlterColumnType<MockModel3>("Price", DestructiveOperationOptions.Allow()), "AlterColumnType");
    }

    private static void RequirePostgresIntegration()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(RunPostgresIntegrationVariable), "1", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Ignore("PostgreSQL integration tests require a local PostgreSQL instance on localhost:5432 with appuser/apppass. Set MODELSYNC_RUN_POSTGRES_INTEGRATION=1 to run this test.");
        }
    }

    private static string GetPostgresConnectionString()
    {
        return Environment.GetEnvironmentVariable(PostgresConnectionStringVariable)
            ?? "Host=localhost;Port=5432;Database=appdb;Username=appuser;Password=apppass;";
    }
}
