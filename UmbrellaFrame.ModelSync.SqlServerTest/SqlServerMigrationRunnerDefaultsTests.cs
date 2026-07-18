using System.Reflection;
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.Services;
using UmbrellaFrame.ModelSync.SqlServer;

namespace UmbrellaFrame.ModelSync.SqlServerTest;

public class SqlServerMigrationRunnerDefaultsTests
{
    [Test]
    public void DefaultRunner_ShouldOnlyConfigureHistorySchema()
    {
        var options = OptionsOf(new SqlServerMigrationRunner("Server=localhost;Database=appdb;Integrated Security=True;"));

        Assert.That(options.Schemas, Is.EqualTo(new[] { "sec" }));
    }

    [Test]
    public void LegacyApplicationSchemasProfile_ShouldRestoreOptInSchemaSet()
    {
        var configured = MigrationRunnerOptions.Default()
            .ApplyCompatibilityProfile(MigrationCompatibilityProfiles.LegacyApplicationSchemas);
        var options = OptionsOf(new SqlServerMigrationRunner("Server=localhost;Database=appdb;Integrated Security=True;", configured));

        Assert.That(options.Schemas, Does.Contain("app"));
        Assert.That(options.Schemas, Does.Contain("crm"));
        Assert.That(options.Schemas, Does.Contain("fin"));
        Assert.That(options.Schemas, Does.Contain("sec"));
    }

    [Test]
    public void ConfiguredSchemas_ShouldBePreservedWithoutAddingDomainDefaults()
    {
        var configured = MigrationRunnerOptions.Default();
        configured.Schemas.Add("app");
        var options = OptionsOf(new SqlServerMigrationRunner(
            "Server=localhost;Database=appdb;Integrated Security=True;",
            configured));

        Assert.That(options.Schemas, Is.EquivalentTo(new[] { "app", "sec" }));
        Assert.That(options.Schemas, Does.Not.Contain("crm"));
        Assert.That(options.Schemas, Does.Not.Contain("fin"));
    }

    [TestCase(208, "Invalid object name 'sec.SchemaMigration_Tables'.", "SchemaMigration_Tables", true)]
    [TestCase(208, "Invalid object name 'dbo.UnrelatedTable'.", "SchemaMigration_Tables", false)]
    [TestCase(229, "The SELECT permission was denied on object 'SchemaMigration_Tables'.", "SchemaMigration_Tables", false)]
    [TestCase(2760, "The specified schema name does not exist.", "SchemaMigration_Tables", false)]
    [TestCase(15151, "Cannot find the object because it does not exist or you do not have permissions.", "SchemaMigration_Tables", false)]
    public void MissingHistoryClassifier_ShouldOnlyAcceptExpectedMissingTable(
        int number,
        string message,
        string expectedTable,
        bool expected)
    {
        var method = typeof(SqlServerMigrationRunner).GetMethod(
            "IsExpectedMissingHistoryObject",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        Assert.That(method!.Invoke(null, new object[] { number, message, expectedTable }), Is.EqualTo(expected));
    }

    private static MigrationRunnerOptions OptionsOf(SqlServerMigrationRunner runner)
    {
        var property = typeof(SqlMigrationRunnerBase).GetProperty("Options", BindingFlags.Instance | BindingFlags.NonPublic);
        return (MigrationRunnerOptions)property!.GetValue(runner)!;
    }
}
