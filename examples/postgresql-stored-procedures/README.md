# PostgreSQL Stored Procedure Sync Example

PostgreSQL procedures can be kept as project SQL files and synchronized with the database.

```csharp
using UmbrellaFrame.ModelSync.PostgreSQL;

var sync = new PostgresStoredProcedureSynchronizer(
    "Host=localhost;Port=5433;Database=modelsync_sp;Username=postgres;Password=ModelSync_Pass123;");

sync.RegisterProcedureFile("Database/Procedures/PostgreSQL/public.usp_get_products.sql");

var plans = await sync.CompareRegisteredAsync();
await sync.SyncRegisteredAsync();
```

PostgreSQL procedures are applied with `CREATE OR REPLACE PROCEDURE`.
