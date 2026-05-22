using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.Interfaces;
using UmbrellaFrame.ModelSync.Core.Services;
using UmbrellaFrame.ModelSync.SqlServer;

/// <summary>No-op generator that skips real DDL execution for unit tests.</summary>
internal class FakeSqlServerTableGenerator : SqlServerTableGenerator, ITableGenerator
{
    public FakeSqlServerTableGenerator() : base("Server=localhost;Database=fake;User Id=fake;Password=fake;TrustServerCertificate=True;") { }
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
public class SqlServerTableGeneratorTests
{
    private const string RunSqlServerIntegrationVariable = "MODELSYNC_RUN_SQLSERVER_INTEGRATION";
    private const string SqlServerConnectionStringVariable = "MODELSYNC_SQLSERVER_CONNECTION_STRING";

    private enum StatusEnum
    {
        Active,
        Inactive,
        Pending
    }

    [SqlServerTableName("SqlServerMockTable")]
    private class MockModel
    {
        [SqlServerColumnType(SqlServerColumnType.INT)]
        [SqlServerColumnPrimaryKey(true)]
        public int Id { get; set; }

        [SqlServerColumnType(SqlServerColumnType.NVARCHAR, "255")]
        [SqlServerColumnNotNull]
        public string Name { get; set; }

        [SqlServerColumnType(SqlServerColumnType.NTEXT)]
        public string Description { get; set; }

        [SqlServerColumnType(SqlServerColumnType.DATETIME)]
        public DateTime CreatedAt { get; set; }

        [SqlServerColumnType(SqlServerColumnType.DECIMAL, "10,2")]
        public decimal Price { get; set; }

        [SqlServerColumnType(SqlServerColumnType.BIT)]
        public bool IsActive { get; set; }

        [SqlServerColumnType(SqlServerColumnType.REAL)]
        public float Rating { get; set; }

        [SqlServerColumnType(SqlServerColumnType.FLOAT)]
        public double Score { get; set; }

        [SqlServerColumnType(SqlServerColumnType.VARBINARY)]
        public byte[] Data { get; set; }

        [SqlServerColumnType(SqlServerColumnType.CHAR, "1")]
        public char Initial { get; set; }

        [SqlServerColumnType(SqlServerColumnType.BIGINT)]
        public long BigNumber { get; set; }

        [SqlServerColumnType(SqlServerColumnType.SMALLINT)]
        public short SmallNumber { get; set; }

        [SqlServerColumnType(SqlServerColumnType.TINYINT)]
        public sbyte TinyNumber { get; set; }

        [SqlServerColumnType(SqlServerColumnType.INT)]
        public int MediumNumber { get; set; }

        [SqlServerColumnType(SqlServerColumnType.NVARCHAR, "50")]
        public string EnumValue { get; set; }

        [SqlServerColumnType(SqlServerColumnType.NVARCHAR, "50")]
        public string SetValue { get; set; }

        [SqlServerColumnType(SqlServerColumnType.NVARCHAR, "50")]
        public StatusEnum Status { get; set; }

        [SqlServerColumnType(SqlServerColumnType.INT)]
        public int ForeignKeyId { get; set; }
    }

    [SqlServerTableName("SqlServerMockTable2")]
    private class MockModel2
    {
        [SqlServerColumnType(SqlServerColumnType.INT)]
        [SqlServerColumnPrimaryKey(true)]
        public int Id { get; set; }

        [SqlServerColumnType(SqlServerColumnType.NVARCHAR, "255")]
        [SqlServerColumnNotNull]
        public string Name { get; set; }

        [SqlServerColumnType(SqlServerColumnType.DATETIME)]
        public DateTime CreatedAt { get; set; }

        [SqlServerColumnType(SqlServerColumnType.DECIMAL, "10,2")]
        public decimal Price { get; set; }
    }

    [SqlServerTableName("SqlServerMockTable3")]
    private class MockModel3
    {
        [SqlServerColumnType(SqlServerColumnType.INT)]
        [SqlServerColumnPrimaryKey]
        public int Id { get; set; }

        [SqlServerColumnType(SqlServerColumnType.NVARCHAR, "255")]
        [SqlServerColumnNotNull]
        public string Name { get; set; }

        [SqlServerColumnType(SqlServerColumnType.DATETIME)]
        public DateTime CreatedAt { get; set; }

        [SqlServerColumnType(SqlServerColumnType.DECIMAL, "10,2")]
        public decimal Price { get; set; }

        [SqlServerColumnType(SqlServerColumnType.BIT)]
        public bool IsActive { get; set; }
    }

    [Test]
    public void GenerateCreateTableCommand1_ShouldGenerateSql_WithoutExecuting()
    {
        // SQL generation only — no live DB required
        var sqlGenerator = new FakeSqlServerTableGenerator();
        var sql  = sqlGenerator.GenerateSqlTable<MockModel>();
        var sql1 = sqlGenerator.GenerateSqlTable<MockModel2>();
        var sql2 = sqlGenerator.GenerateSqlTable<MockModel3>();

        Assert.That(sql,  Does.Contain("[SqlServerMockTable]"));
        Assert.That(sql1, Does.Contain("[SqlServerMockTable2]"));
        Assert.That(sql2, Does.Contain("[SqlServerMockTable3]"));
    }

