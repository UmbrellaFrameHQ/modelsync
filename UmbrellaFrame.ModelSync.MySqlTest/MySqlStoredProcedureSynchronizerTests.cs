using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.MySql;

[TestFixture]
public class MySqlStoredProcedureSynchronizerTests
{
    [Test]
    public void BuildApplySql_WithCreateProcedure_BuildsDropAndCreateScript()
    {
        var sync = CreateSynchronizer();
        var definition = StoredProcedureDefinition.Create(
            "usp_GetProducts",
            "CREATE PROCEDURE usp_GetProducts()\nBEGIN\n    SELECT 1;\nEND",
            schema: "appdb");

        var sql = sync.BuildApplySql(definition);

        Assert.That(sql, Does.StartWith("DROP PROCEDURE IF EXISTS `appdb`.`usp_GetProducts`;"));
        Assert.That(sql, Does.Contain("CREATE PROCEDURE usp_GetProducts()"));
    }

    [Test]
    public void BuildPlan_WhenProcedureDoesNotExist_ReturnsCreatePlan()
    {
        var sync = CreateSynchronizer();
        var definition = StoredProcedureDefinition.Create(
            "usp_GetProducts",
            "CREATE PROCEDURE usp_GetProducts()\nBEGIN\n    SELECT 1;\nEND",
            schema: "appdb");

        var plan = sync.BuildPlan(definition, currentSql: null);

        Assert.That(plan.ChangeType, Is.EqualTo(StoredProcedureChangeType.Create));
        Assert.That(plan.HasChanges, Is.True);
    }

    [Test]
    public void BuildPlan_WhenProcedureMatchesIgnoringDefiner_ReturnsNoChangePlan()
    {
        var sync = CreateSynchronizer();
        var definition = StoredProcedureDefinition.Create(
            "usp_GetProducts",
            "CREATE PROCEDURE usp_GetProducts()\nBEGIN\n    SELECT 1;\nEND",
            schema: "appdb");

        var currentSql = "CREATE DEFINER=`root`@`localhost` PROCEDURE `usp_GetProducts`()\nBEGIN\n    SELECT 1;\nEND";

        var plan = sync.BuildPlan(definition, currentSql);

        Assert.That(plan.ChangeType, Is.EqualTo(StoredProcedureChangeType.None));
        Assert.That(plan.HasChanges, Is.False);
    }

    [Test]
    public void BuildPlan_WhenProcedureDiffers_ReturnsAlterPlan()
    {
        var sync = CreateSynchronizer();
        var definition = StoredProcedureDefinition.Create(
            "usp_GetProducts",
            "CREATE PROCEDURE usp_GetProducts()\nBEGIN\n    SELECT 2;\nEND",
            schema: "appdb");

        var currentSql = "CREATE PROCEDURE usp_GetProducts()\nBEGIN\n    SELECT 1;\nEND";

        var plan = sync.BuildPlan(definition, currentSql);

        Assert.That(plan.ChangeType, Is.EqualTo(StoredProcedureChangeType.Alter));
        Assert.That(plan.SqlToApply, Does.Contain("SELECT 2"));
    }

    [Test]
    public void BuildApplySql_WhenSqlNameDiffersFromDefinition_Throws()
    {
        var sync = CreateSynchronizer();
        var definition = StoredProcedureDefinition.Create(
            "usp_GetProducts",
            "CREATE PROCEDURE usp_UpdateProducts()\nBEGIN\n    SELECT 1;\nEND",
            schema: "appdb");

        Assert.Throws<InvalidOperationException>(() => sync.BuildApplySql(definition));
    }

    private static MySqlStoredProcedureSynchronizer CreateSynchronizer()
        => new MySqlStoredProcedureSynchronizer("Server=localhost;Database=appdb;User=root;Password=pass;");
}
