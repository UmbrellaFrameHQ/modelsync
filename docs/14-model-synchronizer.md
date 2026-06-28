# 14 - Model Synchronizer

Model synchronizers compare attribute-decorated C# schema models with a live database and build a dry-run plan before applying anything.

This is an additive feature. Existing `ITableGenerator`, provider table generators, migration runners, and stored procedure synchronizers remain unchanged.

## Goals

- Read ModelSync table and column attributes from model classes.
- Introspect the live database schema.
- Produce a reviewable diff plan.
- Automatically apply only update-safe operations.
- Report destructive, risky, or unsupported operations without applying them.
- Keep destructive changes behind explicit opt-in.
- Combine model sync and ordered SQL scripts in one result.

## Supported Providers

| Provider | Model diff | Safe apply | Stored procedure scripts | Trigger scripts | Seed scripts | CustomSql scripts |
|---|---:|---:|---:|---:|---:|---:|
| SQL Server / Azure SQL | Yes | Yes | Yes | Yes | Yes | Yes |
| MySQL / MariaDB | Yes | Yes | Yes | Yes | Yes | Yes |
| PostgreSQL | Yes | Yes | Yes | Yes | Yes | Yes |
| SQLite | Yes | Yes | No | Yes | Yes | Yes |

SQLite does not support stored procedures. If a SQLite model synchronizer receives a stored procedure script, the plan contains an unsupported operation and `ApplyAsync()` refuses to continue.

## Safe Operations

- Missing table creation.
- Missing nullable column addition.
- Missing column addition when the column is `NOT NULL` and has a default.
- Missing index creation.
- Missing default/check/unique/foreign key constraint creation where the provider supports safe ALTER syntax.
- Ordered SQL script execution with history/hash tracking.

## Blocked Operations

- Dropping unmapped database tables when `ReportUnmappedTables = true`.
- Dropping columns.
- Renaming columns.
- Column type changes.
- Narrowing or incompatible type changes.
- `NULL` to `NOT NULL` changes.
- Adding a `NOT NULL` column without a default to an existing table.
- Provider-unsupported operations such as SQLite stored procedures or post-create constraint ALTERs.

## SQL Server Example

```csharp
using UmbrellaFrame.ModelSync.SqlServer;

var options = new SqlServerModelSyncOptions
{
    ConnectionString = connectionString,
    HistorySchema = "sec",
    DefaultSchema = "app",
    AllowDestructiveChanges = false,
    ApplyStoredProceduresOnEveryRun = true,
    ApplyTriggersOnEveryRun = true,
    ApplySeedsWithHashTracking = true,
    ApplyCustomSqlWithHashTracking = true
};

var result = await SqlServerModelSynchronizer
    .FromAssemblies(options, typeof(SomeEntity).Assembly)
    .AddSqlScriptsFromEmbeddedResources(
        typeof(SomeEntity).Assembly,
        rootNamespace: "Infrastructure.ER.Database.Providers.SqlServer.Migration.Scripts")
    .CompareAsync(cancellationToken);

foreach (var operation in result.Operations)
{
    Console.WriteLine($"{operation.ChangeType}: {operation.Schema}.{operation.Table}.{operation.Column} - {operation.Reason}");
    if (!string.IsNullOrWhiteSpace(operation.Sql))
        Console.WriteLine(operation.Sql);
}

await result.ThrowIfUnsupportedOrDestructiveAsync();
await result.ApplyAsync(cancellationToken);
```

## Explicit Type Example

Use `FromTypes` when you want full control over exactly which model classes participate:

```csharp
var result = await SqlServerModelSynchronizer
    .FromTypes(options, typeof(ProductSchema), typeof(CustomerSchema))
    .CompareAsync(cancellationToken);
```

`FromAssemblies` is provider-aware. A SQL Server synchronizer reads only SQL Server ModelSync attributes, a MySQL synchronizer reads only MySQL attributes, and so on. If two model classes map to the same schema/table pair, ModelSync throws a clear error instead of producing duplicate operations.

By default, `FromTypes` and `FromAssemblies` synchronize only the supplied/discovered model set and do not report unrelated database tables. Set `ReportUnmappedTables = true` when you want the model set to be authoritative and want extra database tables reported as blocked `DropTable` operations.

## Ordered Scripts

Embedded scripts are discovered and ordered by category:

```text
Tables -> StoredProcedures -> Triggers -> Seeds -> CustomSql
```

Supported embedded resource folder names:

```text
Scripts/Tables
Scripts/StoredProcedures
Scripts/Triggers
Scripts/Seeds
Scripts/CustomSql
```

`CustomSql` has its own history table:

```text
SchemaMigration_CustomSql
```

Changed script hashes are reapplied by the migration runner. Stored procedures and triggers can also be configured to run every time when their provider supports idempotent SQL.

`HistorySchema` controls where schema history tables are created for schema-capable providers such as SQL Server and PostgreSQL. SQL Server stored procedure scripts registered through the model synchronizer or migration runner are normalized to `CREATE OR ALTER PROCEDURE`; keep one procedure per file and do not include `GO` separators inside stored procedure files.

Model diff operations are risk-classified. Registered SQL scripts are trusted project artifacts; ModelSync does not parse arbitrary script text for destructive SQL.

## Provider Classes

| Provider | Options | Synchronizer |
|---|---|---|
| SQL Server | `SqlServerModelSyncOptions` | `SqlServerModelSynchronizer` |
| MySQL / MariaDB | `MySqlModelSyncOptions` | `MySqlModelSynchronizer` |
| PostgreSQL | `PostgresModelSyncOptions` | `PostgresModelSynchronizer` |
| SQLite | `SQLiteModelSyncOptions` | `SQLiteModelSynchronizer` |

## Production Guidance

Run `CompareAsync()` first and log the full plan. Apply automatically only when `BlockedOperations` is empty. For production systems, run synchronizers in a single deployment job before application traffic starts.

Do not use model sync as a silent schema mutation engine. It is designed to make safe additions easy and risky database changes visible.
