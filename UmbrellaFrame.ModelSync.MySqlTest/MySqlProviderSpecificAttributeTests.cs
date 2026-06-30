using NUnit.Framework;
using UmbrellaFrame.ModelSync.MySql;

[TestFixture]
public sealed class MySqlProviderSpecificAttributeTests
{
    [MySqlTableName("ProviderSpecificProducts")]
    private sealed class Product
    {
        [MySqlColumnType(MySqlColumnType.INT)]
        [MySqlColumnPrimaryKey(true)]
        public int Id { get; set; }

        [MySqlColumnType(MySqlColumnType.VARCHAR, "36")]
        [MySqlColumnDefault(MySqlDefaultExpression.Uuid)]
        public string PublicId { get; set; }

        [MySqlColumnType(MySqlColumnType.DECIMAL, "10,2")]
        [MySqlColumnDefault("0")]
        [MySqlColumnCheck("Price >= 0")]
        public decimal Price { get; set; }

        [MySqlColumnType(MySqlColumnType.VARCHAR, "128")]
        [MySqlColumnIndex("IX_ProviderSpecificProducts_Name")]
        public string Name { get; set; }
    }

    [MySqlTableName("ProviderSpecificDefaults")]
    private sealed class DefaultProduct
    {
        [MySqlColumnType(MySqlColumnType.INT)]
        [MySqlColumnPrimaryKey(true)]
        public int Id { get; set; }

        [MySqlColumnType(MySqlColumnType.VARCHAR, "36")]
        [MySqlColumnDefault(MySqlDefaultExpression.Uuid)]
        public string PublicId { get; set; }

        [MySqlColumnType(MySqlColumnType.DATETIME)]
        [MySqlColumnDefault(MySqlDefaultExpression.CurrentTimestamp)]
        public DateTime CreatedAt { get; set; }

        [MySqlColumnType(MySqlColumnType.INT)]
        [MySqlColumnDefaultSql("(1 + 2)")]
        public int RawDefault { get; set; }
    }

    [Test]
    public void ProviderSpecificAttributes_ShouldParticipateInCreateTableAndIndexSql()
    {
        var generator = new FakeMySqlTestGenerator();

        var createSql = generator.GenerateSqlTable<Product>();
        var indexSql = generator.GenerateIndexSql<Product>();

        Assert.That(createSql, Does.Contain("DEFAULT UUID()"));
        Assert.That(createSql, Does.Contain("DEFAULT 0"));
        Assert.That(createSql, Does.Contain("CHECK (Price >= 0)"));
        Assert.That(indexSql, Has.One.Contains("IX_ProviderSpecificProducts_Name"));
    }

    [Test]
    public void ProviderSpecificDefaults_ShouldGenerateAllMySqlDefaultExpressions()
    {
        var generator = new FakeMySqlTestGenerator();

        var createSql = generator.GenerateSqlTable<DefaultProduct>();

        Assert.That(createSql, Does.Contain("DEFAULT UUID()"));
        Assert.That(createSql, Does.Contain("DEFAULT CURRENT_TIMESTAMP"));
        Assert.That(createSql, Does.Contain("DEFAULT (1 + 2)"));
    }
}
