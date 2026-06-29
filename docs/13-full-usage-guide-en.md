# ModelSync - NuGet Full Usage Guide

Installation, model definition, SQL generation, DDL execution, migration runner, stored procedure synchronization, live model synchronization, analyzers, testing, troubleshooting, and production usage.

**Version scope:** 1.2.0
**Author:** UmbrellaFrame / ModelSync

# Table Of Contents

1. [About This Guide](#about-this-guide)
2. [What ModelSync Is](#what-modelsync-is)
3. [What ModelSync Is Not](#what-modelsync-is-not)
4. [Package Architecture](#package-architecture)
5. [Installation](#installation)
6. [Basic Workflow](#basic-workflow)
7. [First Complete Example - MySQL/MariaDB](#first-complete-example---mysqlmariadb)
8. [Provider Quick Starts](#provider-quick-starts)
9. [Attribute System](#attribute-system)
10. [Provider Column Types](#provider-column-types)
11. [SQL Generation API](#sql-generation-api)
12. [Table And Column Operations](#table-and-column-operations)
13. [Dependency Injection And Startup](#dependency-injection-and-startup)
14. [Logging](#logging)
15. [Migration Runner](#migration-runner)
16. [Stored Procedure Synchronization](#stored-procedure-synchronization)
17. [Live Model Synchronization](#live-model-synchronization)
18. [Analyzer](#analyzer)
19. [Troubleshooting](#troubleshooting)
20. [Testing Approach](#testing-approach)
21. [Production Guide](#production-guide)
22. [Complete Project Structure](#complete-project-structure)
23. [Quick API Reference](#quick-api-reference)
24. [Version 1.2.0 Limits](#version-108-limits)
25. [FAQ](#faq)
26. [Conclusion](#conclusion)

# About This Guide

This guide is written for .NET developers who install **ModelSync 1.2.0** from NuGet and want to use it correctly without reading the source code first.

> Legacy runner note: ModelSync 1.2.0 includes compatibility support for embedded SQL runners. See [Legacy Runner Migration - English](legacy-runner-migration-en.md).

### Legacy Execution Modes Preview

The upcoming compatibility work introduces `RunOnce`, `HashTracked`, and `EveryRun` migration script execution modes through `MigrationRunnerOptions.CategoryPolicies`.

`MigrationCompatibilityProfiles.LegacyEmbeddedSql` maps stored procedures and triggers to `EveryRun`, seeds to `RunOnce`, and custom SQL to `HashTracked`. Compare APIs remain read-only: they can report required `SqlHash` upgrade and legacy hash adoption, but they do not mutate history tables or execute scripts.

It covers installation, model definition, SQL generation, table creation, index generation, column operations, migration scripts, stored procedure synchronization, dependency injection, logging, analyzers, testing, and production safety.

> **Most important definition:** ModelSync is not an ORM. It does not save objects as rows, generate LINQ queries, track changes, or provide CRUD repositories. ModelSync exists to generate and optionally execute database DDL from C# metadata and to manage project-side SQL scripts in a controlled way.

# What ModelSync Is

ModelSync is an ORM-free .NET library that reads provider-specific attributes from plain C# classes and generates SQL schema statements.

Main use cases:

- Generate `CREATE TABLE` SQL from C# models.
- Execute generated table SQL against a live database.
- Generate `DROP TABLE`, `TRUNCATE TABLE`, and `CREATE INDEX` SQL.
- Add, drop, rename, or alter columns from explicit attribute metadata.
- Block destructive operations unless the developer explicitly opts in.
- Apply SQL-file-based migration scripts in category order.
- Compare and synchronize SQL Server, MySQL/MariaDB, and PostgreSQL stored procedure files with a live database.
- Report missing ModelSync attributes at compile time through Roslyn analyzers.

# What ModelSync Is Not

| Expectation | ModelSync behavior |
|---|---|
| `Insert`, `Update`, `Delete`, `Select` operations | Not provided. Use Dapper, ADO.NET, EF Core, or another data access tool. |
| LINQ query provider | Not provided. |
| Entity change tracking | Not provided. |
| Automatic silent destructive model-to-live-database mutation | Not provided. Model synchronizers are dry-run-first and apply only safe additive operations automatically. |
| Automatic repair for every possible schema drift | Not provided. Automatic repair is limited to simple missing-column scenarios from changed table scripts. |
| Automatic index execution inside `CreateTables()` | Not performed. `GenerateIndexSql<T>()` returns SQL only; execution is separate. |
| SQLite stored procedures | Not supported because SQLite does not support stored procedures. |
| Relationship navigation model | Not provided. Foreign key SQL is declared explicitly through attributes or migration scripts. |

# Package Architecture

| NuGet package | Purpose | Install directly? |
|---|---|---|
| `UmbrellaFrame.ModelSync.Core` | Shared attributes, interfaces, SQL builder infrastructure, migration and stored procedure models | Normally pulled by provider packages. Install directly only when building a provider. |
| `UmbrellaFrame.ModelSync.SqlServer` | SQL Server / Azure SQL table, migration, and stored procedure implementation | Yes, for SQL Server / Azure SQL. |
| `UmbrellaFrame.ModelSync.MySql` | MySQL / MariaDB implementation | Yes, for MySQL / MariaDB. |
| `UmbrellaFrame.ModelSync.PostgreSQL` | PostgreSQL implementation | Yes, for PostgreSQL. |
| `UmbrellaFrame.ModelSync.SQLite` | SQLite implementation | Yes, for SQLite. |
| `UmbrellaFrame.ModelSync.Analyzers` | Compile-time model validation | Optional, recommended. |

All packages target `netstandard2.0`.

# Installation

Install only the provider you need.

SQL Server / Azure SQL:

```bash
dotnet add package UmbrellaFrame.ModelSync.SqlServer --version 1.2.0
```

MySQL / MariaDB:

```bash
dotnet add package UmbrellaFrame.ModelSync.MySql --version 1.2.0
```

PostgreSQL:

```bash
dotnet add package UmbrellaFrame.ModelSync.PostgreSQL --version 1.2.0
```

SQLite:

```bash
dotnet add package UmbrellaFrame.ModelSync.SQLite --version 1.2.0
```

Analyzer:

```bash
dotnet add package UmbrellaFrame.ModelSync.Analyzers --version 1.2.0
```

Common namespaces:

```csharp
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.Interfaces;
```

Provider namespaces:

```csharp
using UmbrellaFrame.ModelSync.SqlServer;
using UmbrellaFrame.ModelSync.MySql;
using UmbrellaFrame.ModelSync.PostgreSQL;
using UmbrellaFrame.ModelSync.SQLite;
```

# Basic Workflow

ModelSync separates SQL generation from SQL execution.

1. `Generate...Table<T>()` reads the model, generates SQL, and stores it in the generator instance cache.
2. `CreateTables()` or `CreateTablesAsync()` executes cached SQL statements.

```csharp
var generator = new MySqlTableGenerator(connectionString);

var sql = generator.GenerateMySqlTable<Product>(ifNotExists: true);
Console.WriteLine(sql);

await generator.CreateTablesAsync();
```

Important: the cache belongs to the generator instance. If you create a new generator, the previous instance cache is not carried over.

# First Complete Example - MySQL/MariaDB

```csharp
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.MySql;

[MySqlTableName("products")]
public sealed class Product
{
    [MySqlColumnType(MySqlColumnType.INT)]
    [MySqlColumnPrimaryKey(isAutoIncrement: true)]
    public int Id { get; set; }

    [MySqlColumnType(MySqlColumnType.VARCHAR, "200")]
    [MySqlColumnNotNull]
    [DbColumnIndex("idx_products_name")]
    public string Name { get; set; } = string.Empty;

    [MySqlColumnType(MySqlColumnType.DECIMAL, "18,2")]
    [DbColumnDefault("0.00")]
    [DbColumnCheck("Price >= 0")]
    public decimal Price { get; set; }

    [MySqlColumnType(MySqlColumnType.INT)]
    [DbColumnDefault("0")]
    public int Stock { get; set; }

    [MySqlColumnType(MySqlColumnType.DATETIME)]
    [DbColumnDefault("CURRENT_TIMESTAMP")]
    public DateTime CreatedAt { get; set; }
}
```

Create the database if the user has permission:

```csharp
var connectionString =
    "Server=localhost;Port=3306;Database=shopdb;User ID=root;Password=secret;";

var generator = new MySqlTableGenerator(connectionString);
await generator.CreateDatabaseAsync();
```

Generate and execute table SQL:

```csharp
var createSql = generator.GenerateMySqlTable<Product>(ifNotExists: true);
Console.WriteLine(createSql);

await generator.CreateTablesAsync();
```

Generate index SQL:

```csharp
var indexSqlList = generator.GenerateIndexSql<Product>();

foreach (var indexSql in indexSqlList)
{
    Console.WriteLine(indexSql);
}
```

`GenerateIndexSql<T>()` returns SQL only. Execute it yourself through ADO.NET, or manage indexes through migration scripts.

# Provider Quick Starts

## SQL Server / Azure SQL

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
    public string Name { get; set; } = string.Empty;

    [SqlServerColumnType(SqlServerColumnType.DECIMAL, "18,2")]
    [DbColumnDefault("0")]
    [DbColumnCheck("Price >= 0")]
    public decimal Price { get; set; }
}

var generator = new SqlServerTableGenerator(connectionString);
var sql = generator.GenerateSqlServerTable<Product>(ifNotExists: true);
Console.WriteLine(sql);
await generator.CreateTablesAsync();
```

SQL Server does not support inline `CREATE TABLE IF NOT EXISTS`; the provider generates an `OBJECT_ID` guard.

For SQL Server `ifNotExists`, prefer the provider method:

```csharp
generator.GenerateSqlServerTable<Product>(true);
await generator.CreateTablesAsync(cancellationToken);
```

## PostgreSQL

```csharp
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.PostgreSQL;

[PostgresTableName("products")]
public sealed class Product
{
    [PostgresColumnType(PostgresColumnType.SERIAL)]
    [PostgresColumnPrimaryKey]
    public int Id { get; set; }

    [PostgresColumnType(PostgresColumnType.VARCHAR, "200")]
    [PostgresColumnNotNull]
    public string Name { get; set; } = string.Empty;

    [PostgresColumnType(PostgresColumnType.NUMERIC, "18,2")]
    [DbColumnDefault("0")]
    public decimal Price { get; set; }
}

var generator = new PostgresTableGenerator(connectionString);
await generator.CreateDatabaseAsync();
generator.GeneratePostgresTable<Product>(ifNotExists: true);
await generator.CreateTablesAsync();
```

## SQLite

```csharp
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.SQLite;

[SQLiteTableName("products")]
public sealed class Product
{
    [SQLiteColumnType(SQLiteColumnType.INTEGER)]
    [SQLiteColumnPrimaryKey]
    public int Id { get; set; }

    [SQLiteColumnType(SQLiteColumnType.TEXT)]
    [SQLiteColumnNotNull]
    public string Name { get; set; } = string.Empty;
}

var generator = new SQLiteTableGenerator("Data Source=shop.db");
generator.GenerateSQLiteTable<Product>(ifNotExists: true);
await generator.CreateTablesAsync();
```

SQLite creates the database file when a connection is opened. `CreateDatabase()` and `CreateDatabaseAsync()` are no-ops.

SQLite limitations:

- Stored procedures are not supported.
- `ALTER COLUMN TYPE` is not supported and throws `NotSupportedException`.
- Use a create-copy-drop/rename strategy for type changes.
- `GenerateTruncateTableSql<T>()` produces `DELETE FROM "Table";` for SQLite because SQLite has no `TRUNCATE TABLE` command.

# Attribute System

## Table Name

```csharp
[MySqlTableName("users")]
[SqlServerTableName("Users")]
[PostgresTableName("users")]
[SQLiteTableName("users")]
```

If no table name attribute exists, the class name is used. Explicit table names are recommended.

## Column Type

Every public property intended as a column should have a provider column type attribute.

```csharp
[MySqlColumnType(MySqlColumnType.VARCHAR, "255")]
public string Email { get; set; } = string.Empty;
```

By default, the column name is the property name. In the current repository, `DbColumnName("database_column")` can override the database column name and `DbIgnore` can exclude public helper properties from schema discovery. The latest NuGet package remains `1.2.0`; these mapping attributes are included in the 1.2.0 package line.

## Primary Key

```csharp
[MySqlColumnType(MySqlColumnType.INT)]
[MySqlColumnPrimaryKey(isAutoIncrement: true)]
public int Id { get; set; }
```

| Provider | Attribute | Auto increment |
|---|---|---|
| SQL Server | `SqlServerColumnPrimaryKey(isAutoIncrement: true)` | `IDENTITY(1,1)` |
| MySQL | `MySqlColumnPrimaryKey(isAutoIncrement: true)` | `AUTO_INCREMENT` |
| PostgreSQL | `PostgresColumnPrimaryKey` | Use `SERIAL` / `BIGSERIAL` column type. |
| SQLite | `SQLiteColumnPrimaryKey` | Use with `INTEGER`. |

## Composite Primary Key

If more than one property is marked as primary key, ModelSync generates a table-level composite primary key.

```csharp
[MySqlTableName("user_roles")]
public sealed class UserRole
{
    [MySqlColumnType(MySqlColumnType.INT)]
    [MySqlColumnPrimaryKey]
    public int UserId { get; set; }

    [MySqlColumnType(MySqlColumnType.INT)]
    [MySqlColumnPrimaryKey]
    public int RoleId { get; set; }
}
```

Avoid auto-increment on composite key properties.

## NOT NULL And UNIQUE

Provider attributes:

- `SqlServerColumnNotNull`, `SqlServerColumnUnique`
- `MySqlColumnNotNull`, `MySqlColumnUnique`
- `PostgresColumnNotNull`, `PostgresColumnUnique`
- `SQLiteColumnNotNull`, `SQLiteColumnUnique`

C# nullable annotations are not automatically converted to SQL nullability. SQL nullability is controlled through attributes.

## Default, Check, And Index

Cross-provider attributes:

```csharp
[DbColumnDefault("CURRENT_TIMESTAMP")]
[DbColumnCheck("Price >= 0")]
[DbColumnIndex("idx_products_name", isUnique: false)]
```

`DbColumnDefault` and `DbColumnCheck` accept raw SQL fragments by design. Never build these values from user input.

## Foreign Keys

```csharp
[MySqlForeignKey("UserId", "users", "Id")]
[SqlServerColumnForeignKey("UserId", "users", "Id")]
[PostgresForeignKey("UserId", "users", "Id")]
[SQLiteColumnForeignKey("UserId", "users", "Id")]
```

Foreign key snippets are intentionally simple in 1.2.0. Avoid spaces, dots, brackets, quoted names, and schema-qualified names in foreign key parameters. Use migration scripts for advanced cascade behavior or schema-qualified constraints.

# Provider Column Types

ModelSync uses provider enum values as SQL type names.

MySQL/MariaDB supports integer, decimal, floating, date/time, text, binary, `ENUM`, `SET`, `JSON`, geometry, bit, and boolean types.

SQL Server supports integer, decimal, money, date/time, char/varchar/nchar/nvarchar, binary/varbinary, uniqueidentifier, xml, geography, geometry, hierarchyid, and bit types.

PostgreSQL supports integer, numeric, serial, date/time, text, bytea, boolean, uuid, json/jsonb, xml, inet/cidr/macaddr, geometric, bit, array/range-like enum values, and related type names.

SQLite supports type affinity values:

| Type | Typical use |
|---|---|
| `INTEGER` | Integer values and integer primary keys |
| `REAL` | Floating point values |
| `TEXT` | Text |
| `BLOB` | Binary data |
| `NUMERIC` | Numeric affinity |

Always review generated SQL for provider-specific edge cases.

# SQL Generation API

## Create Table

```csharp
string sql = generator.GenerateSqlTable<Product>(ifNotExists: true);
Task<string> sqlTask = generator.GenerateSqlTableAsync<Product>(true, cancellationToken);
```

Provider aliases:

```csharp
generator.GenerateMySqlTable<Product>(true);
generator.GenerateSqlServerTable<Product>(true);
generator.GeneratePostgresTable<Product>(true);
generator.GenerateSQLiteTable<Product>(true);
```

SQL generation:

- Reads public properties unless they are marked with `DbIgnore`.
- Uses the table attribute or class name.
- Uses `DbColumnName` when an explicit database column name is provided.
- Requires provider column type attributes.
- Stores generated SQL in the generator cache.
- Does not open a database connection.

## Drop, Truncate, And Index SQL

```csharp
string dropSql = generator.GenerateDropTableSql<Product>();
string truncateSql = generator.GenerateTruncateTableSql<Product>();
List<string> indexSql = generator.GenerateIndexSql<Product>();
```

`GenerateTruncateTableSql<T>()` returns SQL only. It is destructive and not guarded by `DestructiveOperationOptions` because it only generates text. Apply your own execution policy.

# Table And Column Operations

## Create Database

```csharp
await generator.CreateDatabaseAsync(cancellationToken);
```

Provider behavior:

| Provider | Behavior |
|---|---|
| SQL Server | Connects to `master`, creates target database if missing. |
| MySQL/MariaDB | Connects without database, creates target database if missing. |
| PostgreSQL | Connects to `postgres`, creates target database if missing. |
| SQLite | No-op. |

The connection user must have database creation permission.

## Create Tables

```csharp
generator.GenerateMySqlTable<Product>(true);
await generator.CreateTablesAsync(cancellationToken);
```

`CreateTablesAsync()` executes cached SQL. If the cache is empty, it does nothing.

## Add Column

```csharp
await generator.AddColumnAsync<Product>(nameof(Product.Stock), cancellationToken);
```

`AddColumn` reads the property metadata from the model. It does not perform full model-to-database diffing.

## Rename Column

```csharp
await generator.RenameColumnAsync<Product>("Name", "Title", cancellationToken);
```

Renaming is explicit. ModelSync does not infer renames from property changes.

## Drop Column And Alter Type

These operations are destructive or risky and require explicit permission:

```csharp
var allow = DestructiveOperationOptions.Allow();

await generator.DropColumnAsync<Product>("LegacyCode", allow, cancellationToken);
await generator.AlterColumnTypeAsync<Product>("Price", allow, cancellationToken);
```

Calling destructive overloads without permission throws by design.

## Drop Tables

```csharp
var allow = DestructiveOperationOptions.Allow();
await generator.DropTablesAsync(allow, cancellationToken);
```

`DropTables` drops tables known to the generator cache and uses attribute table names.

# Dependency Injection And Startup

Register the provider generator you use:

```csharp
builder.Services.AddSingleton(sp =>
{
    var cs = builder.Configuration.GetConnectionString("SqlServer")
        ?? throw new InvalidOperationException("Connection string missing.");

    return new SqlServerTableGenerator(
        cs,
        sp.GetService<ILogger<SqlServerTableGenerator>>());
});
```

For SQL Server `ifNotExists`, inject `SqlServerTableGenerator` directly and call `GenerateSqlServerTable<T>(true)` before `CreateTablesAsync()`.

Example startup service:

```csharp
public sealed class SchemaInitializer : IHostedService
{
    private readonly SqlServerTableGenerator _generator;

    public SchemaInitializer(SqlServerTableGenerator generator)
        => _generator = generator;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _generator.GenerateSqlServerTable<ProductSchema>(ifNotExists: true);
        await _generator.CreateTablesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
```

# Logging

Provider generators and synchronizers accept optional `ILogger<T>` instances. Log generated SQL and migration plans in development and deployment pipelines, but avoid logging secrets from connection strings.

# Migration Runner

Migration runners apply ordered SQL scripts and record history.

Supported categories:

```text
Tables -> StoredProcedures -> Triggers -> Seeds
```

Example:

```csharp
using UmbrellaFrame.ModelSync.SqlServer;

var runner = new SqlServerMigrationRunner(connectionString);

runner.RegisterScriptFile("Database/Scripts/Tables/001_CreateProducts.sql");
runner.RegisterScriptFile("Database/Scripts/StoredProcedures/010_GetProducts.sql");
runner.RegisterScriptFile("Database/Scripts/Triggers/020_ProductAudit.sql");
runner.RegisterScriptFile("Database/Scripts/Seeds/030_DefaultProducts.sql");

var plans = await runner.CompareRegisteredAsync(cancellationToken);
await runner.RunAsync(cancellationToken);
```

Provider support:

| Provider | Tables | Stored Procedures | Triggers | Seeds | History | Reset | Batch split |
|---|:---:|:---:|:---:|:---:|:---:|:---:|---|
| SQL Server / Azure SQL | Yes | Yes | Yes | Yes | `sec.SchemaMigration_*` | Yes | `GO` |
| MySQL / MariaDB | Yes | Yes | Yes | Yes | `SchemaMigration_*` | Yes | Single statement |
| PostgreSQL | Yes | Yes | Yes | Yes | `sec.SchemaMigration_*` | Yes | Single statement |
| SQLite | Yes | No | Yes | Yes | `SchemaMigration_*` | No | Single statement |

## Embedded SQL Resources

```csharp
runner.RegisterEmbeddedScripts(
    Assembly.GetExecutingAssembly(),
    "MyApp.Database.Scripts.");
```

Resources must end with `.sql`.

## Migration Plans

`CompareRegisteredAsync()` returns dry-run plans:

| Property | Meaning |
|---|---|
| `Definition` | Script metadata |
| `ChangeType` | `None`, `Apply`, or `Reapply` |
| `CurrentHash` | Hash stored in history |
| `TargetHash` | Hash of current script SQL |
| `SqlToApply` | SQL that will be applied |
| `Reason` | Explanation |

## History Tables

ModelSync creates:

```text
SchemaMigration_Tables
SchemaMigration_StoredProcedures
SchemaMigration_Triggers
SchemaMigration_Seeds
SchemaMigration_CustomSql
```

SQL Server and PostgreSQL store these under the `sec` schema. MySQL/MariaDB and SQLite store them in the current database.

History is used because catalog checks alone cannot answer which script version was applied, whether a seed already ran, or which SQL hash was last deployed.

## Missing Column Repair

When an already-applied table script changes, ModelSync can parse simple `CREATE TABLE` scripts and add missing columns.

This repair is additive only:

- no automatic column drop
- no automatic rename
- no automatic type rewrite
- no automatic constraint rewrite

This feature is script-based. Adding a C# property to a model does not trigger automatic live database diffing.

## Optional Database Reset

Database reset is destructive and requires explicit permission:

```csharp
var options = new MigrationRunnerOptions
{
    ResetDatabase = true,
    DestructiveOptions = DestructiveOperationOptions.Allow()
};

var runner = new SqlServerMigrationRunner(connectionString, options);
await runner.RunAsync();
```

If `ResetDatabase` is true without `DestructiveOperationOptions.Allow()`, ModelSync throws before touching the database.

# Stored Procedure Synchronization

Stored procedures can be stored as `.sql` files and synchronized with a live database.

Provider behavior:

| Provider | Strategy |
|---|---|
| SQL Server / Azure SQL | `CREATE OR ALTER PROCEDURE` |
| MySQL / MariaDB | `DROP PROCEDURE IF EXISTS` + `CREATE PROCEDURE` |
| PostgreSQL | `CREATE OR REPLACE PROCEDURE` |
| SQLite | Not supported |

SQL Server:

```csharp
using UmbrellaFrame.ModelSync.SqlServer;

var synchronizer = new SqlServerStoredProcedureSynchronizer(connectionString);

synchronizer.RegisterProcedureFile(
    "Database/Procedures/SqlServer/dbo.usp_GetProducts.sql");

var plans = await synchronizer.CompareRegisteredAsync(cancellationToken);
await synchronizer.SyncRegisteredAsync(cancellationToken);
```

MySQL/MariaDB:

```csharp
using UmbrellaFrame.ModelSync.MySql;

var synchronizer = new MySqlStoredProcedureSynchronizer(connectionString);

synchronizer.RegisterProcedureFile(
    "Database/Procedures/MySql/appdb.usp_GetProducts.sql");

var plans = await synchronizer.CompareRegisteredAsync(cancellationToken);
await synchronizer.SyncRegisteredAsync(cancellationToken);
```

PostgreSQL:

```csharp
using UmbrellaFrame.ModelSync.PostgreSQL;

var synchronizer = new PostgresStoredProcedureSynchronizer(connectionString);

synchronizer.RegisterProcedureFile(
    "Database/Procedures/PostgreSQL/public.usp_get_products.sql");

var plans = await synchronizer.CompareRegisteredAsync(cancellationToken);
await synchronizer.SyncRegisteredAsync(cancellationToken);
```

Rules:

- Each file should contain one procedure definition.
- The SQL procedure name must match the registered name.
- Do not use SQL Server `GO` in stored procedure synchronizer files.
- PostgreSQL overloaded procedure signatures are not supported in 1.2.0.
- MySQL procedure updates drop and recreate the procedure; review production plans carefully.

# Live Model Synchronization

Model synchronizers are the 1.2.0 dry-run-first layer for comparing attribute models with a live database.

Use them when the database already exists and you want ModelSync to answer:

- Which tables are missing?
- Which columns are missing?
- Which indexes or supported constraints are missing?
- Which differences are risky or destructive and must be reviewed manually?
- Which project SQL scripts need to run?

## Provider APIs

| Provider | Options | Synchronizer |
|---|---|---|
| SQL Server / Azure SQL | `SqlServerModelSyncOptions` | `SqlServerModelSynchronizer` |
| MySQL / MariaDB | `MySqlModelSyncOptions` | `MySqlModelSynchronizer` |
| PostgreSQL | `PostgresModelSyncOptions` | `PostgresModelSynchronizer` |
| SQLite | `SQLiteModelSyncOptions` | `SQLiteModelSynchronizer` |

## SQL Server Example

```csharp
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
    .FromAssemblies(options, typeof(Product).Assembly)
    .AddSqlScriptsFromEmbeddedResources(
        typeof(Product).Assembly,
        "MyApp.Database.Scripts")
    .CompareAsync(cancellationToken);

foreach (var operation in result.Operations)
{
    Console.WriteLine($"{operation.ChangeType}: {operation.Reason}");
    if (!string.IsNullOrWhiteSpace(operation.Sql))
        Console.WriteLine(operation.Sql);
}

await result.ThrowIfUnsupportedOrDestructiveAsync();
await result.ApplyAsync(cancellationToken);
```

## Exact Model Selection

Use `FromTypes` when the assembly contains several schema versions, test models, or DTOs:

```csharp
var result = await SqlServerModelSynchronizer
    .FromTypes(options, typeof(ProductSchema), typeof(CustomerSchema))
    .CompareAsync(cancellationToken);
```

## Table Execution Policies

ModelSync 1.2.0 lets one run mix manual and automatic table ownership:

```csharp
options.DefaultTableMode = ModelSyncTableMode.ManualOnly;

options.TablePolicies
    .ForType<AuditLog>(ModelSyncTableMode.ApplySafeChanges)
    .ForType<Notification>(ModelSyncTableMode.ApplySafeChanges);
```

For the opposite strategy, keep the global behavior automatic-safe and mark sensitive tables manual:

```csharp
options.DefaultTableMode = ModelSyncTableMode.ApplySafeChanges;

options.TablePolicies
    .ForType<User>(ModelSyncTableMode.ManualOnly)
    .ForType<Order>(ModelSyncTableMode.ManualOnly);
```

Legacy tables can be excluded from normal diff generation:

```csharp
options.TablePolicies
    .ForTable("legacy", "OldOrders", ModelSyncTableMode.Ignore);
```

`ManualOnly` operations are reported through `ManualOperations` and are never executed automatically. `ApplySafeChanges` applies only safe, provider-supported, dependency-ready operations; destructive schema changes remain blocked.

## Automatically Applied Safe Operations

- Missing table creation.
- Missing nullable column addition.
- Missing `NOT NULL` column addition when the model column has a default.
- Missing index creation.
- Missing default/check/unique/foreign key constraints where the provider can safely add them.
- Ordered SQL scripts with history/hash tracking.

## Blocked Operations

- Extra database tables are reported as blocked `DropTable` only when `ReportUnmappedTables = true`.
- Extra database columns are reported as `DropColumn` and blocked.
- Rename, type change, and nullable-to-not-null changes are blocked.
- Adding a `NOT NULL` column without a default to an existing table is blocked.
- SQLite stored procedure scripts are unsupported.

`AllowDestructiveChanges` does not make model diff drop/rename/type-change operations automatic. Model diff destructive operations remain review-only. The option is reserved for explicit destructive runner operations and should not be treated as permission for automatic model diff data loss.

## Script Options

`ApplyStoredProceduresOnEveryRun` and `ApplyTriggersOnEveryRun` run those scripts directly each time, which is useful for idempotent `CREATE OR ALTER` style scripts.

`ApplySeedsWithHashTracking` and `ApplyCustomSqlWithHashTracking` default to `true`. When they are true, seeds and custom SQL are applied through migration history/hash tracking. When false, they are treated as every-run scripts.

Model diff operations are classified by risk. Registered SQL scripts are treated as trusted, reviewed project artifacts; ModelSync does not parse arbitrary script text for destructive SQL such as `DROP TABLE` or `DELETE`.

For the focused reference, see [14 - Model Synchronizer](14-model-synchronizer.md).

# Analyzer

Install:

```bash
dotnet add package UmbrellaFrame.ModelSync.Analyzers --version 1.2.0
```

Rules:

| Rule | Severity | Meaning |
|---|---|---|
| `MSYNC001` | Warning | Public property is missing a provider column type attribute. |
| `MSYNC002` | Warning | A class has column attributes but no table-name attribute. |
| `MSYNC003` | Warning | A table model has no primary key attribute. |

Make rules errors in `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.MSYNC001.severity = error
dotnet_diagnostic.MSYNC002.severity = error
dotnet_diagnostic.MSYNC003.severity = error
```

Analyzers help early, but they do not replace SQL review or integration tests.

# Troubleshooting

## Column Has No Type Attribute

Every public property is treated as a column. Add the correct provider column type attribute, or move non-column public properties out of the schema model.

## Invalid SQL Identifier

Identifiers must match:

```text
^[A-Za-z_][A-Za-z0-9_]*$
```

Spaces, dots, quotes, brackets, semicolons, and hyphens are rejected intentionally.

## Destructive Operation Exception

Use explicit approval only after review:

```csharp
var allow = DestructiveOperationOptions.Allow();
```

## CreateTablesAsync Does Nothing

The generator cache is empty. Call `Generate...Table<T>()` on the same generator instance first.

## Foreign Key Target Missing

Create parent tables first, or use migration scripts where ordering is explicit.

## Index Was Not Created

`GenerateIndexSql<T>()` only returns SQL. Execute it separately or use migration scripts.

## SQLite Truncate Behavior

SQLite does not support `TRUNCATE TABLE`. ModelSync's SQLite provider generates:

```sql
DELETE FROM "products";
```

## Database Creation Permission Error

Create the database through DBA/deployment tooling and skip `CreateDatabase()`.

# Testing Approach

## SQL Snapshot Tests

```csharp
[Test]
public void ProductSqlShouldContainExpectedColumns()
{
    var generator = new MySqlTableGenerator("Server=unused;Database=unused;");

    var sql = generator.GenerateMySqlTable<Product>(true);

    Assert.That(sql, Does.Contain("`Name` VARCHAR(200) NOT NULL"));
    Assert.That(sql, Does.Contain("`Price` DECIMAL(18,2)"));
}
```

## Integration Tests

Test against real providers:

1. Start a test database or container.
2. Generate SQL.
3. Create tables.
4. Inspect catalog metadata.
5. Insert test data.
6. Test add/rename/alter/drop scenarios in isolated databases.
7. Clean up.

SQLite shared-memory tests are useful, but they do not replace SQL Server/MySQL/PostgreSQL provider tests.

# Production Guide

Recommended split:

## Prototype / Simple App

- Attribute models.
- Review generated SQL.
- Use `ifNotExists: true`.
- Use startup initialization carefully.

## Production / Enterprise

- Use attribute generation for DDL generation and tests.
- Manage real release changes with immutable migration scripts.
- Run migrations before application request traffic starts.
- Prefer one deployment job/console runner for migrations.
- Log and review dry-run plans.
- Prepare backups and rollback scripts.
- Review stored procedure plans before applying.

Production checklist:

- Correct provider package installed.
- Connection strings come from secret storage.
- Every public schema property has provider column type attributes.
- Identifiers match the safe pattern.
- Generated SQL is reviewed.
- Index execution is handled.
- Foreign key ordering is handled.
- Raw default/check expressions do not use external input.
- Destructive operations happen only in maintenance workflows.
- Migration files are immutable after release.
- `AutoAddMissingColumnsFromTableScripts` is configured deliberately.
- Backups and restore procedures are tested.
- Migration is run by one authority.
- Real provider integration tests pass.

# Complete Project Structure

Recommended folders:

```text
MyApplication/
  Database/
    Models/
      ProductSchema.cs
      CustomerSchema.cs
    Scripts/
      Tables/
        001_CreateProducts.sql
      StoredProcedures/
        010_GetProducts.sql
      Triggers/
      Seeds/
    Procedures/
      SqlServer/
        dbo.usp_GetProducts.sql
    SchemaInitializer.cs
    MigrationService.cs
  Program.cs
  appsettings.json
```

Keep schema models separate from domain entities and API DTOs when possible. ModelSync 1.2.0 can exclude helpers with `DbIgnore`.

# Quick API Reference

## ITableGenerator

| Method | Purpose |
|---|---|
| `GenerateSqlTable<T>()` | Generates and caches CREATE TABLE SQL. |
| `GenerateSqlTableAsync<T>()` | Task-based wrapper for generation. |
| `GenerateDropTableSql<T>()` | Returns DROP TABLE SQL. |
| `GenerateTruncateTableSql<T>()` | Returns provider-specific truncate/delete SQL. |
| `GenerateIndexSql<T>()` | Returns CREATE INDEX SQL list. |
| `CreateDatabase()` / async | Creates the provider database where supported. |
| `CreateTables()` / async | Executes cached CREATE TABLE SQL. |
| `DropTables(options)` / async | Drops cached tables with explicit destructive approval. |
| `AddColumn<T>()` / async | Adds a column from property metadata. |
| `DropColumn<T>(..., options)` / async | Drops a column with explicit approval. |
| `RenameColumn<T>()` / async | Renames a column explicitly. |
| `AlterColumnType<T>(..., options)` / async | Alters a column type with explicit approval. |

## IMigrationRunner

| Method | Purpose |
|---|---|
| `RegisterScript(definition)` | Registers an inline migration script. |
| `RegisterScriptFile(...)` | Registers a SQL file. |
| `RegisterEmbeddedScripts(...)` | Registers embedded `.sql` resources. |
| `CompareRegisteredAsync()` | Produces read-only dry-run migration plans. |
| `EnsureInfrastructureAsync()` | Explicitly creates required schemas/history tables. |
| `RunAsync()` | Creates infrastructure when needed, applies plans, and writes history. |

## IStoredProcedureSynchronizer

| Method | Purpose |
|---|---|
| `RegisterProcedure(...)` | Registers an inline procedure definition. |
| `RegisterProcedureFile(...)` | Registers a SQL file. |
| `CompareAsync(...)` | Produces one procedure plan. |
| `CompareRegisteredAsync()` | Compares all registered procedures. |
| `ApplyAsync(plan)` | Applies one plan. |
| `SyncRegisteredAsync()` | Compares and applies registered procedures. |

## Model Synchronizer

| Method / Member | Purpose |
|---|---|
| `FromAssemblies(options, ...)` | Reads provider-specific ModelSync attributes from assemblies. |
| `FromTypes(options, ...)` | Reads only explicitly supplied model types. |
| `AddSqlScript(...)` | Adds an inline ordered script definition. |
| `AddSqlScriptsFromEmbeddedResources(...)` | Adds embedded SQL resources by folder/category. |
| `CompareAsync()` | Builds a dry-run model/script synchronization result. |
| `ModelSyncResult.SafeOperations` | Operations that can be applied automatically. |
| `ModelSyncResult.BlockedOperations` | Destructive, risky, or unsupported operations. |
| `ModelSyncResult.SkippedOperations` | Safe operations intentionally skipped by configuration. |
| `ApplyAsync()` | Applies only when no blocked operations exist. |

# Version 1.2.0 Limits

- Model-to-live-database diff is additive/safety-first, not a full destructive migration engine.
- ModelSync 1.2.0 includes `DbIgnore` and `DbColumnName` for schema discovery control.
- Schema-qualified table-name attributes are intentionally limited by strict identifier validation.
- Index SQL is not executed automatically.
- Foreign key attributes do not model advanced quoting or cascade behavior.
- Missing tables, indexes, and foreign keys are planned in one apply pass; complex dependency cycles may still require reviewed SQL scripts.
- Migrations are not guaranteed to be one atomic transaction across all batches and history updates.
- Changed table script repair is limited to simple missing-column additions.
- Model synchronizers do not silently apply destructive/risky differences such as drop, rename, type changes, or nullable-to-not-null changes.
- SQLite does not support type alter or stored procedures.
- PostgreSQL overloaded procedures are not supported.
- `DbColumnDefault` and `DbColumnCheck` accept raw SQL.

# FAQ

## Can I use ModelSync with EF Core?

Yes. ModelSync is not an ORM, so it can coexist with EF Core, Dapper, or ADO.NET. Avoid having two independent migration authorities for the same schema.

## Should I install only the Core package?

Usually no. Install the provider package you use; it brings Core as a dependency.

## Can a ModelSync model also be my domain entity?

Technically yes, but separate schema models are safer. Published `1.2.0` packages treat all public properties as columns; ModelSync 1.2.0 can exclude helpers with `DbIgnore`.

## Does `ifNotExists: true` replace migrations?

No. It only makes table creation safer when the table does not exist. It does not manage existing column/type/constraint differences.

## If I add a property, is the table updated automatically?

No. Use an explicit operation:

```csharp
await generator.AddColumnAsync<Model>(nameof(Model.NewProperty));
```

Or add a new immutable SQL migration script.

## Why are indexes separate?

Index SQL is generated separately so it can be reviewed and deployed deliberately. Execution is the user's responsibility.

## Should production run migrations during startup?

For single-instance controlled systems, possibly. For multi-instance production, prefer a separate deployment job or console migration runner.

# Conclusion

ModelSync keeps schema changes visible and developer-controlled.

Recommended workflow:

1. Install the correct provider package.
2. Define schema models with provider attributes.
3. Generate and review SQL.
4. Execute DDL only after review.
5. Manage indexes deliberately.
6. Use explicit column operations or immutable SQL migration scripts for changes.
7. Use destructive operations only with review, backup, and explicit approval.
8. Compare stored procedures before applying.
9. In production, use one migration authority and avoid silent schema mutation.

With that workflow, ModelSync provides provider-specific DDL generation and controlled database schema management without the weight of an ORM.

