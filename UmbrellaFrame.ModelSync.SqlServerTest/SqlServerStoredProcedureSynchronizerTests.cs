using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.SqlServer;

[TestFixture]
public class SqlServerStoredProcedureSynchronizerTests
{
    [Test]
    public void BuildCreateOrAlterSql_WithCreateProcedure_RewritesHeader()
    {
        var sync = CreateSynchronizer();
        var definition = StoredProcedureDefinition.Create(
            "usp_GetProducts",
            "CREATE PROCEDURE dbo.usp_GetProducts AS BEGIN SELECT 1; END");

        var sql = sync.BuildCreateOrAlterSql(definition);

        Assert.That(sql, Does.StartWith("CREATE OR ALTER PROCEDURE dbo.usp_GetProducts"));
    }

    [Test]
    public void BuildPlan_WhenProcedureDoesNotExist_ReturnsCreatePlan()
    {
        var sync = CreateSynchronizer();
        var definition = StoredProcedureDefinition.Create(
            "usp_GetProducts",
            "CREATE PROCEDURE dbo.usp_GetProducts AS BEGIN SELECT 1; END");

        var plan = sync.BuildPlan(definition, currentSql: null);

        Assert.That(plan.ChangeType, Is.EqualTo(StoredProcedureChangeType.Create));
        Assert.That(plan.HasChanges, Is.True);
        Assert.That(plan.SqlToApply, Does.StartWith("CREATE OR ALTER PROCEDURE"));
    }

    [Test]
    public void BuildPlan_WhenProcedureMatches_ReturnsNoChangePlan()
    {
        var sync = CreateSynchronizer();
        var definition = StoredProcedureDefinition.Create(
            "usp_GetProducts",
            "CREATE PROCEDURE dbo.usp_GetProducts AS BEGIN SELECT 1; END");

        var plan = sync.BuildPlan(
            definition,
            currentSql: "ALTER PROCEDURE dbo.usp_GetProducts AS BEGIN SELECT 1; END");

        Assert.That(plan.ChangeType, Is.EqualTo(StoredProcedureChangeType.None));
        Assert.That(plan.HasChanges, Is.False);
        Assert.That(plan.SqlToApply, Is.Empty);
    }

    [Test]
    public void BuildPlan_WhenProcedureDiffers_ReturnsAlterPlan()
    {
        var sync = CreateSynchronizer();
        var definition = StoredProcedureDefinition.Create(
            "usp_GetProducts",
            "CREATE PROCEDURE dbo.usp_GetProducts AS BEGIN SELECT 2; END");

        var plan = sync.BuildPlan(
            definition,
            currentSql: "CREATE PROCEDURE dbo.usp_GetProducts AS BEGIN SELECT 1; END");

        Assert.That(plan.ChangeType, Is.EqualTo(StoredProcedureChangeType.Alter));
        Assert.That(plan.HasChanges, Is.True);
        Assert.That(plan.SqlToApply, Does.Contain("SELECT 2"));
    }

    [Test]
    public void StoredProcedureDefinition_FromFile_UsesSchemaAndNameFromFileName()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dbo.usp_ModelSync_{Guid.NewGuid():N}.sql");
        File.WriteAllText(path, "CREATE PROCEDURE dbo.usp_ModelSync AS BEGIN SELECT 1; END");

        try
        {
            var definition = StoredProcedureDefinition.FromFile(path);

            Assert.That(definition.Schema, Is.EqualTo("dbo"));
            Assert.That(definition.Name, Does.StartWith("usp_ModelSync_"));
            Assert.That(definition.SourcePath, Is.EqualTo(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void RegisterProcedure_RejectsUnsafeProcedureName()
    {
        var sync = CreateSynchronizer();
        var definition = StoredProcedureDefinition.Create(
            "usp_Bad;DROP",
            "CREATE PROCEDURE dbo.usp_Bad AS BEGIN SELECT 1; END");

        Assert.Throws<ArgumentException>(() => sync.RegisterProcedure(definition));
    }

    private static SqlServerStoredProcedureSynchronizer CreateSynchronizer()
        => new SqlServerStoredProcedureSynchronizer(
            "Server=localhost;Database=fake;User Id=fake;Password=fake;TrustServerCertificate=True;");
}
