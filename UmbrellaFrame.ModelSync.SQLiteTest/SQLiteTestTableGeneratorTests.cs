using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.Interfaces;
using UmbrellaFrame.ModelSync.Core.Services;
using UmbrellaFrame.ModelSync.SQLite;

/// <summary>
/// In-memory SQLite generator that keeps a persistent connection alive.
/// SQLite :memory: databases are destroyed when ALL connections close.
/// Holding one open connection guarantees the schema survives across multiple
/// method calls (CreateTables → DropColumn → AddColumn etc.).
/// </summary>
internal class InMemorySQLiteTableGenerator : SQLiteTableGenerator, ITableGenerator, IDisposable
{
    // Unique shared-cache DB name per instance so parallel tests don't collide.
    private static string MakeConnStr()
        => $"Data Source={System.Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    private readonly SqliteConnection _keepAlive;

    public InMemorySQLiteTableGenerator() : this(MakeConnStr()) { }

    private InMemorySQLiteTableGenerator(string cs) : base(cs)
    {
        // Açık tuttuğumuz sürece in-memory DB yaşamaya devam eder.
        _keepAlive = new SqliteConnection(cs);
        _keepAlive.Open();
    }

    public void Dispose() => _keepAlive?.Dispose();

    // ── ALTER TABLE SQL üretimini dışarıya açan wrapper'lar (unit test için) ──
    public string GenerateAddColumnSql<T>(string columnName) where T : class, new()
        => BuildAddColumnSql<T>(columnName);

    public string GenerateDropColumnSql<T>(string columnName) where T : class, new()
        => BuildDropColumnSql<T>(columnName);

    public string GenerateRenameColumnSql<T>(string oldName, string newName) where T : class, new()
        => BuildRenameColumnSql<T>(oldName, newName);

    public void GenerateAlterColumnTypeSql<T>(string columnName) where T : class, new()
        => AlterColumnType<T>(columnName, DestructiveOperationOptions.Allow()); // SQLite'ta NotSupportedException fırlatır
}

[TestFixture]
public class SQLiteTestTableGeneratorTests
{
    private enum StatusEnum
    {
        Active,
        Inactive,
        Pending
    }

    [SQLiteTableName("SQLiteMockTable")]
    private class MockModel
    {
        [SQLiteColumnType(SQLiteColumnType.INTEGER)]
        [SQLiteColumnPrimaryKey()]
        public int Id { get; set; }

        [SQLiteColumnType(SQLiteColumnType.TEXT)]
        [SQLiteColumnNotNull]
        public string Name { get; set; }

        [SQLiteColumnType(SQLiteColumnType.TEXT)]
        public string Description { get; set; }

        [SQLiteColumnType(SQLiteColumnType.REAL)]
        public decimal Price { get; set; }

        [SQLiteColumnType(SQLiteColumnType.REAL)]
        public float Rating { get; set; }

        [SQLiteColumnType(SQLiteColumnType.REAL)]
        public double Score { get; set; }

        [SQLiteColumnType(SQLiteColumnType.BLOB)]
        public byte[] Data { get; set; }

        [SQLiteColumnType(SQLiteColumnType.TEXT)]
        public char Initial { get; set; }

        [SQLiteColumnType(SQLiteColumnType.INTEGER)]
        public long BigNumber { get; set; }

        [SQLiteColumnType(SQLiteColumnType.INTEGER)]
        public short SmallNumber { get; set; }

        [SQLiteColumnType(SQLiteColumnType.INTEGER)]
        public sbyte TinyNumber { get; set; }

        [SQLiteColumnType(SQLiteColumnType.INTEGER)]
        public int MediumNumber { get; set; }

        [SQLiteColumnType(SQLiteColumnType.TEXT)]
        public string EnumValue { get; set; }

        [SQLiteColumnType(SQLiteColumnType.TEXT)]
        public string SetValue { get; set; }

        [SQLiteColumnType(SQLiteColumnType.TEXT)]
        public StatusEnum Status { get; set; }

        [SQLiteColumnType(SQLiteColumnType.INTEGER)]
        public int ForeignKeyId { get; set; }
    }

