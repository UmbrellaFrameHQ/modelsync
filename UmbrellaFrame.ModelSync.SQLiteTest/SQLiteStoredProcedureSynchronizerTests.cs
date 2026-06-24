using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.SQLite;

[TestFixture]
public class SQLiteStoredProcedureSynchronizerTests
{
    [Test]
    public void RegisterProcedure_ThrowsNotSupported()
    {
        var sync = new SQLiteStoredProcedureSynchronizer();
        var definition = StoredProcedureDefinition.Create(
            "usp_get_products",
            "CREATE PROCEDURE usp_get_products AS SELECT 1");

        Assert.Throws<NotSupportedException>(() => sync.RegisterProcedure(definition));
    }
}
