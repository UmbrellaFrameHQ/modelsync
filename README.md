# ModelSync

![ModelSync](https://raw.githubusercontent.com/UmbrellaFrameHQ/modelsync/main/assets/icons/modelsync-core.png)

[![NuGet](https://img.shields.io/nuget/v/UmbrellaFrame.ModelSync.Core.svg?style=flat-square)](https://www.nuget.org/packages/UmbrellaFrame.ModelSync.Core)
[![CI](https://github.com/UmbrellaFrameHQ/modelsync/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/UmbrellaFrameHQ/modelsync/actions/workflows/ci.yml?query=branch%3Amain)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](LICENSE)
[![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-purple?style=flat-square)](https://learn.microsoft.com/dotnet/standard/net-standard)

**Language:** [English](#english) · [Türkçe](#türkçe)

## English

ModelSync keeps database structure close to your .NET code without turning the project into an ORM. It can generate provider-specific table SQL from attributed C# models, compare models with a live database, run ordered SQL migrations, synchronize stored procedures, and leave a readable migration report behind.

It is a good fit for Dapper, ADO.NET, hand-written SQL, and services where schema changes should remain visible in code review.

Current package version: `1.3.0`

### Find The Right Tool

| I need to... | Use |
|---|---|
| Generate or execute table DDL from one model | `TableGenerator` |
| Compare attributed models with a live database | `ModelSynchronizer` |
| Run ordered table, procedure, trigger, seed, and custom SQL files | `MigrationRunner` |
| Synchronize procedure files directly | `StoredProcedureSynchronizer` |
| Validate, preview, and run migrations from a terminal or CI | `modelsync` CLI |

Start with the [full usage guide](docs/13-full-usage-guide-en.md) when choosing between these workflows.

### Packages

| Package | Purpose |
|---|---|
| `UmbrellaFrame.ModelSync.SqlServer` | SQL Server and Azure SQL |
| `UmbrellaFrame.ModelSync.MySql` | MySQL and MariaDB |
| `UmbrellaFrame.ModelSync.PostgreSQL` | PostgreSQL |
| `UmbrellaFrame.ModelSync.SQLite` | SQLite |
| `UmbrellaFrame.ModelSync.Oracle` | Oracle preview: table DDL and safe model comparison |
| `UmbrellaFrame.ModelSync.Analyzers` | Compile-time model checks |
| `UmbrellaFrame.ModelSync.Cli` | Migration CLI and Markdown/JSON reports |

Provider packages pull `UmbrellaFrame.ModelSync.Core` automatically.

```bash
dotnet add package UmbrellaFrame.ModelSync.Core --version 1.3.0
dotnet add package UmbrellaFrame.ModelSync.SqlServer --version 1.3.0
dotnet add package UmbrellaFrame.ModelSync.MySql --version 1.3.0
dotnet add package UmbrellaFrame.ModelSync.PostgreSQL --version 1.3.0
dotnet add package UmbrellaFrame.ModelSync.SQLite --version 1.3.0
dotnet add package UmbrellaFrame.ModelSync.Analyzers --version 1.3.0
```

Oracle remains a preview provider. Its migration runner, stored procedure synchronization, reset, and native lock features are not production-ready yet. See the [provider support matrix](docs/provider-support-matrix.md) before adopting it.

**Oracle note:** the preview package is published for evaluation, but its support surface is intentionally smaller than the four stable providers.

### Five-Minute Table Example

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

var generator = new SqlServerTableGenerator(connectionString);
var sql = generator.GenerateSqlServerTable<Product>(ifNotExists: true);

Console.WriteLine(sql);              // review before execution
await generator.CreateTablesAsync();
```

`Generate...Table<T>()` generates and caches SQL. `CreateTablesAsync()` executes the cached statements. Index SQL can be reviewed separately with `GenerateIndexSql<T>()`.

### Safe Schema Changes

Additive operations are explicit:

```csharp
await generator.AddColumnAsync<Product>("Stock");
```

Operations that may lose data require visible approval:

```csharp
var allow = DestructiveOperationOptions.Allow();

generator.DropColumn<Product>("LegacyCode", allow);
generator.AlterColumnType<Product>("Price", allow);
await generator.DropTablesAsync(allow);
```

ModelSync never treats this approval as permission for arbitrary automatic destructive model changes. Live synchronization still reports drop, rename, narrowing, and risky nullability changes instead of silently applying them.

### Live Model Synchronization

Use a model synchronizer when the database already exists and you want a reviewable diff:

```csharp
var options = new SqlServerModelSyncOptions
{
    ConnectionString = connectionString,
    DefaultSchema = "app",
    HistorySchema = "sec",
    ReportUnmappedTables = false
};

var result = await SqlServerModelSynchronizer
    .FromAssemblies(options, typeof(Product).Assembly)
    .CompareAsync(cancellationToken);

// Inspect AutomaticOperations, ManualOperations, SkippedOperations,
// and BlockedOperations before applying anything.
await result.ApplyAsync(cancellationToken);
```

Table policies can mix ownership in the same run:

```csharp
options.DefaultTableMode = ModelSyncTableMode.ManualOnly;
options.TablePolicies
    .ForType<AuditLog>(ModelSyncTableMode.ApplySafeChanges)
    .ForTable("legacy", "OldOrders", ModelSyncTableMode.Ignore);
```

`ManualOnly` changes are reported but never executed. `ApplySafeChanges` never authorizes destructive changes.

### Ordered SQL Migrations

```csharp
var runner = new SqlServerMigrationRunner(connectionString);

runner.RegisterScriptFile("Database/Scripts/Tables/001_CreateProducts.sql");
runner.RegisterScriptFile("Database/Scripts/StoredProcedures/010_GetProducts.sql");
runner.RegisterScriptFile("Database/Scripts/Triggers/020_ProductAudit.sql");
runner.RegisterScriptFile("Database/Scripts/Seeds/030_DefaultProducts.sql");
runner.RegisterScriptFile("Database/Scripts/CustomSql/999_AfterSetup.sql");

var plan = await runner.CompareRegisteredAsync(cancellationToken); // read-only
var result = await runner.RunWithResultAsync(cancellationToken);
```

Execution order is `Tables -> StoredProcedures -> Triggers -> Seeds -> CustomSql`. History tables and SQL hashes make repeated runs predictable. Application-supplied SQL remains trusted project content; ModelSync does not attempt to prove arbitrary SQL safe.

### CLI: Preview First, Apply Deliberately

```bash
dotnet tool install --global UmbrellaFrame.ModelSync.Cli --version 1.3.0
```

Keep connection strings out of process arguments:

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

`--apply` is required for mutation. `--connection` remains available for compatibility but can expose secrets in process listings. Ctrl+C is propagated to migration operations.

### Controlled Database Reset

Reset is intentionally difficult to trigger accidentally:

```csharp
var options = new MigrationRunnerOptions
{
    ResetDatabase = true,
    ResetOptions = new DatabaseResetOptions
    {
        Enabled = true,
        Approval = DestructiveOperationOptions.Allow(),
        ExpectedDatabaseName = "AppDb",
        EnvironmentName = "Development",
        AllowedEnvironments = new[] { "Development" },
        BackupBeforeReset = true,             // SQL Server only
        BackupDirectory = @"C:\SqlBackups"
    }
};
```

System databases are rejected. SQL Server reset runs before the native migration lock is acquired, then readiness, infrastructure, history, and scripts continue under the normal lock. Backup paths are evaluated from the SQL Server service account's filesystem.

### Provider Support

| Feature | SQL Server | MySQL/MariaDB | PostgreSQL | SQLite | Oracle preview |
|---|:---:|:---:|:---:|:---:|:---:|
| Table and index DDL | Yes | Yes | Yes | Yes | Yes |
| Safe live model sync | Yes | Yes | Yes | Yes | Partial |
| Ordered migration runner | Yes | Yes | Yes | Yes | No |
| Stored procedures | Yes | Yes | Yes | No | No |
| Native migration lock | Yes | Yes | Yes | SQLite write lock | No |
| Controlled DB reset | Yes | Yes | Yes | Limited | No |

The detailed and tested limitations live in the [provider support matrix](docs/provider-support-matrix.md).

### Important Boundaries

- ModelSync is not an ORM and does not provide LINQ, entity tracking, or runtime CRUD.
- `DbColumnDefault`, `DbColumnCheck`, and migration scripts contain reviewed SQL. Never build them from untrusted user input.
- Prefer a deployment-time migration job. If migrations run during application startup, keep provider-native locking enabled.
- Compare APIs are read-only. Infrastructure is created only by explicit mutation APIs.
- Published NuGet versions are immutable. Older packages are not overwritten or unlisted to hide migration work.

### Documentation

| Topic | Document |
|---|---|
| Start here | [Documentation index](docs/index.md) |
| Complete usage | [English](docs/13-full-usage-guide-en.md) · [Türkçe](docs/13-full-usage-guide-tr.md) |
| Provider details | [Provider guides](docs/04-providers.md) · [Support matrix](docs/provider-support-matrix.md) |
| Migration runner | [Migration runner](docs/12-migration-runner.md) |
| Live synchronization | [English](docs/14-model-synchronizer.md) · [Türkçe](docs/14-model-synchronizer-tr.md) |
| CLI and reports | [CLI guide](UmbrellaFrame.ModelSync.Cli/README.md) · [Reporting](docs/migration-reporting.md) |
| CLI and scaffolder direction | [Roadmap](docs/cli-and-scaffolder-roadmap.md) |
| Release | [1.3.0 notes](docs/releases/1.3.0.md) · [Migration guide](docs/migrations/1.2.3-to-1.3.0.md) |

### Versioning, Release Notes and Migration Guides

Start with the current [release notes](docs/releases/1.3.0.md) when upgrading. Breaking or behavior-sensitive changes are paired with a focused [migration guide](docs/migrations/1.2.3-to-1.3.0.md), while the complete history remains in [CHANGELOG.md](CHANGELOG.md).

### Build And Test

```bash
dotnet restore ModelSync.sln
dotnet build ModelSync.sln -c Release --no-restore
dotnet test ModelSync.sln -c Release --no-build --filter "Category!=Integration"
dotnet run --project tools/UmbrellaFrame.ModelSync.RepositoryChecks -- verify-all
```

Live provider tests use [compose.integration.yml](compose.integration.yml) and explicit local-only credentials. Start that environment with `docker compose -f compose.integration.yml up -d`; do not reuse its credentials outside tests.

The opt-in `UmbrellaFrame.ModelSync.ScaleTest` suite creates one million rows on SQL Server, MySQL, MariaDB, PostgreSQL, SQLite, and Oracle. It measures live schema comparison, safe column and index additions, idempotent re-runs, destructive-change blocking, and migration history where the provider supports it. The regular provider integration suite remains the authority for reset, lock, routine, trigger, seed, constraint, and foreign-key behavior. Run the scale suite locally after starting Docker:

```text
$env:MODELSYNC_RUN_SCALE_INTEGRATION = "1"
dotnet test UmbrellaFrame.ModelSync.ScaleTest/UmbrellaFrame.ModelSync.ScaleTest.csproj -c Release --filter "Category=Scale"
```

The same matrix runs weekly and on demand through the `Million Row Scale Tests` GitHub Actions workflow. Scale timings are environment evidence, not a production latency guarantee.

## Türkçe

ModelSync, veritabanı yapısını .NET koduna yakın tutmak isteyen fakat bunun için ağır bir ORM kullanmak istemeyen projeler içindir. Attribute eklenmiş C# modellerinden provider'a uygun tablo SQL'i üretir, canlı veritabanıyla modeli karşılaştırır, sıralı migration dosyalarını çalıştırır, stored procedure'leri senkronize eder ve çalışmanın sonunda okunabilir bir rapor bırakır.

Dapper, ADO.NET ve elle SQL yazılan projelerde özellikle işe yarar. Amaç veritabanını gizlice değiştirmek değil; yapılacak değişikliği görünür, tekrar edilebilir ve incelenebilir hale getirmektir.

Güncel sürüm: **1.3.0**

### Hangisini Kullanmalıyım?

| İhtiyaç | Kullanılacak araç |
|---|---|
| Tek modelden tablo SQL'i üretmek | `TableGenerator` |
| Modelleri canlı veritabanıyla karşılaştırmak | `ModelSynchronizer` |
| Table, procedure, trigger, seed ve özel SQL dosyalarını sırayla çalıştırmak | `MigrationRunner` |
| Stored procedure dosyalarını doğrudan senkronize etmek | `StoredProcedureSynchronizer` |
| Terminal veya CI üzerinden doğrulama, dry-run ve rapor almak | `modelsync` CLI |

Provider paketi Core paketini otomatik getirir:

```bash
dotnet add package UmbrellaFrame.ModelSync.SqlServer --version 1.3.0
dotnet add package UmbrellaFrame.ModelSync.Analyzers --version 1.3.0
```

Oracle şu anda preview durumundadır. Tablo DDL ve güvenli model karşılaştırmasının bir bölümü vardır; migration runner, stored procedure, reset ve native lock desteği henüz production seviyesinde değildir.

### Güvenli Çalışma Akışı

1. SQL'i veya diff planını üretin.
2. Planı ve bloklanan işlemleri inceleyin.
3. Yıkıcı işlemler için açık izin verin.
4. Migration'ı mümkünse deployment sırasında tek bir job ile çalıştırın.
5. Markdown veya JSON raporunu deployment kanıtı olarak saklayın.

CLI kullanımında bağlantı bilgisini environment variable içinde tutun:

```console
$env:MODELSYNC_CONNECTION_STRING = "Data Source=modelsync-preview.db"

modelsync validate --scripts .\Database\Scripts

modelsync run `
  --provider sqlite `
  --connection-env MODELSYNC_CONNECTION_STRING `
  --scripts .\Database\Scripts `
  --dry-run

modelsync run `
  --provider sqlite `
  --connection-env MODELSYNC_CONNECTION_STRING `
  --scripts .\Database\Scripts `
  --apply `
  --report-md .\artifacts\modelsync-report.md `
  --report-json .\artifacts\modelsync-report.json
```

`--apply` olmadan migration uygulanmaz. `--connection` geriye uyumluluk için vardır fakat process listesinde görünebileceği için production ve CI ortamlarında önerilmez.

### Bilmeniz Gereken Sınırlar

- ModelSync bir ORM değildir; veri sorgulama, entity tracking veya LINQ sağlamaz.
- Drop, rename, kolon tipi daraltma ve riskli nullability değişiklikleri otomatik uygulanmaz.
- `DbColumnDefault`, `DbColumnCheck` ve migration dosyaları güvenilir proje SQL'i kabul edilir; kullanıcı girdisinden üretilmemelidir.
- SQLite stored procedure desteklemez ve kolon tipini doğrudan değiştiremez.
- Oracle preview desteği diğer provider'larla aynı migration kapsamına henüz sahip değildir.

Ayrıntılı Türkçe anlatım için [tam kullanım kılavuzuna](docs/13-full-usage-guide-tr.md), provider farkları için [destek matrisine](docs/provider-support-matrix.md), canlı senkronizasyon için [Model Synchronizer kılavuzuna](docs/14-model-synchronizer-tr.md) bakın.

## Contributing

Issues and pull requests are welcome. Start with [CONTRIBUTING.md](CONTRIBUTING.md) and [SECURITY.md](SECURITY.md).

## License

MIT © UmbrellaFrame
