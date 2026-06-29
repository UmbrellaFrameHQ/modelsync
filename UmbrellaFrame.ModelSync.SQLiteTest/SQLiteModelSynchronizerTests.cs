using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.SQLite;

namespace UmbrellaFrame.ModelSync.SQLiteTest;

public class SQLiteModelSynchronizerTests
{
    [SQLiteTableName("sync_products")]
    private sealed class ProductSchema
    {
        [SQLiteColumnType(SQLiteColumnType.INTEGER)]
        [SQLiteColumnPrimaryKey]
        public int Id { get; set; }

        [SQLiteColumnType(SQLiteColumnType.TEXT)]
        public string Name { get; set; } = string.Empty;
    }

    [SQLiteTableName("sync_products")]
    private sealed class ProductSchemaWithRiskyColumn
    {
        [SQLiteColumnType(SQLiteColumnType.INTEGER)]
        [SQLiteColumnPrimaryKey]
        public int Id { get; set; }

        [SQLiteColumnType(SQLiteColumnType.TEXT)]
        public string Name { get; set; } = string.Empty;

        [SQLiteColumnType(SQLiteColumnType.TEXT)]
        [SQLiteColumnNotNull]
        public string Code { get; set; } = string.Empty;
    }

    [SQLiteTableName("sync_indexed_products")]
    private sealed class IndexedProductSchema
    {
        [SQLiteColumnType(SQLiteColumnType.INTEGER)]
        [SQLiteColumnPrimaryKey]
        public int Id { get; set; }

        [SQLiteColumnType(SQLiteColumnType.TEXT)]
        [DbColumnIndex]
        public string Code { get; set; } = string.Empty;
    }

    [SQLiteTableName("sync_unique_products")]
    private sealed class UniqueProductSchema
    {
        [SQLiteColumnType(SQLiteColumnType.INTEGER)]
        [SQLiteColumnPrimaryKey]
        public int Id { get; set; }

        [SQLiteColumnType(SQLiteColumnType.TEXT)]
        [SQLiteColumnUnique]
        public string Code { get; set; } = string.Empty;
    }

    [SQLiteTableName("manual_customers")]
    private sealed class ManualCustomerSchema
    {
        [SQLiteColumnType(SQLiteColumnType.INTEGER)]
        [SQLiteColumnPrimaryKey]
        public int Id { get; set; }
    }

    [SQLiteTableName("automatic_audit_logs")]
    private sealed class AutomaticAuditLogSchema
    {
        [SQLiteColumnType(SQLiteColumnType.INTEGER)]
        [SQLiteColumnPrimaryKey]
        public int Id { get; set; }

        [SQLiteColumnType(SQLiteColumnType.TEXT)]
        public string Message { get; set; } = string.Empty;
    }

    [SQLiteTableName("automatic_notifications")]
    private sealed class AutomaticNotificationSchema
    {
        [SQLiteColumnType(SQLiteColumnType.INTEGER)]
        [SQLiteColumnPrimaryKey]
        public int Id { get; set; }

        [SQLiteColumnType(SQLiteColumnType.INTEGER)]
        [SQLiteColumnForeignKey("CustomerId", "manual_customers", "Id")]
        public int CustomerId { get; set; }
    }

