# 12 - Migration Runner

ModelSync migration runners apply ordered SQL scripts from project files or embedded resources and record what was applied.

Use this when a project needs full setup scripts, not only attribute-generated table DDL:

- table scripts
- stored procedure scripts
- trigger scripts
- seed scripts
- custom SQL scripts
- migration history tables
- dry-run plans
- provider-specific batch execution
- optional destructive database reset
- optional missing-column repair suggestions from changed table scripts

## Provider Support

| Provider | Tables | Stored Procedures | Triggers | Seeds | CustomSql | History | Reset | Batch Split |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|---|
| SQL Server / Azure SQL | Yes | Yes | Yes | Yes | Yes | `sec.SchemaMigration_*` | Yes | `GO` |
| MySQL / MariaDB | Yes | Yes | Yes | Yes | Yes | `SchemaMigration_*` | Yes | Single statement |
| PostgreSQL | Yes | Yes | Yes | Yes | Yes | `sec.SchemaMigration_*` | Yes | Single statement |
| SQLite | Yes | No | Yes | Yes | Yes | `SchemaMigration_*` | No | Single statement |

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
    CustomSql/
      999_AfterSetup.sql
```

Scripts run in this category order:

```text
Tables -> StoredProcedures -> Triggers -> Seeds -> CustomSql
```

Within a category, numeric prefixes run in ascending order.

Category discovery uses exact folder or embedded-resource segments. A file named `001_CreateCustomer.sql` under `Tables` remains a table script; words such as `Customer` or `Customization` in the file name do not make the script `CustomSql`.

## Usage

```csharp
using UmbrellaFrame.ModelSync.SqlServer;

var runner = new SqlServerMigrationRunner(connectionString);

runner.RegisterScriptFile("Database/Scripts/Tables/001_CreateProducts.sql");
runner.RegisterScriptFile("Database/Scripts/StoredProcedures/010_GetProducts.sql");
runner.RegisterScriptFile("Database/Scripts/Triggers/020_ProductAudit.sql");
runner.RegisterScriptFile("Database/Scripts/Seeds/030_DefaultProducts.sql");
runner.RegisterScriptFile("Database/Scripts/CustomSql/999_AfterSetup.sql");

