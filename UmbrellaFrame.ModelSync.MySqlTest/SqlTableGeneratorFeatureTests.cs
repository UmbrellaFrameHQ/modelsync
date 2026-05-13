using System;
using System.Collections.Generic;
using NUnit.Framework;
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.Services;
using UmbrellaFrame.ModelSync.MySql;

[TestFixture]
public class SqlTableGeneratorFeatureTests
{
    // ── Test models ─────────────────────────────────────────────────────────

    [MySqlTableName("products")]
    private class ProductModel
    {
        [MySqlColumnType(MySqlColumnType.INT)]
        [MySqlColumnPrimaryKey(true)]
        public int Id { get; set; }

        [MySqlColumnType(MySqlColumnType.VARCHAR, "255")]
        [MySqlColumnNotNull]
        [DbColumnIndex("idx_products_name")]
        public string Name { get; set; }

        [MySqlColumnType(MySqlColumnType.DECIMAL, "10,2")]
        [DbColumnDefault("0.00")]
        [DbColumnCheck("Price > 0")]
        public decimal Price { get; set; }

        [MySqlColumnType(MySqlColumnType.DATETIME)]
        [DbColumnDefault("CURRENT_TIMESTAMP")]
        public DateTime CreatedAt { get; set; }

        [MySqlColumnType(MySqlColumnType.VARCHAR, "50")]
        [DbColumnIndex("idx_products_sku", isUnique: true)]
        public string Sku { get; set; }
    }

    [MySqlTableName("orders")]
    private class OrderModel
    {
        [MySqlColumnType(MySqlColumnType.INT)]
        [MySqlColumnPrimaryKey(true)]
        public int Id { get; set; }

        [MySqlColumnType(MySqlColumnType.INT)]
        [MySqlForeignKey("ProductId", "products", "Id")]
        public int ProductId { get; set; }
    }

    private FakeMySqlTableGenerator _generator;

    [SetUp]
    public void SetUp()
    {
        _generator = new FakeMySqlTableGenerator();
    }

    // ── IF NOT EXISTS ────────────────────────────────────────────────────────

    [Test]
    public void GenerateSqlTable_WithIfNotExists_ContainsGuard()
    {
        var sql = _generator.GenerateSqlTable<ProductModel>(ifNotExists: true);
        Assert.That(sql, Does.Contain("IF NOT EXISTS"));
    }

    [Test]
    public void GenerateSqlTable_WithoutIfNotExists_DoesNotContainGuard()
    {
        var sql = _generator.GenerateSqlTable<ProductModel>(ifNotExists: false);
        Assert.That(sql, Does.Not.Contain("IF NOT EXISTS"));
    }

    // ── Quoting ──────────────────────────────────────────────────────────────

    [Test]
    public void GenerateSqlTable_MySql_UsesBacktickQuoting()
    {
        var sql = _generator.GenerateSqlTable<ProductModel>();
        Assert.That(sql, Does.Contain("`products`"));
        Assert.That(sql, Does.Contain("`Name`"));
    }

    // ── DbColumnDefault ──────────────────────────────────────────────────────

    [Test]
    public void GenerateSqlTable_WithDefault_ContainsDefaultClause()
    {
        var sql = _generator.GenerateSqlTable<ProductModel>();
        Assert.That(sql, Does.Contain("DEFAULT 0.00"));
        Assert.That(sql, Does.Contain("DEFAULT CURRENT_TIMESTAMP"));
    }

    // ── DbColumnCheck ────────────────────────────────────────────────────────

    [Test]
    public void GenerateSqlTable_WithCheck_ContainsCheckClause()
    {
        var sql = _generator.GenerateSqlTable<ProductModel>();
        Assert.That(sql, Does.Contain("CHECK (Price > 0)"));
    }

    // ── DbColumnIndex ────────────────────────────────────────────────────────

