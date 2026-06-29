using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.SqlGeneration;

namespace UmbrellaFrame.ModelSync.CoreTest;

public class ModelSyncSqlDialectTests
{
    [Test]
    public void FakeProviderDescriptor_ShouldCompileSqlWithoutCoreProviderSwitch()
    {
        var dialect = new ModelSyncSqlDialect(new ModelSyncProviderDescriptor
        {
            ProviderId = "fake-provider",
            OpenQuote = "{",
            CloseQuote = "}",
            SupportsSchemas = true,
            IdentityKeyword = "FAKE_IDENTITY({seed},{increment})",
            AddColumnKeyword = "ADD FIELD",
            AlterColumnTypeStyle = AlterColumnTypeStyle.ModifyColumn,
            HistoryStyle = HistorySqlStyle.ConflictUpdate,
            CatalogStyle = CatalogQueryStyle.StandardInformationSchema
        });

        var create = dialect.BuildCreateTableSql(ProductTable(DbValueGenerationKind.Identity));
        var addColumn = dialect.BuildAddColumnSql(ProductTable(), new ModelColumnDefinition { Name = "Code", StoreType = "TEXT" });
        var index = dialect.BuildCreateIndexSql(ProductTable(), new ModelColumnDefinition { Name = "Name", StoreType = "TEXT", IsIndexed = true });
        var history = dialect.BuildRecordHistoryPlan("meta", MigrationScriptCategory.Tables, "001", "Init", "hash");

        Assert.That(create, Does.StartWith("CREATE TABLE {app}.{Products} ("));
        Assert.That(create, Does.Contain("{Id} INT FAKE_IDENTITY(1,1) PRIMARY KEY"));
        Assert.That(addColumn, Is.EqualTo("ALTER TABLE {app}.{Products} ADD FIELD {Code} TEXT;"));
        Assert.That(index, Is.EqualTo("CREATE INDEX {idx_Products_Name} ON {app}.{Products} ({Name});"));
        Assert.That(history.CommandText, Does.Contain("ON CONFLICT({Id})"));
    }

    [Test]
    public void Descriptor_ShouldSupportSchemaLessFileProviderBehavior()
    {
        var dialect = new ModelSyncSqlDialect(new ModelSyncProviderDescriptor
        {
            ProviderId = "file-provider",
            SupportsSchemas = false,
            OmitSchemaInDdl = true,
            GeneratedValuePlacement = GeneratedValuePlacement.AfterInlinePrimaryKey,
            RowIdKeyword = "ROWID_TOKEN",
            SupportsAddForeignKey = false,
            SupportsAddCheckConstraint = false,
            SupportsAddDefaultConstraint = false,
            HistoryStyle = HistorySqlStyle.FileStoreConflictUpdate,
            CatalogStyle = CatalogQueryStyle.FilePragma
        });

        var create = dialect.BuildCreateTableSql(ProductTable(DbValueGenerationKind.RowIdAlias));
        var fk = dialect.BuildAddForeignKeySql(ProductTable(), new ModelColumnDefinition
        {
            Name = "CustomerId",
            StoreType = "INT",
            ForeignKeyTable = "Customers",
            ForeignKeyReferenceColumn = "Id"
        });

        Assert.That(create, Does.StartWith("CREATE TABLE \"Products\" ("));
        Assert.That(create, Does.Contain("\"Id\" INT PRIMARY KEY ROWID_TOKEN"));
        Assert.That(fk, Is.Empty);
    }

    [Test]
    public void DescriptorDrivenLockCompiler_ShouldCreateNativeLockPlans()
    {
        var application = new ModelSyncSqlDialect(new ModelSyncProviderDescriptor
        {
            ProviderId = "application-lock-provider",
            MigrationLockStyle = MigrationLockStyle.ApplicationRoutine
        });
        var named = new ModelSyncSqlDialect(new ModelSyncProviderDescriptor
        {
            ProviderId = "named-lock-provider",
            MigrationLockStyle = MigrationLockStyle.NamedRoutine
        });
        var advisory = new ModelSyncSqlDialect(new ModelSyncProviderDescriptor
        {
            ProviderId = "advisory-lock-provider",
            MigrationLockStyle = MigrationLockStyle.AdvisoryRoutine
        });
        var file = new ModelSyncSqlDialect(new ModelSyncProviderDescriptor
        {
            ProviderId = "file-lock-provider",
            MigrationLockStyle = MigrationLockStyle.FileImmediateTransaction
        });

        Assert.That(application.BuildAcquireMigrationLockPlan("resource", TimeSpan.FromSeconds(1)).CommandText, Does.Contain("sp_getapplock"));
        Assert.That(named.BuildAcquireMigrationLockPlan("resource", TimeSpan.FromSeconds(1)).CommandText, Does.Contain("GET_LOCK"));
        Assert.That(advisory.BuildAcquireMigrationLockPlan("resource", TimeSpan.FromSeconds(1)).CommandText, Does.Contain("pg_try_advisory_lock"));
        Assert.That(file.BuildAcquireMigrationLockPlan("resource", TimeSpan.FromSeconds(1)).CommandText, Is.EqualTo("BEGIN IMMEDIATE;"));
    }

    private static ModelTableDefinition ProductTable(DbValueGenerationKind generation = DbValueGenerationKind.None)
    {
        var table = new ModelTableDefinition { Schema = "app", Name = "Products" };
        table.Columns.Add(new ModelColumnDefinition
        {
            Name = "Id",
            StoreType = "INT",
            IsPrimaryKey = true,
            ValueGeneration = generation,
            IdentitySeed = generation == DbValueGenerationKind.Identity ? 1 : null,
            IdentityIncrement = generation == DbValueGenerationKind.Identity ? 1 : null
        });
        table.Columns.Add(new ModelColumnDefinition
        {
            Name = "Name",
            StoreType = "VARCHAR(200)",
            IsRequired = true
        });
        return table;
    }
}