    [SQLiteTableName("SQLiteMockTable2")]
    private class MockModel2
    {
        [SQLiteColumnType(SQLiteColumnType.INTEGER)]
        [SQLiteColumnPrimaryKey()]
        public int Id { get; set; }

        [SQLiteColumnType(SQLiteColumnType.TEXT)]
        [SQLiteColumnNotNull]
        public string Name { get; set; }

        [SQLiteColumnType(SQLiteColumnType.REAL)]
        public decimal Price { get; set; }
    }

    [SQLiteTableName("SQLiteMockTable3")]
    private class MockModel3
    {
        [SQLiteColumnType(SQLiteColumnType.INTEGER)]
        [SQLiteColumnPrimaryKey]
        public int Id { get; set; }

        [SQLiteColumnType(SQLiteColumnType.TEXT)]
        [SQLiteColumnNotNull]
        public string Name { get; set; }

        [SQLiteColumnType(SQLiteColumnType.REAL)]
        public decimal Price { get; set; }

        [SQLiteColumnType(SQLiteColumnType.INTEGER)]
        public bool IsActive { get; set; }
    }

    [Test]
    public void GenerateCreateTableCommand1_ShouldGenerateSql_WithoutExecuting()
    {
        // SQL generation only — no file I/O
        var sqlGenerator = new InMemorySQLiteTableGenerator();
        var sql  = sqlGenerator.GenerateSqlTable<MockModel>();
        var sql1 = sqlGenerator.GenerateSqlTable<MockModel2>();
        var sql2 = sqlGenerator.GenerateSqlTable<MockModel3>();

        Assert.That(sql,  Does.Contain("\"SQLiteMockTable\""));
        Assert.That(sql1, Does.Contain("\"SQLiteMockTable2\""));
        Assert.That(sql2, Does.Contain("\"SQLiteMockTable3\""));
    }

    [Test]
    public void GenerateCreateTableCommand1_InMemory_CreateTables()
    {
        // Uses :memory: SQLite — no external dependency
        var sqlGenerator = new InMemorySQLiteTableGenerator();
        sqlGenerator.GenerateSqlTable<MockModel>(ifNotExists: true);
        sqlGenerator.GenerateSqlTable<MockModel2>(ifNotExists: true);
        sqlGenerator.GenerateSqlTable<MockModel3>(ifNotExists: true);
        sqlGenerator.CreateTables(); // executes against :memory:
    }

    [Test]
    [Category("Integration")]
    public void GenerateCreateTableCommand1_Integration_CreateTables()
    {
        var sqlGenerator = new SQLiteTableGenerator("Data Source=appdb.sqlite");
        sqlGenerator.GenerateSqlTable<MockModel>(ifNotExists: true);
        sqlGenerator.GenerateSqlTable<MockModel2>(ifNotExists: true);
        sqlGenerator.GenerateSqlTable<MockModel3>(ifNotExists: true);
        sqlGenerator.CreateDatabase(); // SQLite için no-op — dosya ilk bağlantıda otomatik oluşur
        sqlGenerator.CreateTables();
    }

    [Test]
    [Category("Integration")]
    public void CreateDatabase_Integration_ShouldBeIdempotent()
    {
        // SQLite için CreateDatabase() no-op'tur, her zaman başarılı olmalı
        var sqlGenerator = new SQLiteTableGenerator("Data Source=appdb.sqlite");

        Assert.DoesNotThrow(() => sqlGenerator.CreateDatabase()); // no-op
        Assert.DoesNotThrow(() => sqlGenerator.CreateDatabase()); // no-op
    }

    // ── ALTER TABLE — SQL üretim testleri (DB bağlantısı yok) ───────────────

    [Test]
    public void AlterTable_AddColumn_ShouldGenerateCorrectSql()
    {
        var generator = new InMemorySQLiteTableGenerator();
        var sql = generator.GenerateAddColumnSql<MockModel3>("IsActive");

        // ALTER TABLE "SQLiteMockTable3" ADD "IsActive" INTEGER;
        Assert.That(sql, Does.Contain("ALTER TABLE"));
        Assert.That(sql, Does.Contain("\"SQLiteMockTable3\""));
        Assert.That(sql, Does.Contain("ADD"));
        Assert.That(sql, Does.Contain("\"IsActive\""));
        Assert.That(sql, Does.Contain("INTEGER"));
    }

