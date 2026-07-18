using Npgsql;
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.PostgreSQL;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
internal sealed class LegacyCompatibilityFixtureAttribute : Attribute
{
    public LegacyCompatibilityFixtureAttribute(string provider) => Provider = provider;
    public string Provider { get; }
}

[TestFixture]
[Category("Integration")]
[LegacyCompatibilityFixture("postgresql")]
public sealed class PostgresLegacyCompatibilityIntegrationTests
{
    private const string RunVariable = "MODELSYNC_RUN_POSTGRES_INTEGRATION";
    private const string ConnectionStringVariable = "MODELSYNC_POSTGRES_CONNECTION_STRING";

    [Test]
    public async Task CompareReadOnly()
    {
        RequireIntegration();
        var plans = await CreateRunner().CompareRegisteredAsync();

        Assert.That(plans.Any(p => p.Definition.Category == MigrationScriptCategory.Seeds), Is.True);
    }

    [Test]
    public async Task LegacyUpgradeFirstRun()
    {
        RequireIntegration();
        var result = await CreateRunner().RunWithResultAsync();

        Assert.That(result.Succeeded, Is.True);
    }

    [Test]
    public async Task LegacyUpgradeSecondRun()
    {
        RequireIntegration();
        var runner = CreateRunner();

        await runner.RunWithResultAsync();
        var second = await runner.RunWithResultAsync();

        Assert.That(second.Succeeded, Is.True);
    }

    [Test]
    public async Task ChangedResourceRun()
    {
        RequireIntegration();
        var plans = await CreateRunner("INSERT INTO public.modelsync_legacy_seed(name) VALUES ('changed');").CompareRegisteredAsync();

        Assert.That(plans.Any(p => p.Definition.Category == MigrationScriptCategory.Seeds), Is.True);
    }

    [Test]
    public void FailureSafetyRun()
    {
        RequireIntegration();

        Assert.That(MigrationCompatibilityProfiles.LegacyEmbeddedSql, Is.EqualTo("LegacyEmbeddedSql"));
    }

    [Test]
    public async Task TransactionFailure_ShouldRollbackTableAndHistory()
    {
        RequireIntegration();
        var suffix = Guid.NewGuid().ToString("N");
        var table = "modelsync_tx_" + suffix;
        var scriptId = "tx-" + suffix;
        var runner = new PostgresMigrationRunner(GetConnectionString());
        runner.RegisterScript(MigrationScriptDefinition.Create(
            scriptId,
            "Transactional rollback probe",
            MigrationScriptCategory.Tables,
            $"CREATE TABLE public.\"{table}\"(id INTEGER PRIMARY KEY); INSERT INTO public.modelsync_missing_target(id) VALUES (1);"));

        var result = await runner.RunWithResultAsync();

        Assert.That(result.State, Is.EqualTo(MigrationExecutionState.RolledBack));
        Assert.That(result.TransactionStarted, Is.True);
        Assert.That(result.Items.Single().RollbackSucceeded, Is.True);

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT to_regclass(@name) IS NOT NULL;";
        command.Parameters.AddWithValue("name", "public." + table);
        Assert.That(Convert.ToBoolean(await command.ExecuteScalarAsync()), Is.False);

        command.Parameters.Clear();
        command.CommandText = "SELECT COUNT(*) FROM sec.\"SchemaMigration_Tables\" WHERE \"Id\" = @id;";
        command.Parameters.AddWithValue("id", scriptId);
        Assert.That(Convert.ToInt32(await command.ExecuteScalarAsync()), Is.EqualTo(0));
    }

    private static PostgresMigrationRunner CreateRunner(string seedSql = "CREATE TABLE IF NOT EXISTS public.modelsync_legacy_seed(id SERIAL PRIMARY KEY, name TEXT NOT NULL);")
    {
        var runner = new PostgresMigrationRunner(GetConnectionString(), MigrationRunnerOptions.Default()
            .ApplyCompatibilityProfile(MigrationCompatibilityProfiles.LegacyEmbeddedSql));
        runner.RegisterScript(MigrationScriptDefinition.Create("001", "LegacySeed", MigrationScriptCategory.Seeds, seedSql));
        return runner;
    }

    private static void RequireIntegration()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(RunVariable), "1", StringComparison.OrdinalIgnoreCase))
            Assert.Ignore($"Set {RunVariable}=1 to run PostgreSQL legacy compatibility integration tests.");
    }

    private static string GetConnectionString()
        => Environment.GetEnvironmentVariable(ConnectionStringVariable)
           ?? "Host=localhost;Port=15432;Database=modelsync_integration;Username=modelsync_test;Password=ModelSync_Pass123;";
}
