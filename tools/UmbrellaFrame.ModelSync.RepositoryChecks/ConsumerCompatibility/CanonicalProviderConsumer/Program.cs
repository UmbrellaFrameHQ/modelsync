using UmbrellaFrame.ModelSync.MySql;
using UmbrellaFrame.ModelSync.PostgreSQL;
using UmbrellaFrame.ModelSync.SQLite;
using UmbrellaFrame.ModelSync.SqlServer;

[SqlServerTableName("SqlServerProducts")]
public sealed class SqlServerProduct
{
    [SqlServerColumnType(SqlServerColumnType.INT)] [SqlServerColumnPrimaryKey(true)] public int Id { get; set; }
    [SqlServerColumnType(SqlServerColumnType.UNIQUEIDENTIFIER)] [SqlServerColumnDefault(SqlServerDefaultExpression.NewId)] public Guid A { get; set; }
    [SqlServerColumnType(SqlServerColumnType.UNIQUEIDENTIFIER)] [SqlServerColumnDefault(SqlServerDefaultExpression.NewSequentialId)] public Guid B { get; set; }
    [SqlServerColumnType(SqlServerColumnType.DATETIME)] [SqlServerColumnDefault(SqlServerDefaultExpression.GetDate)] public DateTime C { get; set; }
    [SqlServerColumnType(SqlServerColumnType.DATETIME)] [SqlServerColumnDefault(SqlServerDefaultExpression.GetUtcDate)] public DateTime D { get; set; }
    [SqlServerColumnType(SqlServerColumnType.DATETIME2)] [SqlServerColumnDefault(SqlServerDefaultExpression.SysDateTime)] public DateTime E { get; set; }
    [SqlServerColumnType(SqlServerColumnType.DATETIME2)] [SqlServerColumnDefault(SqlServerDefaultExpression.SysUtcDateTime)] public DateTime F { get; set; }
    [SqlServerColumnType(SqlServerColumnType.INT)] [SqlServerColumnDefaultSql("(1 + 2)")] [SqlServerColumnCheck("G >= 0")] [SqlServerColumnIndex("IX_SqlServerProducts_G")] public int G { get; set; }
}

[MySqlTableName("MySqlProducts")]
public sealed class MySqlProduct
{
    [MySqlColumnType(MySqlColumnType.INT)] [MySqlColumnPrimaryKey(true)] public int Id { get; set; }
    [MySqlColumnType(MySqlColumnType.VARCHAR, "36")] [MySqlColumnDefault(MySqlDefaultExpression.Uuid)] public string A { get; set; } = string.Empty;
    [MySqlColumnType(MySqlColumnType.DATETIME)] [MySqlColumnDefault(MySqlDefaultExpression.CurrentTimestamp)] public DateTime B { get; set; }
    [MySqlColumnType(MySqlColumnType.INT)] [MySqlColumnDefaultSql("(1 + 2)")] [MySqlColumnCheck("C >= 0")] [MySqlColumnIndex("IX_MySqlProducts_C")] public int C { get; set; }
}

[PostgresTableName("PostgresProducts")]
public sealed class PostgresProduct
{
    [PostgresColumnType(PostgresColumnType.INTEGER)] [PostgresColumnPrimaryKey] public int Id { get; set; }
    [PostgresColumnType(PostgresColumnType.UUID)] [PostgresColumnDefault(PostgresDefaultExpression.GenRandomUuid)] public Guid A { get; set; }
    [PostgresColumnType(PostgresColumnType.TIMESTAMP)] [PostgresColumnDefault(PostgresDefaultExpression.CurrentTimestamp)] public DateTime B { get; set; }
    [PostgresColumnType(PostgresColumnType.TIMESTAMP)] [PostgresColumnDefault(PostgresDefaultExpression.Now)] public DateTime C { get; set; }
    [PostgresColumnType(PostgresColumnType.INTEGER)] [PostgresColumnDefaultSql("(1 + 2)")] [PostgresColumnCheck("D >= 0")] [PostgresColumnIndex("IX_PostgresProducts_D")] public int D { get; set; }
}

[SQLiteTableName("SQLiteProducts")]
public sealed class SQLiteProduct
{
    [SQLiteColumnType(SQLiteColumnType.INTEGER)] [SQLiteColumnPrimaryKey] public int Id { get; set; }
    [SQLiteColumnType(SQLiteColumnType.TEXT)] [SQLiteColumnDefault(SQLiteDefaultExpression.CurrentTimestamp)] public string A { get; set; } = string.Empty;
    [SQLiteColumnType(SQLiteColumnType.TEXT)] [SQLiteColumnDefault(SQLiteDefaultExpression.CurrentDate)] public string B { get; set; } = string.Empty;
    [SQLiteColumnType(SQLiteColumnType.TEXT)] [SQLiteColumnDefault(SQLiteDefaultExpression.CurrentTime)] public string C { get; set; } = string.Empty;
    [SQLiteColumnType(SQLiteColumnType.INTEGER)] [SQLiteColumnDefaultSql("(1 + 2)")] [SQLiteColumnCheck("D >= 0")] [SQLiteColumnIndex("IX_SQLiteProducts_D")] public int D { get; set; }
}

public static class Program
{
    public static void Main()
    {
    }
}
