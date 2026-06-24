using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.PostgreSQL;

[TestFixture]
public class PostgresStoredProcedureSynchronizerTests
{
    [Test]
    public void BuildCreateOrReplaceSql_WithCreateProcedure_RewritesHeader()
    {
        var sync = CreateSynchronizer();
        var definition = StoredProcedureDefinition.Create(
            "usp_get_products",
            "CREATE PROCEDURE public.usp_get_products()\nLANGUAGE SQL\nAS $$ SELECT 1; $$;",
            schema: "public");

        var sql = sync.BuildCreateOrReplaceSql(definition);

        Assert.That(sql, Does.StartWith("CREATE OR REPLACE PROCEDURE public.usp_get_products()"));
    }

    [Test]
    public void BuildPlan_WhenProcedureDoesNotExist_ReturnsCreatePlan()
    {
        var sync = CreateSynchronizer();
        var definition = StoredProcedureDefinition.Create(
            "usp_get_products",
            "CREATE PROCEDURE public.usp_get_products()\nLANGUAGE SQL\nAS $$ SELECT 1; $$;",
            schema: "public");

        var plan = sync.BuildPlan(definition, currentSql: null);

        Assert.That(plan.ChangeType, Is.EqualTo(StoredProcedureChangeType.Create));
        Assert.That(plan.HasChanges, Is.True);
    }

    [Test]
    public void BuildPlan_WhenProcedureMatches_ReturnsNoChangePlan()
    {
        var sync = CreateSynchronizer();
        var definition = StoredProcedureDefinition.Create(
            "usp_get_products",
            "CREATE PROCEDURE public.usp_get_products()\nLANGUAGE SQL\nAS $$ SELECT 1; $$;",
            schema: "public");

        var currentSql = "CREATE OR REPLACE PROCEDURE public.usp_get_products()\nLANGUAGE SQL\nAS $$ SELECT 1; $$;";

        var plan = sync.BuildPlan(definition, currentSql);

        Assert.That(plan.ChangeType, Is.EqualTo(StoredProcedureChangeType.None));
        Assert.That(plan.HasChanges, Is.False);
    }

    [Test]
    public void BuildPlan_WhenProcedureDiffers_ReturnsAlterPlan()
    {
        var sync = CreateSynchronizer();
        var definition = StoredProcedureDefinition.Create(
            "usp_get_products",
            "CREATE PROCEDURE public.usp_get_products()\nLANGUAGE SQL\nAS $$ SELECT 2; $$;",
            schema: "public");

        var currentSql = "CREATE PROCEDURE public.usp_get_products()\nLANGUAGE SQL\nAS $$ SELECT 1; $$;";

        var plan = sync.BuildPlan(definition, currentSql);

        Assert.That(plan.ChangeType, Is.EqualTo(StoredProcedureChangeType.Alter));
        Assert.That(plan.SqlToApply, Does.Contain("SELECT 2"));
    }

    [Test]
    public void BuildCreateOrReplaceSql_WhenSqlNameDiffersFromDefinition_Throws()
    {
        var sync = CreateSynchronizer();
        var definition = StoredProcedureDefinition.Create(
            "usp_get_products",
            "CREATE PROCEDURE public.usp_update_products()\nLANGUAGE SQL\nAS $$ SELECT 1; $$;",
            schema: "public");

        Assert.Throws<InvalidOperationException>(() => sync.BuildCreateOrReplaceSql(definition));
    }

    private static PostgresStoredProcedureSynchronizer CreateSynchronizer()
        => new PostgresStoredProcedureSynchronizer("Host=localhost;Database=appdb;Username=postgres;Password=pass;");
}
