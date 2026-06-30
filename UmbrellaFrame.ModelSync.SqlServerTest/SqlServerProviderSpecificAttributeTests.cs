using NUnit.Framework;
using UmbrellaFrame.ModelSync.SqlServer;

[TestFixture]
public sealed class SqlServerProviderSpecificAttributeTests
{
    [SqlServerTableName("ProviderSpecificProducts")]
    private sealed class Product
    {
        [SqlServerColumnType(SqlServerColumnType.INT)]
        [SqlServerColumnPrimaryKey(true)]
        public int Id { get; set; }

        [SqlServerColumnType(SqlServerColumnType.UNIQUEIDENTIFIER)]
        [SqlServerColumnDefault(SqlServerDefaultExpression.NewSequentialId)]
        public Guid PublicId { get; set; }

        [SqlServerColumnType(SqlServerColumnType.DECIMAL, "10,2")]
        [SqlServerColumnDefault("0")]
        [SqlServerColumnCheck("Price >= 0")]
        public decimal Price { get; set; }

        [SqlServerColumnType(SqlServerColumnType.NVARCHAR, "128")]
        [SqlServerColumnIndex("IX_ProviderSpecificProducts_Name")]
        public string Name { get; set; }
    }

    [SqlServerTableName("ProviderSpecificDefaults")]
    private sealed class DefaultProduct
    {
        [SqlServerColumnType(SqlServerColumnType.INT)]
        [SqlServerColumnPrimaryKey(true)]
        public int Id { get; set; }

        [SqlServerColumnType(SqlServerColumnType.UNIQUEIDENTIFIER)]
        [SqlServerColumnDefault(SqlServerDefaultExpression.NewId)]
        public Guid NewIdValue { get; set; }

        [SqlServerColumnType(SqlServerColumnType.UNIQUEIDENTIFIER)]
        [SqlServerColumnDefault(SqlServerDefaultExpression.NewSequentialId)]
        public Guid SequentialIdValue { get; set; }

        [SqlServerColumnType(SqlServerColumnType.DATETIME)]
        [SqlServerColumnDefault(SqlServerDefaultExpression.GetDate)]
        public DateTime LocalDate { get; set; }

        [SqlServerColumnType(SqlServerColumnType.DATETIME)]
        [SqlServerColumnDefault(SqlServerDefaultExpression.GetUtcDate)]
        public DateTime UtcDate { get; set; }

        [SqlServerColumnType(SqlServerColumnType.DATETIME2)]
        [SqlServerColumnDefault(SqlServerDefaultExpression.SysDateTime)]
        public DateTime SystemDate { get; set; }

        [SqlServerColumnType(SqlServerColumnType.DATETIME2)]
        [SqlServerColumnDefault(SqlServerDefaultExpression.SysUtcDateTime)]
        public DateTime SystemUtcDate { get; set; }

        [SqlServerColumnType(SqlServerColumnType.INT)]
        [SqlServerColumnDefaultSql("ABS(-7)")]
        public int RawDefault { get; set; }
    }

    [Test]
    public void ProviderSpecificAttributes_ShouldParticipateInCreateTableAndIndexSql()
    {
        var generator = new FakeSqlServerTableGenerator();

        var createSql = generator.GenerateSqlTable<Product>();
        var indexSql = generator.GenerateIndexSql<Product>();

        Assert.That(createSql, Does.Contain("DEFAULT NEWSEQUENTIALID()"));
        Assert.That(createSql, Does.Contain("DEFAULT 0"));
        Assert.That(createSql, Does.Contain("CHECK (Price >= 0)"));
        Assert.That(indexSql, Has.One.Contains("IX_ProviderSpecificProducts_Name"));
    }

    [Test]
    public void ProviderSpecificDefaults_ShouldGenerateAllSqlServerDefaultExpressions()
    {
        var generator = new FakeSqlServerTableGenerator();

        var createSql = generator.GenerateSqlTable<DefaultProduct>();

        Assert.That(createSql, Does.Contain("DEFAULT NEWID()"));
        Assert.That(createSql, Does.Contain("DEFAULT NEWSEQUENTIALID()"));
        Assert.That(createSql, Does.Contain("DEFAULT GETDATE()"));
        Assert.That(createSql, Does.Contain("DEFAULT GETUTCDATE()"));
        Assert.That(createSql, Does.Contain("DEFAULT SYSDATETIME()"));
        Assert.That(createSql, Does.Contain("DEFAULT SYSUTCDATETIME()"));
        Assert.That(createSql, Does.Contain("DEFAULT ABS(-7)"));
    }
}
