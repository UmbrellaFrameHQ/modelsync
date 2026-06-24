# MySQL Stored Procedure Sync Example

MySQL/MariaDB procedures can be kept as project SQL files and synchronized with the database.

```csharp
using UmbrellaFrame.ModelSync.MySql;

var sync = new MySqlStoredProcedureSynchronizer(
    "Server=localhost;Port=3307;Database=modelsync_sp;User ID=root;Password=ModelSync_Pass123;");

sync.RegisterProcedureFile("Database/Procedures/MySql/modelsync_sp.usp_GetProducts.sql");

var plans = await sync.CompareRegisteredAsync();
await sync.SyncRegisteredAsync();
```

MySQL does not support `CREATE OR ALTER PROCEDURE`, so changed procedures are applied as:

```sql
DROP PROCEDURE IF EXISTS `schema`.`procedure`;
CREATE PROCEDURE ...
```
