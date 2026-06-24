using Microsoft.Data.Sqlite;
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.SQLite;

namespace UmbrellaFrame.ModelSync.SQLiteTest;

public class SQLiteMigrationRunnerTests
{
    [Test]
    public async Task RunAsync_ShouldApplyOrderedScriptsAndRecordHistory()
    {
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"modelsync-{Guid.NewGuid():N}.db");
        var runner = new SQLiteMigrationRunner($"Data Source={path}");

        runner.RegisterScript(MigrationScriptDefinition.Create(
            "001",
            "CreateUsers",
            MigrationScriptCategory.Tables,
            "CREATE TABLE Users(Id INTEGER PRIMARY KEY, Name TEXT);"));

        runner.RegisterScript(MigrationScriptDefinition.Create(
            "010",
            "AuditUsers",
            MigrationScriptCategory.Triggers,
            "CREATE TRIGGER trg_users_ai AFTER INSERT ON Users BEGIN SELECT 1; END;"));

        runner.RegisterScript(MigrationScriptDefinition.Create(
            "020",
            "SeedUsers",
            MigrationScriptCategory.Seeds,
            "INSERT INTO Users(Name) VALUES ('Ada');"));

        var plans = await runner.RunAsync();

        Assert.That(plans.Count(x => x.HasChanges), Is.EqualTo(3));
        using var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Users;";
        Assert.That(Convert.ToInt32(await command.ExecuteScalarAsync()), Is.EqualTo(1));

        command.CommandText = "SELECT COUNT(*) FROM SchemaMigration_Tables;";
        Assert.That(Convert.ToInt32(await command.ExecuteScalarAsync()), Is.EqualTo(1));
        command.CommandText = "SELECT COUNT(*) FROM SchemaMigration_Triggers;";
        Assert.That(Convert.ToInt32(await command.ExecuteScalarAsync()), Is.EqualTo(1));
        command.CommandText = "SELECT COUNT(*) FROM SchemaMigration_Seeds;";
        Assert.That(Convert.ToInt32(await command.ExecuteScalarAsync()), Is.EqualTo(1));
    }

    [Test]
    public void RunAsync_ShouldRejectStoredProcedures()
    {
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"modelsync-{Guid.NewGuid():N}.db");
        var runner = new SQLiteMigrationRunner($"Data Source={path}");
        runner.RegisterScript(MigrationScriptDefinition.Create(
            "010",
            "UnsupportedProcedure",
            MigrationScriptCategory.StoredProcedures,
            "CREATE PROCEDURE p AS SELECT 1;"));

        Assert.ThrowsAsync<NotSupportedException>(async () => await runner.RunAsync());
    }

    [Test]
    public async Task RunAsync_ShouldAddMissingColumnsWhenTableScriptChanges()
    {
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"modelsync-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path}";

        var first = new SQLiteMigrationRunner(connectionString);
        first.RegisterScript(MigrationScriptDefinition.Create(
            "001",
            "CreateUsers",
            MigrationScriptCategory.Tables,
            "CREATE TABLE Users(Id INTEGER PRIMARY KEY);"));
        await first.RunAsync();

        var second = new SQLiteMigrationRunner(connectionString);
        second.RegisterScript(MigrationScriptDefinition.Create(
            "001",
            "CreateUsers",
            MigrationScriptCategory.Tables,
            "CREATE TABLE Users(Id INTEGER PRIMARY KEY, Name TEXT NULL);"));

        var plans = await second.RunAsync();

        Assert.That(plans.Single().ChangeType, Is.EqualTo(MigrationChangeType.Reapply));
        using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Users') WHERE name = 'Name';";
        Assert.That(Convert.ToInt32(await command.ExecuteScalarAsync()), Is.EqualTo(1));
    }
}
