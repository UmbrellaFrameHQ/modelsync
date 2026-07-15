# ModelSync

![ModelSync](https://raw.githubusercontent.com/UmbrellaFrameHQ/modelsync/main/assets/icons/modelsync-core.png)

[![NuGet](https://img.shields.io/nuget/v/UmbrellaFrame.ModelSync.Core.svg?style=flat-square)](https://www.nuget.org/packages/UmbrellaFrame.ModelSync.Core)
[![CI](https://github.com/UmbrellaFrameHQ/modelsync/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/UmbrellaFrameHQ/modelsync/actions/workflows/ci.yml?query=branch%3Amain)
[![License](https://img.shields.io/badge/license-MIT-blue.svg?style=flat-square)](https://github.com/UmbrellaFrameHQ/modelsync/blob/main/LICENSE)

ModelSync is a schema and migration toolkit for .NET projects that want explicit database changes without adopting a full ORM. It generates provider-specific DDL from attributed C# models, compares models with live databases, runs ordered SQL migrations, synchronizes stored procedures, and produces deployment reports.

Current version: **1.3.0**

## What's New in 1.3.0

Version 1.3.0 adds reviewable migration reports, a safer CLI workflow, stronger model analyzers, and clearer provider support boundaries. Existing generator and migration APIs remain available.

## Choose A Workflow

| Goal | API |
|---|---|
| Generate table SQL from a model | `TableGenerator` |
| Compare models with a live database | `ModelSynchronizer` |
| Run ordered SQL files with history | `MigrationRunner` |
| Synchronize procedure files | `StoredProcedureSynchronizer` |
| Validate, preview, and run from CI | `modelsync` CLI |

## Install

Install the provider you need. Core is included automatically.

```bash
dotnet add package UmbrellaFrame.ModelSync.SqlServer --version 1.3.0
dotnet add package UmbrellaFrame.ModelSync.MySql --version 1.3.0
dotnet add package UmbrellaFrame.ModelSync.PostgreSQL --version 1.3.0
dotnet add package UmbrellaFrame.ModelSync.SQLite --version 1.3.0
dotnet add package UmbrellaFrame.ModelSync.Analyzers --version 1.3.0
```

`UmbrellaFrame.ModelSync.Oracle` is a preview provider for table DDL and partial safe model synchronization. Its migration runner, routine synchronization, reset, and native lock features are not production-ready.

## Quick Start

```csharp
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.SqlServer;

[SqlServerTableName("Products")]
public sealed class Product
{
    [SqlServerColumnType(SqlServerColumnType.INT)]
    [SqlServerColumnPrimaryKey(isAutoIncrement: true)]
    public int Id { get; set; }

    [SqlServerColumnType(SqlServerColumnType.NVARCHAR, "200")]
    [SqlServerColumnNotNull]
    [DbColumnIndex("IX_Products_Name")]
    public string Name { get; set; } = string.Empty;
}

var generator = new SqlServerTableGenerator(connectionString);
var sql = generator.GenerateSqlServerTable<Product>(ifNotExists: true);

Console.WriteLine(sql);
await generator.CreateTablesAsync();
```

Generate first, inspect the SQL, then execute. Destructive methods require explicit approval:

```csharp
var allow = DestructiveOperationOptions.Allow();
generator.DropColumn<Product>("LegacyCode", allow);
generator.AlterColumnType<Product>("Price", allow);
await generator.DropTablesAsync(allow);
```

## Live Model Comparison

```csharp
var options = new SqlServerModelSyncOptions
{
    ConnectionString = connectionString,
    DefaultSchema = "app",
    HistorySchema = "sec"
};

var result = await SqlServerModelSynchronizer
    .FromAssemblies(options, typeof(Product).Assembly)
    .CompareAsync(cancellationToken);

// Review AutomaticOperations, ManualOperations, SkippedOperations,
// and BlockedOperations before applying.
await result.ApplyAsync(cancellationToken);
```

Safe additive operations can be automatic. Drop, rename, narrowing, and risky nullability changes are reported instead of silently executed.

## Ordered Migrations

```csharp
var runner = new SqlServerMigrationRunner(connectionString);

runner.RegisterScriptFile("Database/Scripts/Tables/001_CreateProducts.sql");
runner.RegisterScriptFile("Database/Scripts/StoredProcedures/010_GetProducts.sql");
runner.RegisterScriptFile("Database/Scripts/Seeds/020_DefaultProducts.sql");

var plan = await runner.CompareRegisteredAsync(cancellationToken); // read-only
var result = await runner.RunWithResultAsync(cancellationToken);
```

Scripts run in this order:

```text
Tables -> StoredProcedures -> Triggers -> Seeds -> CustomSql
```

History rows and SQL hashes make repeat runs predictable. Registered SQL files are trusted project artifacts; do not build them from user input.

## CLI And Reports

```bash
dotnet tool install --global UmbrellaFrame.ModelSync.Cli --version 1.3.0
```

Pass secrets through an environment variable instead of a process argument:

```bash
export MODELSYNC_CONNECTION_STRING='Data Source=modelsync-preview.db'

modelsync validate --scripts ./Database/Scripts

modelsync run \
  --provider sqlite \
  --connection-env MODELSYNC_CONNECTION_STRING \
  --scripts ./Database/Scripts \
  --dry-run

modelsync run \
  --provider sqlite \
  --connection-env MODELSYNC_CONNECTION_STRING \
  --scripts ./Database/Scripts \
  --apply \
  --report-md ./artifacts/modelsync-report.md \
  --report-json ./artifacts/modelsync-report.json
```

`--apply` is required for mutation. Ctrl+C is forwarded to the migration operation. The inline `--connection` option is retained for compatibility but may be visible in process listings.

## Safety Notes

- ModelSync is not an ORM and does not provide entity tracking, LINQ, or runtime CRUD.
- Compare APIs are read-only; infrastructure is created by explicit mutation APIs.
- `DbColumnDefault`, `DbColumnCheck`, and migration scripts contain reviewed SQL and must not use untrusted input.
- Prefer a deployment-time migration job. If startup migration is unavoidable, keep provider-native locking enabled.
- Database reset requires explicit destructive approval, an expected database name, environment validation, and system-database protection.
- SQL Server can optionally back up a database before reset.

## Provider Snapshot

| Feature | SQL Server | MySQL/MariaDB | PostgreSQL | SQLite | Oracle preview |
|---|:---:|:---:|:---:|:---:|:---:|
| Table DDL | Yes | Yes | Yes | Yes | Yes |
| Safe model sync | Yes | Yes | Yes | Yes | Partial |
| Migration runner | Yes | Yes | Yes | Yes | No |
| Stored procedures | Yes | Yes | Yes | No | No |
| Native migration lock | Yes | Yes | Yes | Write lock | No |

## Documentation

- [Full usage guide](https://github.com/UmbrellaFrameHQ/modelsync/blob/main/docs/13-full-usage-guide-en.md)
- [Türkçe tam kullanım kılavuzu](https://github.com/UmbrellaFrameHQ/modelsync/blob/main/docs/13-full-usage-guide-tr.md)
- [Provider support matrix](https://github.com/UmbrellaFrameHQ/modelsync/blob/main/docs/provider-support-matrix.md)
- [Migration runner](https://github.com/UmbrellaFrameHQ/modelsync/blob/main/docs/12-migration-runner.md)
- [CLI and reporting](https://github.com/UmbrellaFrameHQ/modelsync/blob/main/docs/migration-reporting.md)
- [1.3.0 release notes](https://github.com/UmbrellaFrameHQ/modelsync/blob/main/docs/releases/1.3.0.md)

MIT © UmbrellaFrame
