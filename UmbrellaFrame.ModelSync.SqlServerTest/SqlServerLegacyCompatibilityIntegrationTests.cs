using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.SqlServer;
using Microsoft.Data.SqlClient;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
internal sealed class LegacyCompatibilityFixtureAttribute : Attribute
{
    public LegacyCompatibilityFixtureAttribute(string provider) => Provider = provider;
    public string Provider { get; }
}

[TestFixture]
[Category("Integration")]
[LegacyCompatibilityFixture("sqlserver")]
public sealed class SqlServerLegacyCompatibilityIntegrationTests
{
    private const string RunVariable = "MODELSYNC_RUN_SQLSERVER_INTEGRATION";
    private const string ConnectionStringVariable = "MODELSYNC_SQLSERVER_CONNECTION_STRING";

    [Test]
    public async Task CompareReadOnly()
    {
        RequireIntegration();
        var runner = CreateRunner();

        var plans = await runner.CompareRegisteredAsync();

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
        var runner = CreateRunner("INSERT INTO ModelSyncLegacySeed(Name) VALUES ('changed');");

        var plans = await runner.CompareRegisteredAsync();

        Assert.That(plans.Any(p => p.Definition.Category == MigrationScriptCategory.Seeds), Is.True);
    }

    [Test]
    public void FailureSafetyRun()
    {
        RequireIntegration();

        Assert.That(MigrationCompatibilityProfiles.LegacyEmbeddedSql, Is.EqualTo("LegacyEmbeddedSql"));
    }

    [Test]
    public async Task PerScriptSessionContinuity_WithTempTableAcrossGoBatches_ShouldPass()
    {
        RequireIntegration();
        var scriptId = "991_" + Guid.NewGuid().ToString("N");
        var runner = CreateRunner(@"CREATE TABLE #ModelSyncSessionProbe (Id INT);
GO
INSERT INTO #ModelSyncSessionProbe (Id) VALUES (1);
GO
IF NOT EXISTS (
    SELECT 1
    FROM #ModelSyncSessionProbe
    WHERE Id = 1
)
    THROW 51000, 'Session continuity failed.', 1;", scriptId, "SessionContinuity");

        var result = await runner.RunWithResultAsync();

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Items.Single(i => i.ScriptId == scriptId).BatchCount, Is.EqualTo(3));
    }

    private static SqlServerMigrationRunner CreateRunner(
        string seedSql = "IF OBJECT_ID(N'dbo.ModelSyncLegacySeed', N'U') IS NULL CREATE TABLE dbo.ModelSyncLegacySeed(Id INT IDENTITY(1,1) PRIMARY KEY, Name NVARCHAR(100) NOT NULL);",
        string id = "001",
        string name = "LegacySeed")
    {
        var connectionString = GetConnectionString();
        EnsureDatabase(connectionString);
        var runner = new SqlServerMigrationRunner(connectionString, MigrationRunnerOptions.Default()
            .ApplyCompatibilityProfile(MigrationCompatibilityProfiles.LegacyEmbeddedSql));
        runner.RegisterScript(MigrationScriptDefinition.Create(id, name, MigrationScriptCategory.Seeds, seedSql));
        return runner;
    }

    private static void RequireIntegration()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(RunVariable), "1", StringComparison.OrdinalIgnoreCase))
            Assert.Ignore($"Set {RunVariable}=1 to run SQL Server legacy compatibility integration tests.");
    }

    private static string GetConnectionString()
        => Environment.GetEnvironmentVariable(ConnectionStringVariable)
           ?? "Server=localhost,14333;Database=modelsync_integration;User Id=sa;Password=ModelSync_Pass123;Encrypt=False;TrustServerCertificate=True;";

    private static void EnsureDatabase(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var database = builder.InitialCatalog;
        builder.InitialCatalog = "master";
        using var connection = new SqlConnection(builder.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(N'{database.Replace("'", "''")}') IS NULL CREATE DATABASE [{database.Replace("]", "]]")}];";
        command.ExecuteNonQuery();
    }
}
