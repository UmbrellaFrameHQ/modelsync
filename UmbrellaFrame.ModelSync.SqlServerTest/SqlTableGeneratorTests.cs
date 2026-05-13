using NUnit.Framework;
using UmbrellaFrame.ModelSync.SqlServer;

[TestFixture]
public class SqlTableGeneratorTests
{
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
        [SqlServerColumnPrimaryKey]
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
        [SqlServerColumnForeignKey("ForeignKeyId", "ReferencedTable", "ReferencedColumn")]
        public int ForeignKeyId { get; set; }
    }

    /// <summary>Sadece ADD/DROP/RENAME/ALTER SQL üretimini test eden model.</summary>
    [SqlServerTableName("AlterTestTable")]
    private class AlterModel
    {
        [SqlServerColumnType(SqlServerColumnType.INT)]
        [SqlServerColumnPrimaryKey]
        public int Id { get; set; }

        [SqlServerColumnType(SqlServerColumnType.NVARCHAR, "255")]
        [SqlServerColumnNotNull]
        public string Name { get; set; }

        [SqlServerColumnType(SqlServerColumnType.DECIMAL, "10,2")]
        public decimal Price { get; set; }

        [SqlServerColumnType(SqlServerColumnType.BIT)]
        public bool IsActive { get; set; }

        [SqlServerColumnType(SqlServerColumnType.NVARCHAR, "100")]
        [SqlServerColumnNotNull]
        public string Email { get; set; }
    }

    [Test]
    public void GenerateCreateTableCommand_ShouldGenerateCorrectSql()
    {
        var sqlGenerator = new FakeSqlServerTableGenerator();
        var sql = sqlGenerator.GenerateSqlTable<MockModel>();

        Assert.That(sql, Does.Contain("[SqlServerMockTable]"));
        Assert.That(sql, Does.Contain("[Id] INT"));
        Assert.That(sql, Does.Contain("[Name] NVARCHAR(255) NOT NULL"));
        Assert.That(sql, Does.Contain("[Price] DECIMAL(10,2)"));
        Assert.That(sql, Does.Contain("[IsActive] BIT"));
        Assert.That(sql, Does.Contain("PRIMARY KEY"));
        Assert.That(sql, Does.Contain("FOREIGN KEY"));
    }

    // ── ALTER TABLE — SQL üretim testleri (DB bağlantısı yok) ───────────────

    [Test]
    public void AlterTable_AddColumn_ShouldGenerateCorrectSql()
    {
        var generator = new FakeSqlServerTableGenerator();

        var sql = generator.GenerateAddColumnSql<AlterModel>("IsActive");

        // ALTER TABLE [AlterTestTable] ADD [IsActive] BIT;
        Assert.That(sql, Does.Contain("ALTER TABLE"));
        Assert.That(sql, Does.Contain("[AlterTestTable]"));
        Assert.That(sql, Does.Contain("ADD"));
        Assert.That(sql, Does.Contain("[IsActive]"));
        Assert.That(sql, Does.Contain("BIT"));
    }

    [Test]
    public void AlterTable_AddColumn_WithNotNull_ShouldIncludeNotNullConstraint()
    {
        var generator = new FakeSqlServerTableGenerator();

        var sql = generator.GenerateAddColumnSql<AlterModel>("Email");

        // NOT NULL kısıtı attribute'tan okunmalı
        Assert.That(sql, Does.Contain("NOT NULL"));
        Assert.That(sql, Does.Contain("NVARCHAR(100)"));
    }

    [Test]
    public void AlterTable_DropColumn_ShouldGenerateCorrectSql()
    {
        var generator = new FakeSqlServerTableGenerator();

        var sql = generator.GenerateDropColumnSql<AlterModel>("IsActive");

        // ALTER TABLE [AlterTestTable] DROP COLUMN [IsActive];
        Assert.That(sql, Does.Contain("ALTER TABLE"));
        Assert.That(sql, Does.Contain("[AlterTestTable]"));
        Assert.That(sql, Does.Contain("DROP COLUMN"));
        Assert.That(sql, Does.Contain("[IsActive]"));
    }

    [Test]
    public void AlterTable_RenameColumn_ShouldUseSpRename()
    {
        var generator = new FakeSqlServerTableGenerator();

        var sql = generator.GenerateRenameColumnSql<AlterModel>("Name", "FullName");

        // SQL Server sp_rename kullanır
        Assert.That(sql, Does.Contain("sp_rename"));
        Assert.That(sql, Does.Contain("AlterTestTable.Name"));
        Assert.That(sql, Does.Contain("FullName"));
        Assert.That(sql, Does.Contain("COLUMN"));
    }

    [Test]
    public void AlterTable_AlterColumnType_ShouldGenerateCorrectSql()
    {
        var generator = new FakeSqlServerTableGenerator();

        var sql = generator.GenerateAlterColumnTypeSql<AlterModel>("Price");

        // ALTER TABLE [AlterTestTable] ALTER COLUMN [Price] DECIMAL(10,2);
        Assert.That(sql, Does.Contain("ALTER TABLE"));
        Assert.That(sql, Does.Contain("[AlterTestTable]"));
        Assert.That(sql, Does.Contain("ALTER COLUMN"));
        Assert.That(sql, Does.Contain("[Price]"));
        Assert.That(sql, Does.Contain("DECIMAL(10,2)"));
    }

    [Test]
    public void AlterTable_AddColumn_UnknownColumn_ShouldThrow()
    {
        var generator = new FakeSqlServerTableGenerator();

        // Model'de olmayan kolon adı verilirse exception fırlatmalı
        Assert.Catch<Exception>(() => generator.GenerateAddColumnSql<AlterModel>("NonExistentColumn"));
    }

    [Test]
    public void AlterTable_AlterColumnType_UnknownColumn_ShouldThrow()
    {
        var generator = new FakeSqlServerTableGenerator();

        Assert.Catch<Exception>(() => generator.GenerateAlterColumnTypeSql<AlterModel>("NonExistentColumn"));
    }
}
