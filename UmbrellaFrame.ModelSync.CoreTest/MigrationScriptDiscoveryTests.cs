using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.Services;

namespace UmbrellaFrame.ModelSync.CoreTest;

public class MigrationScriptDiscoveryTests
{
    [Test]
    public void Order_ShouldSortByCategoryThenNumericId()
    {
        var definitions = new[]
        {
            MigrationScriptDefinition.Create("020", "Seed", MigrationScriptCategory.Seeds, "SELECT 1;"),
            MigrationScriptDefinition.Create("002", "Users", MigrationScriptCategory.Tables, "CREATE TABLE Users(Id INT);"),
            MigrationScriptDefinition.Create("001", "Products", MigrationScriptCategory.Tables, "CREATE TABLE Products(Id INT);"),
            MigrationScriptDefinition.Create("010", "Proc", MigrationScriptCategory.StoredProcedures, "CREATE PROCEDURE p AS SELECT 1;"),
            MigrationScriptDefinition.Create("999", "Custom", MigrationScriptCategory.CustomSql, "SELECT 2;")
        };

        var ordered = MigrationScriptDiscovery.Order(definitions).ToList();

        Assert.That(ordered.Select(x => x.Id), Is.EqualTo(new[] { "001", "002", "010", "020", "999" }));
    }

    [Test]
    public void ResolveCategory_ShouldUseKnownSegments()
    {
        Assert.That(MigrationScriptDiscovery.ResolveCategory("Scripts.Tables.001_CreateUsers.sql"), Is.EqualTo(MigrationScriptCategory.Tables));
        Assert.That(MigrationScriptDiscovery.ResolveCategory("Scripts.StoredProcedures.010_GetUsers.sql"), Is.EqualTo(MigrationScriptCategory.StoredProcedures));
        Assert.That(MigrationScriptDiscovery.ResolveCategory("Scripts.Triggers.020_Audit.sql"), Is.EqualTo(MigrationScriptCategory.Triggers));
        Assert.That(MigrationScriptDiscovery.ResolveCategory("Scripts.Seeds.030_Roles.sql"), Is.EqualTo(MigrationScriptCategory.Seeds));
        Assert.That(MigrationScriptDiscovery.ResolveCategory("Scripts.CustomSql.999_AfterSetup.sql"), Is.EqualTo(MigrationScriptCategory.CustomSql));
    }

    [Test]
    public void SqlServerGoSplitter_ShouldSplitGoBatches()
    {
        var batches = SqlBatchSplitter.SplitSqlServerGoBatches("SELECT 1;\r\nGO\r\nSELECT 2;\r\nGO");

        Assert.That(batches, Has.Count.EqualTo(2));
        Assert.That(batches[0], Does.Contain("SELECT 1"));
        Assert.That(batches[1], Does.Contain("SELECT 2"));
    }

    [Test]
    public void TableScriptColumnParser_ShouldReadSimpleColumns()
    {
        var columns = TableScriptColumnParser.Parse(
            "CREATE TABLE [app].[Users]([Id] INT NOT NULL, [Name] NVARCHAR(100) NULL, CONSTRAINT PK PRIMARY KEY([Id]));",
            "dbo");

        Assert.That(columns, Has.Count.EqualTo(2));
        Assert.That(columns[0].Schema, Is.EqualTo("app"));
        Assert.That(columns[0].Table, Is.EqualTo("Users"));
        Assert.That(columns[0].Column, Is.EqualTo("Id"));
        Assert.That(columns[1].Column, Is.EqualTo("Name"));
    }
}
