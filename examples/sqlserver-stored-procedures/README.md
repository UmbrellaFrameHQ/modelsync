# SQL Server Stored Procedure Sync Example

This example keeps stored procedure definitions in project files and synchronizes them with SQL Server.

```csharp
using UmbrellaFrame.ModelSync.SqlServer;

var sync = new SqlServerStoredProcedureSynchronizer(
    "Server=localhost;Database=appdb;Trusted_Connection=True;TrustServerCertificate=True;");

sync.RegisterProcedureFile("Database/Procedures/SqlServer/dbo.usp_GetProducts.sql");

var plans = await sync.CompareRegisteredAsync();

foreach (var plan in plans)
{
    Console.WriteLine($"{plan.Definition.Schema}.{plan.Definition.Name}: {plan.ChangeType}");
}

await sync.SyncRegisteredAsync();
```

The SQL file should start with `CREATE PROCEDURE`, `ALTER PROCEDURE`, or `CREATE OR ALTER PROCEDURE`.
ModelSync rewrites the header to `CREATE OR ALTER PROCEDURE` before applying it.
