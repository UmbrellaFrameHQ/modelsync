# ModelSync

![ModelSync](https://raw.githubusercontent.com/UmbrellaFrameHQ/modelsync/main/assets/icons/modelsync-core.png)

[![NuGet](https://img.shields.io/nuget/v/UmbrellaFrame.ModelSync.Core.svg?style=flat-square)](https://www.nuget.org/packages/UmbrellaFrame.ModelSync.Core)
[![CI](https://github.com/UmbrellaFrameHQ/modelsync/actions/workflows/ci.yml/badge.svg)](https://github.com/UmbrellaFrameHQ/modelsync/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](LICENSE)
[![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-purple?style=flat-square)](https://learn.microsoft.com/dotnet/standard/net-standard)
[![Views](https://hits.sh/github.com/UmbrellaFrameHQ/modelsync.svg?style=flat-square&label=views&color=blue)](https://hits.sh/github.com/UmbrellaFrameHQ/modelsync/)

**Language:** [English](#english)  
**Dil:** [Türkçe](#türkçe)

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
UmbrellaFrame.ModelSync.Analyzers     Roslyn analyzer package
```

Current package version: `1.1.0`

Development status: the repository contains released 1.1.0 hardening work. NuGet packages are validated by the 1.1.0 provider integration gate.

Architecture rule: `UmbrellaFrame.ModelSync.Core` owns SQL generation and migration planning through a provider-agnostic compiler. Provider packages supply structured descriptors, capabilities, mappings, attributes, connection adapters, and execution integration; they do not maintain independent framework SQL engines.

### What ModelSync Does

- Generates `CREATE TABLE` SQL from attributed C# models.
- Supports explicit column mapping with `DbColumnName` and schema-only public property exclusion with `DbIgnore`.
- Generates `CREATE INDEX`, `DROP TABLE`, `TRUNCATE TABLE`, `ADD COLUMN`, `DROP COLUMN`, and `ALTER COLUMN TYPE` SQL where the provider supports it.
- Executes generated DDL through explicit method calls.
- Requires explicit opt-in for destructive operations.
- Supports SQL Server, MySQL/MariaDB, PostgreSQL, and SQLite table generation.
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
dotnet add package UmbrellaFrame.ModelSync.Core --version 1.1.0
dotnet add package UmbrellaFrame.ModelSync.SqlServer --version 1.1.0
dotnet add package UmbrellaFrame.ModelSync.MySql --version 1.1.0
dotnet add package UmbrellaFrame.ModelSync.PostgreSQL --version 1.1.0
dotnet add package UmbrellaFrame.ModelSync.SQLite --version 1.1.0
```

Each provider package pulls `UmbrellaFrame.ModelSync.Core` automatically.

Analyzer package:

```bash
dotnet add package UmbrellaFrame.ModelSync.Analyzers --version 1.1.0
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

ModelSync 1.1.0 adds table execution policies for mixed ownership:

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

## Türkçe

ModelSync, .NET için attribute tabanlı SQL şema ve script yönetim aracıdır. Sade C# sınıflarıyla veritabanı tabloları tanımlamanızı, sağlayıcıya özel SQL üretmenizi, açık DDL işlemleri çalıştırmanızı, stored procedure senkronizasyonu yapmanızı ve sıralı migration scriptleri uygulamanızı sağlar. Bunu ORM bağımlılığı eklemeden yapar.

ModelSync; Dapper, ADO.NET veya elle yazılmış SQL kullanan ama tekrar edilebilir şema üretimi, güvenlik kontrolleri ve sağlayıcıya özel SQL çıktısı isteyen ekipler için tasarlanmıştır.

### Paketler

```text
UmbrellaFrame.ModelSync.Core          Attribute'lar, arayüzler, ortak SQL üretimi
UmbrellaFrame.ModelSync.SqlServer     SQL Server / Azure SQL sağlayıcısı
UmbrellaFrame.ModelSync.MySql         MySQL / MariaDB sağlayıcısı
UmbrellaFrame.ModelSync.PostgreSQL    PostgreSQL sağlayıcısı
UmbrellaFrame.ModelSync.SQLite        SQLite sağlayıcısı
UmbrellaFrame.ModelSync.Analyzers     Roslyn analyzer paketi
```

Güncel paket sürümü: `1.1.0`

Gelistirme durumu: 1.1.0 sertlestirme calismalari provider entegrasyon kapilariyla dogrulanmistir. NuGet paketleri 1.1.0 surumundedir.

Mimari kural: SQL üretimi ve migration planlama, provider-agnostic compiler ile `UmbrellaFrame.ModelSync.Core` katmanına aittir. Provider paketleri structured descriptor, capability, mapping, attribute, connection adapter ve execution entegrasyonu sağlar; bağımsız framework SQL motoru tutmaz.

### ModelSync Ne Yapar?

- Attribute ile işaretlenmiş C# modellerinden `CREATE TABLE` SQL'i üretir.
- `DbColumnName` ile açık kolon adı eşlemesini ve `DbIgnore` ile şema dışı public property hariç tutmayı destekler.
- Sağlayıcı desteklediği sürece `CREATE INDEX`, `DROP TABLE`, `TRUNCATE TABLE`, `ADD COLUMN`, `DROP COLUMN` ve `ALTER COLUMN TYPE` SQL'i üretir.
- Üretilen DDL'i açık metot çağrılarıyla çalıştırır.
- Veri kaybı oluşturabilecek işlemler için açık onay ister.
- SQL Server, MySQL/MariaDB, PostgreSQL ve SQLite için tablo üretimini destekler.
- SQL Server, MySQL/MariaDB ve PostgreSQL için stored procedure senkronizasyonu yapar.
- Table, stored procedure, trigger ve seed scriptlerini sıralı şekilde çalıştırır.
- Attribute modellerini canlı veritabanıyla karşılaştırıp güvenli dry-run senkronizasyon planı üretir.
- Uygulanan migration scriptlerini history tabloları ve SQL hash bilgisiyle takip eder.
- Eksik tablo adı, primary key ve kolon tipi için Roslyn analyzer uyarıları verir.

### ModelSync Ne Değildir?

ModelSync bilinçli olarak ORM değildir. Entity tracking, LINQ query üretimi, change tracking, lazy loading veya runtime data access görevi üstlenmez. Dapper, ADO.NET, EF Core veya kendi repository katmanınızın yerine geçmez.

ModelSync sessiz ve kontrolsüz bir production mutasyon motoru değildir. Canlı model senkronizasyonu dry-run-first çalışır; eksik tablo, güvenli eksik kolon, indeks ve desteklenen constraint gibi additive işlemleri uygulayabilir, fakat drop, rename, tip değişikliği ve nullable-to-not-null gibi riskli işlemleri raporlar ve otomatik uygulamaz.

### Kurulum

İhtiyacınız olan sağlayıcı paketini kurun:

```bash
dotnet add package UmbrellaFrame.ModelSync.Core --version 1.1.0
dotnet add package UmbrellaFrame.ModelSync.SqlServer --version 1.1.0
dotnet add package UmbrellaFrame.ModelSync.MySql --version 1.1.0
dotnet add package UmbrellaFrame.ModelSync.PostgreSQL --version 1.1.0
dotnet add package UmbrellaFrame.ModelSync.SQLite --version 1.1.0
```

Her sağlayıcı paketi `UmbrellaFrame.ModelSync.Core` paketini otomatik olarak getirir.

Analyzer paketi:

```bash
dotnet add package UmbrellaFrame.ModelSync.Analyzers --version 1.1.0
```

### Hızlı Başlangıç

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

Önerilen akış:

1. SQL üret.
2. SQL'i incele.
3. Gerekiyorsa üretilen veya elle yazılan scriptleri repoya ekle.
4. Canlı veritabanına sadece açık deployment adımlarıyla uygula.

### Yıkıcı İşlem Güvenliği

Veri kaybına yol açabilecek işlemler `DestructiveOperationOptions.Allow()` ister:

```csharp
var allow = DestructiveOperationOptions.Allow();

generator.DropColumn<Product>("LegacyCode", allow);
generator.AlterColumnType<Product>("Price", allow);
await generator.DropTablesAsync(allow, cancellationToken);
```

Yıkıcı metotları açık onay vermeden çağırmak tasarım gereği exception fırlatır. Böylece tehlikeli şema operasyonları code review sırasında görünür olur ve production veri kaybı riski azalır.

### Stored Procedure Senkronizasyonu

Stored procedure'ler `.sql` dosyası olarak tutulabilir ve desteklenen sağlayıcılarla senkronize edilebilir.

```csharp
using UmbrellaFrame.ModelSync.SqlServer;

var procedures = new SqlServerStoredProcedureSynchronizer(connectionString);

procedures.RegisterProcedureFile(
    "Database/Procedures/SqlServer/dbo.usp_GetProducts.sql");

var plans = await procedures.CompareRegisteredAsync(cancellationToken);
await procedures.SyncRegisteredAsync(cancellationToken);
```

Sağlayıcı davranışı:

| Sağlayıcı | Destek | Strateji |
|---|---:|---|
| SQL Server / Azure SQL | Var | `CREATE OR ALTER PROCEDURE` |
| MySQL / MariaDB | Var | `DROP PROCEDURE IF EXISTS` + `CREATE PROCEDURE` |
| PostgreSQL | Var | `CREATE OR REPLACE PROCEDURE` |
| SQLite | Yok | SQLite stored procedure özelliği sunmaz |

Değişiklikleri uygulamadan önce dry-run planı görmek için `CompareRegisteredAsync()` kullanın.

### Migration Runner

Sağlayıcı migration runner'ları sıralı SQL scriptleri uygular:

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

Çalışma sırası:

```text
Tables -> StoredProcedures -> Triggers -> Seeds -> CustomSql
```

Runner history tabloları oluşturur, script hash'i tutar, embedded `.sql` resource'larını destekler ve değişen `CREATE TABLE` scriptlerinden eksik kolonları eklemeli şekilde tamir edebilir.

History tabloları gereklidir; çünkü veritabanı kataloğu bir nesnenin var olup olmadığını gösterebilir, fakat hangi script versiyonunun uygulandığını, seed scriptinin daha önce çalışıp çalışmadığını veya son SQL hash'ini güvenilir şekilde tutmaz.

Migration runner katmanı reset onayı, readiness retry, migration lock, transaction policy ve structured execution result için operasyonel sertleştirme sözleşmeleri sunar. Production için her uygulama instance'ının startup sırasında migration çalıştırması yerine deployment-time migration job tercih edin. Startup migration kullanılacaksa production öncesinde provider lock strategy yapılandırın.

Veritabanı reset işlemi yıkıcıdır ve açık izin ister:

```csharp
var options = new MigrationRunnerOptions
{
    ResetDatabase = true,
    DestructiveOptions = DestructiveOperationOptions.Allow()
};
```

### Canlı Model Senkronizasyonu

ModelSync attribute modellerini canlı veritabanıyla karşılaştırıp incelenebilir dry-run planı üretebilir. Yalnız additive/update-safe işlemler otomatik uygulanır; destructive veya riskli işlemler raporlanır ve engellenir.

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

`SafeOperations` otomatik uygulanabilir. `BlockedOperations` manuel incelenmelidir. `SkippedOperations`, seçeneklerle bilinçli kapatılmış güvenli işlemleri gösterir.

Eksik tablo, güvenli eksik kolon, indeks ve desteklenen constraint eklemeleri otomatik uygulanabilir. Drop, rename, tip değişikliği ve nullable-to-not-null işlemleri sessiz uygulanmaz.

Yayınlanmamış sertleştirme çalışması tablo bazlı execution policy ekler:

```csharp
options.DefaultTableMode = ModelSyncTableMode.ManualOnly;
options.TablePolicies
    .ForType<AuditLog>(ModelSyncTableMode.ApplySafeChanges)
    .ForType<Notification>(ModelSyncTableMode.ApplySafeChanges)
    .ForTable("legacy", "OldOrders", ModelSyncTableMode.Ignore);
```

`ManualOnly` operasyonları `ManualOperations` altında raporlanır ve otomatik çalıştırılmaz. `ApplySafeChanges` destructive şema değişikliklerine hiçbir zaman izin vermez.

### Sağlayıcı Destek Matrisi

| Özellik | SQL Server | MySQL / MariaDB | PostgreSQL | SQLite |
|---|:---:|:---:|:---:|:---:|
| Tablo SQL üretimi | Var | Var | Var | Var |
| Index SQL üretimi | Var | Var | Var | Var |
| Kolon ekleme | Var | Var | Var | Var |
| Kolon silme | Var | Var | Var | SQLite sürümüne bağlı sınırlı |
| Kolon tipi değiştirme | Var | Var | Var | Yok |
| Tablo boşaltma | Var | Var | Var | `DELETE FROM` ile taklit edilir |
| Stored procedure senkronizasyonu | Var | Var | Var | Yok |
| Migration runner | Var | Var | Var | Var |
| Canlı model senkronizasyonu | Var | Var | Var | Var |
| `GO` batch ayrımı | Var | Uygulanmaz | Uygulanmaz | Uygulanmaz |
| Veritabanı reset | Var | Var | Var | Yok |

### Desteklenmeyen veya Bilinçli Sınırlanan Özellikler

| Özellik | Durum | Neden |
|---|---|---|
| Runtime ORM davranışı | Desteklenmez | ModelSync bir şema/script aracıdır. Veri erişimi Dapper, ADO.NET, EF Core veya kendi repository katmanınızda kalmalıdır. |
| Sessiz yıkıcı model diff uygulama | Desteklenmez | Drop, rename, tip değişikliği ve nullable-to-not-null işlemleri raporlanır fakat otomatik uygulanmaz. |
| Database-first model scaffolding | Bu repo kapsamı dışında | Scaffolding bir tooling konusudur ve runtime şema paketinden ayrı tutulmalıdır. |
| Visual Studio designer/tooling | Bu repo kapsamı dışında | IDE tooling farklı paketleme ve kullanıcı deneyimi ister. Runtime kütüphanesi küçük ve sağlayıcı odaklı kalır. |
| SQLite stored procedure | Desteklenmez | SQLite stored procedure implementasyonu sunmaz. ModelSync sahte destek vermek yerine açık unsupported davranışı üretir. |
| SQLite doğrudan kolon tipi değiştirme | Desteklenmez | SQLite doğrudan `ALTER COLUMN TYPE` desteklemez; create-copy-drop tablo yeniden kurma stratejisi gerekir. |
| Sessiz yıkıcı operasyonlar | Desteklenmez | Tablo/kolon silme veya tip değiştirme veri kaybı oluşturabilir. Açık `DestructiveOperationOptions.Allow()` gerekir. |
| Kullanıcı girdisinden raw default/check expression | Güvenli değildir | `DbColumnDefault` ve `DbColumnCheck` şema geliştiricileri için raw SQL parçası alır. Güvenilmeyen kullanıcı girdisinden üretilmemelidir. |
| Boşluk/sembol içeren serbest identifier adları | Desteklenmez | Sıkı identifier doğrulaması güvenli ve öngörülebilir SQL üretimi sağlar. |

### Identifier Güvenliği

ModelSync tablo, kolon, veritabanı ve index adlarını quote etmeden önce doğrular.

İzin verilen desen:

```text
^[A-Za-z_][A-Za-z0-9_]*$
```

Boşluk, nokta, tırnak, köşeli parantez, noktalı virgül, tire ve diğer noktalama karakterleri bilinçli olarak reddedilir. Bu yaklaşım üretilen SQL'i öngörülebilir tutar ve şema identifier'ları üzerinden injection riskini azaltır.

### Desteklenen Attribute'lar

Sağlayıcıya özel attribute'lar:

| Attribute | Açıklama |
|---|---|
| `[{Db}TableName("name")]` | Tablo adını belirler |
| `[{Db}ColumnType(Type)]` | Sağlayıcıya özel kolon tipini belirler |
| `[{Db}ColumnPrimaryKey]` | Kolonu primary key olarak işaretler |
| `[{Db}ColumnNotNull]` | `NOT NULL` ekler |
| `[{Db}ColumnUnique]` | `UNIQUE` ekler |
| `[{Db}ForeignKey("column", "table", "ref")]` | Foreign key ekler |

Ortak attribute'lar:

| Attribute | Açıklama |
|---|---|
| `[DbColumnDefault("expr")]` | Raw SQL `DEFAULT` ifadesi ekler |
| `[DbColumnCheck("expr")]` | Raw SQL `CHECK` ifadesi ekler |
| `[DbColumnIndex]` | `GenerateIndexSql<T>()` ile index SQL'i üretir |

Güvenlik notu: `DbColumnDefault` ve `DbColumnCheck` tasarım gereği raw SQL parçası alır. Bunları incelenmiş, sabit şema ifadeleri olarak tutun. Kullanıcı girdisinden üretmeyin.

### Roslyn Analyzer

| Kural | Seviye | Açıklama |
|---|---|---|
| `MSYNC001` | Warning | Public property kolon tipi attribute'u eksik |
| `MSYNC002` | Warning | Sınıfta kolon attribute'u var ama tablo adı attribute'u yok |
| `MSYNC003` | Warning | Model tablosunda primary key yok |

Örnek `.editorconfig`:

```ini
dotnet_diagnostic.MSYNC001.severity = error
dotnet_diagnostic.MSYNC003.severity = none
```

### Geliştirme

Birim testleri çalıştırma:

```bash
dotnet test ModelSync.sln -c Release --filter "Category!=Integration"
```

Solution build:

```bash
dotnet restore ModelSync.sln
dotnet build ModelSync.sln -c Release --no-restore
```

NuGet paketlerini üretme:

```bash
dotnet pack ModelSync.sln -c Release --no-build -o artifacts
```

Entegrasyon testleri canlı veritabanı istediği için opt-in çalışır:

```bash
docker compose -f compose.integration.yml up -d
MODELSYNC_RUN_MYSQL_INTEGRATION=1 MODELSYNC_MYSQL_CONNECTION_STRING="Server=127.0.0.1;Port=13306;Database=modelsync_integration;User ID=root;Password=ModelSync_Pass123;" dotnet test UmbrellaFrame.ModelSync.MySqlTest/UmbrellaFrame.ModelSync.MySqlTest.csproj -c Release --filter "Category=Integration"
```

Stored procedure entegrasyon testleri uygun olduğunda Docker test ortamını kullanabilir:

```bash
MODELSYNC_RUN_SP_INTEGRATION=1 dotnet test ModelSync.sln -c Release --filter "Category=Integration"
```

### Türkçe Kaynaklar

| Kaynak | Açıklama |
|---|---|
| [Tam Kullanım Kılavuzu](docs/13-full-usage-guide-tr.md) | ModelSync 1.1.0 için kapsamlı Türkçe kullanım kılavuzu |
| [Makaleler](articles/README.md) | ModelSync'i anlatmak için hazırlanmış yazılar |
| [Örnekler](examples/README.md) | MySQL, SQL Server, PostgreSQL, SQLite, destructive operation ve stored procedure örnekleri |
| [Stored Procedure Sync](docs/11-stored-procedures.md) | Stored procedure senkronizasyon davranışı |
| [Migration Runner](docs/12-migration-runner.md) | Sıralı SQL scriptleri ve history yönetimi |

### İngilizce Kaynaklar

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

### Karşılaştırma

| Özellik | ModelSync | EF Core | FluentMigrator | DbUp |
|---|:---:|:---:|:---:|:---:|
| ORM bağımlılığı yok | Var | Yok | Var | Var |
| Attribute tabanlı şema üretimi | Var | Var | Yok | Yok |
| Sağlayıcı paketleri | Var | Var | Var | Daha çok script tabanlı |
| Async DDL çalıştırma | Var | Var | Sınırlı | Var |
| Analyzer desteği | Var | Yok | Yok | Yok |
| Açık yıkıcı işlem koruması | Var | Kısmi | Manuel | Manuel |
| Dry-run-first canlı DB model diff | Var | Var | Yok | Yok |
| Script migration runner | Var | Var | Var | Var |
| Stored procedure senkronizasyonu | Var | Manuel | Manuel | Script tabanlı |

### Lisans

MIT (c) UmbrellaFrame