var plans = await runner.CompareRegisteredAsync();
await runner.RunAsync();
```

`CompareRegisteredAsync()` is read-only. If history tables do not exist yet, it treats the registered scripts as pending and does not create infrastructure. `RunAsync()` creates required schemas/history tables before applying scripts. You can also bootstrap infrastructure explicitly:

`RunAsync()` remains as a compatibility API, but it no longer hides execution failures: a failed item throws `MigrationExecutionException`. Use `RunWithResultAsync()` when deployment code needs structured states such as `RolledBack`, `PartiallyApplied`, `Cancelled`, or `LockTimeout`.

```csharp
await runner.EnsureInfrastructureAsync(cancellationToken);
```

Duplicate script IDs are rejected before database access so a bad migration set fails fast.

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

## Changed table scripts and repair suggestions

Released migration files should be immutable. When an already-recorded table script changes, ModelSync blocks automatic execution and does not adopt the complete target hash automatically, because a best-effort column parser cannot prove that type, constraint, or other changes were also applied. Such a plan has `RequiresManualReview = true` and is blocked until the change is represented by a new migration.

`AutoAddMissingColumnsFromTableScripts` is disabled by default. When it is deliberately enabled, comparison may produce additive `RepairSql` suggestions for review, but those suggestions are still not executed automatically and the full target hash is still not recorded.

`SourceSql` is the registered artifact, `PlannedExecutionSql` is the command selected for automatic execution, `RepairSql` contains review-only suggestions, and `UnappliedDrift` explains what remains unresolved. This keeps dry-run output aligned with the mutation path.

## History Tables

ModelSync creates one history table per category:

```text
SchemaMigration_Tables
SchemaMigration_StoredProcedures
SchemaMigration_Triggers
SchemaMigration_Seeds
SchemaMigration_CustomSql
```

Each row stores `Id`, `Name`, `SqlHash`, `AppliedAt`, and `UpdateAt`.

## Missing Column Repair

When `AutoAddMissingColumnsFromTableScripts` is enabled and an already-applied table script changes, ModelSync parses simple `CREATE TABLE` scripts and suggests missing columns with `ALTER TABLE ... ADD COLUMN`.

This is intentionally additive only. It does not automatically drop columns, rename columns, rewrite constraints, or change existing column types.

This repair is script-based. For attribute-model-to-live-database diffing, use provider model synchronizers described in [14 - Model Synchronizer](14-model-synchronizer.md).

## Why History Tables?

ModelSync uses history tables because catalog checks alone cannot describe migration state.

Provider catalogs can tell whether an object exists. They cannot reliably tell which script version was applied, whether a seed script already ran, when a script was last updated, or which SQL hash was deployed.

ModelSync therefore combines:

- history tables for script state and hashes
- provider catalog checks for live verification, such as missing-column repair

## Optional Database Reset

Database reset is destructive and requires the structured reset contract. `DestructiveOptions.Allow()` alone is not enough for reset, because a wrong production connection string would otherwise be too easy to approve accidentally.

```csharp
var options = new MigrationRunnerOptions
{
    ResetDatabase = true,
    ResetOptions = new DatabaseResetOptions
    {
        Enabled = true,
        Approval = DestructiveOperationOptions.Allow(),
        ExpectedDatabaseName = "appdb",
        EnvironmentName = "Development",
        AllowedEnvironments = new[] { "Development" }
    }
};
```

ModelSync rejects empty expected database names, environment mismatches, and known provider system databases such as SQL Server `master`, PostgreSQL `template0`, and MySQL `information_schema`.

When reset is enabled, ModelSync performs the destructive reset before acquiring the provider-native migration lock. This prevents SQL Server `DROP DATABASE` / `ALTER DATABASE` operations from breaking the lock session. After the reset finishes, ModelSync waits for the target database to become reachable using `ReadinessRetryCount` and `ReadinessRetryDelay`, then infrastructure and migration scripts run under the normal migration lock.

Treat reset as a deployment-time or local-development operation, not as a normal multi-instance application startup path. Because the target database may not exist during reset, the provider-native target migration lock is acquired after reset and readiness. If more than one application instance can start at the same time, run reset from a single deployment job or external orchestrator.

SQL Server reset can optionally create a database backup before the database is dropped:

```csharp
var options = new MigrationRunnerOptions
{
    ResetDatabase = true,
    ResetOptions = new DatabaseResetOptions
    {
        Enabled = true,
        Approval = DestructiveOperationOptions.Allow(),
        ExpectedDatabaseName = "appdb",
        BackupBeforeReset = true,
        BackupDirectory = @"C:\SqlBackups",
        BackupFileName = "appdb-before-reset.bak"
    }
};
```

You can also set `BackupFilePath` directly. Backup paths are SQL Server paths, so they must be valid from the SQL Server service account's point of view. If `BackupBeforeReset` is false, ModelSync resets without creating a backup.

## Execution Results

`RunAsync()` is preserved for compatibility and returns the migration plans produced before execution. New code can call:

```csharp
MigrationExecutionResult result = await runner.RunWithResultAsync();
```

The result contains one item per script with category, script id, source, action, hash, timing, batch counts, and sanitized failure metadata. It also contains root failure metadata when infrastructure fails before a script item can be created. It does not include passwords, full connection strings, or full SQL script text. Failed batch previews are bounded and SQL string literal values are redacted.

Execution states describe observed outcomes: `RolledBack` means a started transaction was rolled back, while `PartiallyApplied` means mutation occurred without a successful rollback before a later failure. A failed history write is part of the same transaction on providers that support transactional DDL.

## Operational Contracts

ModelSync now exposes additive contracts for deployment-grade hosting:

- `ModelSyncConnectionFactory` for adapting application-owned connection factories.
- `IDatabaseReadinessStrategy` for provider-specific readiness retry.
- `IMigrationLockStrategy` and `MigrationLockOptions` for provider-specific migration locking.
- `MigrationTransactionPolicy` for transaction policy selection.

SQL Server and PostgreSQL run script batches and history writes in the same execution transaction when the provider and script permit it. SQLite uses its `BEGIN IMMEDIATE` path. MySQL/MariaDB DDL often performs implicit commits, so ModelSync does not claim full migration/history atomicity for that provider.

## SQL Server GO Support

SQL Server runner splits scripts on `GO` batch separators. Other providers execute scripts as provider-native single commands.
