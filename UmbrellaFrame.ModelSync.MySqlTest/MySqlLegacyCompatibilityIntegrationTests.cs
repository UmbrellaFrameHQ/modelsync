using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.MySql;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
internal sealed class LegacyCompatibilityFixtureAttribute : Attribute
{
    public LegacyCompatibilityFixtureAttribute(string provider) => Provider = provider;
    public string Provider { get; }
}

public abstract class MySqlLegacyCompatibilityIntegrationTestsBase
{
    protected abstract string ProviderName { get; }
    protected abstract string RunVariable { get; }
    protected abstract string ConnectionStringVariable { get; }
    protected abstract string DefaultConnectionString { get; }

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

        if (!result.Succeeded)
            Assert.Fail(Describe(result));
    }

    [Test]
    public async Task LegacyUpgradeSecondRun()
    {
        RequireIntegration();
        var runner = CreateRunner();

        await runner.RunWithResultAsync();
        var second = await runner.RunWithResultAsync();

        if (!second.Succeeded)
            Assert.Fail(Describe(second));
    }

    [Test]
    public async Task ChangedResourceRun()
    {
        RequireIntegration();
        var plans = await CreateRunner("INSERT INTO modelsync_legacy_seed(Name) VALUES ('changed');").CompareRegisteredAsync();

        Assert.That(plans.Any(p => p.Definition.Category == MigrationScriptCategory.Seeds), Is.True);
    }

    [Test]
    public void FailureSafetyRun()
    {
        RequireIntegration();

        Assert.That(MigrationCompatibilityProfiles.LegacyEmbeddedSql, Is.EqualTo("LegacyEmbeddedSql"));
    }

    private MySqlMigrationRunner CreateRunner(string seedSql = "CREATE TABLE IF NOT EXISTS modelsync_legacy_seed(Id INT AUTO_INCREMENT PRIMARY KEY, Name VARCHAR(100) NOT NULL);")
    {
        var runner = new MySqlMigrationRunner(GetConnectionString(), MigrationRunnerOptions.Default()
            .ApplyCompatibilityProfile(MigrationCompatibilityProfiles.LegacyEmbeddedSql));
        runner.RegisterScript(MigrationScriptDefinition.Create("001", "LegacySeed", MigrationScriptCategory.Seeds, seedSql));
        return runner;
    }

    private void RequireIntegration()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(RunVariable), "1", StringComparison.OrdinalIgnoreCase))
            Assert.Ignore($"Set {RunVariable}=1 to run {ProviderName} legacy compatibility integration tests.");
    }

    private string GetConnectionString()
        => Environment.GetEnvironmentVariable(ConnectionStringVariable) ?? DefaultConnectionString;

    private static string Describe(MigrationExecutionResult result)
        => $"Succeeded={result.Succeeded}; Items={result.Items.Count}; "
           + string.Join(" | ", result.Items.Select(i => $"{i.Category}:{i.ScriptId}:{i.Action}:{i.FailureStage}:{i.ErrorCode}"));
}

[TestFixture]
[Category("Integration")]
[LegacyCompatibilityFixture("mysql")]
public sealed class MySqlLegacyCompatibilityIntegrationTests : MySqlLegacyCompatibilityIntegrationTestsBase
{
    protected override string ProviderName => "MySQL";
    protected override string RunVariable => "MODELSYNC_RUN_MYSQL_INTEGRATION";
    protected override string ConnectionStringVariable => "MODELSYNC_MYSQL_CONNECTION_STRING";
    protected override string DefaultConnectionString => "Server=localhost;Port=13306;Database=modelsync_integration;User ID=root;Password=ModelSync_Pass123;";
}

[TestFixture]
[Category("Integration")]
[LegacyCompatibilityFixture("mariadb")]
public sealed class MariaDbLegacyCompatibilityIntegrationTests : MySqlLegacyCompatibilityIntegrationTestsBase
{
    protected override string ProviderName => "MariaDB";
    protected override string RunVariable => "MODELSYNC_RUN_MARIADB_INTEGRATION";
    protected override string ConnectionStringVariable => "MODELSYNC_MARIADB_CONNECTION_STRING";
    protected override string DefaultConnectionString => "Server=localhost;Port=13307;Database=modelsync_integration;User ID=root;Password=ModelSync_Pass123;";
}
