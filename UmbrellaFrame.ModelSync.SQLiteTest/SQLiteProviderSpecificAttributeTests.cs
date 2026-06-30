using NUnit.Framework;
using UmbrellaFrame.ModelSync.SQLite;

[TestFixture]
public sealed class SQLiteProviderSpecificAttributeTests
{
    [SQLiteTableName("ProviderSpecificProducts")]
    private sealed class Product
    {
        [SQLiteColumnType(SQLiteColumnType.INTEGER)]
        public int Id { get; set; }

        [SQLiteColumnType(SQLiteColumnType.TEXT)]
        [SQLiteColumnDefault(SQLiteDefaultExpression.CurrentTimestamp)]
        public string CreatedAt { get; set; }

        [SQLiteColumnType(SQLiteColumnType.TEXT)]
        [SQLiteColumnDefault(SQLiteDefaultExpression.CurrentDate)]
        public string CreatedDate { get; set; }

        [SQLiteColumnType(SQLiteColumnType.TEXT)]
        [SQLiteColumnDefault(SQLiteDefaultExpression.CurrentTime)]
        public string CreatedTime { get; set; }

        [SQLiteColumnType(SQLiteColumnType.INTEGER)]
        [SQLiteColumnDefaultSql("(1 + 2)")]
        public int RawDefault { get; set; }

        [SQLiteColumnType(SQLiteColumnType.TEXT)]
        [SQLiteColumnCheck("Name <> ''")]
        [SQLiteColumnIndex("IX_ProviderSpecificProducts_Name")]
        public string Name { get; set; }
    }

    [Test]
    public void ProviderSpecificAttributes_ShouldParticipateInCreateTableAndIndexSql()
    {
        using var generator = new InMemorySQLiteTableGenerator();

        var createSql = generator.GenerateSqlTable<Product>();
        var indexSql = generator.GenerateIndexSql<Product>();

        Assert.That(createSql, Does.Contain("DEFAULT CURRENT_TIMESTAMP"));
        Assert.That(createSql, Does.Contain("DEFAULT CURRENT_DATE"));
        Assert.That(createSql, Does.Contain("DEFAULT CURRENT_TIME"));
        Assert.That(createSql, Does.Contain("DEFAULT (1 + 2)"));
        Assert.That(createSql, Does.Contain("CHECK (Name <> '')"));
        Assert.That(indexSql, Has.One.Contains("IX_ProviderSpecificProducts_Name"));
    }
}
