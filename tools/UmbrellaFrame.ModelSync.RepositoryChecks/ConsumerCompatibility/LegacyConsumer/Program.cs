using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.SqlServer;

[SqlServerTableName("LegacyProducts")]
public sealed class LegacyProduct
{
    [SqlServerColumnType(SqlServerColumnType.INT)]
    [SqlServerColumnPrimaryKey(true)]
    public int Id { get; set; }

    [SqlServerColumnType(SqlServerColumnType.UNIQUEIDENTIFIER)]
    [DbColumnDefault("NEWID()")]
    public Guid PublicId { get; set; }

    [SqlServerColumnType(SqlServerColumnType.DECIMAL, "10,2")]
    [DbColumnDefault("0")]
    [DbColumnCheck("Price >= 0")]
    public decimal Price { get; set; }

    [SqlServerColumnType(SqlServerColumnType.NVARCHAR, "128")]
    [DbColumnIndex("IX_LegacyProducts_Name")]
    public string Name { get; set; } = string.Empty;
}

public static class Program
{
    public static void Main()
    {
        var generator = new SqlServerTableGenerator("Server=localhost;Database=fake;User Id=fake;Password=fake;TrustServerCertificate=True;");
        _ = generator.GenerateSqlTable<LegacyProduct>();
        _ = generator.GenerateIndexSql<LegacyProduct>();

        var options = MigrationRunnerOptions.Default().ApplyCompatibilityProfile(MigrationCompatibilityProfiles.LegacyEmbeddedSql);
        var runner = new SqlServerMigrationRunner("Server=localhost;Database=fake;User Id=fake;Password=fake;TrustServerCertificate=True;", options);
        runner.RegisterScript(MigrationScriptDefinition.Create("001", "Procedure", MigrationScriptCategory.StoredProcedures, "CREATE PROCEDURE dbo.usp_Test AS SELECT 1;"));
        Func<System.Threading.CancellationToken, Task<MigrationExecutionResult>> runWithResult = runner.RunWithResultAsync;
        _ = runWithResult;

        var syncOptions = new SqlServerModelSyncOptions
        {
            ConnectionString = "Server=localhost;Database=fake;User Id=fake;Password=fake;TrustServerCertificate=True;",
            DefaultSchema = "app",
            HistorySchema = "sec"
        };
        _ = SqlServerModelSynchronizer.FromTypes(syncOptions, typeof(LegacyProduct));
    }
}
