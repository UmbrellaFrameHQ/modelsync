using Microsoft.Data.Sqlite;
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.SQLite;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
internal sealed class LegacyCompatibilityFixtureAttribute : Attribute
{
    public LegacyCompatibilityFixtureAttribute(string provider) => Provider = provider;
    public string Provider { get; }
}

[TestFixture]
[Category("Integration")]
[LegacyCompatibilityFixture("sqlite")]
public sealed class SQLiteLegacyCompatibilityIntegrationTests
{
    [Test]
    public async Task CompareReadOnly()
    {
        var database = await CreateFixtureAsync();
        var runner = CreateRunner(database.ConnectionString);

        var compare = await runner.CompareRegisteredAsync();

        Assert.That(await HasSqlHashColumnAsync(database.ConnectionString, "SchemaMigration_Seeds"), Is.False);
        Assert.That(await TableExistsAsync(database.ConnectionString, "SchemaMigration_CustomSql"), Is.False);
        Assert.That(await CountAsync(database.ConnectionString, "SELECT COUNT(*) FROM LegacyUsers WHERE Name = 'duplicate';"), Is.EqualTo(1));
        Assert.That(compare.Single(p => p.Definition.Category == MigrationScriptCategory.Seeds).LegacyHashAdoptionRequired, Is.True);
    }

    [Test]
    public async Task LegacyUpgradeFirstRun()
    {
        var database = await CreateFixtureAsync();
        var runner = CreateRunner(database.ConnectionString);

        var result = await runner.RunWithResultAsync();

        Assert.That(result.Succeeded, Is.True);
        Assert.That(await HasSqlHashColumnAsync(database.ConnectionString, "SchemaMigration_Seeds"), Is.True);
        Assert.That(await HistoryHashAsync(database.ConnectionString, "SchemaMigration_Seeds", "001"), Is.Not.Empty);
        Assert.That(await CountAsync(database.ConnectionString, "SELECT COUNT(*) FROM LegacyUsers WHERE Name = 'duplicate';"), Is.EqualTo(1));
        Assert.That(await CountAsync(database.ConnectionString, "SELECT COUNT(*) FROM LegacyUsers WHERE Name = 'custom';"), Is.EqualTo(1));
        Assert.That(await CountAsync(database.ConnectionString, "SELECT COUNT(*) FROM SchemaMigration_CustomSql WHERE Id = '900';"), Is.EqualTo(1));
    }

    [Test]
    public async Task LegacyUpgradeSecondRun()
    {
        var database = await CreateFixtureAsync();
        var runner = CreateRunner(database.ConnectionString);

        await runner.RunWithResultAsync();
        var second = await runner.RunWithResultAsync();

        Assert.That(second.Succeeded, Is.True);
        Assert.That(await CountAsync(database.ConnectionString, "SELECT COUNT(*) FROM LegacyUsers WHERE Name = 'duplicate';"), Is.EqualTo(1));
        Assert.That(await CountAsync(database.ConnectionString, "SELECT COUNT(*) FROM LegacyUsers WHERE Name = 'custom';"), Is.EqualTo(1));
    }

    [Test]
    public async Task ChangedResourceRun()
    {
        var database = await CreateFixtureAsync();
        var runner = CreateRunner(database.ConnectionString);
        await runner.RunWithResultAsync();

        var changed = new SQLiteMigrationRunner(database.ConnectionString, LegacyOptions());
        changed.RegisterScript(MigrationScriptDefinition.Create(
            "001",
            "SeedUsers",
            MigrationScriptCategory.Seeds,
            "INSERT INTO LegacyUsers(Name) VALUES ('changed-seed');"));
        changed.RegisterScript(MigrationScriptDefinition.Create(
            "900",
            "CustomBootstrap",
            MigrationScriptCategory.CustomSql,
            "INSERT INTO LegacyUsers(Name) VALUES ('changed-custom');"));

        var plans = await changed.CompareRegisteredAsync();
        var seed = plans.Single(p => p.Definition.Category == MigrationScriptCategory.Seeds);
        var custom = plans.Single(p => p.Definition.Category == MigrationScriptCategory.CustomSql);

        Assert.That(seed.ChangeType, Is.EqualTo(MigrationChangeType.None));
        Assert.That(seed.DecisionReason, Does.Contain("RunOnce"));
        Assert.That(custom.ChangeType, Is.EqualTo(MigrationChangeType.Reapply));
        Assert.That(custom.ExecutionMode, Is.EqualTo(MigrationScriptExecutionMode.HashTracked));
    }

