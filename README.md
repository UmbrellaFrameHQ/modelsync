# ModelSync

![ModelSync](https://raw.githubusercontent.com/UmbrellaFrameHQ/modelsync/main/assets/icons/modelsync-core.png)

[![NuGet](https://img.shields.io/nuget/v/UmbrellaFrame.ModelSync.Core.svg?style=flat-square)](https://www.nuget.org/packages/UmbrellaFrame.ModelSync.Core)
[![CI](https://github.com/UmbrellaFrameHQ/modelsync/actions/workflows/ci.yml/badge.svg)](https://github.com/UmbrellaFrameHQ/modelsync/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](LICENSE)
[![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-purple?style=flat-square)](https://learn.microsoft.com/dotnet/standard/net-standard)
[![Views](https://hits.sh/github.com/UmbrellaFrameHQ/modelsync.svg?style=flat-square&label=views&color=blue)](https://hits.sh/github.com/UmbrellaFrameHQ/modelsync/)

**Language / Dil:** [English](#english) - [Turkce](#turkce)

---

## English

**Attribute-based SQL schema generator for .NET**  
Zero ORM dependency - 4 database providers - explicit destructive-operation safety.

ModelSync lets you decorate plain C# classes with attributes and generate or execute SQL DDL for `CREATE TABLE`, `ALTER TABLE`, `DROP TABLE`, `TRUNCATE TABLE`, and `CREATE INDEX` without Entity Framework or a heavy ORM.

```text
UmbrellaFrame.ModelSync.Core          -> Attributes, interfaces, SQL builder
UmbrellaFrame.ModelSync.SqlServer     -> SQL Server / Azure SQL provider
UmbrellaFrame.ModelSync.MySql         -> MySQL / MariaDB provider
UmbrellaFrame.ModelSync.PostgreSQL    -> PostgreSQL provider
UmbrellaFrame.ModelSync.SQLite        -> SQLite provider
UmbrellaFrame.ModelSync.Analyzers     -> Roslyn compile-time checks
```

### What's New

- Stored procedure synchronization now supports SQL Server / Azure SQL, MySQL / MariaDB, and PostgreSQL.
- Migration runners now support ordered table, stored procedure, trigger, and seed scripts with history tables.
- Embedded `.sql` resources can be discovered and applied through provider migration runners.
- SQL Server migration scripts support `GO` batch splitting.
- Changed table scripts can repair missing columns additively.
- SQLite has explicit stored procedure unsupported behavior because SQLite does not provide stored procedures.
- Local Docker test databases and opt-in stored procedure integration smoke tests were added.
- Documentation clarifies the difference between explicit model operations and script-based migration repair.
- Composite primary keys now generate table-level `PRIMARY KEY (col1, col2)` constraints.
- SQL Server `IF OBJECT_ID` guards now use validated object names correctly.
- Analyzer rules `MSYNC001`, `MSYNC002`, and `MSYNC003` now have unit test coverage.
- Raw `DbColumnDefault` and `DbColumnCheck` safety guidance is stronger in docs and package README.
- Repository hygiene was improved by removing tracked backup artifacts and cleaning encoding issues.

### Design Philosophy

ModelSync v1 intentionally favors **explicit, developer-controlled schema operations** over automatic live-database mutation.

Schema changes can be destructive. Dropping columns, changing column types, truncating tables, or dropping tables can cause data loss if applied automatically. For that reason, ModelSync v1 generates SQL and provides explicit DDL methods, but it does not silently synchronize a live database.

Planned Phase 2 direction:

- compare model attributes with the live database schema
- generate an ALTER TABLE plan before applying it
- support dry-run SQL output
- classify risky and destructive operations
- require explicit opt-in before data-loss operations

Database-first model scaffolding and Visual Studio tooling are intentionally kept out of this runtime repository. Those tools can build on the same provider packages, but they should live in separate repositories so ModelSync stays focused on schema generation.

### Installation

Install only the provider you need:

```bash
dotnet add package UmbrellaFrame.ModelSync.SqlServer
dotnet add package UmbrellaFrame.ModelSync.MySql
dotnet add package UmbrellaFrame.ModelSync.PostgreSQL
dotnet add package UmbrellaFrame.ModelSync.SQLite
```

Each provider package pulls `UmbrellaFrame.ModelSync.Core` as a dependency.

Optionally add the analyzer package:

```bash
dotnet add package UmbrellaFrame.ModelSync.Analyzers
```

### Downloads and Tutorials

| Resource | Link |
|---|---|
| GitHub repository | [UmbrellaFrameHQ/modelsync](https://github.com/UmbrellaFrameHQ/modelsync) |
| Latest source download | [Download ZIP](https://github.com/UmbrellaFrameHQ/modelsync/archive/refs/heads/main.zip) |
| Releases | [GitHub Releases](https://github.com/UmbrellaFrameHQ/modelsync/releases) |
| Quick start tutorial | [docs/02-quickstart.md](docs/02-quickstart.md) |
| Provider tutorials | [docs/04-providers.md](docs/04-providers.md) |
| Stored procedure sync | [docs/11-stored-procedures.md](docs/11-stored-procedures.md) |
| Full usage guide | [docs/13-full-usage-guide.md](docs/13-full-usage-guide.md) |
| Examples | [examples/README.md](examples/README.md) |
| NuGet README source | [docs/nuget/README.md](docs/nuget/README.md) |

### Quick Start

```csharp
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.MySql;

[MySqlTableName("products")]
public class Product
{
    [MySqlColumnType(MySqlColumnType.INT)]
    [MySqlColumnPrimaryKey(isAutoIncrement: true)]
    public int Id { get; set; }

    [MySqlColumnType(MySqlColumnType.VARCHAR, "255")]
    [MySqlColumnNotNull]
    public string Name { get; set; }

    [MySqlColumnType(MySqlColumnType.DECIMAL, "10,2")]
    [DbColumnDefault("0.00")]
    [DbColumnCheck("Price >= 0")]
    public decimal Price { get; set; }
}

var generator = new MySqlTableGenerator(
    "Server=localhost;Database=mydb;User=root;Password=pass;"
);

generator.CreateDatabase();
generator.GenerateMySqlTable<Product>(ifNotExists: true);
await generator.CreateTablesAsync(cancellationToken);
```

Generated SQL:

```sql
CREATE TABLE IF NOT EXISTS `products` (
    `Id` INT PRIMARY KEY AUTO_INCREMENT,
    `Name` VARCHAR(255) NOT NULL,
    `Price` DECIMAL(10,2) DEFAULT 0.00 CHECK (Price >= 0)
);
```

### Complete Example with Index and Default

```csharp
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.PostgreSQL;

[PostgresTableName("customers")]
public sealed class CustomerModel
{
    [PostgresColumnType(PostgresColumnType.INTEGER)]
    [PostgresColumnPrimaryKey]
    public int Id { get; set; }

    [PostgresColumnType(PostgresColumnType.VARCHAR, "160")]
    [PostgresColumnNotNull]
    [DbColumnIndex("idx_customers_email", isUnique: true)]
    public string Email { get; set; } = string.Empty;

    [PostgresColumnType(PostgresColumnType.TIMESTAMP)]
    [DbColumnDefault("CURRENT_TIMESTAMP")]
    public DateTime CreatedAt { get; set; }
}

var generator = new PostgresTableGenerator(
    "Host=localhost;Database=appdb;Username=postgres;Password=pass;");

var tableSql = generator.GeneratePostgresTable<CustomerModel>(ifNotExists: true);
var indexes = generator.GenerateIndexSql<CustomerModel>();

Console.WriteLine(tableSql);
foreach (var indexSql in indexes)
{
    Console.WriteLine(indexSql);
}
```

This prints both table DDL and index DDL before anything is executed.

### ALTER TABLE Operations

Safe additive operations can run directly:

```csharp
generator.AddColumn<Product>("Stock");
await generator.AddColumnAsync<Product>("Stock", cancellationToken);
```

Destructive or risky operations require explicit opt-in:

```csharp
var allow = DestructiveOperationOptions.Allow();

generator.DropColumn<Product>("LegacyCode", allow);
generator.AlterColumnType<Product>("Price", allow);
generator.DropTables(allow);

await generator.DropColumnAsync<Product>("LegacyCode", allow, cancellationToken);
await generator.AlterColumnTypeAsync<Product>("Price", allow, cancellationToken);
await generator.DropTablesAsync(allow, cancellationToken);
```

Calling `DropColumn`, `AlterColumnType`, or `DropTables` without `DestructiveOperationOptions.Allow()` throws an exception by design.

SQLite does not support `ALTER COLUMN TYPE` directly. Even with destructive permission, SQLite throws `NotSupportedException`; use a create-copy-drop table rebuild strategy instead.

### Stored Procedures

Stored procedures can be kept as project `.sql` files and synchronized with supported databases.

SQL Server:

```csharp
using UmbrellaFrame.ModelSync.SqlServer;

var procedures = new SqlServerStoredProcedureSynchronizer(connectionString);

procedures.RegisterProcedureFile(
    "Database/Procedures/SqlServer/dbo.usp_GetProducts.sql");

var plans = await procedures.CompareRegisteredAsync();

foreach (var plan in plans)
{
    Console.WriteLine($"{plan.Definition.Schema}.{plan.Definition.Name}: {plan.ChangeType}");
}

await procedures.SyncRegisteredAsync();
```

MySQL / MariaDB:

```csharp
using UmbrellaFrame.ModelSync.MySql;

var procedures = new MySqlStoredProcedureSynchronizer(connectionString);

procedures.RegisterProcedureFile(
    "Database/Procedures/MySql/appdb.usp_GetProducts.sql");

var plans = await procedures.CompareRegisteredAsync();
await procedures.SyncRegisteredAsync();
```

PostgreSQL:

```csharp
using UmbrellaFrame.ModelSync.PostgreSQL;

var procedures = new PostgresStoredProcedureSynchronizer(connectionString);

procedures.RegisterProcedureFile(
    "Database/Procedures/PostgreSQL/public.usp_get_products.sql");

var plans = await procedures.CompareRegisteredAsync();
await procedures.SyncRegisteredAsync();
```

Supported providers:

| Provider | Stored Procedure Sync |
|---|---|
| SQL Server / Azure SQL | Yes, via `CREATE OR ALTER PROCEDURE` |
| MySQL / MariaDB | Yes, via `DROP PROCEDURE IF EXISTS` + `CREATE PROCEDURE` |
| PostgreSQL | Yes, via `CREATE OR REPLACE PROCEDURE` |
| SQLite | Not supported |

ModelSync creates missing procedures and updates changed procedures according to provider capabilities.
Run `CompareRegisteredAsync()` first when you want to preview the SQL before applying it.

See [docs/11-stored-procedures.md](docs/11-stored-procedures.md) for SQL file rules, provider behavior, and local integration test setup.

### Migration Runner

For full project setup scripts, use the provider migration runners:

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

Supported migration categories run in this order:

```text
Tables -> StoredProcedures -> Triggers -> Seeds
```

The runner creates migration history tables, stores script hashes, supports embedded `.sql` resources, and can add missing columns from changed table scripts. SQL Server also supports `GO` batch splitting.

Important scope note: adding a new C# property to a model does not yet trigger automatic live database diffing. Use explicit model operations such as `AddColumn<T>("PropertyName")`, or use migration runner SQL scripts as the source of truth. Missing-column repair in the migration runner is based on changed `CREATE TABLE` scripts and migration history hashes, not direct model-property comparison.

History tables are used because a live database catalog can show whether an object exists, but it cannot reliably show which script version was applied, whether a seed already ran, or which SQL hash was last deployed.

Optional database reset is destructive and requires:

```csharp
new MigrationRunnerOptions
{
    ResetDatabase = true,
    DestructiveOptions = DestructiveOperationOptions.Allow()
};
```

See [docs/12-migration-runner.md](docs/12-migration-runner.md) for provider support and behavior notes.

### Identifier Safety

ModelSync uses strict identifier validation before quoting table, column, index, and database names.

Allowed identifier pattern:

```text
^[A-Za-z_][A-Za-z0-9_]*$
```

This intentionally rejects spaces, dots, quotes, brackets, semicolons, hyphens, and other characters that make generated DDL harder to reason about safely.

### Supported Attributes

Provider-specific attributes:

| Attribute | Description |
|---|---|
| `[{Db}TableName("name")]` | Set table name |
| `[{Db}ColumnType(Type)]` | Set column data type |
| `[{Db}ColumnPrimaryKey]` | Mark as primary key |
| `[{Db}ColumnNotNull]` | Add NOT NULL |
| `[{Db}ColumnUnique]` | Add UNIQUE |
| `[{Db}ForeignKey("column","table","ref")]` | Add foreign key |

Cross-provider attributes:

| Attribute | Description |
|---|---|
| `[DbColumnDefault("expr")]` | Add DEFAULT expression |
| `[DbColumnCheck("expr")]` | Add CHECK expression |
| `[DbColumnIndex]` | Generate CREATE INDEX SQL via `GenerateIndexSql<T>()` |

> Security note: `DbColumnDefault` and `DbColumnCheck` are raw SQL fragments by design. Never build these values from user input; keep them as reviewed, hard-coded schema expressions.

### Roslyn Analyzer

| Rule | Severity | Description |
|---|---|---|
| MSYNC001 | Warning | Public property is missing a column type attribute |
| MSYNC002 | Warning | Class has column attributes but no table name attribute |
| MSYNC003 | Warning | Model table has no primary key defined |

```ini
dotnet_diagnostic.MSYNC001.severity = error
dotnet_diagnostic.MSYNC003.severity = none
```

### Development

Run unit tests without external databases:

```powershell
.\scripts\test.ps1
```

Run all solution build steps on Windows:

```powershell
.\scripts\build.ps1
```

Package NuGet artifacts:

```powershell
.\scripts\pack.ps1
```

If your local PowerShell policy blocks scripts:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\pack.ps1
```

Publish NuGet packages after setting `NUGET_API_KEY`:

```powershell
.\scripts\publish-nuget.ps1
```

Integration tests are opt-in because they require live databases. Set the relevant flag and connection string before running `.\scripts\test.ps1 -Integration`:

```powershell
$env:MODELSYNC_RUN_MYSQL_INTEGRATION = "1"
$env:MODELSYNC_MYSQL_CONNECTION_STRING = "Server=localhost;Port=3306;Database=appdb;User ID=root;Password=rootpass;"
```

Stored procedure integration tests can use the provided Docker test environment:

```powershell
.\scripts\start-test-databases.ps1
$env:MODELSYNC_RUN_SP_INTEGRATION = "1"
dotnet test ModelSync.sln -c Release --filter "Category=Integration"
```

Note: integration tests are intentionally opt-in. A fresh clone can run unit tests without installing MySQL, PostgreSQL, SQL Server, or MariaDB.

### Documentation

| Document | Description |
|---|---|
| [Overview](docs/01-overview.md) | Architecture and design decisions |
| [Quick Start](docs/02-quickstart.md) | Working examples per provider |
| [Attribute Reference](docs/03-attributes.md) | Attribute list and parameter details |
| [Provider Guides](docs/04-providers.md) | Provider-specific behavior |
| [API Reference](docs/05-api-reference.md) | Public API details |
| [Dependency Injection](docs/06-dependency-injection.md) | ASP.NET Core DI usage |
| [Roslyn Analyzers](docs/07-analyzers.md) | Analyzer rules |
| [Architecture](docs/08-architecture.md) | Internal flow and extension points |
| [Contributing](docs/09-contributing.md) | Development setup |
| [Changelog](docs/10-changelog.md) | Version history |
| [Stored Procedure Sync](docs/11-stored-procedures.md) | SQL Server, MySQL/MariaDB, and PostgreSQL procedure file synchronization |
| [Migration Runner](docs/12-migration-runner.md) | Ordered table, procedure, trigger, and seed SQL scripts with history tracking |
| [Full Usage Guide](docs/13-full-usage-guide.md) | Complete Turkish NuGet usage guide for ModelSync 1.0.7 |

### Articles and Examples

| Resource | Description |
|---|---|
| [Articles](articles/README.md) | Three short publish-ready articles for introducing ModelSync |
| [Examples](examples/README.md) | MySQL, SQL Server, PostgreSQL, SQLite, destructive-operation, and stored-procedure examples |

Start with the examples when evaluating the project. They show the recommended flow: generate SQL first, inspect it, and only then execute DDL against a live database.

### Why ModelSync?

| Feature | ModelSync | EF Core | FluentMigrator | DbUp |
|---|:---:|:---:|:---:|:---:|
| Zero ORM dependency | Yes | No | Yes | Yes |
| Attribute-based schema | Yes | Yes | No | No |
| Provider packages | Yes | Yes | Yes | Yes |
| Async DDL execution | Yes | Yes | Limited | Yes |
| Analyzer support | Yes | No | No | No |
| Explicit destructive safety | Yes | Partial | Manual | Manual |
| Automatic live DB diff | Planned | Yes | No | No |

### License

MIT (c) UmbrellaFrame

---

## Turkce

**.NET icin attribute tabanli SQL sema uretici**  
Sifir ORM bagimliligi - 4 veritabani saglayici - yikici islemler icin acik onay guvenligi.

ModelSync, sade C# siniflarini attribute'larla isaretleyerek Entity Framework veya agir bir ORM kullanmadan `CREATE TABLE`, `ALTER TABLE`, `DROP TABLE`, `TRUNCATE TABLE` ve `CREATE INDEX` DDL'i uretmenizi veya calistirmanizi saglar.

### Tasarim Felsefesi

ModelSync v1, canli veritabanini otomatik degistirmek yerine **gelistiricinin acikca kontrol ettigi sema islemlerini** tercih eder.

Sema degisiklikleri yikici olabilir. Sutun silme, sutun tipi degistirme, tabloyu bosaltma veya tablo silme gibi islemler otomatik uygulanirsa veri kaybina neden olabilir. Bu nedenle ModelSync v1 SQL uretir ve acik DDL metotlari saglar; canli veritabanini sessizce kendi kendine senkronize etmez.

Planlanan Faz 2 yonu:

- model attribute'larini canli veritabani semasiyla karsilastirmak
- uygulamadan once ALTER TABLE plani uretmek
- dry-run SQL ciktisi vermek
- riskli ve yikici islemleri siniflandirmak
- veri kaybi olusturabilecek islemler icin acik onay istemek

Database-first model scaffolding ve Visual Studio araclari bu runtime repodan bilincli olarak ayrilmalidir. Bu araclar ayni provider paketlerinden beslenebilir, fakat ModelSync reposu sema uretimine odakli kalmalidir.

### Kurulum

```bash
dotnet add package UmbrellaFrame.ModelSync.SqlServer
dotnet add package UmbrellaFrame.ModelSync.MySql
dotnet add package UmbrellaFrame.ModelSync.PostgreSQL
dotnet add package UmbrellaFrame.ModelSync.SQLite
dotnet add package UmbrellaFrame.ModelSync.Analyzers
```

Her saglayici paketi `UmbrellaFrame.ModelSync.Core` paketini bagimlilik olarak indirir.

### Hizli Baslangic

```csharp
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.MySql;

[MySqlTableName("urunler")]
public class Urun
{
    [MySqlColumnType(MySqlColumnType.INT)]
    [MySqlColumnPrimaryKey(isAutoIncrement: true)]
    public int Id { get; set; }

    [MySqlColumnType(MySqlColumnType.VARCHAR, "255")]
    [MySqlColumnNotNull]
    public string Ad { get; set; }

    [MySqlColumnType(MySqlColumnType.DECIMAL, "10,2")]
    [DbColumnDefault("0.00")]
    [DbColumnCheck("Fiyat >= 0")]
    public decimal Fiyat { get; set; }
}

var generator = new MySqlTableGenerator(
    "Server=localhost;Database=mydb;User=root;Password=pass;"
);

generator.CreateDatabase();
generator.GenerateMySqlTable<Urun>(ifNotExists: true);
await generator.CreateTablesAsync(cancellationToken);
```

### ALTER TABLE Islemleri

Guvenli ekleme islemleri dogrudan calisabilir:

```csharp
generator.AddColumn<Urun>("Stok");
await generator.AddColumnAsync<Urun>("Stok", cancellationToken);
```

Yikici veya riskli islemler acik onay ister:

```csharp
var allow = DestructiveOperationOptions.Allow();

generator.DropColumn<Urun>("EskiKod", allow);
generator.AlterColumnType<Urun>("Fiyat", allow);
generator.DropTables(allow);
```

`DropColumn`, `AlterColumnType` veya `DropTables` metotlarini `DestructiveOperationOptions.Allow()` olmadan cagirmak tasarim geregi exception firlatir.

SQLite `ALTER COLUMN TYPE` islemini dogrudan desteklemez. Destructive izin verilse bile SQLite saglayicisi `NotSupportedException` firlatir; tabloyu yeniden olusturup veriyi tasima stratejisi gerekir.

### Stored Procedure Senkronizasyonu

Stored procedure dosyalari proje icinde `.sql` olarak tutulabilir ve desteklenen veritabanlariyla senkronize edilebilir.

Destek durumu:

| Saglayici | Stored Procedure Sync |
|---|---|
| SQL Server / Azure SQL | Evet, `CREATE OR ALTER PROCEDURE` ile |
| MySQL / MariaDB | Evet, `DROP PROCEDURE IF EXISTS` + `CREATE PROCEDURE` ile |
| PostgreSQL | Evet, `CREATE OR REPLACE PROCEDURE` ile |
| SQLite | Desteklenmez |

Ornek:

```csharp
using UmbrellaFrame.ModelSync.SqlServer;

var procedures = new SqlServerStoredProcedureSynchronizer(connectionString);

procedures.RegisterProcedureFile(
    "Database/Procedures/SqlServer/dbo.usp_GetProducts.sql");

var plans = await procedures.CompareRegisteredAsync();
await procedures.SyncRegisteredAsync();
```

Canli veritabanina uygulamadan once `CompareRegisteredAsync()` ile dry-run planini inceleyin. Detaylar ve Docker test ortami icin [docs/11-stored-procedures.md](docs/11-stored-procedures.md) dosyasina bakin.

### Migration Runner

Tam proje kurulumu icin provider migration runner'lari kullanilabilir:

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

Script sirasi:

```text
Tables -> StoredProcedures -> Triggers -> Seeds
```

Runner history tablolari olusturur, script hash'i tutar, embedded `.sql` resource'larini okuyabilir ve degisen tablo scriptlerinden eksik kolonlari ekleyebilir. SQL Server icin `GO` batch ayrimi desteklenir.

Onemli kapsam notu: modele yeni C# property eklemek henuz otomatik live database diff calistirmaz. Bunun icin `AddColumn<T>("PropertyName")` gibi acik operasyon kullanilir veya migration runner tarafinda SQL scriptleri kaynak kabul edilir. Migration runner'daki eksik kolon tamiri model property karsilastirmasindan degil, degisen `CREATE TABLE` scriptinden ve history hash bilgisinden gelir.

History tablolari gereklidir cunku live database kataloglari nesnenin var olup olmadigini gosterir, fakat hangi script versiyonunun uygulandigini, seed'in daha once calisip calismadigini veya son SQL hash'ini guvenilir sekilde tutmaz.

Detaylar icin [docs/12-migration-runner.md](docs/12-migration-runner.md) dosyasina bakin.

### Identifier Guvenligi

ModelSync tablo, kolon, index ve veritabani adlarini quote etmeden once siki sekilde dogrular.

Izin verilen desen:

```text
^[A-Za-z_][A-Za-z0-9_]*$
```

Bosluk, nokta, tirnak, koseli parantez, noktali virgul, tire ve benzeri karakterler bilincli olarak reddedilir.

### Desteklenen Attribute'lar

| Attribute | Aciklama |
|---|---|
| `[{Db}TableName("isim")]` | Tablo adini belirler |
| `[{Db}ColumnType(Tip)]` | Sutun veri tipini belirler |
| `[{Db}ColumnPrimaryKey]` | Primary key olarak isaretler |
| `[{Db}ColumnNotNull]` | NOT NULL ekler |
| `[{Db}ColumnUnique]` | UNIQUE ekler |
| `[{Db}ForeignKey("sutun","tablo","ref")]` | Foreign key ekler |
| `[DbColumnDefault("ifade")]` | DEFAULT ifadesi ekler |
| `[DbColumnCheck("ifade")]` | CHECK ifadesi ekler |
| `[DbColumnIndex]` | `GenerateIndexSql<T>()` ile index SQL'i uretir |

> Guvenlik notu: `DbColumnDefault` ve `DbColumnCheck` bilerek raw SQL parcasi alir. Bu degerleri kullanici girdisinden uretmeyin; sadece incelenmis, sabit sema ifadeleri olarak kullanin.

### Makaleler ve Ornekler

| Kaynak | Aciklama |
|---|---|
| [Makaleler](articles/README.md) | ModelSync'i tanitmak icin hazir uc kisa yazi |
| [Ornekler](examples/README.md) | MySQL, SQL Server, PostgreSQL, SQLite, destructive-operation ve stored procedure ornekleri |
| [Tam Kullanim Kilavuzu](docs/13-full-usage-guide.md) | ModelSync 1.0.7 icin eksiksiz NuGet kullanim kilavuzu |

Projeyi degerlendirirken once orneklerden baslayin. Onerilen akis once SQL uretmek, SQL'i incelemek ve ancak sonra canli veritabaninda DDL calistirmaktir.

### Lisans

MIT (c) UmbrellaFrame
