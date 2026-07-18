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

        var ex = Assert.ThrowsAsync<MigrationExecutionException>(async () => await runner.RunAsync());
        Assert.That(ex!.Item!.FailureStage, Is.EqualTo("ProviderCapability"));
    }

    [Test]
    public async Task ChangedTableScript_ShouldExposeRepairWithoutAdvancingHistoryOrMutatingDatabase()
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

        var second = new SQLiteMigrationRunner(connectionString, new MigrationRunnerOptions
        {
            AutoAddMissingColumnsFromTableScripts = true
        });
        second.RegisterScript(MigrationScriptDefinition.Create(
            "001",
            "CreateUsers",
            MigrationScriptCategory.Tables,
            "CREATE TABLE Users(Id INTEGER PRIMARY KEY, Name TEXT NULL);"));

        var plans = await second.CompareRegisteredAsync();

        Assert.That(plans.Single().ChangeType, Is.EqualTo(MigrationChangeType.Reapply));
        Assert.That(plans.Single().RequiresManualReview, Is.True);
        Assert.That(plans.Single().PlannedExecutionSql, Is.Empty);
        Assert.That(plans.Single().RepairSql.Single(), Does.Contain("ADD COLUMN"));
        Assert.That(plans.Single().HistoryDecision, Is.EqualTo(MigrationHistoryDecision.ManualReviewRequired));

        var result = await second.RunWithResultAsync();
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Items.Single().Action, Is.EqualTo(MigrationExecutionAction.Blocked));

        using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Users') WHERE name = 'Name';";
        Assert.That(Convert.ToInt32(await command.ExecuteScalarAsync()), Is.EqualTo(0));

        command.CommandText = "SELECT SqlHash FROM SchemaMigration_Tables WHERE Id = '001';";
        Assert.That(await command.ExecuteScalarAsync(), Is.EqualTo(plans.Single().CurrentHash));
    }

    [Test]
    public async Task ChangedTableScript_DefaultPolicy_ShouldRequireManualReviewWithoutRepair()
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

        var plans = await second.CompareRegisteredAsync();

        Assert.That(plans.Single().ChangeType, Is.EqualTo(MigrationChangeType.Reapply));
        Assert.That(plans.Single().RequiresManualReview, Is.True);
        Assert.That(plans.Single().PlannedExecutionSql, Is.Empty);
        Assert.That(plans.Single().RepairSql, Is.Empty);
        Assert.That(plans.Single().HistoryDecision, Is.EqualTo(MigrationHistoryDecision.ManualReviewRequired));
        Assert.That(plans.Single().UnappliedDrift, Has.Some.Contains("repair is disabled"));

        var result = await second.RunWithResultAsync();
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Items.Single().Action, Is.EqualTo(MigrationExecutionAction.Blocked));

        using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Users') WHERE name = 'Name';";
        Assert.That(Convert.ToInt32(await command.ExecuteScalarAsync()), Is.EqualTo(0));

        command.CommandText = "SELECT SqlHash FROM SchemaMigration_Tables WHERE Id = '001';";
        Assert.That(await command.ExecuteScalarAsync(), Is.EqualTo(plans.Single().CurrentHash));
    }

    [Test]
    public async Task CompareRegisteredAsync_ShouldBeReadOnlyWhenHistoryTablesAreMissing()
    {
        var cs = $"Data Source={Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var keepAlive = new SqliteConnection(cs);
        await keepAlive.OpenAsync();

        var runner = new SQLiteMigrationRunner(cs);
        runner.RegisterScript(MigrationScriptDefinition.Create(
            "001",
            "CreateUsers",
            MigrationScriptCategory.Tables,
            "CREATE TABLE Users(Id INTEGER PRIMARY KEY);"));

        var plans = await runner.CompareRegisteredAsync();

        Assert.That(plans.Single().ChangeType, Is.EqualTo(MigrationChangeType.Apply));

        await using var command = keepAlive.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name LIKE 'SchemaMigration_%';";
        Assert.That(Convert.ToInt32(await command.ExecuteScalarAsync()), Is.EqualTo(0));
    }

    [Test]
    public async Task RunWithResultAsync_ShouldRollbackScriptAndHistoryWhenBatchFails()
    {
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"modelsync-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path}";
        var runner = new SQLiteMigrationRunner(connectionString);

        runner.RegisterScript(MigrationScriptDefinition.Create(
            "001",
            "CreateThenFail",
            MigrationScriptCategory.Tables,
            "CREATE TABLE RollbackProbe(Id INTEGER PRIMARY KEY);\nINSERT INTO MissingTable(Id) VALUES (1);"));

        var result = await runner.RunWithResultAsync();

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.State, Is.EqualTo(MigrationExecutionState.RolledBack));
        Assert.That(result.TransactionStarted, Is.True);
        Assert.That(result.Items.Single().Action, Is.EqualTo(MigrationExecutionAction.Failed));
        Assert.That(result.Items.Single().RollbackSucceeded, Is.True);

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'RollbackProbe';";
        Assert.That(Convert.ToInt32(await command.ExecuteScalarAsync()), Is.EqualTo(0));

        command.CommandText = "SELECT COUNT(*) FROM SchemaMigration_Tables WHERE Id = '001';";
        Assert.That(Convert.ToInt32(await command.ExecuteScalarAsync()), Is.EqualTo(0));
    }

    [Test]
    public async Task RunWithResultAsync_ShouldRespectSQLiteImmediateWriteLock()
    {
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"modelsync-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path};Default Timeout=1";

        await using var blocker = new SqliteConnection(connectionString);
        await blocker.OpenAsync();
        await using (var command = blocker.CreateCommand())
        {
            command.CommandText = "BEGIN IMMEDIATE;";
            await command.ExecuteNonQueryAsync();
        }

        try
        {
            var runner = new SQLiteMigrationRunner(connectionString);
            runner.RegisterScript(MigrationScriptDefinition.Create(
                "001",
                "CreateLocked",
                MigrationScriptCategory.Tables,
                "CREATE TABLE LockedProbe(Id INTEGER PRIMARY KEY);"));

            var result = await runner.RunWithResultAsync();

            Assert.That(result.Succeeded, Is.False);
        }
        finally
        {
            await using var command = blocker.CreateCommand();
            command.CommandText = "ROLLBACK;";
            await command.ExecuteNonQueryAsync();
        }

        var retry = new SQLiteMigrationRunner(connectionString);
        retry.RegisterScript(MigrationScriptDefinition.Create(
            "001",
            "CreateLocked",
            MigrationScriptCategory.Tables,
            "CREATE TABLE LockedProbe(Id INTEGER PRIMARY KEY);"));

        var retryResult = await retry.RunWithResultAsync();

        Assert.That(retryResult.Succeeded, Is.True);
    }

    [Test]
    public async Task ConcurrentSQLiteRunners_ShouldRecordMigrationOnceAndConverge()
    {
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"modelsync-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path};Default Timeout=5";
        var first = new SQLiteMigrationRunner(connectionString);
        var second = new SQLiteMigrationRunner(connectionString);
        var script = MigrationScriptDefinition.Create(
            "001",
            "CreateConcurrentProbe",
            MigrationScriptCategory.Tables,
            "CREATE TABLE ConcurrentProbe(Id INTEGER PRIMARY KEY);");
        first.RegisterScript(script);
        second.RegisterScript(script);

        var results = await Task.WhenAll(first.RunWithResultAsync(), second.RunWithResultAsync());

        Assert.That(results.Count(result => result.Succeeded), Is.GreaterThanOrEqualTo(1));

        var verification = new SQLiteMigrationRunner(connectionString);
        verification.RegisterScript(script);
        var converged = await verification.RunWithResultAsync();
        Assert.That(converged.Succeeded, Is.True);
        Assert.That(converged.Items.Single().Action, Is.EqualTo(MigrationExecutionAction.Skipped));

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'ConcurrentProbe';";
        Assert.That(Convert.ToInt32(await command.ExecuteScalarAsync()), Is.EqualTo(1));
        command.CommandText = "SELECT COUNT(*) FROM SchemaMigration_Tables WHERE Id = '001';";
        Assert.That(Convert.ToInt32(await command.ExecuteScalarAsync()), Is.EqualTo(1));
    }

    [Test]
    public void CompareRegisteredAsync_ShouldFailFastForDuplicateScriptIdInSameCategory()
    {
        var runner = new SQLiteMigrationRunner($"Data Source={Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        runner.RegisterScript(MigrationScriptDefinition.Create("001", "A", MigrationScriptCategory.Tables, "SELECT 1;", "a.sql"));
        runner.RegisterScript(MigrationScriptDefinition.Create("001", "B", MigrationScriptCategory.Tables, "SELECT 2;", "b.sql"));

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => await runner.CompareRegisteredAsync());

        Assert.That(ex!.Message, Does.Contain("Duplicate migration script id '001'"));
        Assert.That(ex.Message, Does.Contain("a.sql"));
        Assert.That(ex.Message, Does.Contain("b.sql"));
    }
}
