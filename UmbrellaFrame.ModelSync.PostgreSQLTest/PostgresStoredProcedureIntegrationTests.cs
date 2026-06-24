using Npgsql;
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.PostgreSQL;

[TestFixture]
public class PostgresStoredProcedureIntegrationTests
{
    private const string RunVariable = "MODELSYNC_RUN_SP_INTEGRATION";
    private const string ConnectionStringVariable = "MODELSYNC_POSTGRES_SP_CONNECTION_STRING";

    [Test]
    [Category("Integration")]
    public async Task SyncRegisteredAsync_CreatesProcedure_ThenDetectsNoChange()
    {
        RequireIntegration();
        var connectionString = GetConnectionString();
        new PostgresTableGenerator(connectionString).CreateDatabase();
        await DropProcedureAsync(connectionString);

        var sync = new PostgresStoredProcedureSynchronizer(connectionString);
        sync.RegisterProcedure(StoredProcedureDefinition.Create(
            "usp_modelsync_smoke",
            "CREATE PROCEDURE public.usp_modelsync_smoke()\nLANGUAGE plpgsql\nAS $$\nBEGIN\n    RAISE NOTICE 'ok';\nEND;\n$$;",
            schema: "public"));

        var first = await sync.SyncRegisteredAsync();
        var second = await sync.CompareRegisteredAsync();

        Assert.That(first.Single().ChangeType, Is.EqualTo(StoredProcedureChangeType.Create));
        Assert.That(second.Single().ChangeType, Is.EqualTo(StoredProcedureChangeType.None));
    }

    private static async Task DropProcedureAsync(string connectionString)
    {
        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        using var command = new NpgsqlCommand("DROP PROCEDURE IF EXISTS public.usp_modelsync_smoke();", connection);
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
           ?? "Host=localhost;Port=5433;Database=modelsync_sp;Username=postgres;Password=ModelSync_Pass123;";
}
