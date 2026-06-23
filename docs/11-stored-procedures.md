# 11 - Stored Procedure Sync

ModelSync can track SQL Server stored procedure files in your project and synchronize them with a live database.

The project file is the source of truth:

```text
Database/
  Procedures/
    SqlServer/
      dbo.usp_GetProducts.sql
      dbo.usp_UpdateStock.sql
```

When a procedure is synchronized:

- if it does not exist in the database, ModelSync creates it
- if it exists but differs from the project file, ModelSync alters it
- if it already matches, ModelSync does nothing

SQL Server uses `CREATE OR ALTER PROCEDURE`, so the same script can cover create and update scenarios.

## Example

```csharp
using UmbrellaFrame.ModelSync.SqlServer;

var procedures = new SqlServerStoredProcedureSynchronizer(connectionString);

procedures.RegisterProcedureFile(
    "Database/Procedures/SqlServer/dbo.usp_GetProducts.sql");

var plans = await procedures.CompareRegisteredAsync();

foreach (var plan in plans)
{
    Console.WriteLine($"{plan.Definition.Schema}.{plan.Definition.Name}: {plan.ChangeType}");
    Console.WriteLine(plan.SqlToApply);
}

await procedures.SyncRegisteredAsync();
```

## SQL File Rules

Each SQL file should contain a single stored procedure definition and should start with one of:

```sql
CREATE PROCEDURE
CREATE PROC
ALTER PROCEDURE
CREATE OR ALTER PROCEDURE
```

Do not include `GO` batch separators inside the file. `GO` is a client-side command, not a SQL Server statement.

## File Naming

If the file name follows `schema.procedure.sql`, ModelSync automatically resolves the schema and name:

```text
dbo.usp_GetProducts.sql
```

resolves to:

```text
Schema: dbo
Name:   usp_GetProducts
```

You can also pass the name and schema explicitly:

```csharp
procedures.RegisterProcedureFile(
    "Database/Procedures/SqlServer/GetProducts.sql",
    name: "usp_GetProducts",
    schema: "dbo");
```

## Safety

Stored procedure synchronization changes executable database logic. ModelSync always supports a dry-run step through `CompareAsync` or `CompareRegisteredAsync` before applying changes.

Use `SyncRegisteredAsync` only when the project files have been reviewed.
