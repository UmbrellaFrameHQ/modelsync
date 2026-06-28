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
}
