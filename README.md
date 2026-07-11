# ModelSync

![ModelSync](https://raw.githubusercontent.com/UmbrellaFrameHQ/modelsync/main/assets/icons/modelsync-core.png)

[![NuGet](https://img.shields.io/nuget/v/UmbrellaFrame.ModelSync.Core.svg?style=flat-square)](https://www.nuget.org/packages/UmbrellaFrame.ModelSync.Core)
[![CI](https://github.com/UmbrellaFrameHQ/modelsync/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/UmbrellaFrameHQ/modelsync/actions/workflows/ci.yml?query=branch%3Amain)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](LICENSE)
[![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-purple?style=flat-square)](https://learn.microsoft.com/dotnet/standard/net-standard)
[![Views](https://hits.sh/github.com/UmbrellaFrameHQ/modelsync.svg?style=flat-square&label=views&color=blue)](https://hits.sh/github.com/UmbrellaFrameHQ/modelsync/)

**Language:** [English](#english)  
**Dil:** [TÃ¼rkÃ§e](#tÃ¼rkÃ§e)

---

## English

ModelSync is an attribute-based SQL schema and script management toolkit for .NET. It lets you define database tables with plain C# classes, generate provider-specific SQL, execute explicit DDL operations, synchronize stored procedures, and run ordered migration scripts without introducing an ORM.

ModelSync is built for teams that want Dapper, ADO.NET, or hand-written SQL, but still want repeatable schema generation, safety checks, and provider-specific SQL output.

### Packages

```text
UmbrellaFrame.ModelSync.Core          Attributes, interfaces, shared SQL generation
UmbrellaFrame.ModelSync.SqlServer     SQL Server / Azure SQL provider
UmbrellaFrame.ModelSync.MySql         MySQL / MariaDB provider
UmbrellaFrame.ModelSync.PostgreSQL    PostgreSQL provider
UmbrellaFrame.ModelSync.SQLite        SQLite provider
UmbrellaFrame.ModelSync.Oracle        Oracle provider (table DDL, netstandard2.1)
UmbrellaFrame.ModelSync.Analyzers     Roslyn analyzer package
```

Current package version: `1.2.3`

## Versioning, Release Notes and Migration Guides

- [Changelog](CHANGELOG.md)
- [Release notes index](docs/releases/README.md)
- [Current 1.2.3 notes](docs/releases/1.2.3.md)
- [1.2.2 to 1.2.3 migration guide](docs/migrations/1.2.2-to-1.2.3.md)
- [Versioning and compatibility](docs/versioning-and-compatibility.md)
- [Deprecation policy](docs/deprecation-policy.md)
- [1.3 roadmap](docs/roadmap-1.3.md)

ModelSync packages can be restored by NuGet CLI, MSBuild, CI agents, Artifactory and package mirrors. Download counts are not unique user counts, but older versions continuing to restore is still a compatibility signal. Published package versions should not be overwritten or unlisted as a substitute for migration documentation.

Release status: ModelSync 1.2.3 is a SQL Server DBReset/native migration lock fix release. It keeps the 1.2.x API compatible and is intended for applications that run full database reset followed by table sync and ordered SQL migrations.

NuGet consumption note: ModelSync packages are restored by NuGet CLI, MSBuild, CI systems, Artifactory and mirrors. Download counts are not unique user counts, so this README does not publish temporary download snapshots. Continued restores of older versions still require disciplined API compatibility, migration documentation and non-destructive package version handling.

Architecture rule: `UmbrellaFrame.ModelSync.Core` owns SQL generation and migration planning through a provider-agnostic compiler. Provider packages supply structured descriptors, capabilities, mappings, attributes, connection adapters, and execution integration; they do not maintain independent framework SQL engines.

### 1.2.3 SQL Server DBReset and Native Lock Fix

The repository is shipping the 1.2.3 line for SQL Server reset/native migration lock reliability. The 1.2.0 compatibility contract remains valid.

1.2.3 compatibility scope:

- `RunOnce`, `HashTracked`, and `EveryRun` migration execution modes.
- `CategoryPolicies` for per-category script execution behavior.
- `MigrationCompatibilityProfiles.LegacyEmbeddedSql` for legacy table/seed/procedure/trigger ownership.
- Additive `SqlHash` upgrade for existing history tables that were created before hash tracking.
- Legacy history row adoption without rerunning matching `RunOnce` seed scripts.
- Stored procedure and trigger `EveryRun` behavior for legacy runner parity.
- `CustomSql` history bootstrap and hash tracking.
- Read-only compare behavior: compare reports required work but does not create tables, add columns, adopt hashes, or execute scripts.

Provider support matrix for the 1.2.0 compatibility gate:

| Provider | Legacy history upgrade | Row adoption | SP scripts | Trigger scripts | CustomSql | Native lock | Transaction note |
|---|---|---|---|---|---|---|---|
| SQL Server | Planned full fixture | Planned full fixture | EveryRun | EveryRun | HashTracked | `sp_getapplock` | DDL behavior depends on SQL Server/database settings |
| MySQL | Planned full fixture | Planned full fixture | EveryRun | EveryRun | HashTracked | `GET_LOCK` | DDL must not be reported as fully atomic |
| MariaDB | Planned separate fixture | Planned separate fixture | EveryRun | EveryRun | HashTracked | named lock | Separate from MySQL results |
| PostgreSQL | Planned full fixture | Planned full fixture | EveryRun | EveryRun | HashTracked | advisory lock | Transactional DDL expected |
| SQLite | Initial real fixture present | Initial real fixture present | Unsupported | Trigger/generic EveryRun | HashTracked | `BEGIN IMMEDIATE` | File transaction/rollback scoped |
| Oracle | Table DDL fixture present | Not yet in legacy runner | Not yet supported | Not yet supported | Not yet supported | Unsupported | Oracle provider targets netstandard2.1 because the managed Oracle client requires it |

Legacy runner migration guides:

- [Legacy Runner Migration - English](docs/legacy-runner-migration-en.md)
- [Legacy Runner Migration - Turkish](docs/legacy-runner-migration-tr.md)

### What ModelSync Does

- Generates `CREATE TABLE` SQL from attributed C# models.
- Supports explicit column mapping with `DbColumnName` and schema-only public property exclusion with `DbIgnore`.
- Generates `CREATE INDEX`, `DROP TABLE`, `TRUNCATE TABLE`, `ADD COLUMN`, `DROP COLUMN`, and `ALTER COLUMN TYPE` SQL where the provider supports it.
- Executes generated DDL through explicit method calls.
- Requires explicit opt-in for destructive operations.
- Supports SQL Server, MySQL/MariaDB, PostgreSQL, SQLite, and Oracle table generation.
- Synchronizes stored procedures for SQL Server, MySQL/MariaDB, and PostgreSQL.
- Runs ordered SQL migration scripts for tables, stored procedures, triggers, and seed data.
- Compares attribute models with live databases and applies only safe additive synchronization plans.
- Tracks applied migration scripts with history tables and SQL hashes.
- Provides Roslyn analyzer warnings for missing table, primary key, and column type attributes.

### What ModelSync Is Not

ModelSync is intentionally not an ORM. It does not track entities, generate LINQ queries, manage change tracking, lazy-load relations, or replace Dapper/ADO.NET/EF Core for runtime data access.

It is also not a silent production mutation engine. Live model synchronization is dry-run-first: ModelSync can apply safe additive operations, but destructive or risky operations are reported and blocked instead of being silently executed.

Registered SQL scripts are treated as trusted project artifacts. ModelSync risk-classifies model-diff operations, but it does not parse arbitrary SQL script text to prove whether the script itself is destructive.

### Installation

Install the provider package you need:

```bash
dotnet add package UmbrellaFrame.ModelSync.Core --version 1.2.3
dotnet add package UmbrellaFrame.ModelSync.SqlServer --version 1.2.3
dotnet add package UmbrellaFrame.ModelSync.MySql --version 1.2.3
dotnet add package UmbrellaFrame.ModelSync.PostgreSQL --version 1.2.3
dotnet add package UmbrellaFrame.ModelSync.SQLite --version 1.2.3
dotnet add package UmbrellaFrame.ModelSync.Oracle --version 1.2.3
```

Each provider package pulls `UmbrellaFrame.ModelSync.Core` automatically.

Analyzer package:

```bash
dotnet add package UmbrellaFrame.ModelSync.Analyzers --version 1.2.3
```

### Quick Start

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

    [SqlServerColumnType(SqlServerColumnType.DECIMAL, "18,2")]
    [DbColumnDefault("0")]
    [DbColumnCheck("Price >= 0")]
    public decimal Price { get; set; }
}

var generator = new SqlServerTableGenerator(
    "Server=localhost;Database=AppDb;Integrated Security=True;TrustServerCertificate=True;");

var sql = generator.GenerateSqlServerTable<Product>(ifNotExists: true);
var indexes = generator.GenerateIndexSql<Product>();

Console.WriteLine(sql);
foreach (var indexSql in indexes)
{
    Console.WriteLine(indexSql);
}

await generator.CreateTablesAsync(cancellationToken);
```

Recommended workflow:

1. Generate SQL.
2. Review SQL.
3. Commit generated or hand-written scripts when needed.
4. Execute against live databases only through explicit deployment steps.

### Destructive Operation Safety

Operations that may cause data loss require `DestructiveOperationOptions.Allow()`:

```csharp
var allow = DestructiveOperationOptions.Allow();

generator.DropColumn<Product>("LegacyCode", allow);
generator.AlterColumnType<Product>("Price", allow);
await generator.DropTablesAsync(allow, cancellationToken);
```

Calling destructive methods without explicit options throws by design. This makes dangerous schema operations visible in code review and prevents accidental production data loss.

### Stored Procedure Synchronization

Stored procedures can be stored as `.sql` files and synchronized with supported providers.

```csharp
using UmbrellaFrame.ModelSync.SqlServer;

var procedures = new SqlServerStoredProcedureSynchronizer(connectionString);

procedures.RegisterProcedureFile(
    "Database/Procedures/SqlServer/dbo.usp_GetProducts.sql");

var plans = await procedures.CompareRegisteredAsync(cancellationToken);
await procedures.SyncRegisteredAsync(cancellationToken);
```

Provider behavior:

| Provider | Support | Strategy |
|---|---:|---|
| SQL Server / Azure SQL | Yes | `CREATE OR ALTER PROCEDURE` |
| MySQL / MariaDB | Yes | `DROP PROCEDURE IF EXISTS` + `CREATE PROCEDURE` |
| PostgreSQL | Yes | `CREATE OR REPLACE PROCEDURE` |
| SQLite | No | SQLite has no stored procedure feature |

Use `CompareRegisteredAsync()` before applying changes when you want a dry-run plan.

### Migration Runner

Provider migration runners apply ordered SQL scripts:

```csharp
using UmbrellaFrame.ModelSync.SqlServer;

var runner = new SqlServerMigrationRunner(connectionString);

runner.RegisterScriptFile("Database/Scripts/Tables/001_CreateProducts.sql");
runner.RegisterScriptFile("Database/Scripts/StoredProcedures/010_GetProducts.sql");
runner.RegisterScriptFile("Database/Scripts/Triggers/020_ProductAudit.sql");
runner.RegisterScriptFile("Database/Scripts/Seeds/030_DefaultProducts.sql");
runner.RegisterScriptFile("Database/Scripts/CustomSql/999_AfterSetup.sql");

var plans = await runner.CompareRegisteredAsync(cancellationToken);
await runner.RunAsync(cancellationToken);
```

Execution order:

```text
Tables -> StoredProcedures -> Triggers -> Seeds -> CustomSql
```

The runner creates history tables, stores script hashes, supports embedded `.sql` resources, and can repair missing columns additively from changed `CREATE TABLE` scripts.

History tables are used because a database catalog can tell whether an object exists, but it cannot reliably tell which script version was applied, whether a seed script already ran, or which SQL hash was last deployed.

Database reset is destructive and requires explicit permission:

```csharp
var options = new MigrationRunnerOptions
{
    ResetDatabase = true,
    DestructiveOptions = DestructiveOperationOptions.Allow()
};
```

For controlled reset flows, prefer `DatabaseResetOptions` with `ExpectedDatabaseName` and explicit approval. SQL Server reset runs before the provider-native migration lock is acquired, then schemas, history tables, and migration scripts run under the normal lock. Optional SQL Server backups are available with `BackupBeforeReset`, `BackupDirectory` / `BackupFileName`, or `BackupFilePath`.

### Live Model Synchronization

ModelSync can compare attribute models with a live database and produce a reviewable dry-run plan. Only additive/update-safe operations are applied automatically; destructive or risky operations are reported and blocked.

```csharp
var options = new SqlServerModelSyncOptions
{
    ConnectionString = connectionString,
    HistorySchema = "sec",
    DefaultSchema = "app",
    AllowDestructiveChanges = false,
    ApplyStoredProceduresOnEveryRun = true,
    ApplyTriggersOnEveryRun = true,
    ApplySeedsWithHashTracking = true
};

var result = await SqlServerModelSynchronizer
    .FromAssemblies(options, typeof(Product).Assembly)
    .AddSqlScriptsFromEmbeddedResources(
        typeof(Product).Assembly,
        "MyApp.Database.Scripts")
    .CompareAsync(cancellationToken);

await result.ThrowIfUnsupportedOrDestructiveAsync();
await result.ApplyAsync(cancellationToken);
```

`SafeOperations` can be applied automatically. `BlockedOperations` must be reviewed manually. `SkippedOperations` contains safe operations that were intentionally disabled by options, such as index or constraint apply flags.

Safe automatic operations include missing tables, safe missing columns, indexes, and supported constraints. Drop, rename, type changes, and nullable-to-not-null changes are never silently applied.

`FromAssemblies` is provider-aware and `FromTypes` scopes synchronization to the supplied model types. Extra database tables are reported as blocked `DropTable` operations only when `ReportUnmappedTables = true`. Registered SQL scripts are trusted project artifacts; ModelSync does not parse arbitrary script text for destructive SQL.

ModelSync 1.2.0 adds table execution policies for mixed ownership:

```csharp
options.DefaultTableMode = ModelSyncTableMode.ManualOnly;
options.TablePolicies
    .ForType<AuditLog>(ModelSyncTableMode.ApplySafeChanges)
    .ForType<Notification>(ModelSyncTableMode.ApplySafeChanges)
    .ForTable("legacy", "OldOrders", ModelSyncTableMode.Ignore);
```

`ManualOnly` operations are reported through `ManualOperations` and are never executed automatically. `ApplySafeChanges` never authorizes destructive schema changes.

Migration runners expose operational hardening contracts for reset approval, readiness retry, migration locking, transaction policy, and structured execution results. Prefer a deployment-time migration job over every application instance running migrations at startup. If startup migration is used, configure a provider lock strategy before production rollout.

### Provider Support Matrix

| Feature | SQL Server | MySQL / MariaDB | PostgreSQL | SQLite |
|---|:---:|:---:|:---:|:---:|
| Table SQL generation | Yes | Yes | Yes | Yes |
| Index SQL generation | Yes | Yes | Yes | Yes |
| Add column | Yes | Yes | Yes | Yes |
| Drop column | Yes | Yes | Yes | Limited by SQLite version |
| Alter column type | Yes | Yes | Yes | No |
| Truncate table | Yes | Yes | Yes | Emulated with `DELETE FROM` |
| Stored procedure sync | Yes | Yes | Yes | No |
| Migration runner | Yes | Yes | Yes | Yes |
| Live model synchronization | Yes | Yes | Yes | Yes |
| `GO` batch splitting | Yes | Not applicable | Not applicable | Not applicable |
| Reset database | Yes | Yes | Yes | No |

### Unsupported or Intentionally Limited Features

| Feature | Status | Why |
|---|---|---|
| Runtime ORM behavior | Not supported | ModelSync is a schema/script tool. Data access should remain in Dapper, ADO.NET, EF Core, or your own repository layer. |
| Silent destructive model diff apply | Not supported | Drop, rename, type changes, and nullable-to-not-null changes are reported but not automatically applied. |
| Database-first model scaffolding | Out of scope for this repository | Scaffolding is a tooling concern and should live separately from the runtime schema package. |
| Visual Studio designer/tooling | Out of scope for this repository | IDE tooling has different packaging and UX requirements. The runtime library stays small and provider-focused. |
| SQLite stored procedures | Not supported | SQLite does not implement stored procedures. ModelSync throws explicit unsupported behavior instead of pretending support exists. |
| SQLite direct alter-column-type | Not supported | SQLite does not support direct `ALTER COLUMN TYPE`; use a create-copy-drop rebuild strategy. |
| Silent destructive operations | Not supported | Dropping tables/columns or changing types can lose data. Explicit `DestructiveOperationOptions.Allow()` is required. |
| User-supplied raw default/check expressions | Not safe by design | `DbColumnDefault` and `DbColumnCheck` accept raw SQL fragments for schema authors. They must not be built from untrusted user input. |
| Arbitrary identifier names with spaces/symbols | Not supported | Strict identifier validation prevents unsafe or ambiguous generated SQL. |

### Identifier Safety

ModelSync validates identifiers before quoting table, column, database, and index names.

Allowed pattern:

```text
^[A-Za-z_][A-Za-z0-9_]*$
```

Spaces, dots, quotes, brackets, semicolons, hyphens, and other punctuation are rejected intentionally. This keeps generated SQL predictable and reduces injection risk in schema identifiers.

### Supported Attributes

Provider-specific attributes:

| Attribute | Description |
|---|---|
| `[{Db}TableName("name")]` | Sets table name |
| `[{Db}ColumnType(Type)]` | Sets provider-specific column type |
| `[{Db}ColumnPrimaryKey]` | Marks a primary key column |
| `[{Db}ColumnNotNull]` | Adds `NOT NULL` |
| `[{Db}ColumnUnique]` | Adds `UNIQUE` |
| `[{Db}ForeignKey("column", "table", "ref")]` | Adds a foreign key |

Cross-provider attributes:

| Attribute | Description |
|---|---|
| `[DbColumnDefault("expr")]` | Adds a raw SQL `DEFAULT` expression |
| `[DbColumnCheck("expr")]` | Adds a raw SQL `CHECK` expression |
| `[DbColumnIndex]` | Generates index SQL with `GenerateIndexSql<T>()` |

Security note: `DbColumnDefault` and `DbColumnCheck` are raw SQL fragments by design. Keep them as reviewed, hard-coded schema expressions. Do not build them from user input.

### Roslyn Analyzer

| Rule | Severity | Description |
|---|---|---|
| `MSYNC001` | Warning | Public property is missing a column type attribute |
| `MSYNC002` | Warning | Class has column attributes but no table name attribute |
| `MSYNC003` | Warning | Model table has no primary key defined |

Example `.editorconfig`:

```ini
dotnet_diagnostic.MSYNC001.severity = error
dotnet_diagnostic.MSYNC003.severity = none
```

### Development

Run unit tests:

```bash
dotnet test ModelSync.sln -c Release --filter "Category!=Integration"
```

Build solution:

```bash
dotnet restore ModelSync.sln
dotnet build ModelSync.sln -c Release --no-restore
```

Pack NuGet artifacts:

```bash
dotnet pack ModelSync.sln -c Release --no-build -o artifacts
```

Integration tests are opt-in because they require live databases:

```bash
docker compose -f compose.integration.yml up -d
MODELSYNC_RUN_MYSQL_INTEGRATION=1 MODELSYNC_MYSQL_CONNECTION_STRING="Server=127.0.0.1;Port=13306;Database=modelsync_integration;User ID=root;Password=ModelSync_Pass123;" dotnet test UmbrellaFrame.ModelSync.MySqlTest/UmbrellaFrame.ModelSync.MySqlTest.csproj -c Release --filter "Category=Integration"
```

The Compose file uses explicit local-only test credentials. Do not reuse them in production. The release gate expects separate live results for SQL Server, MySQL, MariaDB, PostgreSQL, and SQLite; skipped provider tests are not counted as release evidence.

Oracle provider work starts from the same local test environment. The `modelsync-oracle` service uses Oracle Database Free and exposes `FREEPDB1` on local port `11521`:

```bash
docker compose -f compose.integration.yml up -d modelsync-oracle
# Future Oracle provider connection string:
# User Id=MODELSYNC_TEST;Password=ModelSync_Pass123;Data Source=127.0.0.1:11521/FREEPDB1
```

Stored procedure integration tests can use the same Docker test environment when available:

```bash
MODELSYNC_RUN_SP_INTEGRATION=1 dotnet test ModelSync.sln -c Release --filter "Category=Integration"
```

### English Resources

| Resource | Description |
|---|---|
| [Overview](docs/01-overview.md) | Architecture and design decisions |
| [Quick Start](docs/02-quickstart.md) | Provider examples |
| [Attribute Reference](docs/03-attributes.md) | Attribute list and parameters |
| [Provider Guides](docs/04-providers.md) | Provider-specific behavior |
| [API Reference](docs/05-api-reference.md) | Public API surface |
| [Dependency Injection](docs/06-dependency-injection.md) | ASP.NET Core DI usage |
| [Roslyn Analyzers](docs/07-analyzers.md) | Analyzer rules |
| [Architecture](docs/08-architecture.md) | Internal flow and extension points |
| [Contributing](docs/09-contributing.md) | Development setup |
| [Changelog](docs/10-changelog.md) | Version history |
| [Stored Procedure Sync](docs/11-stored-procedures.md) | Procedure synchronization |
| [Migration Runner](docs/12-migration-runner.md) | Ordered SQL scripts and history |
| [Model Synchronizer](docs/14-model-synchronizer.md) | Live database diff from attribute models |
| [Full Usage Guide](docs/13-full-usage-guide-en.md) | Complete English guide |
| [Examples](examples/README.md) | Example projects and snippets |
| [NuGet README Source](docs/nuget/README.md) | Package README source |

### Comparison

| Feature | ModelSync | EF Core | FluentMigrator | DbUp |
|---|:---:|:---:|:---:|:---:|
| Zero ORM dependency | Yes | No | Yes | Yes |
| Attribute-based schema generation | Yes | Yes | No | No |
| Provider packages | Yes | Yes | Yes | Mostly script-based |
| Async DDL execution | Yes | Yes | Limited | Yes |
| Analyzer support | Yes | No | No | No |
| Explicit destructive-operation guard | Yes | Partial | Manual | Manual |
| Automatic live DB model diff | Planned | Yes | No | No |
| Script migration runner | Yes | Yes | Yes | Yes |
| Stored procedure synchronization | Yes | Manual | Manual | Script-based |

### License

MIT (c) UmbrellaFrame

---

## TÃ¼rkÃ§e

ModelSync, .NET iÃ§in attribute tabanlÄ± SQL ÅŸema ve script yÃ¶netim aracÄ±dÄ±r. Sade C# sÄ±nÄ±flarÄ±yla veritabanÄ± tablolarÄ± tanÄ±mlamanÄ±zÄ±, saÄŸlayÄ±cÄ±ya Ã¶zel SQL Ã¼retmenizi, aÃ§Ä±k DDL iÅŸlemleri Ã§alÄ±ÅŸtÄ±rmanÄ±zÄ±, stored procedure senkronizasyonu yapmanÄ±zÄ± ve sÄ±ralÄ± migration scriptleri uygulamanÄ±zÄ± saÄŸlar. Bunu ORM baÄŸÄ±mlÄ±lÄ±ÄŸÄ± eklemeden yapar.

ModelSync; Dapper, ADO.NET veya elle yazÄ±lmÄ±ÅŸ SQL kullanan ama tekrar edilebilir ÅŸema Ã¼retimi, gÃ¼venlik kontrolleri ve saÄŸlayÄ±cÄ±ya Ã¶zel SQL Ã§Ä±ktÄ±sÄ± isteyen ekipler iÃ§in tasarlanmÄ±ÅŸtÄ±r.

### Paketler

```text
UmbrellaFrame.ModelSync.Core          Attribute'lar, arayÃ¼zler, ortak SQL Ã¼retimi
UmbrellaFrame.ModelSync.SqlServer     SQL Server / Azure SQL saÄŸlayÄ±cÄ±sÄ±
UmbrellaFrame.ModelSync.MySql         MySQL / MariaDB saÄŸlayÄ±cÄ±sÄ±
UmbrellaFrame.ModelSync.PostgreSQL    PostgreSQL saÄŸlayÄ±cÄ±sÄ±
UmbrellaFrame.ModelSync.SQLite        SQLite saÄŸlayÄ±cÄ±sÄ±
UmbrellaFrame.ModelSync.Oracle        Oracle saglayicisi (table DDL, netstandard2.1)
UmbrellaFrame.ModelSync.Analyzers     Roslyn analyzer paketi
```

GÃ¼ncel paket sÃ¼rÃ¼mÃ¼: `1.2.3`

YayÄ±n durumu: ModelSync 1.2.3, MySQL/MariaDB integration workflow gate dÃ¼zeltildikten sonra yayÄ±mlanmamÄ±ÅŸ 1.2.1 tag sÃ¼rÃ¼mÃ¼nÃ¼n yerini alÄ±r.

NuGet tuketim notu: ModelSync paketleri NuGet CLI, MSBuild, CI sistemleri, Artifactory ve mirror istemcileri tarafindan restore edilir. Download sayilari benzersiz kullanici sayisi degildir; bu nedenle README gecici download snapshot yayinlamaz. Buna ragmen eski surumlerin restore edilmeye devam etmesi API compatibility, migration dokumantasyonu ve package version'lari overwrite/unlist etmeme disiplinini gerekli kilar.

Mimari kural: SQL Ã¼retimi ve migration planlama, provider-agnostic compiler ile `UmbrellaFrame.ModelSync.Core` katmanÄ±na aittir. Provider paketleri structured descriptor, capability, mapping, attribute, connection adapter ve execution entegrasyonu saÄŸlar; baÄŸÄ±msÄ±z framework SQL motoru tutmaz.

### ModelSync Ne Yapar?

- Attribute ile iÅŸaretlenmiÅŸ C# modellerinden `CREATE TABLE` SQL'i Ã¼retir.
- `DbColumnName` ile aÃ§Ä±k kolon adÄ± eÅŸlemesini ve `DbIgnore` ile ÅŸema dÄ±ÅŸÄ± public property hariÃ§ tutmayÄ± destekler.
- SaÄŸlayÄ±cÄ± desteklediÄŸi sÃ¼rece `CREATE INDEX`, `DROP TABLE`, `TRUNCATE TABLE`, `ADD COLUMN`, `DROP COLUMN` ve `ALTER COLUMN TYPE` SQL'i Ã¼retir.
- Ãœretilen DDL'i aÃ§Ä±k metot Ã§aÄŸrÄ±larÄ±yla Ã§alÄ±ÅŸtÄ±rÄ±r.
- Veri kaybÄ± oluÅŸturabilecek iÅŸlemler iÃ§in aÃ§Ä±k onay ister.
- SQL Server, MySQL/MariaDB, PostgreSQL, SQLite ve Oracle icin tablo uretimini destekler.
- SQL Server, MySQL/MariaDB ve PostgreSQL iÃ§in stored procedure senkronizasyonu yapar.
- Table, stored procedure, trigger ve seed scriptlerini sÄ±ralÄ± ÅŸekilde Ã§alÄ±ÅŸtÄ±rÄ±r.
- Attribute modellerini canlÄ± veritabanÄ±yla karÅŸÄ±laÅŸtÄ±rÄ±p gÃ¼venli dry-run senkronizasyon planÄ± Ã¼retir.
- Uygulanan migration scriptlerini history tablolarÄ± ve SQL hash bilgisiyle takip eder.
- Eksik tablo adÄ±, primary key ve kolon tipi iÃ§in Roslyn analyzer uyarÄ±larÄ± verir.

### ModelSync Ne DeÄŸildir?

ModelSync bilinÃ§li olarak ORM deÄŸildir. Entity tracking, LINQ query Ã¼retimi, change tracking, lazy loading veya runtime data access gÃ¶revi Ã¼stlenmez. Dapper, ADO.NET, EF Core veya kendi repository katmanÄ±nÄ±zÄ±n yerine geÃ§mez.

ModelSync sessiz ve kontrolsÃ¼z bir production mutasyon motoru deÄŸildir. CanlÄ± model senkronizasyonu dry-run-first Ã§alÄ±ÅŸÄ±r; eksik tablo, gÃ¼venli eksik kolon, indeks ve desteklenen constraint gibi additive iÅŸlemleri uygulayabilir, fakat drop, rename, tip deÄŸiÅŸikliÄŸi ve nullable-to-not-null gibi riskli iÅŸlemleri raporlar ve otomatik uygulamaz.

### Kurulum

Ä°htiyacÄ±nÄ±z olan saÄŸlayÄ±cÄ± paketini kurun:

```bash
dotnet add package UmbrellaFrame.ModelSync.Core --version 1.2.3
dotnet add package UmbrellaFrame.ModelSync.SqlServer --version 1.2.3
dotnet add package UmbrellaFrame.ModelSync.MySql --version 1.2.3
dotnet add package UmbrellaFrame.ModelSync.PostgreSQL --version 1.2.3
dotnet add package UmbrellaFrame.ModelSync.SQLite --version 1.2.3
dotnet add package UmbrellaFrame.ModelSync.Oracle --version 1.2.3
```

Her saÄŸlayÄ±cÄ± paketi `UmbrellaFrame.ModelSync.Core` paketini otomatik olarak getirir.

Analyzer paketi:

```bash
dotnet add package UmbrellaFrame.ModelSync.Analyzers --version 1.2.3
```

### HÄ±zlÄ± BaÅŸlangÄ±Ã§

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

    [SqlServerColumnType(SqlServerColumnType.DECIMAL, "18,2")]
    [DbColumnDefault("0")]
    [DbColumnCheck("Price >= 0")]
    public decimal Price { get; set; }
}

var generator = new SqlServerTableGenerator(
    "Server=localhost;Database=AppDb;Integrated Security=True;TrustServerCertificate=True;");

var sql = generator.GenerateSqlServerTable<Product>(ifNotExists: true);
var indexes = generator.GenerateIndexSql<Product>();

Console.WriteLine(sql);
foreach (var indexSql in indexes)
{
    Console.WriteLine(indexSql);
}

await generator.CreateTablesAsync(cancellationToken);
```

Ã–nerilen akÄ±ÅŸ:

1. SQL Ã¼ret.
2. SQL'i incele.
3. Gerekiyorsa Ã¼retilen veya elle yazÄ±lan scriptleri repoya ekle.
4. CanlÄ± veritabanÄ±na sadece aÃ§Ä±k deployment adÄ±mlarÄ±yla uygula.

### YÄ±kÄ±cÄ± Ä°ÅŸlem GÃ¼venliÄŸi

Veri kaybÄ±na yol aÃ§abilecek iÅŸlemler `DestructiveOperationOptions.Allow()` ister:

```csharp
var allow = DestructiveOperationOptions.Allow();

generator.DropColumn<Product>("LegacyCode", allow);
generator.AlterColumnType<Product>("Price", allow);
await generator.DropTablesAsync(allow, cancellationToken);
```

YÄ±kÄ±cÄ± metotlarÄ± aÃ§Ä±k onay vermeden Ã§aÄŸÄ±rmak tasarÄ±m gereÄŸi exception fÄ±rlatÄ±r. BÃ¶ylece tehlikeli ÅŸema operasyonlarÄ± code review sÄ±rasÄ±nda gÃ¶rÃ¼nÃ¼r olur ve production veri kaybÄ± riski azalÄ±r.

### Stored Procedure Senkronizasyonu

Stored procedure'ler `.sql` dosyasÄ± olarak tutulabilir ve desteklenen saÄŸlayÄ±cÄ±larla senkronize edilebilir.

```csharp
using UmbrellaFrame.ModelSync.SqlServer;

var procedures = new SqlServerStoredProcedureSynchronizer(connectionString);

procedures.RegisterProcedureFile(
    "Database/Procedures/SqlServer/dbo.usp_GetProducts.sql");

var plans = await procedures.CompareRegisteredAsync(cancellationToken);
await procedures.SyncRegisteredAsync(cancellationToken);
```

SaÄŸlayÄ±cÄ± davranÄ±ÅŸÄ±:

| SaÄŸlayÄ±cÄ± | Destek | Strateji |
|---|---:|---|
| SQL Server / Azure SQL | Var | `CREATE OR ALTER PROCEDURE` |
| MySQL / MariaDB | Var | `DROP PROCEDURE IF EXISTS` + `CREATE PROCEDURE` |
| PostgreSQL | Var | `CREATE OR REPLACE PROCEDURE` |
| SQLite | Yok | SQLite stored procedure Ã¶zelliÄŸi sunmaz |

DeÄŸiÅŸiklikleri uygulamadan Ã¶nce dry-run planÄ± gÃ¶rmek iÃ§in `CompareRegisteredAsync()` kullanÄ±n.

### Migration Runner

SaÄŸlayÄ±cÄ± migration runner'larÄ± sÄ±ralÄ± SQL scriptleri uygular:

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

Ã‡alÄ±ÅŸma sÄ±rasÄ±:

```text
Tables -> StoredProcedures -> Triggers -> Seeds -> CustomSql
```

Runner history tablolarÄ± oluÅŸturur, script hash'i tutar, embedded `.sql` resource'larÄ±nÄ± destekler ve deÄŸiÅŸen `CREATE TABLE` scriptlerinden eksik kolonlarÄ± eklemeli ÅŸekilde tamir edebilir.

History tablolarÄ± gereklidir; Ã§Ã¼nkÃ¼ veritabanÄ± kataloÄŸu bir nesnenin var olup olmadÄ±ÄŸÄ±nÄ± gÃ¶sterebilir, fakat hangi script versiyonunun uygulandÄ±ÄŸÄ±nÄ±, seed scriptinin daha Ã¶nce Ã§alÄ±ÅŸÄ±p Ã§alÄ±ÅŸmadÄ±ÄŸÄ±nÄ± veya son SQL hash'ini gÃ¼venilir ÅŸekilde tutmaz.

Migration runner katmanÄ± reset onayÄ±, readiness retry, migration lock, transaction policy ve structured execution result iÃ§in operasyonel sertleÅŸtirme sÃ¶zleÅŸmeleri sunar. Production iÃ§in her uygulama instance'Ä±nÄ±n startup sÄ±rasÄ±nda migration Ã§alÄ±ÅŸtÄ±rmasÄ± yerine deployment-time migration job tercih edin. Startup migration kullanÄ±lacaksa production Ã¶ncesinde provider lock strategy yapÄ±landÄ±rÄ±n.

VeritabanÄ± reset iÅŸlemi yÄ±kÄ±cÄ±dÄ±r ve aÃ§Ä±k izin ister:

```csharp
var options = new MigrationRunnerOptions
{
    ResetDatabase = true,
    DestructiveOptions = DestructiveOperationOptions.Allow()
};
```

### CanlÄ± Model Senkronizasyonu

ModelSync attribute modellerini canlÄ± veritabanÄ±yla karÅŸÄ±laÅŸtÄ±rÄ±p incelenebilir dry-run planÄ± Ã¼retebilir. YalnÄ±z additive/update-safe iÅŸlemler otomatik uygulanÄ±r; destructive veya riskli iÅŸlemler raporlanÄ±r ve engellenir.

```csharp
var options = new SqlServerModelSyncOptions
{
    ConnectionString = connectionString,
    HistorySchema = "sec",
    DefaultSchema = "app",
    AllowDestructiveChanges = false,
    ApplyStoredProceduresOnEveryRun = true,
    ApplyTriggersOnEveryRun = true,
    ApplySeedsWithHashTracking = true
};

var result = await SqlServerModelSynchronizer
    .FromAssemblies(options, typeof(Product).Assembly)
    .AddSqlScriptsFromEmbeddedResources(
        typeof(Product).Assembly,
        "MyApp.Database.Scripts")
    .CompareAsync(cancellationToken);

await result.ThrowIfUnsupportedOrDestructiveAsync();
await result.ApplyAsync(cancellationToken);
```

`SafeOperations` otomatik uygulanabilir. `BlockedOperations` manuel incelenmelidir. `SkippedOperations`, seÃ§eneklerle bilinÃ§li kapatÄ±lmÄ±ÅŸ gÃ¼venli iÅŸlemleri gÃ¶sterir.

Eksik tablo, gÃ¼venli eksik kolon, indeks ve desteklenen constraint eklemeleri otomatik uygulanabilir. Drop, rename, tip deÄŸiÅŸikliÄŸi ve nullable-to-not-null iÅŸlemleri sessiz uygulanmaz.

YayÄ±nlanmamÄ±ÅŸ sertleÅŸtirme Ã§alÄ±ÅŸmasÄ± tablo bazlÄ± execution policy ekler:

```csharp
options.DefaultTableMode = ModelSyncTableMode.ManualOnly;
options.TablePolicies
    .ForType<AuditLog>(ModelSyncTableMode.ApplySafeChanges)
    .ForType<Notification>(ModelSyncTableMode.ApplySafeChanges)
    .ForTable("legacy", "OldOrders", ModelSyncTableMode.Ignore);
```

`ManualOnly` operasyonlarÄ± `ManualOperations` altÄ±nda raporlanÄ±r ve otomatik Ã§alÄ±ÅŸtÄ±rÄ±lmaz. `ApplySafeChanges` destructive ÅŸema deÄŸiÅŸikliklerine hiÃ§bir zaman izin vermez.

### SaÄŸlayÄ±cÄ± Destek Matrisi

| Ã–zellik | SQL Server | MySQL / MariaDB | PostgreSQL | SQLite |
|---|:---:|:---:|:---:|:---:|
| Tablo SQL Ã¼retimi | Var | Var | Var | Var |
| Index SQL Ã¼retimi | Var | Var | Var | Var |
| Kolon ekleme | Var | Var | Var | Var |
| Kolon silme | Var | Var | Var | SQLite sÃ¼rÃ¼mÃ¼ne baÄŸlÄ± sÄ±nÄ±rlÄ± |
| Kolon tipi deÄŸiÅŸtirme | Var | Var | Var | Yok |
| Tablo boÅŸaltma | Var | Var | Var | `DELETE FROM` ile taklit edilir |
| Stored procedure senkronizasyonu | Var | Var | Var | Yok |
| Migration runner | Var | Var | Var | Var |
| CanlÄ± model senkronizasyonu | Var | Var | Var | Var |
| `GO` batch ayrÄ±mÄ± | Var | Uygulanmaz | Uygulanmaz | Uygulanmaz |
| VeritabanÄ± reset | Var | Var | Var | Yok |

### Desteklenmeyen veya BilinÃ§li SÄ±nÄ±rlanan Ã–zellikler

| Ã–zellik | Durum | Neden |
|---|---|---|
| Runtime ORM davranÄ±ÅŸÄ± | Desteklenmez | ModelSync bir ÅŸema/script aracÄ±dÄ±r. Veri eriÅŸimi Dapper, ADO.NET, EF Core veya kendi repository katmanÄ±nÄ±zda kalmalÄ±dÄ±r. |
| Sessiz yÄ±kÄ±cÄ± model diff uygulama | Desteklenmez | Drop, rename, tip deÄŸiÅŸikliÄŸi ve nullable-to-not-null iÅŸlemleri raporlanÄ±r fakat otomatik uygulanmaz. |
| Database-first model scaffolding | Bu repo kapsamÄ± dÄ±ÅŸÄ±nda | Scaffolding bir tooling konusudur ve runtime ÅŸema paketinden ayrÄ± tutulmalÄ±dÄ±r. |
| Visual Studio designer/tooling | Bu repo kapsamÄ± dÄ±ÅŸÄ±nda | IDE tooling farklÄ± paketleme ve kullanÄ±cÄ± deneyimi ister. Runtime kÃ¼tÃ¼phanesi kÃ¼Ã§Ã¼k ve saÄŸlayÄ±cÄ± odaklÄ± kalÄ±r. |
| SQLite stored procedure | Desteklenmez | SQLite stored procedure implementasyonu sunmaz. ModelSync sahte destek vermek yerine aÃ§Ä±k unsupported davranÄ±ÅŸÄ± Ã¼retir. |
| SQLite doÄŸrudan kolon tipi deÄŸiÅŸtirme | Desteklenmez | SQLite doÄŸrudan `ALTER COLUMN TYPE` desteklemez; create-copy-drop tablo yeniden kurma stratejisi gerekir. |
| Sessiz yÄ±kÄ±cÄ± operasyonlar | Desteklenmez | Tablo/kolon silme veya tip deÄŸiÅŸtirme veri kaybÄ± oluÅŸturabilir. AÃ§Ä±k `DestructiveOperationOptions.Allow()` gerekir. |
| KullanÄ±cÄ± girdisinden raw default/check expression | GÃ¼venli deÄŸildir | `DbColumnDefault` ve `DbColumnCheck` ÅŸema geliÅŸtiricileri iÃ§in raw SQL parÃ§asÄ± alÄ±r. GÃ¼venilmeyen kullanÄ±cÄ± girdisinden Ã¼retilmemelidir. |
| BoÅŸluk/sembol iÃ§eren serbest identifier adlarÄ± | Desteklenmez | SÄ±kÄ± identifier doÄŸrulamasÄ± gÃ¼venli ve Ã¶ngÃ¶rÃ¼lebilir SQL Ã¼retimi saÄŸlar. |

### Identifier GÃ¼venliÄŸi

ModelSync tablo, kolon, veritabanÄ± ve index adlarÄ±nÄ± quote etmeden Ã¶nce doÄŸrular.

Ä°zin verilen desen:

```text
^[A-Za-z_][A-Za-z0-9_]*$
```

BoÅŸluk, nokta, tÄ±rnak, kÃ¶ÅŸeli parantez, noktalÄ± virgÃ¼l, tire ve diÄŸer noktalama karakterleri bilinÃ§li olarak reddedilir. Bu yaklaÅŸÄ±m Ã¼retilen SQL'i Ã¶ngÃ¶rÃ¼lebilir tutar ve ÅŸema identifier'larÄ± Ã¼zerinden injection riskini azaltÄ±r.

### Desteklenen Attribute'lar

SaÄŸlayÄ±cÄ±ya Ã¶zel attribute'lar:

| Attribute | AÃ§Ä±klama |
|---|---|
| `[{Db}TableName("name")]` | Tablo adÄ±nÄ± belirler |
| `[{Db}ColumnType(Type)]` | SaÄŸlayÄ±cÄ±ya Ã¶zel kolon tipini belirler |
| `[{Db}ColumnPrimaryKey]` | Kolonu primary key olarak iÅŸaretler |
| `[{Db}ColumnNotNull]` | `NOT NULL` ekler |
| `[{Db}ColumnUnique]` | `UNIQUE` ekler |
| `[{Db}ForeignKey("column", "table", "ref")]` | Foreign key ekler |

Ortak attribute'lar:

| Attribute | AÃ§Ä±klama |
|---|---|
| `[DbColumnDefault("expr")]` | Raw SQL `DEFAULT` ifadesi ekler |
| `[DbColumnCheck("expr")]` | Raw SQL `CHECK` ifadesi ekler |
| `[DbColumnIndex]` | `GenerateIndexSql<T>()` ile index SQL'i Ã¼retir |

GÃ¼venlik notu: `DbColumnDefault` ve `DbColumnCheck` tasarÄ±m gereÄŸi raw SQL parÃ§asÄ± alÄ±r. BunlarÄ± incelenmiÅŸ, sabit ÅŸema ifadeleri olarak tutun. KullanÄ±cÄ± girdisinden Ã¼retmeyin.

### Roslyn Analyzer

| Kural | Seviye | AÃ§Ä±klama |
|---|---|---|
| `MSYNC001` | Warning | Public property kolon tipi attribute'u eksik |
| `MSYNC002` | Warning | SÄ±nÄ±fta kolon attribute'u var ama tablo adÄ± attribute'u yok |
| `MSYNC003` | Warning | Model tablosunda primary key yok |

Ã–rnek `.editorconfig`:

```ini
dotnet_diagnostic.MSYNC001.severity = error
dotnet_diagnostic.MSYNC003.severity = none
```

### GeliÅŸtirme

Birim testleri Ã§alÄ±ÅŸtÄ±rma:

```bash
dotnet test ModelSync.sln -c Release --filter "Category!=Integration"
```

Solution build:

```bash
dotnet restore ModelSync.sln
dotnet build ModelSync.sln -c Release --no-restore
```

NuGet paketlerini Ã¼retme:

```bash
dotnet pack ModelSync.sln -c Release --no-build -o artifacts
```

Entegrasyon testleri canlÄ± veritabanÄ± istediÄŸi iÃ§in opt-in Ã§alÄ±ÅŸÄ±r:

```bash
docker compose -f compose.integration.yml up -d
MODELSYNC_RUN_MYSQL_INTEGRATION=1 MODELSYNC_MYSQL_CONNECTION_STRING="Server=127.0.0.1;Port=13306;Database=modelsync_integration;User ID=root;Password=ModelSync_Pass123;" dotnet test UmbrellaFrame.ModelSync.MySqlTest/UmbrellaFrame.ModelSync.MySqlTest.csproj -c Release --filter "Category=Integration"
```

Stored procedure entegrasyon testleri uygun olduÄŸunda Docker test ortamÄ±nÄ± kullanabilir:

```bash
MODELSYNC_RUN_SP_INTEGRATION=1 dotnet test ModelSync.sln -c Release --filter "Category=Integration"
```

### TÃ¼rkÃ§e Kaynaklar

| Kaynak | AÃ§Ä±klama |
|---|---|
| [Tam KullanÄ±m KÄ±lavuzu](docs/13-full-usage-guide-tr.md) | ModelSync 1.2.3 iÃ§in kapsamlÄ± TÃ¼rkÃ§e kullanÄ±m kÄ±lavuzu |
| [Makaleler](articles/README.md) | ModelSync'i anlatmak iÃ§in hazÄ±rlanmÄ±ÅŸ yazÄ±lar |
| [Ã–rnekler](examples/README.md) | MySQL, SQL Server, PostgreSQL, SQLite, destructive operation ve stored procedure Ã¶rnekleri |
| [Stored Procedure Sync](docs/11-stored-procedures.md) | Stored procedure senkronizasyon davranÄ±ÅŸÄ± |
| [Migration Runner](docs/12-migration-runner.md) | SÄ±ralÄ± SQL scriptleri ve history yÃ¶netimi |

### Ä°ngilizce Kaynaklar

| Resource | Description |
|---|---|
| [Full Usage Guide](docs/13-full-usage-guide-en.md) | Complete English guide |
| [Overview](docs/01-overview.md) | Architecture and design decisions |
| [Quick Start](docs/02-quickstart.md) | Provider examples |
| [Attribute Reference](docs/03-attributes.md) | Attribute list and parameters |
| [Provider Guides](docs/04-providers.md) | Provider-specific behavior |
| [API Reference](docs/05-api-reference.md) | Public API surface |
| [Dependency Injection](docs/06-dependency-injection.md) | ASP.NET Core DI usage |
| [Roslyn Analyzers](docs/07-analyzers.md) | Analyzer rules |
| [Architecture](docs/08-architecture.md) | Internal flow and extension points |
| [Contributing](docs/09-contributing.md) | Development setup |
| [Changelog](docs/10-changelog.md) | Version history |
| [NuGet README Source](docs/nuget/README.md) | Package README source |

### KarÅŸÄ±laÅŸtÄ±rma

| Ã–zellik | ModelSync | EF Core | FluentMigrator | DbUp |
|---|:---:|:---:|:---:|:---:|
| ORM baÄŸÄ±mlÄ±lÄ±ÄŸÄ± yok | Var | Yok | Var | Var |
| Attribute tabanlÄ± ÅŸema Ã¼retimi | Var | Var | Yok | Yok |
| SaÄŸlayÄ±cÄ± paketleri | Var | Var | Var | Daha Ã§ok script tabanlÄ± |
| Async DDL Ã§alÄ±ÅŸtÄ±rma | Var | Var | SÄ±nÄ±rlÄ± | Var |
| Analyzer desteÄŸi | Var | Yok | Yok | Yok |
| AÃ§Ä±k yÄ±kÄ±cÄ± iÅŸlem korumasÄ± | Var | KÄ±smi | Manuel | Manuel |
| Dry-run-first canlÄ± DB model diff | Var | Var | Yok | Yok |
| Script migration runner | Var | Var | Var | Var |
| Stored procedure senkronizasyonu | Var | Manuel | Manuel | Script tabanlÄ± |

### Lisans

MIT (c) UmbrellaFrame
