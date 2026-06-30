using NUnit.Framework;
using UmbrellaFrame.ModelSync.PostgreSQL;

[TestFixture]
public sealed class PostgresProviderSpecificAttributeTests
{
    [PostgresTableName("ProviderSpecificProducts")]
    private sealed class Product
    {
        [PostgresColumnType(PostgresColumnType.INTEGER)]
        [PostgresColumnPrimaryKey]
        public int Id { get; set; }

        [PostgresColumnType(PostgresColumnType.UUID)]
        [PostgresColumnDefault(PostgresDefaultExpression.GenRandomUuid)]
        public Guid PublicId { get; set; }

        [PostgresColumnType(PostgresColumnType.NUMERIC, "10,2")]
        [PostgresColumnDefault("0")]
        [PostgresColumnCheck("Price >= 0")]
        public decimal Price { get; set; }

        [PostgresColumnType(PostgresColumnType.VARCHAR, "128")]
        [PostgresColumnIndex("IX_ProviderSpecificProducts_Name")]
        public string Name { get; set; }
    }

    [PostgresTableName("ProviderSpecificDefaults")]
    private sealed class DefaultProduct
    {
        [PostgresColumnType(PostgresColumnType.INTEGER)]
        [PostgresColumnPrimaryKey]
        public int Id { get; set; }

        [PostgresColumnType(PostgresColumnType.UUID)]
        [PostgresColumnDefault(PostgresDefaultExpression.GenRandomUuid)]
        public Guid PublicId { get; set; }

        [PostgresColumnType(PostgresColumnType.TIMESTAMP)]
        [PostgresColumnDefault(PostgresDefaultExpression.CurrentTimestamp)]
        public DateTime CreatedAt { get; set; }

        [PostgresColumnType(PostgresColumnType.TIMESTAMP)]
        [PostgresColumnDefault(PostgresDefaultExpression.Now)]
        public DateTime UpdatedAt { get; set; }

        [PostgresColumnType(PostgresColumnType.INTEGER)]
        [PostgresColumnDefaultSql("(1 + 2)")]
        public int RawDefault { get; set; }
    }

    [Test]
    public void ProviderSpecificAttributes_ShouldParticipateInCreateTableAndIndexSql()
    {
        var generator = new FakePostgresTestGenerator();

        var createSql = generator.GenerateSqlTable<Product>();
        var indexSql = generator.GenerateIndexSql<Product>();

        Assert.That(createSql, Does.Contain("DEFAULT gen_random_uuid()"));
        Assert.That(createSql, Does.Contain("DEFAULT 0"));
        Assert.That(createSql, Does.Contain("CHECK (Price >= 0)"));
        Assert.That(indexSql, Has.One.Contains("IX_ProviderSpecificProducts_Name"));
    }

    [Test]
    public void ProviderSpecificDefaults_ShouldGenerateAllPostgresDefaultExpressions()
    {
        var generator = new FakePostgresTestGenerator();

        var createSql = generator.GenerateSqlTable<DefaultProduct>();

        Assert.That(createSql, Does.Contain("DEFAULT gen_random_uuid()"));
        Assert.That(createSql, Does.Contain("DEFAULT CURRENT_TIMESTAMP"));
        Assert.That(createSql, Does.Contain("DEFAULT NOW()"));
        Assert.That(createSql, Does.Contain("DEFAULT (1 + 2)"));
    }
}
