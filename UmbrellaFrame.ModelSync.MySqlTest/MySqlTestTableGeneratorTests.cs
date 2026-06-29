using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.Interfaces;
using UmbrellaFrame.ModelSync.Core.Services;
using UmbrellaFrame.ModelSync.MySql;

/// <summary>No-op generator that skips real DDL execution for unit tests.</summary>
internal class FakeMySqlTestGenerator : MySqlTableGenerator, ITableGenerator
{
    public FakeMySqlTestGenerator() : base("Server=fake;Database=fake;User=fake;Password=fake;") { }
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
public class MySqlTestTableGeneratorTests
{
    private const string RunMySqlIntegrationVariable = "MODELSYNC_RUN_MYSQL_INTEGRATION";
    private const string RunMariaDbIntegrationVariable = "MODELSYNC_RUN_MARIADB_INTEGRATION";
    private const string MySqlConnectionStringVariable = "MODELSYNC_MYSQL_CONNECTION_STRING";
    private const string MariaDbConnectionStringVariable = "MODELSYNC_MARIADB_CONNECTION_STRING";

    [Test]
    public void GenerateSqlTable_WithAutoIncrementPrimaryKey_ShouldPlaceAutoIncrementBeforePrimaryKey()
    {
        var sqlGenerator = new FakeMySqlTestGenerator();

        var sql = sqlGenerator.GenerateSqlTable<MockModel>();

        Assert.That(sql, Does.Contain("`Id` INT AUTO_INCREMENT PRIMARY KEY"));
        Assert.That(sql, Does.Not.Contain("`Id` INT PRIMARY KEY AUTO_INCREMENT"));
    }

    private enum StatusEnum
    {
        Active,
        Inactive,
        Pending
    }

    [MySqlTableName("MySqlMockTable")]
    private class MockModel
    {
        [MySqlColumnType(MySqlColumnType.INT)]
        [MySqlColumnPrimaryKey(true)]
        public int Id { get; set; }

        [MySqlColumnType(MySqlColumnType.VARCHAR, "255")]
        [MySqlColumnNotNull]
        public string Name { get; set; }

        [MySqlColumnType(MySqlColumnType.TEXT)]
        public string Description { get; set; }

        [MySqlColumnType(MySqlColumnType.DATETIME)]
        public DateTime CreatedAt { get; set; }

        [MySqlColumnType(MySqlColumnType.DECIMAL, "10,2")]
        public decimal Price { get; set; }

        //[MySqlColumnType(MySqlColumnType.BIT)]
        //public bool IsActive { get; set; }

        [MySqlColumnType(MySqlColumnType.FLOAT)]
        public float Rating { get; set; }

        [MySqlColumnType(MySqlColumnType.DOUBLE)]
        public double Score { get; set; }

        [MySqlColumnType(MySqlColumnType.VARBINARY, "50")]
        public byte[] Data { get; set; }

        [MySqlColumnType(MySqlColumnType.CHAR, "1")]
        public char Initial { get; set; }

        [MySqlColumnType(MySqlColumnType.BIGINT)]
        public long BigNumber { get; set; }

        [MySqlColumnType(MySqlColumnType.SMALLINT)]
        public short SmallNumber { get; set; }

        [MySqlColumnType(MySqlColumnType.TINYINT, "1")]
        public sbyte TinyNumber { get; set; }

        [MySqlColumnType(MySqlColumnType.INT)]
        public int MediumNumber { get; set; }

        [MySqlColumnType(MySqlColumnType.VARCHAR, "50")]
        public string EnumValue { get; set; }

        [MySqlColumnType(MySqlColumnType.VARCHAR, "50")]
        public string SetValue { get; set; }

        [MySqlColumnType(MySqlColumnType.VARCHAR, "50")]
        public StatusEnum Status { get; set; }

        [MySqlColumnType(MySqlColumnType.INT)]
        public int ForeignKeyId { get; set; }
    }

    [MySqlTableName("MySqlMockTable2")]
    private class MockModel2
    {
        [MySqlColumnType(MySqlColumnType.INT)]
        [MySqlColumnPrimaryKey(true)]
        public int Id { get; set; }

        [MySqlColumnType(MySqlColumnType.VARCHAR, "255")]
        [MySqlColumnNotNull]
        public string Name { get; set; }

        [MySqlColumnType(MySqlColumnType.DATETIME)]
        public DateTime CreatedAt { get; set; }

        [MySqlColumnType(MySqlColumnType.DECIMAL, "10,2")]
        public decimal Price { get; set; }
    }

    [MySqlTableName("MySqlMockTable3")]
    private class MockModel3
    {
        [MySqlColumnType(MySqlColumnType.INT)]
        [MySqlColumnPrimaryKey]
        public int Id { get; set; }

        [MySqlColumnType(MySqlColumnType.VARCHAR, "255")]
        [MySqlColumnNotNull]
        public string Name { get; set; }

        [MySqlColumnType(MySqlColumnType.DATETIME)]
        public DateTime CreatedAt { get; set; }

