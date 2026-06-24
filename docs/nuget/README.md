# ModelSync

![ModelSync](https://raw.githubusercontent.com/UmbrellaFrameHQ/modelsync/main/assets/icons/modelsync-core.png)

[![NuGet](https://img.shields.io/nuget/v/UmbrellaFrame.ModelSync.Core.svg?style=flat-square)](https://www.nuget.org/packages/UmbrellaFrame.ModelSync.Core)
[![CI](https://github.com/UmbrellaFrameHQ/modelsync/actions/workflows/ci.yml/badge.svg)](https://github.com/UmbrellaFrameHQ/modelsync/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](https://github.com/UmbrellaFrameHQ/modelsync/blob/main/LICENSE)
[![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-purple?style=flat-square)](https://learn.microsoft.com/dotnet/standard/net-standard)

ModelSync is an attribute-based SQL schema generator for .NET. It lets you define database schema with plain C# classes and generate or execute DDL without Entity Framework or a heavy ORM.

## What's New in 1.0.6

- Provider migration runners can apply ordered table, stored procedure, trigger, and seed scripts.
- Migration history tables track script `Id`, `Name`, `SqlHash`, `AppliedAt`, and `UpdateAt`.
- Embedded `.sql` resources can be discovered and applied.
- SQL Server migration scripts support `GO` batch splitting.
- Changed table scripts can repair missing columns additively.
- Stored procedure synchronization supports SQL Server, MySQL/MariaDB, and PostgreSQL.

## Packages

| Package | Purpose |
|---|---|
| `UmbrellaFrame.ModelSync.Core` | Shared attributes, interfaces, and SQL builder |
| `UmbrellaFrame.ModelSync.MySql` | MySQL and MariaDB provider |
| `UmbrellaFrame.ModelSync.SqlServer` | SQL Server and Azure SQL provider |
| `UmbrellaFrame.ModelSync.PostgreSQL` | PostgreSQL provider |
| `UmbrellaFrame.ModelSync.SQLite` | SQLite provider |
| `UmbrellaFrame.ModelSync.Analyzers` | Roslyn compile-time model validation |

## Install

Install only the provider you need:

```bash
dotnet add package UmbrellaFrame.ModelSync.MySql
dotnet add package UmbrellaFrame.ModelSync.SqlServer
dotnet add package UmbrellaFrame.ModelSync.PostgreSQL
dotnet add package UmbrellaFrame.ModelSync.SQLite
```

Optional analyzer package:

```bash
dotnet add package UmbrellaFrame.ModelSync.Analyzers
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

var plans = await runner.CompareRegisteredAsync();
await runner.RunAsync();
```

Scripts run in this order:

```text
Tables -> StoredProcedures -> Triggers -> Seeds
```

Migration runners create history tables, store script hashes, support embedded `.sql` resources, and can add missing columns from changed table scripts. SQL Server supports `GO` batch splitting.

## Analyzer Rules

| Rule | Description |
|---|---|
| `MSYNC001` | Public property is missing a column type attribute |
| `MSYNC002` | Class has column attributes but no table name attribute |
| `MSYNC003` | Model table has no primary key defined |

## Documentation

- Repository: https://github.com/UmbrellaFrameHQ/modelsync
- Quick start: https://github.com/UmbrellaFrameHQ/modelsync/blob/main/docs/02-quickstart.md
- Provider guides: https://github.com/UmbrellaFrameHQ/modelsync/blob/main/docs/04-providers.md
- Stored procedure sync: https://github.com/UmbrellaFrameHQ/modelsync/blob/main/docs/11-stored-procedures.md
- Examples: https://github.com/UmbrellaFrameHQ/modelsync/tree/main/examples

## Notes

ModelSync validates table, column, database, and index identifiers before quoting them. Names with spaces, dots, semicolons, quotes, or hyphens are rejected intentionally.

ModelSync runtime packages are focused on SQL schema generation. Visual Studio tooling and scaffolding experiments should live in separate repositories so provider packages stay small and predictable.
