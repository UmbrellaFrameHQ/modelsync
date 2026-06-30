using MySqlConnector;
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.MySql;

[TestFixture]
public class MySqlStoredProcedureIntegrationTests
{
    private const string RunVariable = "MODELSYNC_RUN_SP_INTEGRATION";
    private const string ConnectionStringVariable = "MODELSYNC_MYSQL_SP_CONNECTION_STRING";

    [Test]
    [Category("Integration")]
    public async Task SyncRegisteredAsync_CreatesProcedure_ThenDetectsNoChange()
    {
        RequireIntegration();
        var connectionString = GetConnectionString();
        new MySqlTableGenerator(connectionString).CreateDatabase();
        await DropProcedureAsync(connectionString);

        var sync = new MySqlStoredProcedureSynchronizer(connectionString);
        sync.RegisterProcedure(StoredProcedureDefinition.Create(
            "usp_ModelSyncSmoke",
            "CREATE PROCEDURE usp_ModelSyncSmoke()\nBEGIN\n    SELECT 1 AS Value;\nEND",
            schema: "modelsync_integration"));

        var first = await sync.SyncRegisteredAsync();
        var second = await sync.CompareRegisteredAsync();

        Assert.That(first.Single().ChangeType, Is.EqualTo(StoredProcedureChangeType.Create));
        Assert.That(second.Single().ChangeType, Is.EqualTo(StoredProcedureChangeType.None));
    }

    private static async Task DropProcedureAsync(string connectionString)
    {
        using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        using var command = new MySqlCommand("DROP PROCEDURE IF EXISTS usp_ModelSyncSmoke;", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static void RequireIntegration()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(RunVariable), "1", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Ignore($"Set {RunVariable}=1 to run stored procedure integration tests.");
        }
    }

    private static string GetConnectionString()
        => Environment.GetEnvironmentVariable(ConnectionStringVariable)
           ?? "Server=localhost;Port=3307;Database=modelsync_sp;User ID=root;Password=ModelSync_Pass123;";
}
