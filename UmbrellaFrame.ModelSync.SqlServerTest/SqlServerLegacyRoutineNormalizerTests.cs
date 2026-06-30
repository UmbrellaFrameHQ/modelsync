using System.Reflection;

[TestFixture]
public sealed class SqlServerLegacyRoutineNormalizerTests
{
    [Test]
    public void Normalize_WithSetOptionsTerminalGoAndTrailingPrint_ReturnsOnlyProcedure()
    {
        var sql = @"SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE PROCEDURE dbo.usp_ModelSyncProbe
AS
BEGIN
    PRINT 'body print stays';
    SELECT 1;
END
GO
PRINT 'deployment done';
GO";

        var normalized = Normalize(sql);

        Assert.That(normalized, Does.StartWith("CREATE PROCEDURE dbo.usp_ModelSyncProbe"));
        Assert.That(normalized, Does.Contain("PRINT 'body print stays'"));
        Assert.That(normalized, Does.Not.Contain("deployment done"));
        Assert.That(normalized, Does.Not.Contain("GO"));
    }

    [Test]
    public void Normalize_WithMultipleProcedures_ThrowsDiagnosticCode()
    {
        var ex = Assert.Throws<TargetInvocationException>(() => Normalize(
            "CREATE PROC dbo.a AS SELECT 1;\nGO\nCREATE PROC dbo.b AS SELECT 2;"));

        Assert.That(ex!.InnerException!.Message, Is.EqualTo("LegacyRoutineMultipleDefinitions"));
    }

    [Test]
    public void Normalize_WithSqlCmdCommand_ThrowsDiagnosticCode()
    {
        var ex = Assert.Throws<TargetInvocationException>(() => Normalize(
            ":setvar DbName test\nCREATE PROC dbo.a AS SELECT 1;"));

        Assert.That(ex!.InnerException!.Message, Is.EqualTo("LegacyRoutineUnsupportedSqlCmd"));
    }

    [Test]
    public void Normalize_WithExecutableSideBatch_ThrowsDiagnosticCode()
    {
        var ex = Assert.Throws<TargetInvocationException>(() => Normalize(
            "CREATE TABLE dbo.SideEffect(Id int);\nGO\nCREATE PROC dbo.a AS SELECT 1;"));

        Assert.That(ex!.InnerException!.Message, Is.EqualTo("LegacyRoutineExecutableSideBatch"));
    }

    private static string Normalize(string sql)
    {
        var assembly = typeof(UmbrellaFrame.ModelSync.SqlServer.SqlServerMigrationRunner).Assembly;
        var type = assembly.GetType("UmbrellaFrame.ModelSync.SqlServer.SqlServerLegacyRoutineNormalizer", throwOnError: true)!;
        var method = type.GetMethod("Normalize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string)method.Invoke(null, new object[] { sql })!;
    }
}
