# ModelSync

![ModelSync](https://raw.githubusercontent.com/UmbrellaFrameHQ/modelsync/main/assets/icons/modelsync-core.png)

[![NuGet](https://img.shields.io/nuget/v/UmbrellaFrame.ModelSync.Core.svg?style=flat-square)](https://www.nuget.org/packages/UmbrellaFrame.ModelSync.Core)
[![CI](https://github.com/UmbrellaFrameHQ/modelsync/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/UmbrellaFrameHQ/modelsync/actions/workflows/ci.yml?query=branch%3Amain)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](https://github.com/UmbrellaFrameHQ/modelsync/blob/main/LICENSE)
[![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-purple?style=flat-square)](https://learn.microsoft.com/dotnet/standard/net-standard)

ModelSync is for .NET projects that want database schema to stay explicit, close to the code, and easy to review. You define tables with plain C# classes, keep migration SQL in the project, and use ModelSync to generate provider-specific DDL, run ordered migrations, synchronize stored procedures, and produce deployment reports without adopting a heavy ORM.

Framework-owned SQL is rendered by ModelSync Core through a provider-agnostic compiler. Provider packages supply structured descriptors and thin ADO.NET adapters; application-supplied SQL files remain user-authored artifacts.

## 1.3.0 CLI, Dry-Run and Migration Reporting

The current package line is `1.3.0`. This release adds the first official CLI, script validation, dry-run previews, and Markdown/JSON migration reports. Existing 1.2.x code remains source-compatible, but teams now have a more practical workflow for checking and documenting migrations before they reach production.

Previewed capabilities:

- `MigrationScriptExecutionMode.RunOnce`, `HashTracked`, and `EveryRun`.
- Per-category `CategoryPolicies`.
- `MigrationCompatibilityProfiles.LegacyEmbeddedSql`.
- Additive history `SqlHash` upgrade for old history tables.
- Legacy row adoption without duplicate seed execution.
- Stored procedure and trigger `EveryRun` behavior.
- `CustomSql` history bootstrap.
- Compare APIs stay read-only.

SQLite stored procedures remain unsupported.

## What's New in 1.3.0

- Provider migration runners can apply ordered table, stored procedure, trigger, seed, and custom SQL scripts.
- Migration history tables track script `Id`, `Name`, `SqlHash`, `AppliedAt`, and `UpdateAt`.
- Embedded `.sql` resources can be discovered and applied.
- SQL Server migration scripts support `GO` batch splitting.
- Changed table scripts can repair missing columns additively.
- Stored procedure synchronization supports SQL Server, MySQL/MariaDB, and PostgreSQL.
- Provider model synchronizers can compare attribute models with a live database and apply only safe additive changes.
- Migration runners explain why history tables are used instead of relying only on live catalog checks.
- Migration execution results can be rendered as Markdown and JSON reports for deployment evidence.
- The `modelsync` CLI can validate migration script folders, preview plans with `--dry-run`, run migrations, and write Markdown/JSON reports without adding another migration engine.
- Oracle provider preview adds source/local-package table DDL generation and targets `netstandard2.1`; public NuGet publication is pending package-owner API-key permission for the new package ID.
- SQL Server `ResetDatabase=true` now performs the destructive reset before acquiring the provider-native migration lock, so `DROP DATABASE` / `ALTER DATABASE` cannot break the lock release session.
- SQL Server reset can optionally run `BACKUP DATABASE` before dropping the target database by setting `BackupBeforeReset`.

## 1.3.0 Operational Hardening

ModelSync 1.3.0 is the current package line for reporting and production usage clarity.

1.3.0 includes:

- `DbColumnName` for explicit column-name mapping.
- `DbIgnore` for excluding schema-only public helper properties.
- Provider-aware model discovery for live model synchronization.
- Structured identity/auto-increment metadata for SQL Server, MySQL, PostgreSQL, and SQLite synchronizers.
- Read-only migration comparison; infrastructure creation is explicit through `RunAsync()` or `EnsureInfrastructureAsync()`.
- `SkippedOperations` for safe operations disabled by options.
- Structured reset options, readiness strategy contracts, migration lock contracts, transaction policy metadata, and `RunWithResultAsync()` execution reporting.
- Controlled DB reset support with explicit approval, expected database validation, provider system-database protection, SQL Server reset-before-lock ordering, and optional SQL Server backup-before-reset.

## Packages

| Package | Purpose |
|---|---|
| `UmbrellaFrame.ModelSync.Core` | Shared attributes, interfaces, and SQL builder |
| `UmbrellaFrame.ModelSync.MySql` | MySQL and MariaDB provider |
| `UmbrellaFrame.ModelSync.SqlServer` | SQL Server and Azure SQL provider |
| `UmbrellaFrame.ModelSync.PostgreSQL` | PostgreSQL provider |
| `UmbrellaFrame.ModelSync.SQLite` | SQLite provider |
| `UmbrellaFrame.ModelSync.Oracle` | Oracle provider preview for table DDL; public NuGet pending |
| `UmbrellaFrame.ModelSync.Analyzers` | Roslyn compile-time model validation |
| `UmbrellaFrame.ModelSync.Cli` | `modelsync` command-line migration runner and report tool |

## Install

Install only the provider you need:

```bash
dotnet add package UmbrellaFrame.ModelSync.Core --version 1.3.0
dotnet add package UmbrellaFrame.ModelSync.MySql --version 1.3.0
dotnet add package UmbrellaFrame.ModelSync.SqlServer --version 1.3.0
dotnet add package UmbrellaFrame.ModelSync.PostgreSQL --version 1.3.0
dotnet add package UmbrellaFrame.ModelSync.SQLite --version 1.3.0
```

Oracle note: the Oracle provider exists in source and local package validation as a preview provider. Do not document it as a public NuGet dependency until the package-owner API key can create/publish `UmbrellaFrame.ModelSync.Oracle`.

Optional analyzer package:

```bash
dotnet add package UmbrellaFrame.ModelSync.Analyzers --version 1.3.0
```

CLI tool:

```bash
dotnet tool install --global UmbrellaFrame.ModelSync.Cli --version 1.3.0
modelsync version
```

Safe-first CLI flow:

```bash
modelsync validate --scripts ./Database/Scripts
modelsync run --provider sqlite --connection "Data Source=modelsync-preview.db" --scripts ./Database/Scripts --dry-run
```

## Quick Start

```csharp
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.MySql;

[MySqlTableName("products")]
public sealed class ProductModel
{
    [MySqlColumnType(MySqlColumnType.INT)]
    [MySqlColumnPrimaryKey(isAutoIncrement: true)]
    public int Id { get; set; }

    [MySqlColumnType(MySqlColumnType.VARCHAR, "255")]
    [MySqlColumnNotNull]
    [DbColumnIndex("idx_products_name")]
    public string Name { get; set; } = string.Empty;

    [MySqlColumnType(MySqlColumnType.DECIMAL, "10,2")]
    [DbColumnDefault("0.00")]
    [DbColumnCheck("Price >= 0")]
    public decimal Price { get; set; }
}

var generator = new MySqlTableGenerator(
    "Server=localhost;Database=appdb;User=root;Password=pass;");

var sql = generator.GenerateMySqlTable<ProductModel>(ifNotExists: true);
var indexes = generator.GenerateIndexSql<ProductModel>();

Console.WriteLine(sql);
foreach (var indexSql in indexes)
{
    Console.WriteLine(indexSql);
}
```

## Safe DDL

Additive changes can run directly:

```csharp
generator.AddColumn<ProductModel>("Stock");
```

Destructive operations require explicit opt-in:

```csharp
var allow = DestructiveOperationOptions.Allow();

generator.DropColumn<ProductModel>("LegacyCode", allow);
generator.AlterColumnType<ProductModel>("Price", allow);
generator.DropTables(allow);
```

`DbColumnDefault` and `DbColumnCheck` accept raw SQL expressions by design. Do not build those expressions from user input; keep them as reviewed, hard-coded schema definitions.

## Live Model Synchronization

ModelSync can compare C# attribute models with a live database and build a dry-run plan before applying changes.

Only safe additive changes are applied automatically. Destructive or risky operations are reported and blocked.

If you want a direct explicit column operation, you can still use:

```csharp
generator.AddColumn<ProductModel>("Stock");
```

The provider synchronizers are dry-run-first:

```csharp
var options = new SqlServerModelSyncOptions
{
    ConnectionString = connectionString,
    HistorySchema = "sec",
    DefaultSchema = "app",
    AllowDestructiveChanges = false,
    ApplyStoredProceduresOnEveryRun = true,
    ApplyTriggersOnEveryRun = true
};

var result = await SqlServerModelSynchronizer
    .FromAssemblies(options, typeof(ProductModel).Assembly)
    .CompareAsync();

await result.ThrowIfUnsupportedOrDestructiveAsync();
await result.ApplyAsync();
```

The migration runner has a different source of truth: SQL scripts. If an already-applied table script changes, ModelSync compares the script hash from the history table and can add missing columns from the changed `CREATE TABLE` script. That additive repair is script-based, not model-property-based.

`FromAssemblies` is provider-aware and `FromTypes` scopes synchronization to the supplied model types. Extra database tables are reported as blocked `DropTable` operations only when `ReportUnmappedTables = true`. Registered SQL scripts are trusted project artifacts; ModelSync does not parse arbitrary script text for destructive SQL.

ModelSync 1.2.0 adds table execution policies for mixed manual/automatic ownership:

```csharp
options.DefaultTableMode = ModelSyncTableMode.ManualOnly;
options.TablePolicies
    .ForType<AuditLog>(ModelSyncTableMode.ApplySafeChanges)
    .ForType<Notification>(ModelSyncTableMode.ApplySafeChanges)
    .ForTable("legacy", "OldOrders", ModelSyncTableMode.Ignore);
```

`ManualOnly` operations are reported through `ManualOperations` and are never applied automatically. `ApplySafeChanges` applies only safe provider-supported changes; destructive changes remain blocked.

## Stored Procedures

Stored procedures can be kept as project `.sql` files and synchronized with SQL Server, MySQL/MariaDB, and PostgreSQL:

```csharp
using UmbrellaFrame.ModelSync.SqlServer;

var procedures = new SqlServerStoredProcedureSynchronizer(connectionString);
procedures.RegisterProcedureFile("Database/Procedures/SqlServer/dbo.usp_GetProducts.sql");

var plans = await procedures.CompareRegisteredAsync();
await procedures.SyncRegisteredAsync();
```

Provider behavior:

| Provider | Strategy |
|---|---|
| SQL Server / Azure SQL | `CREATE OR ALTER PROCEDURE` |
| MySQL / MariaDB | `DROP PROCEDURE IF EXISTS` + `CREATE PROCEDURE` |
| PostgreSQL | `CREATE OR REPLACE PROCEDURE` |
| SQLite | Not supported |

## Migration Runner

Provider migration runners can apply ordered project SQL scripts and record migration history:

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

Scripts run in this order:

```text
Tables -> StoredProcedures -> Triggers -> Seeds -> CustomSql
```

Migration runners create history tables, store script hashes, support embedded `.sql` resources, and can add missing columns from changed table scripts. SQL Server supports `GO` batch splitting.

### Why History Tables?

ModelSync uses migration history tables because live catalog checks alone cannot answer every migration question.

A database catalog can tell whether a table, column, procedure, trigger, or seed target exists. It cannot reliably tell:

- which script version was applied
- whether the script text changed
- when it was applied
- whether a seed script already ran
- whether a procedure or trigger was updated from the current project file
- what hash was used for the last applied script

For that reason, ModelSync combines two ideas:

- history tables track applied script state and hashes
- provider catalog checks are used where live verification is needed, such as missing-column repair

SQL Server and PostgreSQL store history tables under the `sec` schema. MySQL/MariaDB and SQLite store them in the current database.

## Analyzer Rules

| Rule | Description |
|---|---|
| `MSYNC001` | Public property is missing a column type attribute |
| `MSYNC002` | Class has column attributes but no table name attribute |
| `MSYNC003` | Model table has no primary key defined |

## Documentation

- Repository: https://github.com/UmbrellaFrameHQ/modelsync
- Full usage guide (English): https://github.com/UmbrellaFrameHQ/modelsync/blob/main/docs/13-full-usage-guide-en.md
- Tam kullanim kilavuzu (Turkce): https://github.com/UmbrellaFrameHQ/modelsync/blob/main/docs/13-full-usage-guide-tr.md
- Quick start: https://github.com/UmbrellaFrameHQ/modelsync/blob/main/docs/02-quickstart.md
- Provider guides: https://github.com/UmbrellaFrameHQ/modelsync/blob/main/docs/04-providers.md
- Stored procedure sync: https://github.com/UmbrellaFrameHQ/modelsync/blob/main/docs/11-stored-procedures.md
- Migration runner: https://github.com/UmbrellaFrameHQ/modelsync/blob/main/docs/12-migration-runner.md
- Model synchronizer: https://github.com/UmbrellaFrameHQ/modelsync/blob/main/docs/14-model-synchronizer.md
- Examples: https://github.com/UmbrellaFrameHQ/modelsync/tree/main/examples

## Notes

ModelSync validates table, column, database, and index identifiers before quoting them. Names with spaces, dots, semicolons, quotes, or hyphens are rejected intentionally.

ModelSync runtime packages are focused on SQL schema generation. Visual Studio tooling and scaffolding experiments should live in separate repositories so provider packages stay small and predictable.
