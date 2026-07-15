# 11 - Stored Procedure Sync

ModelSync can track stored procedure SQL files in your project and synchronize them with a live database.

The project file is the source of truth:

```text
Database/
  Procedures/
    SqlServer/
      dbo.usp_GetProducts.sql
    MySql/
      appdb.usp_GetProducts.sql
    PostgreSQL/
      public.usp_get_products.sql
```

When a procedure is synchronized:

- if it does not exist in the database, ModelSync creates it
- if it exists but differs from the project file, ModelSync updates it
- if it already matches, ModelSync does nothing

Always call `CompareAsync` or `CompareRegisteredAsync` first when you want a dry-run plan before applying changes.

## Provider Support

| Provider | Status | Apply Strategy |
|---|---|---|
| SQL Server / Azure SQL | Supported | `CREATE OR ALTER PROCEDURE` |
| MySQL / MariaDB | Supported | `DROP PROCEDURE IF EXISTS` + `CREATE PROCEDURE` |
| PostgreSQL | Supported | `CREATE OR REPLACE PROCEDURE` |
| SQLite | Not supported | Throws `NotSupportedException` |

MySQL/MariaDB does not have native `CREATE OR ALTER PROCEDURE`; changed procedures are recreated. Review plans before applying them in production.

PostgreSQL overloaded procedure signatures are not supported yet. If multiple procedures share the same schema/name with different arguments, ModelSync throws and asks you to resolve the ambiguity.

## SQL Server Example

```csharp
using UmbrellaFrame.ModelSync.SqlServer;

var procedures = new SqlServerStoredProcedureSynchronizer(connectionString);

procedures.RegisterProcedureFile(
    "Database/Procedures/SqlServer/dbo.usp_GetProducts.sql");

var plans = await procedures.CompareRegisteredAsync();
await procedures.SyncRegisteredAsync();
```

SQL file:

```sql
CREATE PROCEDURE dbo.usp_GetProducts
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Name, Price FROM dbo.Products;
END
```

## MySQL / MariaDB Example

```csharp
using UmbrellaFrame.ModelSync.MySql;

var procedures = new MySqlStoredProcedureSynchronizer(connectionString);

procedures.RegisterProcedureFile(
    "Database/Procedures/MySql/appdb.usp_GetProducts.sql");

var plans = await procedures.CompareRegisteredAsync();
await procedures.SyncRegisteredAsync();
```

SQL file:

```sql
CREATE PROCEDURE usp_GetProducts()
BEGIN
    SELECT Id, Name, Price FROM Products;
END
```

## PostgreSQL Example

```csharp
using UmbrellaFrame.ModelSync.PostgreSQL;

var procedures = new PostgresStoredProcedureSynchronizer(connectionString);

procedures.RegisterProcedureFile(
    "Database/Procedures/PostgreSQL/public.usp_get_products.sql");

var plans = await procedures.CompareRegisteredAsync();
await procedures.SyncRegisteredAsync();
```

SQL file:

```sql
CREATE PROCEDURE public.usp_get_products()
LANGUAGE SQL
AS $$
    SELECT 1;
$$;
```

## SQL File Rules

Each SQL file should contain a single stored procedure definition.

SQL Server files should start with one of:

```sql
CREATE PROCEDURE
CREATE PROC
ALTER PROCEDURE
CREATE OR ALTER PROCEDURE
```

MySQL/MariaDB files should start with:

```sql
CREATE PROCEDURE
```

PostgreSQL files should start with one of:

```sql
CREATE PROCEDURE
ALTER PROCEDURE
CREATE OR REPLACE PROCEDURE
```

In strict mode, do not include SQL Server `GO` separators inside procedure files because `GO` is a client command, not SQL. The explicit `LegacyEmbeddedSql` compatibility profile can normalize supported deployment-style files with terminal `GO`, `SET ANSI_NULLS`, and `SET QUOTED_IDENTIFIER` batches; unsupported side batches remain blocked.

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

The procedure name inside the SQL file must match the registered name. ModelSync rejects mismatches before applying SQL.

## Local Test Environment

Use the provided Docker Compose file to start local test databases:

```bash
docker compose -f docker-compose.modelsync-test.yml up -d
```

Default local connection strings:

```text
SQL Server:  Server=localhost,14333;Database=modelsync_sp;User Id=sa;Password=ModelSync_Pass123;Encrypt=False;TrustServerCertificate=True;
MySQL:       Server=localhost;Port=3307;Database=modelsync_sp;User ID=root;Password=ModelSync_Pass123;
PostgreSQL: Host=localhost;Port=5433;Database=modelsync_sp;Username=postgres;Password=ModelSync_Pass123;
```

Run the opt-in stored procedure integration smoke tests:

```bash
MODELSYNC_RUN_SP_INTEGRATION=1 dotnet test ModelSync.sln -c Release --filter "Category=Integration"
```

You can override the default connections with:

```bash
MODELSYNC_SQLSERVER_SP_CONNECTION_STRING="Server=localhost,14333;Database=modelsync_sp;User Id=sa;Password=ModelSync_Pass123;Encrypt=False;TrustServerCertificate=True;"
MODELSYNC_MYSQL_SP_CONNECTION_STRING="Server=localhost;Port=3307;Database=modelsync_sp;User ID=root;Password=ModelSync_Pass123;"
MODELSYNC_POSTGRES_SP_CONNECTION_STRING="Host=localhost;Port=5433;Database=modelsync_sp;Username=postgres;Password=ModelSync_Pass123;"
```

Stop the environment:

```bash
docker compose -f docker-compose.modelsync-test.yml down
```

## Safety

Stored procedure synchronization changes executable database logic. ModelSync supports a dry-run step through `CompareAsync` or `CompareRegisteredAsync` before applying changes.

Use `SyncRegisteredAsync` only when the project files have been reviewed.
