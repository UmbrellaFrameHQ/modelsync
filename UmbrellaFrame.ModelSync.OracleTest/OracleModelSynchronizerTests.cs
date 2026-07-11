using Oracle.ManagedDataAccess.Client;
using UmbrellaFrame.ModelSync.Oracle;

namespace UmbrellaFrame.ModelSync.OracleTest;

public sealed class OracleModelSynchronizerTests
{
    private const string TableName = "MS_ORACLE_SYNC_PRODUCTS";
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("MODELSYNC_ORACLE_CONNECTION")
        ?? "User Id=MODELSYNC_TEST;Password=ModelSync_Pass123;Data Source=127.0.0.1:11521/FREEPDB1";

    [Test]
    [Category("Integration")]
    public async Task Oracle_ModelSynchronizer_ShouldApplySafeMissingTableAndColumn()
    {
        Cleanup();

        var initialOptions = new OracleModelSyncOptions { ConnectionString = ConnectionString };
        var initial = await OracleModelSynchronizer
            .FromTypes(initialOptions, typeof(OracleSyncProductBase))
            .CompareAsync();

        Assert.That(initial.BlockedOperations, Is.Empty);
        Assert.That(initial.AutomaticOperations.Any(o => o.ChangeType == UmbrellaFrame.ModelSync.Core.ModelSyncChangeType.CreateTable), Is.True);
        await initial.ApplyAsync();
        Assert.That(TableExists(), Is.True);

        var expanded = await OracleModelSynchronizer
            .FromTypes(new OracleModelSyncOptions { ConnectionString = ConnectionString }, typeof(OracleSyncProductExpanded))
            .CompareAsync();

        Assert.That(expanded.BlockedOperations, Is.Empty);
        Assert.That(expanded.AutomaticOperations.Any(o => o.ChangeType == UmbrellaFrame.ModelSync.Core.ModelSyncChangeType.AddColumn && o.Column == "Stock"), Is.True);
        await expanded.ApplyAsync();
        Assert.That(ColumnExists("Stock"), Is.True);

        var stable = await OracleModelSynchronizer
            .FromTypes(new OracleModelSyncOptions { ConnectionString = ConnectionString }, typeof(OracleSyncProductExpanded))
            .CompareAsync();

        Assert.That(stable.AutomaticOperations, Is.Empty);
        Assert.That(stable.BlockedOperations, Is.Empty);

        Cleanup();
    }

    private static bool TableExists()
    {
        using var connection = new OracleConnection(ConnectionString);
        connection.Open();
        using var command = new OracleCommand("SELECT COUNT(*) FROM USER_TABLES WHERE TABLE_NAME = :tableName", connection);
        command.Parameters.Add(new OracleParameter("tableName", TableName));
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static bool ColumnExists(string column)
    {
        using var connection = new OracleConnection(ConnectionString);
        connection.Open();
        using var command = new OracleCommand("SELECT COUNT(*) FROM USER_TAB_COLUMNS WHERE TABLE_NAME = :tableName AND COLUMN_NAME = :columnName", connection);
        command.Parameters.Add(new OracleParameter("tableName", TableName));
        command.Parameters.Add(new OracleParameter("columnName", column));
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static void Cleanup()
    {
        using var connection = new OracleConnection(ConnectionString);
        connection.Open();
        using var command = new OracleCommand(
            "BEGIN EXECUTE IMMEDIATE 'DROP TABLE \"MS_ORACLE_SYNC_PRODUCTS\" CASCADE CONSTRAINTS'; EXCEPTION WHEN OTHERS THEN IF SQLCODE != -942 THEN RAISE; END IF; END;",
            connection);
        command.ExecuteNonQuery();
    }

    [OracleTableName(TableName)]
    private sealed class OracleSyncProductBase
    {
        [OracleColumnType(OracleColumnType.NUMBER, "10")]
        [OracleColumnPrimaryKey(isIdentity: true)]
        public int Id { get; set; }

        [OracleColumnType(OracleColumnType.VARCHAR2, "80")]
        [OracleColumnNotNull]
        public string Name { get; set; } = string.Empty;
    }

    [OracleTableName(TableName)]
    private sealed class OracleSyncProductExpanded
    {
        [OracleColumnType(OracleColumnType.NUMBER, "10")]
        [OracleColumnPrimaryKey(isIdentity: true)]
        public int Id { get; set; }

        [OracleColumnType(OracleColumnType.VARCHAR2, "80")]
        [OracleColumnNotNull]
        public string Name { get; set; } = string.Empty;

        [OracleColumnType(OracleColumnType.NUMBER, "10")]
        public int Stock { get; set; }
    }
}
