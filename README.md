<div align="center">

<img src="assets/icons/modelsync-core.png" alt="ModelSync" width="160"/>

# ModelSync

[![NuGet](https://img.shields.io/nuget/v/UmbrellaFrame.ModelSync.Core.svg?style=flat-square)](https://www.nuget.org/packages/UmbrellaFrame.ModelSync.Core)
[![CI](https://github.com/umbrellaframe/modelsync/actions/workflows/ci.yml/badge.svg)](https://github.com/umbrellaframe/modelsync/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](LICENSE)
[![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-purple?style=flat-square)](https://learn.microsoft.com/en-us/dotnet/standard/net-standard)

**Language / Dil:** [English](#-english) - [Türkçe](#-turkce)

</div>

---

## 🇬🇧 English

**.NET için Attribute Tabanlı SQL Şema Üretici**  
Sıfır ORM bağımlılığı · 4 veritabanı sağlayıcı · Derleme zamanı güvenliği

### What is ModelSync?

ModelSync lets you decorate plain C# classes with attributes and **automatically generates and executes CREATE TABLE, ALTER TABLE, DROP TABLE, TRUNCATE TABLE and CREATE INDEX SQL** — no Entity Framework, no heavy ORM, no XML migration files.

```
UmbrellaFrame.ModelSync.Core          ← Attributes, interfaces, SQL builder
UmbrellaFrame.ModelSync.SqlServer     ← SQL Server / Azure SQL provider
UmbrellaFrame.ModelSync.MySql         ← MySQL / MariaDB provider
UmbrellaFrame.ModelSync.PostgreSQL    ← PostgreSQL provider
UmbrellaFrame.ModelSync.SQLite        ← SQLite provider
UmbrellaFrame.ModelSync.Analyzers     ← Roslyn compile-time checks
```

---

### 📦 Installation

Install only the provider you need:

| Provider | Package | Icon |
|---|---|:---:|
| SQL Server | `dotnet add package UmbrellaFrame.ModelSync.SqlServer` | <img src="assets/icons/modelsync-sqlserver.png" width="32"/> |
| MySQL | `dotnet add package UmbrellaFrame.ModelSync.MySql` | <img src="assets/icons/modelsync-mysql.png" width="32"/> |
| PostgreSQL | `dotnet add package UmbrellaFrame.ModelSync.PostgreSQL` | <img src="assets/icons/modelsync-postgresql.png" width="32"/> |
| SQLite | `dotnet add package UmbrellaFrame.ModelSync.SQLite` | <img src="assets/icons/modelsync-sqlite.png" width="32"/> |

> Each provider package automatically pulls `UmbrellaFrame.ModelSync.Core` as a dependency.

Optionally add the Roslyn analyzer for compile-time warnings:
```bash
dotnet add package UmbrellaFrame.ModelSync.Analyzers
```

---

### 🚀 Quick Start

#### 1 — Define your model

```csharp
using UmbrellaFrame.ModelSync.MySql.Attributes;
using UmbrellaFrame.ModelSync.Core.Attributes;

[MySqlTableName("products")]
public class Product
{
    [MySqlColumnType(MySqlType.INT)]
    [MySqlColumnPrimaryKey(isAutoIncrement: true)]
    public int Id { get; set; }

    [MySqlColumnType(MySqlType.VARCHAR, "255")]
    [MySqlColumnNotNull]
    public string Name { get; set; }

    [MySqlColumnType(MySqlType.DECIMAL, "10,2")]
    [DbColumnDefault("0.00")]
    [DbColumnCheck("Price >= 0")]
    public decimal Price { get; set; }

    [MySqlColumnType(MySqlType.DATETIME)]
    public DateTime CreatedAt { get; set; }
}
```

#### 2 — Create the generator and run DDL

```csharp
var generator = new MySqlTableGenerator(
    "Server=localhost;Database=mydb;User=root;Password=pass;"
);

// Creates the database if it does not exist
generator.CreateDatabase();

// Registers the model; generates SQL internally
generator.GenerateMySqlTable<Product>(ifNotExists: true);

// Executes CREATE TABLE on the live database (async)
await generator.CreateTablesAsync(cancellationToken);
```

Generated SQL (MySQL):
```sql
CREATE TABLE IF NOT EXISTS `products` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `Name` VARCHAR(255) NOT NULL,
  `Price` DECIMAL(10,2) DEFAULT 0.00 CHECK (Price >= 0),
  `CreatedAt` DATETIME,
  PRIMARY KEY (`Id`)
);
```

---

### 🔄 ALTER TABLE Operations

After your schema is live you can modify it without touching SQL:

```csharp
// Add a new column (column must exist as a property on the model)
generator.AddColumn<Product>("Stock");

// Rename an existing column
generator.RenameColumn<Product>("Name", "Title");

// Change the column type
generator.AlterColumnType<Product>("Price");

// Drop a column
generator.DropColumn<Product>("CreatedAt");

// All operations have async variants
await generator.AddColumnAsync<Product>("Stock", cancellationToken);
await generator.RenameColumnAsync<Product>("Name", "Title", cancellationToken);
await generator.AlterColumnTypeAsync<Product>("Price", cancellationToken);
await generator.DropColumnAsync<Product>("CreatedAt", cancellationToken);
```

> ⚠️ **SQLite limitation:** SQLite does not support `ALTER COLUMN TYPE`. Calling `AlterColumnType` on the SQLite provider throws `NotSupportedException` by design.

---

### 🗄️ Provider Examples

#### SQL Server

```csharp
[SqlServerTableName("Employees")]
public class Employee
{
    [SqlServerColumnType(SqlServerType.INT)]
    [SqlServerColumnPrimaryKey(isIdentity: true)]
    public int Id { get; set; }

    [SqlServerColumnType(SqlServerType.NVARCHAR, "200")]
    [SqlServerColumnNotNull]
    public string FullName { get; set; }

    [SqlServerColumnType(SqlServerType.DECIMAL, "18,2")]
    [DbColumnDefault("0")]
    public decimal Salary { get; set; }
}

var gen = new SqlServerTableGenerator(
    "Server=localhost;Database=HrDb;Trusted_Connection=True;"
);
gen.CreateDatabase();                         // CREATE DATABASE IF NOT EXISTS
gen.GenerateSqlServerTable<Employee>();
gen.CreateTables();                           // Idempotent — safe to call multiple times
```

#### PostgreSQL

```csharp
[PostgresTableName("orders")]
public class Order
{
    [PostgresColumnType(PostgresType.BIGINT)]
    [PostgresColumnPrimaryKey]
    public long Id { get; set; }

    [PostgresColumnType(PostgresType.UUID)]
    [PostgresColumnNotNull]
    public Guid CustomerId { get; set; }

    [PostgresColumnType(PostgresType.NUMERIC, "12,2")]
    public decimal Total { get; set; }
}

var gen = new PostgresTableGenerator(
    "Host=localhost;Database=shopdb;Username=postgres;Password=pass;"
);
gen.CreateDatabase();
gen.GenerateSqlTable<Order>(ifNotExists: true);
await gen.CreateTablesAsync();
```

#### SQLite

```csharp
[SQLiteTableName("logs")]
public class LogEntry
{
    [SQLiteColumnType(SQLiteType.INTEGER)]
    [SQLiteColumnPrimaryKey]
    public int Id { get; set; }

    [SQLiteColumnType(SQLiteType.TEXT)]
    public string Message { get; set; }

    [SQLiteColumnType(SQLiteType.REAL)]
    public double Timestamp { get; set; }
}

// In-memory database (useful for testing)
var gen = new SQLiteTableGenerator("Data Source=:memory:");
gen.GenerateSqlTable<LogEntry>(ifNotExists: true);
gen.CreateTables();
```

---

### 🧪 Test Scenarios

ModelSync ships with **116 tests** across 5 test projects covering both unit (SQL generation) and integration (live database) scenarios.

#### SQL Generation Tests (no database required)

```csharp
// Verify CREATE TABLE SQL contains expected clauses
var sql = generator.GenerateSqlTable<Product>(ifNotExists: true);
Assert.That(sql, Does.Contain("CREATE TABLE IF NOT EXISTS"));
Assert.That(sql, Does.Contain("`products`"));
Assert.That(sql, Does.Contain("DECIMAL(10,2)"));
Assert.That(sql, Does.Contain("DEFAULT 0.00"));
Assert.That(sql, Does.Contain("CHECK (Price >= 0)"));

// Verify ALTER TABLE SQL
var addSql = generator.BuildAddColumnSql<Product>("Stock");
Assert.That(addSql, Does.Contain("ALTER TABLE"));
Assert.That(addSql, Does.Contain("ADD COLUMN"));

// Verify unknown column throws
Assert.Catch<Exception>(() => generator.BuildAddColumnSql<Product>("NonExistent"));
```

#### Integration Tests (live database)

```csharp
[Test, Category("Integration")]
public void AlterTable_Integration_AddColumn()
{
    CreateFreshTable();   // drop + recreate for isolation
    Assert.DoesNotThrow(() => generator.AddColumn<Product>("Stock"));
}

[Test, Category("Integration")]
public void AlterTable_Integration_RenameColumn()
{
    CreateFreshTable();
    Assert.DoesNotThrow(() => generator.RenameColumn<Product>("Name", "Title"));
}

[Test, Category("Integration")]
public void AlterTable_Integration_AlterColumnType()
{
    CreateFreshTable();
    Assert.DoesNotThrow(() => generator.AlterColumnType<Product>("Price"));
}

[Test, Category("Integration")]
public void CreateDatabase_Integration_ShouldBeIdempotent()
{
    // Safe to call multiple times — no exception
    Assert.DoesNotThrow(() => generator.CreateDatabase());
    Assert.DoesNotThrow(() => generator.CreateDatabase());
}
```

#### SQLite In-Memory Tests

```csharp
// SQLite :memory: requires a persistent shared connection
// ModelSync handles this automatically with Mode=Memory;Cache=Shared
var gen = new InMemorySQLiteTableGenerator();
gen.CreateTables();

Assert.DoesNotThrow(() => gen.AddColumn<LogEntry>("Level"));
Assert.DoesNotThrow(() => gen.RenameColumn<LogEntry>("Message", "Body"));
Assert.DoesNotThrow(() => gen.DropColumn<LogEntry>("Timestamp"));

// AlterColumnType is NOT supported on SQLite
Assert.Throws<NotSupportedException>(
    () => gen.AlterColumnType<LogEntry>("Level")
);
```

---

### 🔌 Dependency Injection (ASP.NET Core)

```csharp
// Program.cs
builder.Services.AddSingleton<ITableGenerator>(sp =>
    new MySqlTableGenerator(
        builder.Configuration.GetConnectionString("MySQL")!,
        sp.GetService<ILogger<MySqlTableGenerator>>()
    ));

// Or register per provider
builder.Services.AddSingleton<MySqlTableGenerator>(sp =>
    new MySqlTableGenerator(connectionString));

// Inject and use in a hosted service or controller
public class SchemaInitializer : IHostedService
{
    private readonly ITableGenerator _gen;
    public SchemaInitializer(ITableGenerator gen) => _gen = gen;

    public async Task StartAsync(CancellationToken ct)
    {
        _gen.CreateDatabase();
        _gen.GenerateSqlTable<Product>(ifNotExists: true);
        await _gen.CreateTablesAsync(ct);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

---

### 📋 Supported Attributes

#### Provider-specific (replace `{Db}` with `MySql` / `SqlServer` / `Postgres` / `SQLite`)

| Attribute | Description |
|---|---|
| `[{Db}TableName("name")]` | Set table name |
| `[{Db}ColumnType(Type)]` | Set column data type |
| `[{Db}ColumnPrimaryKey]` | Mark as PRIMARY KEY |
| `[{Db}ColumnNotNull]` | Add NOT NULL constraint |
| `[{Db}ColumnUnique]` | Add UNIQUE constraint |
| `[{Db}ForeignKey("col","table","ref")]` | Add FOREIGN KEY |

#### Cross-provider (Core package)

| Attribute | Description |
|---|---|
| `[DbColumnDefault("expr")]` | Set DEFAULT value |
| `[DbColumnCheck("expr")]` | Add CHECK constraint |
| `[DbColumnIndex]` | Create an index via `GenerateIndexSql<T>()` |

---

### 🔤 Provider Identifier Quoting

| Provider | Quote Style | Example |
|---|---|---|
| MySQL / MariaDB | Backtick | `` `column` `` |
| SQL Server | Square bracket | `[column]` |
| PostgreSQL | Double quote | `"column"` |
| SQLite | Double quote | `"column"` |

---

### 🔍 Roslyn Analyzer

| Rule | Severity | Description |
|---|---|---|
| MSYNC001 | Warning | Public property is missing a column type attribute |
| MSYNC002 | Warning | Class has column attributes but no table name attribute |
| MSYNC003 | Warning | Model table has no primary key defined |

> Analyzer messages are displayed in the IDE language automatically (Turkish / English).

```ini
# .editorconfig — customize severity per rule
dotnet_diagnostic.MSYNC001.severity = error
dotnet_diagnostic.MSYNC003.severity = none
```

---

### 📊 Why ModelSync?

| Feature | ModelSync | EF Core | FluentMigrator | Dapper |
|---|:---:|:---:|:---:|:---:|
| Zero ORM dependency | ✅ | ❌ | ✅ | ✅ |
| Attribute-based schema | ✅ | ✅ | ❌ | ❌ |
| Async DDL execution | ✅ | ✅ | ❌ | — |
| DI ready | ✅ | ✅ | ✅ | ✅ |
| Roslyn Analyzer | ✅ | ❌ | ❌ | ❌ |
| Zero configuration | ✅ | ❌ | ❌ | ✅ |
| .NET Standard 2.0 | ✅ | ❌ | ✅ | ✅ |
| 4 DB providers | ✅ | ✅ | ✅ | ✅ |
| ALTER TABLE support | ✅ | ✅ | ✅ | ❌ |
| Auto database creation | ✅ | ❌ | ❌ | ❌ |

---

### 📄 License

MIT © UmbrellaFrame

---

## 🇹🇷 Turkce

**.NET için Attribute Tabanlı SQL Şema Üretici**  
Sıfır ORM bağımlılığı · 4 veritabanı sağlayıcı · Derleme zamanı güvenliği

### ModelSync Nedir?

ModelSync, sıradan C# sınıflarını attribute'larla işaretlemenize ve **CREATE TABLE, ALTER TABLE, DROP TABLE, TRUNCATE TABLE ve CREATE INDEX SQL'i otomatik olarak üretip çalıştırmanıza** olanak tanır — Entity Framework yok, ağır ORM yok, XML migration dosyası yok.

```
UmbrellaFrame.ModelSync.Core          ← Attribute'lar, arayüzler, SQL üretici
UmbrellaFrame.ModelSync.SqlServer     ← SQL Server / Azure SQL sağlayıcı
UmbrellaFrame.ModelSync.MySql         ← MySQL / MariaDB sağlayıcı
UmbrellaFrame.ModelSync.PostgreSQL    ← PostgreSQL sağlayıcı
UmbrellaFrame.ModelSync.SQLite        ← SQLite sağlayıcı
UmbrellaFrame.ModelSync.Analyzers     ← Roslyn derleme zamanı kontrolleri
```

---

### 📦 Kurulum

Sadece ihtiyaç duyduğunuz sağlayıcıyı yükleyin:

| Sağlayıcı | Paket |
|---|---|
| SQL Server | `dotnet add package UmbrellaFrame.ModelSync.SqlServer` |
| MySQL | `dotnet add package UmbrellaFrame.ModelSync.MySql` |
| PostgreSQL | `dotnet add package UmbrellaFrame.ModelSync.PostgreSQL` |
| SQLite | `dotnet add package UmbrellaFrame.ModelSync.SQLite` |

> Her sağlayıcı paketi, `UmbrellaFrame.ModelSync.Core`'u otomatik olarak bağımlılık olarak indirir.

---

### 🚀 Hızlı Başlangıç

#### 1 — Modelinizi tanımlayın

```csharp
[MySqlTableName("urunler")]
public class Urun
{
    [MySqlColumnType(MySqlType.INT)]
    [MySqlColumnPrimaryKey(isAutoIncrement: true)]
    public int Id { get; set; }

    [MySqlColumnType(MySqlType.VARCHAR, "255")]
    [MySqlColumnNotNull]
    public string Ad { get; set; }

    [MySqlColumnType(MySqlType.DECIMAL, "10,2")]
    [DbColumnDefault("0.00")]
    [DbColumnCheck("Fiyat >= 0")]
    public decimal Fiyat { get; set; }
}
```

#### 2 — Generator oluşturun ve DDL çalıştırın

```csharp
var generator = new MySqlTableGenerator(
    "Server=localhost;Database=mydb;User=root;Password=pass;"
);

generator.CreateDatabase();                   // Veritabanı yoksa oluşturur
generator.GenerateMySqlTable<Urun>(ifNotExists: true);
await generator.CreateTablesAsync();          // Canlı veritabanında çalıştırır
```

---

### 🔄 ALTER TABLE İşlemleri

```csharp
// Yeni sütun ekle
generator.AddColumn<Urun>("Stok");

// Sütun yeniden adlandır
generator.RenameColumn<Urun>("Ad", "Baslik");

// Sütun tipi değiştir
generator.AlterColumnType<Urun>("Fiyat");

// Sütun sil
generator.DropColumn<Urun>("OlusturulmaTarihi");

// Tüm işlemlerin async versiyonları mevcuttur
await generator.AddColumnAsync<Urun>("Stok");
```

> ⚠️ **SQLite kısıtlaması:** SQLite `ALTER COLUMN TYPE` desteklemez. SQLite sağlayıcısında `AlterColumnType` çağrısı tasarım gereği `NotSupportedException` fırlatır.

---

### 🧪 Test Senaryoları

ModelSync, 5 test projesi ve **116 test** ile hem SQL üretimi (birim) hem de canlı veritabanı (entegrasyon) senaryolarını kapsar.

#### SQL Üretimi Testleri (veritabanı gerekmez)

```csharp
var sql = generator.GenerateSqlTable<Urun>(ifNotExists: true);
Assert.That(sql, Does.Contain("CREATE TABLE IF NOT EXISTS"));
Assert.That(sql, Does.Contain("`urunler`"));
Assert.That(sql, Does.Contain("DECIMAL(10,2)"));
Assert.That(sql, Does.Contain("DEFAULT 0.00"));

// Bilinmeyen sütun exception fırlatmalı
Assert.Catch<Exception>(() => generator.BuildAddColumnSql<Urun>("Olmayan"));
```

#### Entegrasyon Testleri (canlı veritabanı)

```csharp
[Test, Category("Integration")]
public void AlterTable_Entegrasyon_SutunEkle()
{
    TabloyuSifirla();   // Test izolasyonu için drop + recreate
    Assert.DoesNotThrow(() => generator.AddColumn<Urun>("Stok"));
}

[Test, Category("Integration")]
public void CreateDatabase_Idempotent_OlmaliDir()
{
    // Birden fazla kez çağrılabilir — exception fırlatmaz
    Assert.DoesNotThrow(() => generator.CreateDatabase());
    Assert.DoesNotThrow(() => generator.CreateDatabase());
}
```

---

### 🔌 Dependency Injection (ASP.NET Core)

```csharp
// Program.cs
builder.Services.AddSingleton<ITableGenerator>(sp =>
    new MySqlTableGenerator(
        builder.Configuration.GetConnectionString("MySQL")!,
        sp.GetService<ILogger<MySqlTableGenerator>>()
    ));

// HostedService ile otomatik şema yönetimi
public class SemaBaslatici : IHostedService
{
    private readonly ITableGenerator _gen;
    public SemaBaslatici(ITableGenerator gen) => _gen = gen;

    public async Task StartAsync(CancellationToken ct)
    {
        _gen.CreateDatabase();                            // DB yoksa oluştur
        _gen.GenerateSqlTable<Urun>(ifNotExists: true);  // Modeli kaydet
        await _gen.CreateTablesAsync(ct);                 // Tabloları oluştur
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

---

### 📋 Desteklenen Attribute'lar

#### Sağlayıcıya özel (`{Db}` yerine `MySql` / `SqlServer` / `Postgres` / `SQLite` yazın)

| Attribute | Açıklama |
|---|---|
| `[{Db}TableName("isim")]` | Tablo adını belirler |
| `[{Db}ColumnType(Tip)]` | Sütun veri tipini belirler |
| `[{Db}ColumnPrimaryKey]` | PRIMARY KEY olarak işaretler |
| `[{Db}ColumnNotNull]` | NOT NULL kısıtlaması ekler |
| `[{Db}ColumnUnique]` | UNIQUE kısıtlaması ekler |
| `[{Db}ForeignKey("sut","tablo","ref")]` | FOREIGN KEY ekler |

#### Tüm sağlayıcılarda çalışan (Core paketi)

| Attribute | Açıklama |
|---|---|
| `[DbColumnDefault("ifade")]` | DEFAULT değer belirler |
| `[DbColumnCheck("ifade")]` | CHECK kısıtlaması ekler |
| `[DbColumnIndex]` | `GenerateIndexSql<T>()` ile indeks oluşturur |

---

### 🔍 Roslyn Analyzer

| Kural | Önem | Açıklama |
|---|---|---|
| MSYNC001 | Uyarı | Public property'de kolon tipi attribute eksik |
| MSYNC002 | Uyarı | Sınıfta kolon attribute'u var ama tablo adı attribute'u eksik |
| MSYNC003 | Uyarı | Model tabloda primary key tanımlanmamış |

```ini
# .editorconfig
dotnet_diagnostic.MSYNC001.severity = error
dotnet_diagnostic.MSYNC003.severity = none
```

---

### 📄 Lisans

MIT © UmbrellaFrame

**Attribute-based SQL schema generator for .NET**
Zero ORM dependency - 4 database providers - Compile-time safety

### What is ModelSync?

ModelSync lets you decorate plain C# classes with attributes and **automatically generates CREATE TABLE, DROP TABLE, TRUNCATE TABLE, and CREATE INDEX SQL** - no Entity Framework, no heavy ORM, no XML migration files.

### Installation

Install only the provider you need:

```bash
dotnet add package UmbrellaFrame.ModelSync.MySql
dotnet add package UmbrellaFrame.ModelSync.SqlServer
dotnet add package UmbrellaFrame.ModelSync.PostgreSQL
dotnet add package UmbrellaFrame.ModelSync.SQLite
dotnet add package UmbrellaFrame.ModelSync.Analyzers
```

> Each provider package automatically pulls UmbrellaFrame.ModelSync.Core as a dependency.

### Quick Start

```csharp
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

var generator = new MySqlTableGenerator("Server=localhost;Database=mydb;User=root;Password=pass;");
generator.GenerateMySqlTable<Product>(ifNotExists: true);
await generator.CreateTablesAsync(cancellationToken);
```

### Why ModelSync?

| Feature | ModelSync | EF Core | FluentMigrator | Dapper |
|---|:---:|:---:|:---:|:---:|
| Zero ORM dependency | Yes | No | Yes | Yes |
| Attribute-based schema | Yes | Yes | No | No |
| Async DDL execution | Yes | Yes | No | - |
| DI ready | Yes | Yes | Yes | Yes |
| Roslyn Analyzer | Yes | No | No | No |
| Zero configuration | Yes | No | No | Yes |
| .NET Standard 2.0 | Yes | No | Yes | Yes |
| 4 DB providers | Yes | Yes | Yes | Yes |

### Supported Attributes

Provider-specific (replace {Db} with MySql / SqlServer / Postgres / SQLite):

| Attribute | Description |
|---|---|
| [{Db}TableName("name")] | Set table name |
| [{Db}ColumnType(Type)] | Set column data type |
| [{Db}ColumnPrimaryKey] | Mark as PRIMARY KEY |
| [{Db}ColumnNotNull] | Add NOT NULL constraint |
| [{Db}ColumnUnique] | Add UNIQUE constraint |
| [{Db}ForeignKey("col","table","ref")] | Add FOREIGN KEY |

Cross-provider (Core package):

| Attribute | Description |
|---|---|
| [DbColumnDefault("expr")] | Set DEFAULT value |
| [DbColumnCheck("expr")] | Add CHECK constraint |
| [DbColumnIndex] | Create an index via GenerateIndexSql<T>() |

### Provider Identifier Quoting

| Provider | Quote Style | Example |
|---|---|---|
| MySQL / MariaDB | Backtick | `column` |
| SQL Server | Square bracket | [column] |
| PostgreSQL | Double quote | "column" |
| SQLite | Double quote | "column" |

### Roslyn Analyzer

| Rule | Severity | Description |
|---|---|---|
| MSYNC001 | Warning | Public property is missing a column type attribute |
| MSYNC002 | Warning | Class has column attributes but no table name attribute |
| MSYNC003 | Warning | Model table has no primary key defined |

> Analyzer messages are displayed in the IDE language automatically (Turkish / English).

```ini
# .editorconfig
dotnet_diagnostic.MSYNC001.severity = error
dotnet_diagnostic.MSYNC003.severity = none
```

### Documentation

| | |
|---|---|
| [Overview](docs/01-overview.md) | Architecture, design decisions |
| [Quick Start](docs/02-quickstart.md) | Full working examples per provider |
| [Attribute Reference](docs/03-attributes.md) | Every attribute with parameter tables |
| [Provider Guides](docs/04-providers.md) | MySQL, SQL Server, PostgreSQL, SQLite specifics |
| [API Reference](docs/05-api-reference.md) | ITableGenerator full API |
| [Dependency Injection](docs/06-dependency-injection.md) | ASP.NET Core DI, HostedService |
| [Roslyn Analyzers](docs/07-analyzers.md) | MSYNC001/002/003 rules and editorconfig |
| [Architecture](docs/08-architecture.md) | Layer diagram, SQL generation flow |
| [Contributing](docs/09-contributing.md) | Dev setup, PR checklist |
| [Changelog](docs/10-changelog.md) | Version history |

### Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

### Security

See [SECURITY.md](SECURITY.md).

### License

MIT (c) UmbrellaFrame

---

## Turkce

**.NET icin Attribute Tabanli SQL Sema Uretici**
Sifir ORM bagimlilik - 4 veritabani saglayici - Derleme zamani guvenligi

### ModelSync Nedir?

ModelSync, siradan C# siniflarini attribute'larla isaretlemenize ve **CREATE TABLE, DROP TABLE, TRUNCATE TABLE ve CREATE INDEX SQL'i otomatik olarak uretmenize** olanak tanir - Entity Framework yok, agir ORM yok, XML migration dosyasi yok.

### Kurulum

Sadece ihtiyac duydugunuz saglayiciyi yukleyin:

```bash
dotnet add package UmbrellaFrame.ModelSync.MySql
dotnet add package UmbrellaFrame.ModelSync.SqlServer
dotnet add package UmbrellaFrame.ModelSync.PostgreSQL
dotnet add package UmbrellaFrame.ModelSync.SQLite
dotnet add package UmbrellaFrame.ModelSync.Analyzers
```

> Her saglayici paketi, UmbrellaFrame.ModelSync.Core'u otomatik olarak bagimlilik olarak indirir.

### Hizli Baslangic

```csharp
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

var generator = new MySqlTableGenerator("Server=localhost;Database=mydb;User=root;Password=pass;");
generator.GenerateMySqlTable<Urun>(ifNotExists: true);
await generator.CreateTablesAsync(cancellationToken);
```

### Neden ModelSync?

| Ozellik | ModelSync | EF Core | FluentMigrator | Dapper |
|---|:---:|:---:|:---:|:---:|
| Sifir ORM bagimlilik | Evet | Hayir | Evet | Evet |
| Attribute tabanli sema | Evet | Evet | Hayir | Hayir |
| Async DDL calistirma | Evet | Evet | Hayir | - |
| DI destegi | Evet | Evet | Evet | Evet |
| Roslyn Analyzer | Evet | Hayir | Hayir | Hayir |
| Sifir konfigurasyon | Evet | Hayir | Hayir | Evet |
| .NET Standard 2.0 | Evet | Hayir | Evet | Evet |
| 4 veritabani saglayici | Evet | Evet | Evet | Evet |

### Desteklenen Attribute'lar

Saglayiciya ozel ({Db} yerine MySql / SqlServer / Postgres / SQLite yazin):

| Attribute | Aciklama |
|---|---|
| [{Db}TableName("isim")] | Tablo adini belirler |
| [{Db}ColumnType(Tip)] | Sutun veri tipini belirler |
| [{Db}ColumnPrimaryKey] | PRIMARY KEY olarak isaretler |
| [{Db}ColumnNotNull] | NOT NULL kisitlamasi ekler |
| [{Db}ColumnUnique] | UNIQUE kisitlamasi ekler |
| [{Db}ForeignKey("sut","tablo","ref")] | FOREIGN KEY ekler |

Tum saglayicilarda calisan (Core paketi):

| Attribute | Aciklama |
|---|---|
| [DbColumnDefault("ifade")] | DEFAULT deger belirler |
| [DbColumnCheck("ifade")] | CHECK kisitlamasi ekler |
| [DbColumnIndex] | GenerateIndexSql<T>() ile indeks olusturur |

### Saglayici Tanimlayici Alintilama

| Saglayici | Alinti Stili | Ornek |
|---|---|---|
| MySQL / MariaDB | Backtick | `sutun` |
| SQL Server | Koseli parantez | [sutun] |
| PostgreSQL | Cift tirnak | "sutun" |
| SQLite | Cift tirnak | "sutun" |

### Roslyn Analyzer

| Kural | Onem Derecesi | Aciklama |
|---|---|---|
| MSYNC001 | Uyari | Public property'de kolon tipi attribute eksik |
| MSYNC002 | Uyari | Sinifta kolon attribute'u var ama tablo adi attribute'u eksik |
| MSYNC003 | Uyari | Model tabloda primary key tanimlanmamis |

> Analyzer mesajlari IDE diline gore otomatik gosterilir (Turkce / Ingilizce).

```ini
# .editorconfig
dotnet_diagnostic.MSYNC001.severity = error
dotnet_diagnostic.MSYNC003.severity = none
```

### Dependency Injection

```csharp
builder.Services.AddSingleton<ITableGenerator>(sp =>
    new MySqlTableGenerator(
        builder.Configuration.GetConnectionString("MySQL"),
        sp.GetService<ILogger<MySqlTableGenerator>>()
    ));
```

### Dokumantasyon

| | |
|---|---|
| [Genel Bakis](docs/01-overview.md) | Mimari, tasarim kararlari |
| [Hizli Baslangic](docs/02-quickstart.md) | Saglayiciya gore tam calisan ornekler |
| [Attribute Referansi](docs/03-attributes.md) | Her attribute ve parametre tablolari |
| [Saglayici Kilavuzlari](docs/04-providers.md) | MySQL, SQL Server, PostgreSQL, SQLite detaylari |
| [API Referansi](docs/05-api-reference.md) | ITableGenerator tam API |
| [Dependency Injection](docs/06-dependency-injection.md) | ASP.NET Core DI, HostedService |
| [Roslyn Analyzer'lar](docs/07-analyzers.md) | MSYNC001/002/003 kurallari ve editorconfig |
| [Mimari](docs/08-architecture.md) | Katman diyagrami, SQL uretim akisi |
| [Katkida Bulunma](docs/09-contributing.md) | Gelistirici kurulumu, PR kontrol listesi |
| [Degisiklik Gunlugu](docs/10-changelog.md) | Surum gecmisi |

### Katkida Bulunma

Bkz. [CONTRIBUTING.md](CONTRIBUTING.md).

### Guvenlik

Bkz. [SECURITY.md](SECURITY.md).

### Lisans

MIT (c) UmbrellaFrame