    [Test]
    public void GenerateIndexSql_ReturnsCorrectIndexStatements()
    {
        _generator.GenerateSqlTable<ProductModel>();
        var indexes = _generator.GenerateIndexSql<ProductModel>();

        Assert.That(indexes.Count, Is.EqualTo(2));
        Assert.That(indexes, Has.Some.Contains("idx_products_name"));
        Assert.That(indexes, Has.Some.Contains("UNIQUE").And.Contains("idx_products_sku"));
    }

    // ── DropTable ────────────────────────────────────────────────────────────

    [Test]
    public void GenerateDropTableSql_ReturnsDropStatement()
    {
        var sql = _generator.GenerateDropTableSql<ProductModel>();
        Assert.That(sql, Does.Contain("DROP TABLE"));
        Assert.That(sql, Does.Contain("products"));
    }

    // ── TruncateTable ────────────────────────────────────────────────────────

    [Test]
    public void GenerateTruncateTableSql_ReturnsTruncateStatement()
    {
        var sql = _generator.GenerateTruncateTableSql<ProductModel>();
        Assert.That(sql, Does.Contain("TRUNCATE TABLE"));
        Assert.That(sql, Does.Contain("products"));
    }

    // ── Property ordering ────────────────────────────────────────────────────

    [Test]
    public void GenerateSqlTable_ColumnsAreInDeclarationOrder()
    {
        var sql = _generator.GenerateSqlTable<ProductModel>();
        var idPos = sql.IndexOf("`Id`", StringComparison.Ordinal);
        var namePos = sql.IndexOf("`Name`", StringComparison.Ordinal);
        var pricePos = sql.IndexOf("`Price`", StringComparison.Ordinal);

        Assert.That(idPos, Is.LessThan(namePos));
        Assert.That(namePos, Is.LessThan(pricePos));
    }

    // ── Cache isolation ──────────────────────────────────────────────────────

    [Test]
    public void SqlCache_IsPerInstance_NotSharedAcrossGenerators()
    {
        var gen1 = new FakeMySqlTableGenerator();
        var gen2 = new FakeMySqlTableGenerator();

        gen1.GenerateSqlTable<ProductModel>();
        Assert.That(gen2.CacheCount, Is.EqualTo(0),
            "Caches must be per-instance; gen2 should not see gen1's cached SQL.");
    }

    // ── Async ────────────────────────────────────────────────────────────────

    [Test]
    public async System.Threading.Tasks.Task GenerateSqlTableAsync_ReturnsSameSqlAsSync()
    {
        var sync  = _generator.GenerateSqlTable<ProductModel>();
        var async = await _generator.GenerateSqlTableAsync<ProductModel>();
        Assert.That(async, Is.EqualTo(sync));
    }

    // ── ForeignKey ───────────────────────────────────────────────────────────

    [Test]
    public void GenerateSqlTable_WithForeignKey_ContainsForeignKeyClause()
    {
        var sql = _generator.GenerateSqlTable<OrderModel>();
        Assert.That(sql, Does.Contain("FOREIGN KEY"));
        Assert.That(sql, Does.Contain("products"));
    }

    // ── Missing attribute ────────────────────────────────────────────────────

    [MySqlTableName("bad")]
    private class BadModel
    {
        public int Id { get; set; } // no column type attribute
    }

    [Test]
    public void GenerateSqlTable_MissingColumnTypeAttribute_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => _generator.GenerateSqlTable<BadModel>());
    }
}

/// <summary>Testable subclass that exposes cache count without requiring a real DB connection.</summary>
internal class FakeMySqlTableGenerator : UmbrellaFrame.ModelSync.Core.Services.SqlTableGenerator
{
    protected override string QuoteIdentifier(string identifier) => $"`{identifier}`";
    protected override string IfNotExistsClause => "IF NOT EXISTS";

    public int CacheCount => SqlCache.Count;

    public void CreateTables() { }
    public System.Threading.Tasks.Task CreateTablesAsync(System.Threading.CancellationToken ct = default) => System.Threading.Tasks.Task.CompletedTask;
    public void DropTables() { }
    public System.Threading.Tasks.Task DropTablesAsync(System.Threading.CancellationToken ct = default) => System.Threading.Tasks.Task.CompletedTask;
}