    [Test]
    public async Task ApplyAsync_ShouldCreateMissingTable()
    {
        var cs = $"Data Source={System.Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var keepAlive = new SqliteConnection(cs);
        await keepAlive.OpenAsync();

        var options = new SQLiteModelSyncOptions { ConnectionString = cs };
        var result = await SQLiteModelSynchronizer
            .FromTypes(options, typeof(ProductSchema))
            .CompareAsync();

        Assert.That(result.Operations.Any(x => x.ChangeType == ModelSyncChangeType.CreateTable), Is.True);

        await result.ApplyAsync();

        await using var command = keepAlive.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'sync_products';";
        var count = (long)(await command.ExecuteScalarAsync())!;
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public async Task CompareAsync_ShouldBlockNotNullColumnWithoutDefault()
    {
        var cs = $"Data Source={System.Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var keepAlive = new SqliteConnection(cs);
        await keepAlive.OpenAsync();

        var initial = new SQLiteModelSyncOptions { ConnectionString = cs };
        await SQLiteModelSynchronizer
            .FromTypes(initial, typeof(ProductSchema))
            .CompareAsync()
            .ContinueWith(t => t.Result.ApplyAsync())
            .Unwrap();

        var risky = new SQLiteModelSyncOptions { ConnectionString = cs };
        var result = await SQLiteModelSynchronizer
            .FromTypes(risky, typeof(ProductSchemaWithRiskyColumn))
            .CompareAsync();

        Assert.That(result.BlockedOperations.Any(x =>
            x.ChangeType == ModelSyncChangeType.AddColumn &&
            x.Column == "Code" &&
            x.Risk == ModelSyncOperationRisk.Risky), Is.True);
    }

    [Test]
    public async Task CompareAsync_ShouldReportExtraDatabaseTableAsBlockedDropTable()
    {
        var cs = $"Data Source={System.Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var keepAlive = new SqliteConnection(cs);
        await keepAlive.OpenAsync();

        await using (var command = keepAlive.CreateCommand())
        {
            command.CommandText = "CREATE TABLE orphan_table(Id INTEGER);";
            await command.ExecuteNonQueryAsync();
        }

        var options = new SQLiteModelSyncOptions { ConnectionString = cs, ReportUnmappedTables = true };
        var result = await SQLiteModelSynchronizer
            .FromTypes(options, typeof(ProductSchema))
            .CompareAsync();

        Assert.That(result.BlockedOperations.Any(x =>
            x.ChangeType == ModelSyncChangeType.DropTable &&
            x.Table == "orphan_table" &&
            x.Risk == ModelSyncOperationRisk.Destructive), Is.True);
    }

    [Test]
    public async Task CompareAsync_ShouldIgnoreExtraDatabaseTableByDefault()
    {
        var cs = $"Data Source={System.Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var keepAlive = new SqliteConnection(cs);
        await keepAlive.OpenAsync();

        await using (var command = keepAlive.CreateCommand())
        {
            command.CommandText = "CREATE TABLE orphan_table(Id INTEGER);";
            await command.ExecuteNonQueryAsync();
        }

        var options = new SQLiteModelSyncOptions { ConnectionString = cs };
        var result = await SQLiteModelSynchronizer
            .FromTypes(options, typeof(ProductSchema))
            .CompareAsync();

        Assert.That(result.BlockedOperations.Any(x => x.ChangeType == ModelSyncChangeType.DropTable), Is.False);
    }

    [Test]
    public async Task CompareAsync_WhenTableIsMissing_ShouldPlanIndexesInSameResult()
    {
        var cs = $"Data Source={System.Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var keepAlive = new SqliteConnection(cs);
        await keepAlive.OpenAsync();

        var options = new SQLiteModelSyncOptions { ConnectionString = cs };
        var result = await SQLiteModelSynchronizer
            .FromTypes(options, typeof(IndexedProductSchema))
            .CompareAsync();

        Assert.That(result.SafeOperations.Any(x => x.ChangeType == ModelSyncChangeType.CreateTable), Is.True);
        Assert.That(result.SafeOperations.Any(x => x.ChangeType == ModelSyncChangeType.AddIndex), Is.True);
    }

    [Test]
    public async Task CompareAsync_WhenSafeOperationIsDisabled_ShouldSkipWithoutBlockingOtherSafeOperations()
    {
        var cs = $"Data Source={System.Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var keepAlive = new SqliteConnection(cs);
        await keepAlive.OpenAsync();

        var options = new SQLiteModelSyncOptions
        {
            ConnectionString = cs,
            AddMissingIndexes = false
        };
        var result = await SQLiteModelSynchronizer
            .FromTypes(options, typeof(IndexedProductSchema))
            .CompareAsync();

        Assert.That(result.SafeOperations.Any(x => x.ChangeType == ModelSyncChangeType.CreateTable), Is.True);
        Assert.That(result.SkippedOperations.Any(x => x.ChangeType == ModelSyncChangeType.AddIndex), Is.True);
        Assert.That(result.BlockedOperations, Is.Empty);
    }

    [Test]
    public async Task CompareAsync_ShouldUseSemanticUniqueIndexInsteadOfGeneratedName()
    {
        var cs = $"Data Source={System.Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var keepAlive = new SqliteConnection(cs);
        await keepAlive.OpenAsync();

        await using (var command = keepAlive.CreateCommand())
        {
            command.CommandText = @"
CREATE TABLE sync_unique_products(Id INTEGER PRIMARY KEY, Code TEXT);
CREATE UNIQUE INDEX UX_ManuallyNamed_Code ON sync_unique_products(Code);";
            await command.ExecuteNonQueryAsync();
        }

        var options = new SQLiteModelSyncOptions
        {
            ConnectionString = cs,
            AddMissingConstraints = true
        };
        var result = await SQLiteModelSynchronizer
            .FromTypes(options, typeof(UniqueProductSchema))
            .CompareAsync();

        Assert.That(result.Operations.Any(x => x.ChangeType == ModelSyncChangeType.AddUniqueConstraint), Is.False);
    }

    [Test]
    public async Task CompareAsync_WithRegisteredScript_ShouldNotCreateHistoryInfrastructure()
    {
        var cs = $"Data Source={System.Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var keepAlive = new SqliteConnection(cs);
        await keepAlive.OpenAsync();

        var script = new MigrationScriptDefinition
        {
            Category = MigrationScriptCategory.CustomSql,
            Id = "001",
            Name = "CreateProbe",
            Source = "inline",
            Sql = "CREATE TABLE probe(Id INTEGER);"
        };

        var options = new SQLiteModelSyncOptions { ConnectionString = cs };
        var result = await SQLiteModelSynchronizer
            .FromTypes(options, typeof(ProductSchema))
            .AddSqlScript(script)
            .CompareAsync();

        Assert.That(result.Operations.Any(x => x.ChangeType == ModelSyncChangeType.ApplySqlScript), Is.True);

        await using var command = keepAlive.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name LIKE 'SchemaMigration_%';";
        var count = (long)(await command.ExecuteScalarAsync())!;
        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public async Task CompareAsync_WithMixedTablePolicies_ShouldKeepManualTablesOutOfAutomaticApply()
    {
        var cs = $"Data Source={System.Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var keepAlive = new SqliteConnection(cs);
        await keepAlive.OpenAsync();

        var options = new SQLiteModelSyncOptions
        {
            ConnectionString = cs,
            AddMissingConstraints = true
        };
        options.TablePolicies
            .ForType<ManualCustomerSchema>(ModelSyncTableMode.ManualOnly)
            .ForType<AutomaticAuditLogSchema>(ModelSyncTableMode.ApplySafeChanges)
            .ForType<AutomaticNotificationSchema>(ModelSyncTableMode.ApplySafeChanges);

        var result = await SQLiteModelSynchronizer
            .FromTypes(options, typeof(ManualCustomerSchema), typeof(AutomaticAuditLogSchema), typeof(AutomaticNotificationSchema))
            .CompareAsync();

        Assert.That(result.ManualOperations.Any(x => x.ChangeType == ModelSyncChangeType.CreateTable && x.Table == "manual_customers"), Is.True);
        Assert.That(result.AutomaticOperations.Any(x => x.ChangeType == ModelSyncChangeType.CreateTable && x.Table == "automatic_audit_logs"), Is.True);
        Assert.That(result.AutomaticOperations.Any(x => x.ChangeType == ModelSyncChangeType.CreateTable && x.Table == "automatic_notifications"), Is.True);
        var planDump = string.Join(" | ", result.Operations.Select(x => $"{x.Disposition}:{x.Risk}:{x.ChangeType}:{x.Table}:{x.Column}:{x.Reason}"));
        Assert.That(result.BlockedOperations.Any(x =>
            x.ChangeType == ModelSyncChangeType.AddForeignKey &&
            x.Table == "automatic_notifications" &&
            x.Reason == "Required manual dependency 'main.manual_customers' does not exist."), Is.True, planDump);

        await using (var command = keepAlive.CreateCommand())
        {
            command.CommandText = "CREATE TABLE manual_customers(Id INTEGER PRIMARY KEY);";
            await command.ExecuteNonQueryAsync();
        }

        var applyOptions = new SQLiteModelSyncOptions
        {
            ConnectionString = cs,
            AddMissingConstraints = false
        };
        applyOptions.TablePolicies
            .ForType<ManualCustomerSchema>(ModelSyncTableMode.ManualOnly)
            .ForType<AutomaticAuditLogSchema>(ModelSyncTableMode.ApplySafeChanges)
            .ForType<AutomaticNotificationSchema>(ModelSyncTableMode.ApplySafeChanges);

        var second = await SQLiteModelSynchronizer
            .FromTypes(applyOptions, typeof(ManualCustomerSchema), typeof(AutomaticAuditLogSchema), typeof(AutomaticNotificationSchema))
            .CompareAsync();

        Assert.That(second.BlockedOperations, Is.Empty);
        Assert.That(second.SkippedOperations.Any(x => x.ChangeType == ModelSyncChangeType.AddForeignKey), Is.True);

        await second.ApplyAsync();

        Assert.That(await TableExistsAsync(keepAlive, "manual_customers"), Is.True);
        Assert.That(await TableExistsAsync(keepAlive, "automatic_audit_logs"), Is.True);
        Assert.That(await TableExistsAsync(keepAlive, "automatic_notifications"), Is.True);
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string table)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $table;";
        command.Parameters.AddWithValue("$table", table);
        var count = (long)(await command.ExecuteScalarAsync())!;
        return count == 1;
    }
}