        [MySqlColumnType(MySqlColumnType.DECIMAL, "10,2")]
        public decimal Price { get; set; }

        [MySqlColumnType(MySqlColumnType.BIT)]
        public bool IsActive { get; set; }
    }

    [Test]
    public void GenerateCreateTableCommand1_ShouldGenerateSql_WithoutExecuting()
    {
        var sqlGenerator = new FakeMySqlTestGenerator();
        var sql  = sqlGenerator.GenerateSqlTable<MockModel>();
        var sql1 = sqlGenerator.GenerateSqlTable<MockModel2>();
        var sql2 = sqlGenerator.GenerateSqlTable<MockModel3>();

        Assert.That(sql,  Does.Contain("`MySqlMockTable`"));
        Assert.That(sql1, Does.Contain("`MySqlMockTable2`"));
        Assert.That(sql2, Does.Contain("`MySqlMockTable3`"));
    }

    [Test]
    [Category("Integration")]
    public void GenerateCreateTableCommand1_Integration_MySQL_CreateTables()
    {
        RequireMySqlIntegration();

        var sqlGenerator = new MySqlTableGenerator(GetMySqlConnectionString());
        sqlGenerator.GenerateSqlTable<MockModel>(ifNotExists: true);
        sqlGenerator.GenerateSqlTable<MockModel2>(ifNotExists: true);
        sqlGenerator.GenerateSqlTable<MockModel3>(ifNotExists: true);
        sqlGenerator.CreateDatabase(); // DB yoksa oluşturur
        sqlGenerator.CreateTables();
    }

    [Test]
    [Category("Integration")]
    public void GenerateCreateTableCommand1_Integration_MariaDB_CreateTables()
    {
        RequireMariaDbIntegration();

        var sqlGenerator = new MySqlTableGenerator(GetMariaDbConnectionString());
        sqlGenerator.GenerateSqlTable<MockModel>(ifNotExists: true);
        sqlGenerator.GenerateSqlTable<MockModel2>(ifNotExists: true);
        sqlGenerator.GenerateSqlTable<MockModel3>(ifNotExists: true);
        sqlGenerator.CreateDatabase(); // DB yoksa oluşturur
        sqlGenerator.CreateTables();
    }

    [Test]
    [Category("Integration")]
    public void CreateDatabase_Integration_MySQL_ShouldBeIdempotent()
    {
        RequireMySqlIntegration();

        var sqlGenerator = new MySqlTableGenerator(GetMySqlConnectionString());

        Assert.DoesNotThrow(() => sqlGenerator.CreateDatabase()); // DB yoksa oluşturur
        Assert.DoesNotThrow(() => sqlGenerator.CreateDatabase()); // DB varsa hiçbir şey yapmaz
    }

    [Test]
    [Category("Integration")]
    public void CreateDatabase_Integration_MariaDB_ShouldBeIdempotent()
    {
        RequireMariaDbIntegration();

        var sqlGenerator = new MySqlTableGenerator(GetMariaDbConnectionString());

        Assert.DoesNotThrow(() => sqlGenerator.CreateDatabase()); // DB yoksa oluşturur
        Assert.DoesNotThrow(() => sqlGenerator.CreateDatabase()); // DB varsa hiçbir şey yapmaz
    }

    // ── ALTER TABLE — SQL üretim testleri (DB bağlantısı yok) ───────────────

    [Test]
    public void AlterTable_AddColumn_ShouldGenerateCorrectSql()
    {
        var generator = new FakeMySqlTestGenerator();
        var sql = generator.GenerateAddColumnSql<MockModel3>("IsActive");

        // ALTER TABLE `MySqlMockTable3` ADD `IsActive` BIT;
        Assert.That(sql, Does.Contain("ALTER TABLE"));
        Assert.That(sql, Does.Contain("`MySqlMockTable3`"));
        Assert.That(sql, Does.Contain("ADD"));
        Assert.That(sql, Does.Contain("`IsActive`"));
        Assert.That(sql, Does.Contain("BIT"));
    }

    [Test]
    public void AlterTable_DropColumn_ShouldGenerateCorrectSql()
    {
        var generator = new FakeMySqlTestGenerator();
        var sql = generator.GenerateDropColumnSql<MockModel3>("IsActive");

        Assert.That(sql, Does.Contain("ALTER TABLE"));
        Assert.That(sql, Does.Contain("`MySqlMockTable3`"));
        Assert.That(sql, Does.Contain("DROP COLUMN"));
        Assert.That(sql, Does.Contain("`IsActive`"));
    }

    [Test]
    public void AlterTable_RenameColumn_ShouldGenerateCorrectSql()
    {
        var generator = new FakeMySqlTestGenerator();
        var sql = generator.GenerateRenameColumnSql<MockModel3>("Name", "FullName");

        // MySQL 8+ standard RENAME COLUMN syntax
        Assert.That(sql, Does.Contain("ALTER TABLE"));
        Assert.That(sql, Does.Contain("`MySqlMockTable3`"));
        Assert.That(sql, Does.Contain("RENAME COLUMN"));
        Assert.That(sql, Does.Contain("`Name`"));
        Assert.That(sql, Does.Contain("`FullName`"));
    }

