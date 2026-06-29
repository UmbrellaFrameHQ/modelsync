using System;
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.Services;

namespace UmbrellaFrame.ModelSync.CoreTest;

public class ModelSchemaReaderTests
{
    [FakeTableName("provider_products")]
    private sealed class ProviderProduct
    {
        [FakeColumnType("INT")]
        [FakePrimaryKey("PRIMARY KEY GENERATED")]
        public int Id { get; set; }

        [DbColumnName("ProductCode")]
        [FakeColumnType("VARCHAR(32)")]
        public string Code { get; set; } = string.Empty;

        [DbIgnore]
        [FakeColumnType("INT")]
        public int Ignored { get; set; }
    }

    [OtherTableName("other_products")]
    private sealed class OtherProviderProduct
    {
        [OtherColumnType("INTEGER")]
        public int Id { get; set; }
    }

    [FakeTableName("same_table")]
    private sealed class DuplicateA
    {
        [FakeColumnType("INT")]
        public int Id { get; set; }
    }

    [FakeTableName("same_table")]
    private sealed class DuplicateB
    {
        [FakeColumnType("INT")]
        public int Id { get; set; }
    }

    [Test]
    public void FromTypes_WithProviderAttributeFilter_ShouldIgnoreOtherProviders()
    {
        var tables = ModelSchemaReader.FromTypes(
            "app",
            typeof(FakeColumnTypeAttribute),
            typeof(FakeTableNameAttribute),
            typeof(ProviderProduct),
            typeof(OtherProviderProduct));

        Assert.That(tables, Has.Count.EqualTo(1));
        Assert.That(tables[0].Name, Is.EqualTo("provider_products"));
        Assert.That(tables[0].Columns[0].StoreType, Is.EqualTo("INT"));
        Assert.That(tables[0].Columns.Any(c => c.Name == "Ignored"), Is.False);
        Assert.That(tables[0].Columns.Any(c => c.Name == "ProductCode"), Is.True);
    }

    [Test]
    public void FromTypes_WithProviderAttributeSet_ShouldReadStructuredValueGeneration()
    {
        var provider = new ProviderAttributeSet(
            typeof(FakeTableNameAttribute),
            typeof(FakeColumnTypeAttribute),
            typeof(FakePrimaryKeyAttribute),
            typeof(FakeNotNullAttribute),
            typeof(FakeUniqueAttribute),
            typeof(FakeForeignKeyAttribute),
            (pk, column) => DbValueGenerationKind.Identity);

        var table = ModelSchemaReader.FromTypes("app", provider, typeof(ProviderProduct)).Single();

        Assert.That(table.Columns.Single(c => c.Name == "Id").ValueGeneration, Is.EqualTo(DbValueGenerationKind.Identity));
    }

    [Test]
    public void FromTypes_WhenTwoModelsMapToSameTable_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ModelSchemaReader.FromTypes(
                "app",
                typeof(FakeColumnTypeAttribute),
                typeof(FakeTableNameAttribute),
                typeof(DuplicateA),
                typeof(DuplicateB)));
    }

    [AttributeUsage(AttributeTargets.Property)]
    private sealed class FakeColumnTypeAttribute : DbColumnTypeAttribute
    {
        private readonly string _columnType;

        public FakeColumnTypeAttribute(string columnType)
            : base(columnType)
        {
            _columnType = columnType;
        }

        public override string GetColumnType()
            => _columnType;
    }

    [AttributeUsage(AttributeTargets.Property)]
    private sealed class OtherColumnTypeAttribute : DbColumnTypeAttribute
    {
        private readonly string _columnType;

        public OtherColumnTypeAttribute(string columnType)
            : base(columnType)
        {
            _columnType = columnType;
        }

        public override string GetColumnType()
            => _columnType;
    }

    [AttributeUsage(AttributeTargets.Class)]
    private sealed class FakeTableNameAttribute : DbTableNameAttribute
    {
        public FakeTableNameAttribute(string tableName)
            : base(tableName)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    private sealed class FakePrimaryKeyAttribute : DbColumnPrimaryKeyAttribute
    {
        private readonly string _snippet;

        public FakePrimaryKeyAttribute(string snippet)
        {
            _snippet = snippet;
        }

        public override string GetSqlSnippet()
            => _snippet;
    }

    private sealed class FakeNotNullAttribute : DbColumnNotNullAttribute
    {
        public override string GetSqlSnippet()
            => "NOT NULL";
    }

    private sealed class FakeUniqueAttribute : DbColumnUniqueAttribute
    {
        public override string GetSqlSnippet()
            => "UNIQUE";
    }

    private sealed class FakeForeignKeyAttribute : DbColumnForeignKeyAttribute
    {
        public FakeForeignKeyAttribute()
            : base("Id", "Other", "Id")
        {
        }

        public override string GetSqlSnippet()
            => "FOREIGN KEY (Id) REFERENCES Other(Id)";
    }

    [AttributeUsage(AttributeTargets.Class)]
    private sealed class OtherTableNameAttribute : DbTableNameAttribute
    {
        public OtherTableNameAttribute(string tableName)
            : base(tableName)
        {
        }
    }
}