    [Test]
    public void AlterTable_DropColumn_ShouldGenerateCorrectSql()
    {
        var generator = new InMemorySQLiteTableGenerator();
        var sql = generator.GenerateDropColumnSql<MockModel3>("IsActive");

        Assert.That(sql, Does.Contain("ALTER TABLE"));
        Assert.That(sql, Does.Contain("\"SQLiteMockTable3\""));
        Assert.That(sql, Does.Contain("DROP COLUMN"));
        Assert.That(sql, Does.Contain("\"IsActive\""));
    }

    [Test]
    public void AlterTable_RenameColumn_ShouldGenerateCorrectSql()
    {
        var generator = new InMemorySQLiteTableGenerator();
        var sql = generator.GenerateRenameColumnSql<MockModel3>("Name", "FullName");

        // SQLite 3.25+ standard RENAME COLUMN syntax
        Assert.That(sql, Does.Contain("ALTER TABLE"));
        Assert.That(sql, Does.Contain("\"SQLiteMockTable3\""));
        Assert.That(sql, Does.Contain("RENAME COLUMN"));
        Assert.That(sql, Does.Contain("\"Name\""));
        Assert.That(sql, Does.Contain("\"FullName\""));
    }

    [Test]
    public void AlterTable_AlterColumnType_ShouldThrowNotSupported()
    {
        // SQLite ALTER COLUMN TYPE'ı desteklemez — NotSupportedException fırlatmalı
        var generator = new InMemorySQLiteTableGenerator();
        Assert.Throws<NotSupportedException>(() => generator.GenerateAlterColumnTypeSql<MockModel3>("Price"));
    }

    [Test]
    public void AlterTable_AddColumn_WithNotNull_ShouldIncludeConstraint()
    {
        var generator = new InMemorySQLiteTableGenerator();
        var sql = generator.GenerateAddColumnSql<MockModel3>("Name");

        Assert.That(sql, Does.Contain("NOT NULL"));
        Assert.That(sql, Does.Contain("TEXT"));
    }

    [Test]
    public void GenerateTruncateTableSql_ShouldUseDeleteFromBecauseSQLiteHasNoTruncate()
    {
        var generator = new InMemorySQLiteTableGenerator();
        var sql = generator.GenerateTruncateTableSql<MockModel>();

        Assert.That(sql, Is.EqualTo("DELETE FROM \"SQLiteMockTable\";"));
    }

    [Test]
    public void AlterTable_AddColumn_UnknownColumn_ShouldThrow()
    {
        var generator = new InMemorySQLiteTableGenerator();
        Assert.Catch<Exception>(() => generator.GenerateAddColumnSql<MockModel3>("NonExistent"));
    }

    [Test]
    public void AlterTable_Integration_InMemory_AddDropColumn()
    {
        // :memory: SQLite ile gerçek ALTER TABLE çalıştır
        using var generator = new InMemorySQLiteTableGenerator();
        generator.GenerateSqlTable<MockModel3>(ifNotExists: true);
        generator.CreateTables();

        Assert.DoesNotThrow(() => generator.DropColumn<MockModel3>("IsActive", DestructiveOperationOptions.Allow()),  "DropColumn");
        Assert.DoesNotThrow(() => generator.AddColumn<MockModel3>("IsActive"),   "AddColumn");
    }

    [Test]
    public void AlterTable_Integration_InMemory_RenameColumn()
    {
        using var generator = new InMemorySQLiteTableGenerator();
        generator.GenerateSqlTable<MockModel3>(ifNotExists: true);
        generator.CreateTables();

        Assert.DoesNotThrow(() => generator.RenameColumn<MockModel3>("Name", "FullName"), "RenameColumn");
        Assert.DoesNotThrow(() => generator.RenameColumn<MockModel3>("FullName", "Name"), "RenameColumn (geri al)");
    }
}