    [Test]
    public void AlterTable_AlterColumnType_ShouldUseMysqlModifySyntax()
    {
        var generator = new FakeMySqlTestGenerator();
        var sql = generator.GenerateAlterColumnTypeSql<MockModel3>("Price");

        // MySQL uses MODIFY COLUMN
        Assert.That(sql, Does.Contain("ALTER TABLE"));
        Assert.That(sql, Does.Contain("`MySqlMockTable3`"));
        Assert.That(sql, Does.Contain("MODIFY COLUMN"));
        Assert.That(sql, Does.Contain("`Price`"));
        Assert.That(sql, Does.Contain("DECIMAL(10,2)"));
    }

    [Test]
    public void AlterTable_AddColumn_WithNotNull_ShouldIncludeConstraint()
    {
        var generator = new FakeMySqlTestGenerator();
        var sql = generator.GenerateAddColumnSql<MockModel3>("Name");

        Assert.That(sql, Does.Contain("NOT NULL"));
        Assert.That(sql, Does.Contain("VARCHAR(255)"));
    }

    [Test]
    public void AlterTable_AddColumn_UnknownColumn_ShouldThrow()
    {
        var generator = new FakeMySqlTestGenerator();
        Assert.Catch<Exception>(() => generator.GenerateAddColumnSql<MockModel3>("NonExistent"));
    }

    // ── ALTER TABLE — Integration testleri (MySQL) ────────────────────────────────

    private static MySqlTableGenerator CreateFreshMySqlMockTable3()
    {
        RequireMySqlIntegration();

        var cs = GetMySqlConnectionString();
        var gen = new MySqlTableGenerator(cs);
        gen.CreateDatabase();

        // Tabloyu temizle
        using var conn = new MySqlConnector.MySqlConnection(cs);
        conn.Open();
        using (var cmd = new MySqlConnector.MySqlCommand("DROP TABLE IF EXISTS `MySqlMockTable3`;", conn))
            cmd.ExecuteNonQuery();

        gen.GenerateSqlTable<MockModel3>(ifNotExists: false);
        gen.CreateTables();
        return gen;
    }

    [Test]
    [Category("Integration")]
    public void AlterTable_Integration_MySQL_AddColumn()
    {
        var gen = CreateFreshMySqlMockTable3();
        Assert.DoesNotThrow(() => gen.DropColumn<MockModel3>("IsActive", DestructiveOperationOptions.Allow()), "DropColumn (hazırlık)");
        Assert.DoesNotThrow(() => gen.AddColumn<MockModel3>("IsActive"),  "AddColumn");
    }

    [Test]
    [Category("Integration")]
    public void AlterTable_Integration_MySQL_RenameColumn()
    {
        var gen = CreateFreshMySqlMockTable3();
        Assert.DoesNotThrow(() => gen.RenameColumn<MockModel3>("Name", "FullName"), "RenameColumn");
        Assert.DoesNotThrow(() => gen.RenameColumn<MockModel3>("FullName", "Name"),  "RenameColumn (geri al)");
    }

    [Test]
    [Category("Integration")]
    public void AlterTable_Integration_MySQL_DropColumn()
    {
        var gen = CreateFreshMySqlMockTable3();
        Assert.DoesNotThrow(() => gen.DropColumn<MockModel3>("IsActive", DestructiveOperationOptions.Allow()), "DropColumn");
        Assert.DoesNotThrow(() => gen.AddColumn<MockModel3>("IsActive"),  "AddColumn (geri al)");
    }

    [Test]
    [Category("Integration")]
    public void AlterTable_Integration_MySQL_AlterColumnType()
    {
        var gen = CreateFreshMySqlMockTable3();
        Assert.DoesNotThrow(() => gen.AlterColumnType<MockModel3>("Price", DestructiveOperationOptions.Allow()), "AlterColumnType");
    }

    private static void RequireMySqlIntegration()
    {
        RequireIntegration(RunMySqlIntegrationVariable, "MySQL integration tests require a local MySQL instance on localhost:3306 with appuser/apppass.");
    }

    private static void RequireMariaDbIntegration()
    {
        RequireIntegration(RunMariaDbIntegrationVariable, "MariaDB integration tests require a local MariaDB instance on localhost:3307 with appuser/apppass.");
    }

    private static void RequireIntegration(string variableName, string reason)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(variableName), "1", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Ignore($"{reason} Set {variableName}=1 to run this test.");
        }
    }

    private static string GetMySqlConnectionString()
    {
        return Environment.GetEnvironmentVariable(MySqlConnectionStringVariable)
            ?? "Server=localhost;Port=3306;Database=appdb;User ID=appuser;Password=apppass;";
    }

    private static string GetMariaDbConnectionString()
    {
        return Environment.GetEnvironmentVariable(MariaDbConnectionStringVariable)
            ?? "Server=localhost;Port=3307;Database=appdb;User ID=appuser;Password=apppass;";
    }
}
