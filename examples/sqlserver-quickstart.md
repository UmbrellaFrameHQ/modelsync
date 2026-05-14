# SQL Server Quickstart

## Paket

```bash
dotnet add package UmbrellaFrame.ModelSync.SqlServer
```

## Program.cs

```csharp
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.SqlServer;

[SqlServerTableName("Employees")]
public class Employee
{
    [SqlServerColumnType(SqlServerColumnType.INT)]
    [SqlServerColumnPrimaryKey(isAutoIncrement: true)]
    public int Id { get; set; }

    [SqlServerColumnType(SqlServerColumnType.NVARCHAR, "200")]
    [SqlServerColumnNotNull]
    public string FullName { get; set; }

    [SqlServerColumnType(SqlServerColumnType.DECIMAL, "18,2")]
    [DbColumnDefault("0")]
    [DbColumnCheck("Salary >= 0")]
    public decimal Salary { get; set; }

    [SqlServerColumnType(SqlServerColumnType.DATETIME2)]
    [DbColumnDefault("SYSUTCDATETIME()")]
    public DateTime CreatedAt { get; set; }
}

var connectionString =
    "Server=localhost;Database=HrDb;Trusted_Connection=True;TrustServerCertificate=True;";

var generator = new SqlServerTableGenerator(connectionString);

generator.CreateDatabase();

var sql = generator.GenerateSqlServerTable<Employee>(ifNotExists: true);
Console.WriteLine(sql);

generator.CreateTables();
```

## Notlar

- SQL Server `CREATE TABLE IF NOT EXISTS` sentaksini dogrudan desteklemez; provider guard block uretir.
- Identifier'lar SQL Server icin `[Name]` formatinda quote edilir.