    [Test]
    public async Task FailureSafetyRun()
    {
        var database = await CreateFixtureAsync();
        var runner = new SQLiteMigrationRunner(database.ConnectionString, LegacyOptions());
        runner.RegisterScript(MigrationScriptDefinition.Create(
            "901",
            "BrokenCustom",
            MigrationScriptCategory.CustomSql,
            "INSERT INTO LegacyUsers(Name) VALUES ('before-failure'); SELECT * FROM MissingTable;"));

        var result = await runner.RunWithResultAsync();

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Items.Single().Action, Is.EqualTo(MigrationExecutionAction.Failed));
        Assert.That(await TableExistsAsync(database.ConnectionString, "SchemaMigration_CustomSql"), Is.True);
        Assert.That(await CountAsync(database.ConnectionString, "SELECT COUNT(*) FROM SchemaMigration_CustomSql WHERE Id = '901';"), Is.EqualTo(0));
    }

    private static SQLiteMigrationRunner CreateRunner(string connectionString)
    {
        var runner = new SQLiteMigrationRunner(connectionString, LegacyOptions());
        runner.RegisterScript(MigrationScriptDefinition.Create(
            "001",
            "SeedUsers",
            MigrationScriptCategory.Seeds,
            "INSERT INTO LegacyUsers(Name) VALUES ('duplicate');"));
        runner.RegisterScript(MigrationScriptDefinition.Create(
            "900",
            "CustomBootstrap",
            MigrationScriptCategory.CustomSql,
            "INSERT INTO LegacyUsers(Name) VALUES ('custom');"));
        return runner;
    }

    private static MigrationRunnerOptions LegacyOptions()
        => MigrationRunnerOptions.Default()
            .ApplyCompatibilityProfile(MigrationCompatibilityProfiles.LegacyEmbeddedSql);

    private static async Task<LegacyDatabase> CreateFixtureAsync()
    {
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"modelsync-legacy-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        foreach (var sql in new[]
        {
            "CREATE TABLE LegacyUsers(Id INTEGER PRIMARY KEY, Name TEXT NOT NULL);",
            "INSERT INTO LegacyUsers(Name) VALUES ('duplicate');",
            "CREATE TABLE SchemaMigration_Tables(Id TEXT NOT NULL PRIMARY KEY, Name TEXT NOT NULL, AppliedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, UpdateAt TEXT NULL);",
            "CREATE TABLE SchemaMigration_StoredProcedures(Id TEXT NOT NULL PRIMARY KEY, Name TEXT NOT NULL, AppliedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, UpdateAt TEXT NULL);",
            "CREATE TABLE SchemaMigration_Triggers(Id TEXT NOT NULL PRIMARY KEY, Name TEXT NOT NULL, AppliedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, UpdateAt TEXT NULL);",
            "CREATE TABLE SchemaMigration_Seeds(Id TEXT NOT NULL PRIMARY KEY, Name TEXT NOT NULL, AppliedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, UpdateAt TEXT NULL);",
            "INSERT INTO SchemaMigration_Seeds(Id, Name) VALUES ('001', 'SeedUsers');",
            "INSERT INTO SchemaMigration_Tables(Id, Name) VALUES ('orphan', 'OrphanTable');"
        })
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }

        return new LegacyDatabase(connectionString);
    }

    private static async Task<bool> HasSqlHashColumnAsync(string connectionString, string table)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table});";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), "SqlHash", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static async Task<bool> TableExistsAsync(string connectionString, string table)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", table);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }

    private static async Task<string> HistoryHashAsync(string connectionString, string table, string id)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT SqlHash FROM {table} WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return Convert.ToString(await command.ExecuteScalarAsync()) ?? string.Empty;
    }

    private static async Task<int> CountAsync(string connectionString, string sql)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private sealed class LegacyDatabase
    {
        public LegacyDatabase(string connectionString) => ConnectionString = connectionString;
        public string ConnectionString { get; }
    }
}