    [Test]
    [Category("Integration")]
    public void GenerateCreateTableCommand1_Integration_CreateTables()
    {
        RequireSqlServerIntegration();

        var sqlGenerator = new SqlServerTableGenerator(GetSqlServerConnectionString());
        sqlGenerator.GenerateSqlTable<MockModel>(ifNotExists: true);
        sqlGenerator.GenerateSqlTable<MockModel2>(ifNotExists: true);
        sqlGenerator.GenerateSqlTable<MockModel3>(ifNotExists: true);
        sqlGenerator.CreateTables(); // CreateDatabase() dahili olarak çağrılır
    }

    [Test]
    [Category("Integration")]
    public void CreateDatabase_Integration_ShouldBeIdempotent()
    {
        RequireSqlServerIntegration();

        // DB zaten varsa hata vermemeli, tekrar çağrılabilmeli
        var sqlGenerator = new SqlServerTableGenerator(GetSqlServerConnectionString());

        Assert.DoesNotThrow(() => sqlGenerator.CreateDatabase()); // ilk çağrı — DB yoksa oluşturur
        Assert.DoesNotThrow(() => sqlGenerator.CreateDatabase()); // ikinci çağrı — DB varsa hiçbir şey yapmaz
    }

    // ── ALTER TABLE integration yardımcı metodu ─────────────────────────────

    /// <summary>
    /// Her ALTER testinden önce SqlServerMockTable3'ü tamamen drop edip yeniden oluşturur.
    /// Böylece testler birbirinden bağımsız (izole) çalışır.
    /// </summary>
    private static SqlServerTableGenerator CreateFreshMockTable3()
    {
        RequireSqlServerIntegration();

        var cs = GetSqlServerConnectionString();
        var gen = new SqlServerTableGenerator(cs);
        gen.CreateDatabase();

        // Tabloyu temizle (varsa drop et)
        using var conn = new Microsoft.Data.SqlClient.SqlConnection(cs);
        conn.Open();
        using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(
            "IF OBJECT_ID(N'[SqlServerMockTable3]', N'U') IS NOT NULL DROP TABLE [SqlServerMockTable3];", conn))
            cmd.ExecuteNonQuery();

        // Yeniden oluştur
        gen.GenerateSqlTable<MockModel3>(ifNotExists: false);
        gen.CreateTables();
        return gen;
    }

    [Test]
    [Category("Integration")]
    public void AlterTable_Integration_AddColumn()
    {
        var sqlGenerator = CreateFreshMockTable3();

        // IsActive tablodan sil, ardından geri ekle — her iki işlem hatasız olmalı
        Assert.DoesNotThrow(() => sqlGenerator.DropColumn<MockModel3>("IsActive", DestructiveOperationOptions.Allow()), "DropColumn (hazırlık)");
        Assert.DoesNotThrow(() => sqlGenerator.AddColumn<MockModel3>("IsActive"),  "AddColumn");
    }

    [Test]
    [Category("Integration")]
    public void AlterTable_Integration_RenameColumn()
    {
        var sqlGenerator = CreateFreshMockTable3();

        // Name → FullName → Name (geri al): tablo başlangıç durumuna döner
        Assert.DoesNotThrow(() => sqlGenerator.RenameColumn<MockModel3>("Name", "FullName"), "RenameColumn");
        Assert.DoesNotThrow(() => sqlGenerator.RenameColumn<MockModel3>("FullName", "Name"),  "RenameColumn (geri al)");
    }

    [Test]
    [Category("Integration")]
    public void AlterTable_Integration_DropColumn()
    {
        var sqlGenerator = CreateFreshMockTable3();

        // IsActive'i sil → geri ekle
        Assert.DoesNotThrow(() => sqlGenerator.DropColumn<MockModel3>("IsActive", DestructiveOperationOptions.Allow()), "DropColumn");
        Assert.DoesNotThrow(() => sqlGenerator.AddColumn<MockModel3>("IsActive"),  "AddColumn (geri al)");
    }

    [Test]
    [Category("Integration")]
    public void AlterTable_Integration_AlterColumnType()
    {
        var sqlGenerator = CreateFreshMockTable3();

        // Price kolonunun tipini attribute'tan okuyarak günceller → DECIMAL(10,2)
        Assert.DoesNotThrow(() => sqlGenerator.AlterColumnType<MockModel3>("Price", DestructiveOperationOptions.Allow()), "AlterColumnType");
    }

    private static void RequireSqlServerIntegration()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(RunSqlServerIntegrationVariable), "1", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Ignore("SQL Server integration tests require a local SQL Server instance on localhost with sa/123. Set MODELSYNC_RUN_SQLSERVER_INTEGRATION=1 to run this test.");
        }
    }

    private static string GetSqlServerConnectionString()
    {
        return Environment.GetEnvironmentVariable(SqlServerConnectionStringVariable)
            ?? "Server=localhost;Database=appdb;User Id=sa;Password=123;Encrypt=False;TrustServerCertificate=True;";
    }
}
