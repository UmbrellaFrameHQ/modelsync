# 12 - Migration Runner

ModelSync migration runners apply ordered SQL scripts from project files or embedded resources and record what was applied.

Use this when a project needs full setup scripts, not only attribute-generated table DDL:

- table scripts
- stored procedure scripts
- trigger scripts
- seed scripts
- migration history tables
- dry-run plans
- provider-specific batch execution
- optional destructive database reset
- missing-column repair from changed table scripts

## Provider Support

| Provider | Tables | Stored Procedures | Triggers | Seeds | History | Reset | Batch Split |
|---|:---:|:---:|:---:|:---:|:---:|:---:|---|
| SQL Server / Azure SQL | Yes | Yes | Yes | Yes | `sec.SchemaMigration_*` | Yes | `GO` |
| MySQL / MariaDB | Yes | Yes | Yes | Yes | `SchemaMigration_*` | Yes | Single statement |
| PostgreSQL | Yes | Yes | Yes | Yes | `sec.SchemaMigration_*` | Yes | Single statement |
| SQLite | Yes | No | Yes | Yes | `SchemaMigration_*` | No | Single statement |

SQLite does not support stored procedures. Applying a stored procedure script with `SQLiteMigrationRunner` throws `NotSupportedException`.

## Script Folders

```text
Database/
  Scripts/
    Tables/
      001_CreateProducts.sql
    StoredProcedures/
      010_GetProducts.sql
    Triggers/
      020_ProductAudit.sql
    Seeds/
      030_DefaultProducts.sql
```

Scripts run in this category order:

```text
Tables -> StoredProcedures -> Triggers -> Seeds
```

Within a category, numeric prefixes run in ascending order.

## Usage

```csharp
using UmbrellaFrame.ModelSync.SqlServer;

var runner = new SqlServerMigrationRunner(connectionString);

runner.RegisterScriptFile("Database/Scripts/Tables/001_CreateProducts.sql");
runner.RegisterScriptFile("Database/Scripts/StoredProcedures/010_GetProducts.sql");
runner.RegisterScriptFile("Database/Scripts/Triggers/020_ProductAudit.sql");
runner.RegisterScriptFile("Database/Scripts/Seeds/030_DefaultProducts.sql");

var plans = await runner.CompareRegisteredAsync();
await runner.RunAsync();
```

## Embedded Resources

```csharp
using System.Reflection;
using UmbrellaFrame.ModelSync.SqlServer;

var runner = new SqlServerMigrationRunner(connectionString);

runner.RegisterEmbeddedScripts(
    Assembly.GetExecutingAssembly(),
    "MyApp.Database.Scripts.");

await runner.RunAsync();
```

Each embedded resource must end with `.sql`.

## History Tables

ModelSync creates one history table per category:

```text
SchemaMigration_Tables
SchemaMigration_StoredProcedures
SchemaMigration_Triggers
SchemaMigration_Seeds
```

Each row stores `Id`, `Name`, `SqlHash`, `AppliedAt`, and `UpdateAt`.

## Missing Column Repair

When an already-applied table script changes, ModelSync parses simple `CREATE TABLE` scripts and adds missing columns with `ALTER TABLE ... ADD COLUMN`.

This is intentionally additive only. It does not automatically drop columns, rename columns, rewrite constraints, or change existing column types.

This repair is script-based. Adding a new property to a C# model does not yet trigger automatic live database diffing. Use explicit model operations such as `AddColumn<T>("PropertyName")`, or keep table SQL scripts as the migration runner source of truth.

## Why History Tables?

ModelSync uses history tables because catalog checks alone cannot describe migration state.

Provider catalogs can tell whether an object exists. They cannot reliably tell which script version was applied, whether a seed script already ran, when a script was last updated, or which SQL hash was deployed.

ModelSync therefore combines:

- history tables for script state and hashes
- provider catalog checks for live verification, such as missing-column repair

## Optional Database Reset

Database reset is destructive and requires explicit opt-in:

```csharp
var options = new MigrationRunnerOptions
{
    ResetDatabase = true,
    DestructiveOptions = DestructiveOperationOptions.Allow()
};

var runner = new SqlServerMigrationRunner(connectionString, options);
await runner.RunAsync();
```

If `ResetDatabase` is enabled without `DestructiveOperationOptions.Allow()`, ModelSync throws before touching the database.

## SQL Server GO Support

SQL Server runner splits scripts on `GO` batch separators. Other providers execute scripts as provider-native single commands.
