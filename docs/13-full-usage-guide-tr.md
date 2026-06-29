# ModelSync — NuGet Tam Kullaným Kýlavuzu

Kurulum, model tanýmlama, SQL üretimi, DDL yürütme, migration, stored procedure, canlý model senkronizasyonu ve production kullanýmý.

**Sürüm kapsamý:** 1.1.0
**Hazýrlayan:** UmbrellaFrame / ModelSync

# Ýçindekiler

1. [Belge hakkýnda](#belge-hakkýnda)
2. [ModelSync nedir?](#1-modelsync-nedir)
3. [Kurulum](#kurulum)
4. [Temel çalýþma modeli](#temel-çalýþma-modeli)
5. [Ýlk tam örnek — MySQL/MariaDB](#ilk-tam-örnek--mysqlmariadb)
6. [Provider bazýnda hýzlý baþlangýç](#provider-bazýnda-hýzlý-baþlangýç)
7. [Attribute sistemi](#attribute-sistemi)
8. [Provider kolon tipleri](#provider-kolon-tipleri)
9. [SQL üretme API’si](#sql-üretme-apisi)
10. [Tablo ve kolon operasyonlarý](#tablo-ve-kolon-operasyonlarý)
11. [Dependency Injection ve uygulama baþlangýcý](#dependency-injection-ve-uygulama-baþlangýcý)
12. [Logging](#logging)
13. [Migration Runner](#migration-runner)
14. [Stored Procedure senkronizasyonu](#stored-procedure-senkronizasyonu)
15. [Canlý model senkronizasyonu](#canlý-model-senkronizasyonu)
16. [Analyzer](#analyzer)
17. [Hata yönetimi ve troubleshooting](#hata-yönetimi-ve-troubleshooting)
18. [Test yaklaþýmý](#test-yaklaþýmý)
19. [Production kullaným rehberi](#production-kullaným-rehberi)
20. [Tam örnek proje yapýsý](#tam-örnek-proje-yapýsý)
21. [API hýzlý referans](#api-hýzlý-referans)
22. [Sürüm 1.1.0 sýnýrlarý](#sürüm-108-sýnýrlarý)
23. [Sýk sorulan sorular](#sýk-sorulan-sorular)
24. [Sonuç](#sonuç)

# Belge hakkýnda

Bu kýlavuz, **ModelSync 1.1.0** paketlerini NuGet üzerinden yükleyen bir .NET geliþtiricisinin projeyi kaynak koda bakmadan doðru biçimde kullanabilmesi için hazýrlanmýþtýr. Kurulumdan baþlayarak model tanýmlama, SQL üretme, tablo oluþturma, indeks yürütme, kolon iþlemleri, migration scriptleri, stored procedure senkronizasyonu, dependency injection, logging, analyzer, test ve production güvenliði ele alýnýr.

> **En önemli taným:** ModelSync bir ORM deðildir. Nesneleri satýrlara kaydetmez, LINQ sorgusu üretmez, change tracking yapmaz ve CRUD repository saðlamaz. ModelSync’in iþi; C# model metadata’sýndan DDL üretmek, DDL’i isteðe baðlý çalýþtýrmak ve proje tarafýndaki SQL scriptlerini kontrollü biçimde yönetmektir.

## 1. ModelSync nedir?

ModelSync, düz C# sýnýflarýný provider’a özel attribute’larla iþaretleyerek SQL þema ifadeleri üretmenizi saðlayan, ORM baðýmlýlýðý olmayan bir .NET kütüphanesidir.

Baþlýca kullaným alanlarý:

- C# modelinden `CREATE TABLE` SQL’i üretmek.
- Üretilen tablo SQL’lerini veritabanýnda çalýþtýrmak.
- `DROP TABLE`, `TRUNCATE TABLE` ve `CREATE INDEX` SQL’leri üretmek.
- Attribute metadata’sýna göre kolon eklemek, silmek, yeniden adlandýrmak veya tip deðiþtirmek.
- Veri kaybýna yol açabilecek iþlemleri açýk onay olmadan engellemek.
- SQL dosyasý tabanlý migration scriptlerini kategorilere göre sýralayýp uygulamak.
- SQL Server, MySQL/MariaDB ve PostgreSQL stored procedure dosyalarýný canlý veritabanýyla karþýlaþtýrmak ve senkronize etmek.
- Roslyn analyzer ile eksik ModelSync attribute’larýný derleme zamanýnda bildirmek.

## 2. ModelSync ne yapmaz?

| Beklenti | ModelSync davranýþý |
|---|---|
| `Insert`, `Update`, `Delete`, `Select` iþlemleri | Saðlamaz. Dapper, ADO.NET, EF Core veya baþka bir veri eriþim aracý kullanýlýr. |
| LINQ sorgu saðlayýcýsý | Saðlamaz. |
| Entity change tracking | Saðlamaz. |
| Model deðiþince canlý veritabanýný sessiz ve yýkýcý þekilde deðiþtirme | Yoktur. Model synchronizer dry-run-first çalýþýr ve yalnýz güvenli additive iþlemleri otomatik uygular. |
| Uygulanmýþ migration’ýn her türlü þema farkýný güvenle düzeltmesi | Saðlamaz. Otomatik onarým yalnýz basit, eksik kolon ekleme yaklaþýmýdýr. |
| Ýndeksleri `CreateTables()` ile otomatik oluþturma | Yapmaz. `GenerateIndexSql<T>()` yalnýz SQL döndürür; SQL ayrýca yürütülmelidir. |
| SQLite stored procedure | SQLite stored procedure desteklemediði için saðlanmaz. |
| Ýliþkisel model navigasyonlarý | Saðlamaz. Foreign key SQL’i attribute ile açýk tanýmlanýr. |

## 3. Paket mimarisi ve hangi paket neden vardýr?

| NuGet paketi | Amaç | Doðrudan kurulmalý mý? |
|---|---|---|
| `UmbrellaFrame.ModelSync.Core` | Ortak attribute’lar, arayüzler, SQL builder altyapýsý, migration/stored procedure modelleri | Provider paketi otomatik getirir. Yalnýz provider geliþtirecekseniz doðrudan kurun. |
| `UmbrellaFrame.ModelSync.SqlServer` | SQL Server ve Azure SQL DDL/migration/stored procedure uygulamasý | SQL Server kullanýyorsanýz evet. |
| `UmbrellaFrame.ModelSync.MySql` | MySQL ve MariaDB uygulamasý | MySQL/MariaDB kullanýyorsanýz evet. |
| `UmbrellaFrame.ModelSync.PostgreSQL` | PostgreSQL uygulamasý | PostgreSQL kullanýyorsanýz evet. |
| `UmbrellaFrame.ModelSync.SQLite` | SQLite uygulamasý | SQLite kullanýyorsanýz evet. |
| `UmbrellaFrame.ModelSync.Analyzers` | Model attribute hatalarýný IDE ve build sýrasýnda bulur | Ýsteðe baðlý, tavsiye edilir. |

Paketler `netstandard2.0` hedefler. Bu nedenle modern .NET uygulamalarýnda kullanýlabilir. Bu kýlavuzdaki örnekler modern SDK stili projeler ve async kullaným üzerinden verilmiþtir.

# Kurulum

## 4. Yeni proje oluþturma

```bash
dotnet new console -n ModelSyncDemo
cd ModelSyncDemo
```

ASP.NET Core kullanýyorsanýz:

```bash
dotnet new webapi -n ModelSyncDemo
cd ModelSyncDemo
```

## 5. Provider paketini yükleme

Yalnýz kullandýðýnýz provider’ý yükleyin.

### SQL Server / Azure SQL

```bash
dotnet add package UmbrellaFrame.ModelSync.SqlServer --version 1.1.0
```

### MySQL / MariaDB

```bash
dotnet add package UmbrellaFrame.ModelSync.MySql --version 1.1.0
```

### PostgreSQL

```bash
dotnet add package UmbrellaFrame.ModelSync.PostgreSQL --version 1.1.0
```

### SQLite

```bash
dotnet add package UmbrellaFrame.ModelSync.SQLite --version 1.1.0
```

### Analyzer

```bash
dotnet add package UmbrellaFrame.ModelSync.Analyzers --version 1.1.0
```

`--version` kaldýrýlýrsa NuGet’teki mevcut kararlý sürüm yüklenir. Bu belge 1.1.0 API’sine göre hazýrlanmýþtýr.

## 6. Namespace’ler

Ortak attribute ve modeller:

```csharp
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.Interfaces;
```

Provider namespace’leri:

```csharp
using UmbrellaFrame.ModelSync.SqlServer;
using UmbrellaFrame.ModelSync.MySql;
using UmbrellaFrame.ModelSync.PostgreSQL;
using UmbrellaFrame.ModelSync.SQLite;
```

# Temel çalýþma modeli

## 7. ModelSync akýþý neden iki aþamalýdýr?

ModelSync tablo iþlemlerini iki aþamaya ayýrýr:

1. `Generate...Table<T>()` modeli okur, SQL üretir ve generator örneðinin iç önbelleðine kaydeder.
2. `CreateTables()` veya `CreateTablesAsync()` önbellekteki SQL’leri veritabanýnda çalýþtýrýr.

Bu ayrým þu yararlarý saðlar:

- SQL’i çalýþtýrmadan önce görebilirsiniz.
- Review, log veya test yapabilirsiniz.
- Birden fazla tabloyu kaydedip sonra toplu çalýþtýrabilirsiniz.
- SQL üretimi ile canlý veritabaný deðiþikliðini birbirinden ayýrabilirsiniz.

```csharp
var generator = new MySqlTableGenerator(connectionString);

var sql = generator.GenerateMySqlTable<Product>(ifNotExists: true);
Console.WriteLine(sql);          // yalnýz üretir ve cache'e alýr

await generator.CreateTablesAsync(); // cache'teki SQL'i çalýþtýrýr
```

> Yeni bir generator örneði oluþturursanýz önceki örneðin cache’i taþýnmaz. `CreateTablesAsync()` çaðrýsý ayný generator örneðinde yapýlmalýdýr.

# Ýlk tam örnek — MySQL/MariaDB

## 8. Model tanýmlama

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

## 9. Generator oluþturma ve database hazýrlama

```csharp
var connectionString =
    "Server=localhost;Port=3306;Database=shopdb;User ID=root;Password=secret;";

var generator = new MySqlTableGenerator(connectionString);

// Kullanýcýnýn CREATE DATABASE yetkisi varsa çalýþtýrýn.
await generator.CreateDatabaseAsync();
```

`CreateDatabaseAsync()` connection string içindeki `Database` deðerini alýr, database’siz baðlantý açar ve `CREATE DATABASE IF NOT EXISTS` çalýþtýrýr. Veritabaný baþka bir süreç tarafýndan oluþturuluyorsa bu adýmý atlayabilirsiniz.

## 10. SQL üretme, inceleme ve tablo oluþturma

```csharp
var createSql = generator.GenerateMySqlTable<Product>(ifNotExists: true);
Console.WriteLine(createSql);

await generator.CreateTablesAsync();
```

Beklenen yapýya benzer SQL:

```sql
CREATE TABLE IF NOT EXISTS `products` (
    `Id` INT PRIMARY KEY AUTO_INCREMENT,
    `Name` VARCHAR(200) NOT NULL,
    `Price` DECIMAL(18,2) DEFAULT 0.00 CHECK (Price >= 0),
    `Stock` INT DEFAULT 0,
    `CreatedAt` DATETIME DEFAULT CURRENT_TIMESTAMP
);
```

## 11. Ýndeks SQL’lerini üretme ve çalýþtýrma

`DbColumnIndex` tablo SQL’inin içine eklenmez. Ýndeksler ayrý SQL listesi olarak üretilir:

```csharp
var indexSqlList = generator.GenerateIndexSql<Product>();

foreach (var indexSql in indexSqlList)
{
    Console.WriteLine(indexSql);
}
```

MySQL’de çalýþtýrma örneði:

```csharp
using MySqlConnector;

await using var connection = new MySqlConnection(connectionString);
await connection.OpenAsync();

foreach (var indexSql in generator.GenerateIndexSql<Product>())
{
    await using var command = new MySqlCommand(indexSql, connection);
    await command.ExecuteNonQueryAsync();
}
```

> Ayný indeks ikinci kez oluþturulursa provider hata verebilir. Ýndeks yürütmesini migration scriptine almak veya veritabaný kataloðundan varlýk kontrolü yapmak production için daha güvenlidir.

# Provider bazýnda hýzlý baþlangýç

## 12. SQL Server / Azure SQL

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

    [SqlServerColumnType(SqlServerColumnType.DATETIME2)]
    [DbColumnDefault("SYSUTCDATETIME()")]
    public DateTime CreatedAt { get; set; }
}

var connectionString =
    "Server=localhost;Database=ShopDb;Integrated Security=True;TrustServerCertificate=True;";

var generator = new SqlServerTableGenerator(connectionString);

// SQL Server provider CreateTablesAsync içinde CreateDatabaseAsync de çaðýrýr.
var sql = generator.GenerateSqlServerTable<Product>(ifNotExists: true);
Console.WriteLine(sql);
await generator.CreateTablesAsync();
```

SQL Server inline `CREATE TABLE IF NOT EXISTS` desteklemediði için provider `OBJECT_ID` guard bloðu üretir.

### SQL Server kullaným notu

`ifNotExists: true` kullanýrken doðrudan provider metodunu tercih edin:

```csharp
generator.GenerateSqlServerTable<Product>(ifNotExists: true);
```

Generic ve async üretim çaðrýlarý da SQL Server provider override davranýþýný kullanýr; yine de okunabilirlik için provider-specific metodu tercih edebilirsiniz:

```csharp
generator.GenerateSqlServerTable<Product>(true);
await generator.CreateTablesAsync(cancellationToken);
```

## 13. PostgreSQL

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

    [PostgresColumnType(PostgresColumnType.TIMESTAMPTZ)]
    [DbColumnDefault("CURRENT_TIMESTAMP")]
    public DateTime CreatedAt { get; set; }
}

var connectionString =
    "Host=localhost;Port=5432;Database=shopdb;Username=postgres;Password=secret;";

var generator = new PostgresTableGenerator(connectionString);

// PostgreSQL provider CreateTablesAsync database oluþturmayý otomatik çaðýrmaz.
await generator.CreateDatabaseAsync();
generator.GeneratePostgresTable<Product>(ifNotExists: true);
await generator.CreateTablesAsync();
```

PostgreSQL kimlik/otomatik sayý için `SERIAL` veya `BIGSERIAL` kolon tipi kullanýlýr. `PostgresColumnPrimaryKey` ayrýca `PRIMARY KEY` üretir.

## 14. SQLite

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

    [SQLiteColumnType(SQLiteColumnType.NUMERIC)]
    [DbColumnDefault("0")]
    public decimal Price { get; set; }
}

var connectionString = "Data Source=shop.db";
var generator = new SQLiteTableGenerator(connectionString);

generator.GenerateSQLiteTable<Product>(ifNotExists: true);
await generator.CreateTablesAsync();
```

SQLite database dosyasýný ilk baðlantýda oluþturur. `CreateDatabase()` ve `CreateDatabaseAsync()` no-op’tur.

### SQLite bellek içi test

`Data Source=:memory:` database’i baðlantý kapanýnca yok olur. ModelSync her yürütmede kendi baðlantýsýný açýp kapattýðý için daha sonra ayný veritabanýna eriþmeniz gerekiyorsa named shared memory ve açýk tutulan bir keeper connection kullanýn:

```csharp
using Microsoft.Data.Sqlite;
using UmbrellaFrame.ModelSync.SQLite;

var cs = "Data Source=ModelSyncTests;Mode=Memory;Cache=Shared";

await using var keeper = new SqliteConnection(cs);
await keeper.OpenAsync();

var generator = new SQLiteTableGenerator(cs);
generator.GenerateSQLiteTable<Product>(ifNotExists: true);
await generator.CreateTablesAsync();

// keeper açýk kaldýðý sürece baþka baðlantýlar ayný in-memory database'i görür.
```

### SQLite sýnýrlamalarý

- Stored procedure desteklenmez.
- `ALTER COLUMN TYPE` doðrudan desteklenmez; provider `NotSupportedException` fýrlatýr.
- Tip deðiþikliði için create-copy-drop/rename stratejisi gerekir.
- `GenerateTruncateTableSql<T>()` SQLite provider’da `DELETE FROM "Table";` üretir; çünkü SQLite `TRUNCATE TABLE` komutunu desteklemez.

# Attribute sistemi

## 15. Tablo adý attribute’larý

| Provider | Kullaným |
|---|---|
| SQL Server | `[SqlServerTableName("Products")]` |
| MySQL/MariaDB | `[MySqlTableName("products")]` |
| PostgreSQL | `[PostgresTableName("products")]` |
| SQLite | `[SQLiteTableName("products")]` |

Tablo adý verilmezse class adý kullanýlýr. Buna raðmen açýk tablo adý kullanmak tavsiye edilir; refactor sýrasýnda database adý istemeden deðiþmez.

## 16. Kolon tipi attribute’larý

Her public property’nin provider’a uygun kolon tipi attribute’ü olmalýdýr.

```csharp
[MySqlColumnType(MySqlColumnType.VARCHAR, "255")]
public string Email { get; set; } = string.Empty;
```

Varsayilan kolon adi property adidir. ModelSync 1.1.0 ile DbColumnName database kolon adini degistirebilir, DbIgnore ise public yardimci propertyleri schema discovery disina cikarabilir.

## 17. Primary key

```csharp
[MySqlColumnType(MySqlColumnType.INT)]
[MySqlColumnPrimaryKey(isAutoIncrement: true)]
public int Id { get; set; }
```

Provider karþýlýklarý:

| Provider | Attribute | Auto increment yaklaþýmý |
|---|---|---|
| SQL Server | `SqlServerColumnPrimaryKey(isAutoIncrement: true)` | `IDENTITY(1,1)` |
| MySQL | `MySqlColumnPrimaryKey(isAutoIncrement: true)` | `AUTO_INCREMENT` |
| PostgreSQL | `PostgresColumnPrimaryKey` | Otomatik artýþ için kolon tipi `SERIAL`/`BIGSERIAL` seçilir. |
| SQLite | `SQLiteColumnPrimaryKey` | 1.1.0 `PRIMARY KEY AUTOINCREMENT` üretir; yalnýz `INTEGER` kolonla kullanýn. |

## 18. Composite primary key

Birden fazla property primary key attribute’ü taþýyorsa generator table-level composite key üretir:

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

Üretilen yapý:

```sql
PRIMARY KEY (`UserId`, `RoleId`)
```

Composite key property’lerinde auto increment kullanmayýn. Table-level composite key üretiminde property-level auto increment snippet’i kullanýlmaz.

## 19. NOT NULL

```csharp
[MySqlColumnNotNull]
public string Name { get; set; } = string.Empty;
```

Provider attribute’larý:

- `SqlServerColumnNotNull`
- `MySqlColumnNotNull`
- `PostgresColumnNotNull`
- `SQLiteColumnNotNull`

C# nullable/non-nullable durumu otomatik SQL’e çevrilmez. SQL nullability yalnýz attribute ile belirlenir.

## 20. UNIQUE

```csharp
[MySqlColumnUnique]
public string Sku { get; set; } = string.Empty;
```

Provider attribute’larý:

- `SqlServerColumnUnique`
- `MySqlColumnUnique`
- `PostgresColumnUnique`
- `SQLiteColumnUnique`

Bu attribute column-level `UNIQUE` constraint üretir. Ayrý isimli bir unique indeks istiyorsanýz `DbColumnIndex(..., isUnique: true)` kullanýn.

## 21. DEFAULT

`DbColumnDefault` Core paketindedir ve tüm provider’larda kullanýlýr:

```csharp
[DbColumnDefault("0")]
public int Stock { get; set; }

[DbColumnDefault("CURRENT_TIMESTAMP")]
public DateTime CreatedAt { get; set; }

[DbColumnDefault("'Active'")]
public string Status { get; set; } = string.Empty;
```

`DbColumnDefault` deðeri **raw SQL**’dir. String default için SQL quote’larýný sizin vermeniz gerekir.

> Kullanýcý girdisini, HTTP parametresini veya dýþ kaynaktan gelen metni `DbColumnDefault` içine yerleþtirmeyin.

## 22. CHECK

```csharp
[DbColumnCheck("Price >= 0")]
public decimal Price { get; set; }
```

Üretilen bölüm:

```sql
CHECK (Price >= 0)
```

Ýfade raw SQL’dir. Provider’ýn desteklediði SQL sözdizimini kullanýn ve dýþ girdiden üretmeyin.

## 23. Ýndeks

```csharp
[DbColumnIndex]
public string Name { get; set; } = string.Empty;

[DbColumnIndex("idx_users_email", isUnique: true)]
public string Email { get; set; } = string.Empty;
```

Ýsim verilmezse:

```text
idx_{table}_{property}
```

formatý kullanýlýr.

`DbColumnIndex` yalnýz `GenerateIndexSql<T>()` çýktýsýna etki eder. `CreateTables()` indeksleri yürütmez.

## 24. Foreign key

Provider’larýn foreign key attribute adlarý:

| Provider | Attribute |
|---|---|
| SQL Server | `SqlServerColumnForeignKey` |
| MySQL | `MySqlForeignKey` |
| PostgreSQL | `PostgresForeignKey` |
| SQLite | `SQLiteColumnForeignKey` |

MySQL örneði:

```csharp
[MySqlTableName("orders")]
public sealed class Order
{
    [MySqlColumnType(MySqlColumnType.INT)]
    [MySqlColumnPrimaryKey(isAutoIncrement: true)]
    public int Id { get; set; }

    [MySqlColumnType(MySqlColumnType.INT)]
    [MySqlForeignKey("CustomerId", "customers", "Id")]
    public int CustomerId { get; set; }
}
```

Parametreler:

1. Yerel kolon adý.
2. Referans tablo adý.
3. Referans kolon adý.

### Foreign key kullaným kurallarý

- Parametre adlarýný property ve gerçek database adlarýyla birebir eþleþtirin.
- Boþluk, tire, nokta veya schema-qualified ad kullanmayýn; 1.1.0 foreign key snippet’i bu adlarý ayrýca quote etmez.
- Parent tabloyu child tablodan önce oluþturun.
- Ayný generator cache’indeki tablo yürütme sýrasý baðýmlýlýk sýrasýný garanti etmez. Ýliþkili tablolar için ayrý kontrollü aþamalar veya migration scriptleri tercih edin.
- Cascade seçenekleri için 1.1.0’de hazýr attribute parametresi yoktur; migration scripti kullanýn.

# Provider kolon tipleri

## 25. SQL Server kolon tipleri

`SqlServerColumnType` enum deðerleri:

```text
TINYINT, SMALLINT, INT, BIGINT,
DECIMAL, NUMERIC, FLOAT, REAL, MONEY, SMALLMONEY,
DATE, DATETIME, DATETIME2, DATETIMEOFFSET, SMALLDATETIME, TIME,
CHAR, VARCHAR, NCHAR, NVARCHAR, TEXT, NTEXT,
BINARY, VARBINARY, IMAGE,
UNIQUEIDENTIFIER, XML, GEOGRAPHY, GEOMETRY, HIERARCHYID, BIT
```

Örnekler:

```csharp
[SqlServerColumnType(SqlServerColumnType.NVARCHAR, "200")]
[SqlServerColumnType(SqlServerColumnType.NVARCHAR, "MAX")]
[SqlServerColumnType(SqlServerColumnType.DECIMAL, "18,4")]
[SqlServerColumnType(SqlServerColumnType.UNIQUEIDENTIFIER)]
[SqlServerColumnType(SqlServerColumnType.VARBINARY, "MAX")]
```

`TEXT`, `NTEXT` ve `IMAGE` SQL Server’da legacy tiplerdir. Yeni projelerde `VARCHAR(MAX)`, `NVARCHAR(MAX)` ve `VARBINARY(MAX)` tercih edin.

## 26. MySQL/MariaDB kolon tipleri

`MySqlColumnType` enum deðerleri:

```text
TINYINT, SMALLINT, MEDIUMINT, INT, BIGINT,
DECIMAL, NUMERIC, FLOAT, DOUBLE,
DATE, DATETIME, TIMESTAMP, TIME, YEAR,
CHAR, VARCHAR, TINYTEXT, TEXT, MEDIUMTEXT, LONGTEXT,
BINARY, VARBINARY, TINYBLOB, BLOB, MEDIUMBLOB, LONGBLOB,
ENUM, SET, JSON, GEOMETRY, BIT, BOOLEAN
```

Örnekler:

```csharp
[MySqlColumnType(MySqlColumnType.VARCHAR, "255")]
[MySqlColumnType(MySqlColumnType.DECIMAL, "18,2")]
[MySqlColumnType(MySqlColumnType.JSON)]
```

Enum deðerlerinden MySQL `ENUM` üretme:

```csharp
public enum ProductStatus
{
    Draft,
    Active,
    Archived
}

[MySqlColumnType(MySqlColumnType.ENUM, typeof(ProductStatus))]
public ProductStatus Status { get; set; }
```

Üretim enum isimlerini SQL string deðerleri olarak kullanýr. Enum adý deðiþikliklerini migration olarak yönetin.

## 27. PostgreSQL kolon tipleri

`PostgresColumnType` enum deðerleri:

```text
SMALLINT, INTEGER, BIGINT,
DECIMAL, NUMERIC, REAL, DOUBLE_PRECISION, SERIAL, BIGSERIAL, MONEY,
DATE, TIME, TIMESTAMP, TIMESTAMPTZ, INTERVAL,
CHAR, VARCHAR, TEXT, BYTEA, BOOLEAN, UUID,
JSON, JSONB, XML,
CIDR, INET, MACADDR,
POINT, LINE, LSEG, BOX, PATH, POLYGON, CIRCLE,
BIT, VARBIT, HSTORE, ARRAY, RANGE
```

Örnekler:

```csharp
[PostgresColumnType(PostgresColumnType.VARCHAR, "200")]
[PostgresColumnType(PostgresColumnType.NUMERIC, "18,2")]
[PostgresColumnType(PostgresColumnType.JSONB)]
[PostgresColumnType(PostgresColumnType.UUID)]
[PostgresColumnType(PostgresColumnType.DOUBLE_PRECISION)]
```

`ARRAY`, `RANGE` ve bazý extension tabanlý tiplerde üretilecek SQL’i mutlaka kontrol edin; 1.1.0 yalnýz enum adýný/uzunluðu birleþtirir ve geliþmiþ type declaration modellemesi yapmaz.

## 28. SQLite kolon tipleri

`SQLiteColumnType` enum deðerleri:

```text
INTEGER, REAL, TEXT, BLOB, NUMERIC
```

Önerilen eþlemeler:

| C# | SQLite |
|---|---|
| `int`, `long`, `short`, `bool` | `INTEGER` |
| `float`, `double` | `REAL` |
| `decimal` | `NUMERIC` |
| `string`, `char`, `Guid`, ISO tarih metni | `TEXT` |
| `byte[]` | `BLOB` |

# SQL üretme API’si

## 29. Ortak API

```csharp
string GenerateSqlTable<T>(bool ifNotExists = false);
Task<string> GenerateSqlTableAsync<T>(
    bool ifNotExists = false,
    CancellationToken cancellationToken = default);

string GenerateDropTableSql<T>();
string GenerateTruncateTableSql<T>();
List<string> GenerateIndexSql<T>();

void CreateDatabase();
Task CreateDatabaseAsync(CancellationToken cancellationToken = default);

void CreateTables();
Task CreateTablesAsync(CancellationToken cancellationToken = default);
```

Provider alias metotlarý:

```csharp
GenerateSqlServerTable<T>()
GenerateMySqlTable<T>()
GeneratePostgresTable<T>()
GenerateSQLiteTable<T>()
```

## 30. SQL üretip hiç çalýþtýrmama

ModelSync, yalnýz SQL generator olarak da kullanýlabilir:

```csharp
var generator = new PostgresTableGenerator(connectionString);

var create = generator.GeneratePostgresTable<Customer>(true);
var drop = generator.GenerateDropTableSql<Customer>();
var truncate = generator.GenerateTruncateTableSql<Customer>();
var indexes = generator.GenerateIndexSql<Customer>();

File.WriteAllText("customer-create.sql", create);
```

Bu kullaným CI’da DDL snapshot testleri veya manuel DBA review süreci için uygundur.

## 31. Identifier güvenliði

Tablo, kolon ve indeks adlarý þu desene uymalýdýr:

```text
^[A-Za-z_][A-Za-z0-9_]*$
```

Geçerli:

```text
products
ProductItems
idx_products_name
_customer
```

Geçersiz:

```text
product-items
sales.products
product name
products;DROP TABLE users
```

Schema-qualified tablo adlarý doðrudan table-name attribute’ünde kullanýlamaz. Schema ihtiyacý olan geliþmiþ yapýlar için migration scripti tercih edin.

# Tablo ve kolon operasyonlarý

## 32. Kolon ekleme

Önce yeni property’yi modelde attribute’larýyla tanýmlayýn:

```csharp
[MySqlColumnType(MySqlColumnType.INT)]
[DbColumnDefault("0")]
public int Stock { get; set; }
```

Sonra property adýný vererek ekleyin:

```csharp
await generator.AddColumnAsync<Product>(
    nameof(Product.Stock),
    cancellationToken);
```

Kolon tanýmý model attribute’larýndan okunur. `nameof` kullanmak refactor güvenliði saðlar.

## 33. Kolon yeniden adlandýrma

```csharp
await generator.RenameColumnAsync<Product>(
    oldColumnName: "OldName",
    newColumnName: "Name",
    cancellationToken);
```

Provider sözdizimleri farklýdýr:

- SQL Server `sp_rename` kullanýr.
- Modern MySQL, PostgreSQL ve SQLite standarda yakýn `RENAME COLUMN` kullanýr.

Database sürümünüzün bu komutu desteklediðini doðrulayýn.

## 34. Kolon silme

Kolon silmek veri kaybýdýr ve açýk izin ister:

```csharp
var destructive = DestructiveOperationOptions.Allow();

await generator.DropColumnAsync<Product>(
    nameof(Product.LegacyCode),
    destructive,
    cancellationToken);
```

Aþaðýdaki çaðrý tasarým gereði exception fýrlatýr:

```csharp
await generator.DropColumnAsync<Product>(nameof(Product.LegacyCode));
```

## 35. Kolon tipi deðiþtirme

Modelde property’nin type attribute’ünü yeni SQL tipiyle güncelledikten sonra:

```csharp
var destructive = DestructiveOperationOptions.Allow();

await generator.AlterColumnTypeAsync<Product>(
    nameof(Product.Price),
    destructive,
    cancellationToken);
```

Dikkat edilmesi gerekenler:

- Tip dönüþümü mevcut verilerle uyumsuzsa provider hata verir.
- ModelSync otomatik veri dönüþtürme veya `USING` ifadesi oluþturmaz.
- PostgreSQL karmaþýk dönüþümlerde manuel SQL gerekebilir.
- SQLite bunu desteklemez ve `NotSupportedException` fýrlatýr.

## 36. Tablolarý silme

Yalnýz generator cache’ine daha önce alýnmýþ model tablolarý hedeflenir:

```csharp
generator.GenerateMySqlTable<User>();
generator.GenerateMySqlTable<Product>();

await generator.DropTablesAsync(
    DestructiveOperationOptions.Allow(),
    cancellationToken);
```

Tablolar arasý foreign key varsa drop sýrasý hata üretebilir. Production’da baðýmlýlýk sýralý migration scriptleri kullanýn.

## 37. Truncate SQL’i

```csharp
var sql = generator.GenerateTruncateTableSql<Product>();
```

Bu metot yalnýz SQL döndürür; yürütme metodu yoktur. `TRUNCATE` veri kaybýna yol açar ve Core API bunu ayrýca guard etmez. Çalýþtýrmadan önce kendi güvenlik politikanýzý uygulayýn.

# Dependency Injection ve uygulama baþlangýcý

## 38. Tavsiye edilen servis ömrü

Generator örneði mutable SQL cache taþýr.

- Yalnýz startup schema initialization için kullanýlan tek bir servis: singleton kullanýlabilir.
- Farklý operasyonlarýn cache paylaþmasýný istemiyorsanýz: scoped veya transient tercih edin.
- Request baþýna tablo üretmek genellikle doðru deðildir; schema deðiþikliklerini kontrollü startup/deployment adýmýnda çalýþtýrýn.

## 39. ASP.NET Core kaydý — SQL Server

```csharp
using UmbrellaFrame.ModelSync.Core.Interfaces;
using UmbrellaFrame.ModelSync.SqlServer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ITableGenerator>(sp =>
{
    var connectionString = builder.Configuration
        .GetConnectionString("SqlServer")
        ?? throw new InvalidOperationException("SqlServer connection string missing.");

    var logger = sp.GetRequiredService<ILogger<SqlServerTableGenerator>>();
    return new SqlServerTableGenerator(connectionString, logger);
});
```

## 40. appsettings.json

```json
{
  "ConnectionStrings": {
    "SqlServer": "Server=localhost;Database=AppDb;Integrated Security=True;TrustServerCertificate=True;",
    "MySql": "Server=localhost;Database=appdb;User ID=root;Password=secret;",
    "PostgreSql": "Host=localhost;Database=appdb;Username=postgres;Password=secret;",
    "SQLite": "Data Source=app.db"
  }
}
```

Connection string’i kaynak koda gömmeyin. Production’da environment variable, secret manager veya platform secret store kullanýn.

## 41. Schema initializer service

```csharp
using UmbrellaFrame.ModelSync.Core.Interfaces;

public sealed class SchemaInitializer
{
    private readonly ITableGenerator _generator;
    private readonly ILogger<SchemaInitializer> _logger;

    public SchemaInitializer(
        ITableGenerator generator,
        ILogger<SchemaInitializer> logger)
    {
        _generator = generator;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var userSql = await _generator.GenerateSqlTableAsync<User>(
            ifNotExists: true,
            cancellationToken);

        var productSql = await _generator.GenerateSqlTableAsync<Product>(
            ifNotExists: true,
            cancellationToken);

        _logger.LogInformation("User DDL: {Sql}", userSql);
        _logger.LogInformation("Product DDL: {Sql}", productSql);

        await _generator.CreateTablesAsync(cancellationToken);
    }
}
```

SQL Server 1.1.0’de provider-specific `ifNotExists` guard’ý için initializer’a doðrudan `SqlServerTableGenerator` enjekte edip `GenerateSqlServerTable<T>(true)` kullanýn.

## 42. Hosted service

```csharp
public sealed class SchemaInitializerHostedService : IHostedService
{
    private readonly SchemaInitializer _initializer;

    public SchemaInitializerHostedService(SchemaInitializer initializer)
        => _initializer = initializer;

    public Task StartAsync(CancellationToken cancellationToken)
        => _initializer.InitializeAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
```

Kayýt:

```csharp
builder.Services.AddSingleton<SchemaInitializer>();
builder.Services.AddHostedService<SchemaInitializerHostedService>();
```

> Birden fazla uygulama instance’ý ayný anda baþlýyorsa schema iþlemlerinin eþ zamanlý çalýþmasý risklidir. Production migration’ýný deployment job olarak tek instance üzerinden çalýþtýrmak daha güvenlidir.

# Logging

## 43. Logger kullanýmý

Provider constructor’larý opsiyonel `ILogger<T>` kabul eder:

```csharp
var generator = new MySqlTableGenerator(connectionString, logger);
```

SQL üretimi debug seviyesinde, bazý migration iþlemleri information seviyesinde loglanýr. Connection string ve þifreleri loglamayýn.

Console app örneði:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UmbrellaFrame.ModelSync.MySql;

var services = new ServiceCollection()
    .AddLogging(x => x.AddConsole().SetMinimumLevel(LogLevel.Debug))
    .BuildServiceProvider();

var logger = services.GetRequiredService<ILogger<MySqlTableGenerator>>();
var generator = new MySqlTableGenerator(connectionString, logger);
```

# Migration Runner

## 44. Ne zaman migration runner kullanýlmalý?

Attribute tabanlý generator basit ve açýk model DDL’i için uygundur. Aþaðýdaki ihtiyaçlarda SQL migration dosyalarý daha doðru olur:

- Schema, extension, view veya geliþmiþ constraint oluþturma.
- Veri dönüþümü.
- Seed data.
- Trigger.
- Provider’a özgü karmaþýk SQL.
- Ýndeks varlýk kontrolleri.
- Uygulama sürümleri arasýnda açýk, sýralý database deðiþiklikleri.

## 45. Önerilen klasör yapýsý

```text
Database/
  Scripts/
    Tables/
      001_CreateProducts.sql
      002_AddProductsSku.sql
    StoredProcedures/
      010_GetProducts.sql
    Triggers/
      020_ProductAudit.sql
    Seeds/
      030_DefaultProducts.sql
```

Kategori sýrasý:

```text
Tables -> StoredProcedures -> Triggers -> Seeds
```

Kategori içinde dosya adýnýn `_` öncesindeki numeric ID’si sýralamada kullanýlýr.

```text
001_CreateProducts.sql
```

þöyle çözülür:

```text
Id   = 001
Name = CreateProducts
```

## 46. SQL Server migration runner örneði

```csharp
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.SqlServer;

var options = new MigrationRunnerOptions
{
    EnsureHistoryTables = true,

    // Production için uygulanmýþ migration dosyalarýný deðiþtirmeyin.
    // Otomatik eksik kolon onarýmýný kapatmak daha güvenli bir varsayýmdýr.
    AutoAddMissingColumnsFromTableScripts = false
};

options.Schemas.Add("sec");

var runner = new SqlServerMigrationRunner(
    connectionString,
    options);

runner.RegisterScriptFile("Database/Scripts/Tables/001_CreateProducts.sql");
runner.RegisterScriptFile("Database/Scripts/Tables/002_AddProductsSku.sql");
runner.RegisterScriptFile("Database/Scripts/StoredProcedures/010_GetProducts.sql");
runner.RegisterScriptFile("Database/Scripts/Triggers/020_ProductAudit.sql");
runner.RegisterScriptFile("Database/Scripts/Seeds/030_DefaultProducts.sql");

var plans = await runner.CompareRegisteredAsync(cancellationToken);

foreach (var plan in plans)
{
    Console.WriteLine(
        $"{plan.Definition.Category} | " +
        $"{plan.Definition.Id} | " +
        $"{plan.ChangeType} | " +
        $"{plan.Reason}");
}

if (plans.Any(x => x.HasChanges))
{
    await runner.RunAsync(cancellationToken);
}
```

## 47. Diðer migration runner sýnýflarý

```csharp
var mysqlRunner = new MySqlMigrationRunner(connectionString, options);
var postgresRunner = new PostgresMigrationRunner(connectionString, options);
var sqliteRunner = new SQLiteMigrationRunner(connectionString, options);
```

## 48. Inline migration tanýmý

```csharp
var definition = MigrationScriptDefinition.Create(
    id: "001",
    name: "CreateProducts",
    category: MigrationScriptCategory.Tables,
    sql: "CREATE TABLE ...;",
    source: "inline");

runner.RegisterScript(definition);
```

## 49. Kategori veya ID’yi açýk verme

```csharp
runner.RegisterScriptFile(
    path: "Database/Custom/setup.sql",
    category: MigrationScriptCategory.Tables,
    id: "001",
    name: "CreateProducts");
```

## 50. Embedded resource scriptleri

`.csproj`:

```xml
<ItemGroup>
  <EmbeddedResource Include="Database\Scripts\**\*.sql" />
</ItemGroup>
```

Kayýt:

```csharp
using System.Reflection;

runner.RegisterEmbeddedScripts(
    Assembly.GetExecutingAssembly(),
    "MyApplication.Database.Scripts.");

await runner.RunAsync(cancellationToken);
```

Yalnýz `.sql` ile biten resource’lar alýnýr.

## 51. Migration planý

`MigrationSyncPlan` temel alanlarý:

| Alan | Anlamý |
|---|---|
| `Definition` | Script ID, ad, kategori, SQL ve kaynak bilgisi |
| `ChangeType` | `None`, `Apply`, `Reapply` |
| `CurrentHash` | History tablosundaki mevcut hash |
| `TargetHash` | Proje SQL’inin hesaplanan hash’i |
| `SqlToApply` | Uygulanacak SQL |
| `Reason` | Planýn neden bu durumda olduðu |
| `HasChanges` | `ChangeType != None` |

## 52. History tablolarý

Kategori baþýna bir tablo kullanýlýr:

```text
SchemaMigration_Tables
SchemaMigration_StoredProcedures
SchemaMigration_Triggers
SchemaMigration_Seeds
SchemaMigration_CustomSql
```

Temel olarak þu bilgiler saklanýr:

- `Id`
- `Name`
- `SqlHash`
- `AppliedAt`
- `UpdateAt`

History tablosu migration’ýn daha önce uygulanýp uygulanmadýðýný ve SQL hash’inin deðiþip deðiþmediðini takip eder.

## 53. Database reset

Reset tüm database’i etkileyebilecek yýkýcý bir iþlemdir:

```csharp
var options = new MigrationRunnerOptions
{
    ResetDatabase = true,
    DestructiveOptions = DestructiveOperationOptions.Allow()
};

var runner = new SqlServerMigrationRunner(connectionString, options);
await runner.RunAsync(cancellationToken);
```

Açýk destructive izin verilmezse iþlem baþlamadan exception oluþur. SQLite runner reset desteklemez.

## 54. SQL Server `GO`

SQL Server migration runner, tek satýrdaki `GO` batch separator’larýný ayýrýr:

```sql
CREATE TABLE dbo.Products (...);
GO
CREATE INDEX IX_Products_Name ON dbo.Products(Name);
GO
```

Stored procedure synchronizer dosyalarýnda `GO` kullanmayýn. Migration runner ile stored procedure scripti çalýþtýrýyorsanýz batch yapýsýný dikkatle test edin.

## 55. Migration production güvenlik kurallarý

ModelSync 1.1.0 kullanýrken aþaðýdaki kurallarý zorunlu süreç kabul edin:

1. **Uygulanmýþ migration dosyasýný deðiþtirmeyin.** Yeni deðiþiklik için yeni ID’li dosya ekleyin.
2. Production’da `AutoAddMissingColumnsFromTableScripts = false` önerilir.
3. `CompareRegisteredAsync()` çýktýsýný loglayýn veya onaylayýn.
4. Scriptlerin idempotent olmasýný saðlayýn veya yalnýz bir kez çalýþacaðýný garanti edin.
5. Database yedeði alýn.
6. Ayný migration runner’ý eþ zamanlý birden fazla uygulama instance’ýnda çalýþtýrmayýn.
7. Baþarýsýzlýk sonrasý database’i kontrol etmeden tekrar çalýþtýrmayýn.
8. 1.1.0’de batch/script/history iþlemleri tüm provider’larda tek atomik transaction olarak garanti edilmez.
9. Otomatik eksik kolon onarýmý kolon tipi, constraint, rename veya drop farkýný çözmez.
10. Duplicate migration ID kullanmayýn; ID’leri repository seviyesinde unique tutun.

# Stored Procedure senkronizasyonu

## 56. Ne için kullanýlýr?

Stored procedure SQL dosyanýz proje tarafýnda source of truth olur. Synchronizer:

- Procedure yoksa `Create` planý üretir.
- Procedure varsa ve SQL farklýysa `Alter` planý üretir.
- Aynýysa `None` üretir.
- Planý uyguladýðýnýzda provider’a uygun create/replace stratejisini çalýþtýrýr.

Destek:

| Provider | Destek | Uygulama stratejisi |
|---|---|---|
| SQL Server / Azure SQL | Var | `CREATE OR ALTER PROCEDURE` |
| MySQL / MariaDB | Var | `DROP PROCEDURE IF EXISTS` + `CREATE PROCEDURE` |
| PostgreSQL | Var | `CREATE OR REPLACE PROCEDURE` |
| SQLite | Yok | `NotSupportedException` |

## 57. Önerilen dosya yapýsý

```text
Database/
  Procedures/
    SqlServer/
      dbo.usp_GetProducts.sql
    MySql/
      appdb.usp_GetProducts.sql
    PostgreSQL/
      public.usp_get_products.sql
```

Dosya adý `schema.procedure.sql` biçimindeyse schema ve procedure adý otomatik çözülür.

## 58. SQL Server stored procedure

Dosya:

```sql
CREATE PROCEDURE dbo.usp_GetProducts
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Name, Price
    FROM dbo.Products;
END
```

Kod:

```csharp
using UmbrellaFrame.ModelSync.SqlServer;

var synchronizer = new SqlServerStoredProcedureSynchronizer(
    connectionString);

synchronizer.RegisterProcedureFile(
    "Database/Procedures/SqlServer/dbo.usp_GetProducts.sql");

var plans = await synchronizer.CompareRegisteredAsync(cancellationToken);

foreach (var plan in plans)
{
    Console.WriteLine(
        $"{plan.Definition.Schema}.{plan.Definition.Name}: " +
        $"{plan.ChangeType}");
}

await synchronizer.SyncRegisteredAsync(cancellationToken);
```

## 59. MySQL/MariaDB stored procedure

Dosya:

```sql
CREATE PROCEDURE usp_GetProducts()
BEGIN
    SELECT Id, Name, Price FROM Products;
END
```

Kod:

```csharp
using UmbrellaFrame.ModelSync.MySql;

var synchronizer = new MySqlStoredProcedureSynchronizer(
    connectionString);

synchronizer.RegisterProcedureFile(
    "Database/Procedures/MySql/appdb.usp_GetProducts.sql");

var plans = await synchronizer.CompareRegisteredAsync(cancellationToken);
await synchronizer.SyncRegisteredAsync(cancellationToken);
```

MySQL procedure deðiþikliðinde mevcut procedure drop edilir ve yeniden oluþturulur. Create baþarýsýz olursa procedure geçici olarak bulunmayabilir; production review ve bakým penceresi uygulayýn.

## 60. PostgreSQL stored procedure

Dosya:

```sql
CREATE PROCEDURE public.usp_get_products()
LANGUAGE SQL
AS $$
    SELECT 1;
$$;
```

Kod:

```csharp
using UmbrellaFrame.ModelSync.PostgreSQL;

var synchronizer = new PostgresStoredProcedureSynchronizer(
    connectionString);

synchronizer.RegisterProcedureFile(
    "Database/Procedures/PostgreSQL/public.usp_get_products.sql");

var plans = await synchronizer.CompareRegisteredAsync(cancellationToken);
await synchronizer.SyncRegisteredAsync(cancellationToken);
```

1.1.0 PostgreSQL overloaded procedure signature’larýný desteklemez. Ayný schema ve adla farklý parametre listesine sahip procedure’ler kullanýyorsanýz manuel migration yönetin.

## 61. Inline stored procedure tanýmý

```csharp
var definition = StoredProcedureDefinition.Create(
    name: "usp_GetProducts",
    sql: sqlText,
    schema: "dbo");

synchronizer.RegisterProcedure(definition);
```

## 62. Tek procedure karþýlaþtýrma ve uygulama

```csharp
var definition = StoredProcedureDefinition.FromFile(
    "Database/Procedures/SqlServer/dbo.usp_GetProducts.sql");

var plan = await synchronizer.CompareAsync(
    definition,
    cancellationToken);

if (plan.HasChanges)
{
    await synchronizer.ApplyAsync(plan, cancellationToken);
}
```

## 63. SQL dosyasý kurallarý

- Her dosya tek procedure tanýmý içermelidir.
- SQL içindeki procedure adý kayýt edilen adla eþleþmelidir.
- SQL Server dosyasýnda `CREATE PROCEDURE`, `CREATE PROC`, `ALTER PROCEDURE` veya `CREATE OR ALTER PROCEDURE` kullanýlabilir.
- MySQL dosyasýnda `CREATE PROCEDURE` kullanýlmalýdýr.
- PostgreSQL dosyasýnda `CREATE PROCEDURE`, `ALTER PROCEDURE` veya `CREATE OR REPLACE PROCEDURE` kullanýlabilir.
- Stored procedure synchronizer dosyasýnda SQL Server `GO` kullanmayýn.
- Dry-run için önce `Compare...` çaðrýsý yapýn.

# Canlý model senkronizasyonu

Model synchronizer sýnýflarý, 1.1.0 ile gelen dry-run-first canlý veritabaný karþýlaþtýrma katmanýdýr.

Bu katmaný þu sorular için kullanýn:

- Hangi tablolar eksik?
- Hangi kolonlar eksik?
- Hangi indeks veya desteklenen constraint eksik?
- Hangi farklar riskli/yýkýcý ve manuel review gerektiriyor?
- Hangi proje SQL scriptleri çalýþmalý?

## Provider API'leri

| Provider | Options | Synchronizer |
|---|---|---|
| SQL Server / Azure SQL | `SqlServerModelSyncOptions` | `SqlServerModelSynchronizer` |
| MySQL / MariaDB | `MySqlModelSyncOptions` | `MySqlModelSynchronizer` |
| PostgreSQL | `PostgresModelSyncOptions` | `PostgresModelSynchronizer` |
| SQLite | `SQLiteModelSyncOptions` | `SQLiteModelSynchronizer` |

## SQL Server örneði

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

## Net model seçimi

Assembly içinde test modeli, eski þema modeli veya DTO varsa `FromTypes` kullanýn:

```csharp
var result = await SqlServerModelSynchronizer
    .FromTypes(options, typeof(ProductSchema), typeof(CustomerSchema))
    .CompareAsync(cancellationToken);
```

## Tablo bazli execution policy

Yayinlanmamis sertlestirme calismasi ayni calistirmada manuel ve otomatik tablo sahipligini karistirmaya izin verir:

```csharp
options.DefaultTableMode = ModelSyncTableMode.ManualOnly;

options.TablePolicies
    .ForType<AuditLog>(ModelSyncTableMode.ApplySafeChanges)
    .ForType<Notification>(ModelSyncTableMode.ApplySafeChanges);
```

Tersi stratejide global davranisi automatic-safe tutup hassas tablolari manuel isaretleyebilirsiniz:

```csharp
options.DefaultTableMode = ModelSyncTableMode.ApplySafeChanges;

options.TablePolicies
    .ForType<User>(ModelSyncTableMode.ManualOnly)
    .ForType<Order>(ModelSyncTableMode.ManualOnly);
```

Legacy tablolar normal diff uretiminden cikarilabilir:

```csharp
options.TablePolicies
    .ForTable("legacy", "OldOrders", ModelSyncTableMode.Ignore);
```

`ManualOnly` operasyonlari `ManualOperations` altinda raporlanir ve otomatik calistirilmaz. `ApplySafeChanges` yalniz guvenli, provider tarafindan desteklenen ve dependency'leri hazir operasyonlari uygular; destructive sema degisiklikleri bloklu kalir.

## Otomatik uygulanabilen güvenli iþlemler

- Eksik tablo oluþturma.
- Eksik nullable kolon ekleme.
- Default deðeri olan eksik `NOT NULL` kolon ekleme.
- Eksik indeks ekleme.
- Provider güvenli ALTER sözdizimi destekliyorsa eksik default/check/unique/foreign key constraint ekleme.
- History/hash takipli sýralý SQL scriptleri.

## Bloklanan iþlemler

- Model setinde bulunmayan canlý database tablolarý yalnýz `ReportUnmappedTables = true` ise `DropTable` olarak raporlanýr ve bloklanýr.
- Modelde bulunmayan canlý database kolonlarý `DropColumn` olarak raporlanýr ve bloklanýr.
- Rename, tip deðiþikliði ve nullable-to-not-null deðiþiklikleri bloklanýr.
- Mevcut tabloya defaultsuz `NOT NULL` kolon eklemek bloklanýr.
- SQLite stored procedure scriptleri desteklenmez.

`AllowDestructiveChanges`, model diff içindeki drop/rename/type-change iþlemlerini otomatik yapmaz. Model diff tarafýndaki yýkýcý iþlemler review-only kalýr. Bu seçenek otomatik veri kaybý izni gibi deðerlendirilmemelidir.

## Script seçenekleri

`ApplyStoredProceduresOnEveryRun` ve `ApplyTriggersOnEveryRun`, idempotent scriptleri her çalýþtýrmada doðrudan uygular.

`ApplySeedsWithHashTracking` ve `ApplyCustomSqlWithHashTracking` varsayýlan olarak `true` deðerindedir. True iken seed ve custom SQL scriptleri migration history/hash ile uygulanýr. False yapýlýrsa her çalýþtýrmada doðrudan uygulanýrlar.

Model diff iþlemleri risk sýnýflandýrmasýndan geçer. Kaydedilen SQL scriptleri ise review edilmiþ, güvenilir proje artifact'i kabul edilir; ModelSync script metnini `DROP TABLE` veya `DELETE` gibi destructive SQL açýsýndan parse etmez.

Odaklý referans için [14 - Model Synchronizer](14-model-synchronizer.md) belgesine bakýn.

# Analyzer

## 64. Neden kullanýlmalý?

Runtime’da SQL üretirken karþýlaþacaðýnýz bazý model hatalarýný daha kod yazarken gösterir.

Kurulum:

```bash
dotnet add package UmbrellaFrame.ModelSync.Analyzers --version 1.1.0
```

## 65. Analyzer kurallarý

| Kural | Þiddet | Anlamý |
|---|---|---|
| `MSYNC001` | Warning | Public property’de provider column type attribute’ü eksik |
| `MSYNC002` | Warning | Column type kullanýlan class’ta table-name attribute’ü eksik |
| `MSYNC003` | Warning | Modelde primary key attribute’ü eksik |

CI’da error yapmak için `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.MSYNC001.severity = error
dotnet_diagnostic.MSYNC002.severity = error
dotnet_diagnostic.MSYNC003.severity = error
```

Kuralý lokal bastýrma:

```csharp
#pragma warning disable MSYNC003
// kasýtlý primary-key'siz model
#pragma warning restore MSYNC003
```

> Analyzer bir yardýmcý kontroldür; generated SQL review ve integration test yerine geçmez. Özellikle provider’a özgü edge case’leri test edin.

# Hata yönetimi ve troubleshooting

## 66. Sýk görülen hatalar

### “Column has no type attribute”

Neden: Public property üzerinde provider `ColumnType` attribute’ü yok.

Çözüm:

```csharp
[MySqlColumnType(MySqlColumnType.INT)]
public int Count { get; set; }
```

ModelSync 1.1.0 DbIgnore ve DbColumnName destegi icerir. Database kolonu olmayacak hesaplanmis public propertyler DbIgnore ile schema discovery disina alinabilir.

### “Invalid SQL identifier”

Neden: Tablo, kolon, indeks veya database adý güvenli identifier desenine uymuyor.

Çözüm: Yalnýz harf/underscore ile baþlayan, devamýnda harf/rakam/underscore içeren ad kullanýn.

### “... is destructive and may cause data loss”

Neden: `DropTables`, `DropColumn` veya `AlterColumnType` açýk izin olmadan çaðrýldý.

Çözüm:

```csharp
var allow = DestructiveOperationOptions.Allow();
```

Ýþlemi gerçekten istediðinizi doðruladýktan sonra ilgili overload’a verin.

### `CreateTablesAsync()` hiçbir þey yapmýyor

Neden: Generator cache’i boþ.

Çözüm: Ayný generator örneðinde önce `Generate...Table<T>()` çaðýrýn.

### Foreign key oluþtururken referenced table bulunamadý

Neden: Parent tablo henüz oluþturulmadý veya cache yürütme sýrasý baðýmlýlýðý karþýlamadý.

Çözüm: Parent tabloyu ayrý aþamada önce oluþturun veya migration scripti kullanýn.

### Ýndeks oluþmadý

Neden: `GenerateIndexSql<T>()` yalnýz SQL üretir.

Çözüm: SQL’i ADO.NET ile ayrýca yürütün veya migration scriptine taþýyýn.

### SQLite truncate davranýþý

Neden: SQLite `TRUNCATE TABLE` desteklemez. SQLite provider bu nedenle `DELETE FROM` SQL’i üretir.

Üretilen örnek:

```sql
DELETE FROM "products";
```

### SQLite “ALTER COLUMN” hatasý

Neden: SQLite doðrudan type alter desteklemez.

Çözüm: Yeni tablo oluþtur, veriyi dönüþtürerek kopyala, eski tabloyu sil, yeni tabloyu yeniden adlandýr.

### Database oluþturma yetki hatasý

Neden: Connection kullanýcýsýnda `CREATE DATABASE` yetkisi yok.

Çözüm: Database’i DBA/deployment ile önceden oluþturun ve `CreateDatabase()` çaðrýsýný kaldýrýn.

## 67. CancellationToken kullanýmý

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

await generator.CreateTablesAsync(cts.Token);
```

Migration ve stored procedure metotlarý da cancellation token kabul eder. Cancellation, database’in o ana kadar yaptýðý deðiþiklikleri otomatik geri alacaðý anlamýna gelmez.

# Test yaklaþýmý

## 68. SQL snapshot testi

```csharp
[Test]
public void Product_sql_should_contain_expected_columns()
{
    var generator = new MySqlTableGenerator("Server=unused;Database=unused;");

    var sql = generator.GenerateMySqlTable<Product>(true);

    StringAssert.Contains("`Name` VARCHAR(200) NOT NULL", sql);
    StringAssert.Contains("`Price` DECIMAL(18,2)", sql);
}
```

SQL üretimi baðlantý açmadan yapýlýr; connection string constructor doðrulamasýný geçecek biçimde dolu olmalýdýr.

## 69. Integration test

Gerçek provider üzerinde þu akýþý test edin:

1. Test database/container oluþtur.
2. Model SQL’ini üret.
3. Tabloyu oluþtur.
4. Provider kataloðundan tablo/kolon/constraint kontrolü yap.
5. Test verisi ekle.
6. Add/Rename/Alter/Drop senaryolarýný ayrý database’te dene.
7. Test database’ini temizle.

SQLite shared-memory küçük testler için uygundur; SQL Server/MySQL/PostgreSQL davranýþýnýn birebir yerine geçmez.

# Production kullaným rehberi

## 70. ModelSync’i production’da hangi biçimde kullanmalýyým?

Önerilen ayrým:

### Basit uygulama/prototip

- Attribute model.
- Generated SQL review.
- `ifNotExists: true`.
- Startup initializer.

### Kurumsal/production uygulama

- Attribute generator’ý DDL üretimi ve test için kullanýn.
- Gerçek sürüm deðiþikliklerini immutable migration scriptleriyle yönetin.
- Migration’ý uygulama request trafiði baþlamadan, tek deployment job’da çalýþtýrýn.
- Dry-run planý loglayýn ve onaylayýn.
- Database yedeði ve rollback scripti hazýrlayýn.
- Stored procedure deðiþikliklerini compare + review sonrasýnda uygulayýn.

## 71. Production checklist

- [ ] Doðru provider paketi kuruldu.
- [ ] Connection string secret store’dan geliyor.
- [ ] Tüm public model property’lerinde doðru column type attribute’ü var.
- [ ] Tablo ve identifier adlarý güvenli desene uyuyor.
- [ ] Generated SQL code review’den geçti.
- [ ] Ýndekslerin ayrýca yürütüldüðü doðrulandý.
- [ ] Foreign key parent/child sýrasý kontrol edildi.
- [ ] Raw default/check ifadelerinde dýþ girdi yok.
- [ ] Destructive operasyonlar ayrý maintenance adýmýnda.
- [ ] Production migration dosyalarý immutable.
- [ ] `AutoAddMissingColumnsFromTableScripts` production’da bilinçli ayarlandý.
- [ ] Database backup/restore prosedürü test edildi.
- [ ] Migration tek instance tarafýndan çalýþtýrýlýyor.
- [ ] Integration test gerçek provider sürümünde geçti.
- [ ] Stored procedure planlarý uygulanmadan önce incelendi.
- [ ] SQLite sýnýrlamalarý dikkate alýndý.

# Tam örnek proje yapýsý

## 72. Önerilen klasörler

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

Þema modellerini domain entity veya API DTO’larýndan ayýrmak hâlâ faydalýdýr. Yayýnlanmýþ 1.1.0 paketleri tüm public property’leri kolon kabul eder; mevcut repository’deki yayýnlanmamýþ `DbIgnore` desteði bu riski azaltýr.

## 73. Uçtan uca SQL Server startup örneði

```csharp
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.SqlServer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(sp =>
{
    var cs = builder.Configuration.GetConnectionString("SqlServer")
        ?? throw new InvalidOperationException("Connection string missing.");

    return new SqlServerTableGenerator(
        cs,
        sp.GetRequiredService<ILogger<SqlServerTableGenerator>>());
});

builder.Services.AddSingleton<DatabaseBootstrapper>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var bootstrapper = scope.ServiceProvider
        .GetRequiredService<DatabaseBootstrapper>();

    await bootstrapper.InitializeAsync(app.Lifetime.ApplicationStopping);
}

app.Run();

[SqlServerTableName("Products")]
public sealed class ProductSchema
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

public sealed class DatabaseBootstrapper
{
    private readonly SqlServerTableGenerator _generator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseBootstrapper> _logger;

    public DatabaseBootstrapper(
        SqlServerTableGenerator generator,
        IConfiguration configuration,
        ILogger<DatabaseBootstrapper> logger)
    {
        _generator = generator;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var sql = _generator.GenerateSqlServerTable<ProductSchema>(
            ifNotExists: true);

        _logger.LogInformation("Prepared DDL: {Sql}", sql);
        await _generator.CreateTablesAsync(cancellationToken);

        // Ýndeksleri production'da tercihen migration scriptiyle yönetin.
    }
}
```

# API hýzlý referans

## 74. `ITableGenerator`

| Metot | Amaç |
|---|---|
| `GenerateSqlTable<T>()` | CREATE TABLE SQL üretir ve cache’e alýr. |
| `GenerateSqlTableAsync<T>()` | Ayný üretimin Task tabanlý biçimi. |
| `GenerateDropTableSql<T>()` | DROP TABLE SQL döndürür. |
| `GenerateTruncateTableSql<T>()` | Provider'a özel truncate/delete SQL'i döndürür. |
| `GenerateIndexSql<T>()` | Index SQL listesi döndürür. |
| `CreateDatabase()` / Async | Provider’a göre database oluþturur; SQLite no-op. |
| `CreateTables()` / Async | Cache’teki CREATE TABLE SQL’lerini çalýþtýrýr. |
| `DropTables(options)` / Async | Cache’teki tablolarý açýk destructive izinle siler. |
| `AddColumn<T>()` / Async | Property metadata’sýndan kolon ekler. |
| `DropColumn<T>(..., options)` / Async | Kolon siler; açýk destructive izin gerekir. |
| `RenameColumn<T>()` / Async | Kolon adýný deðiþtirir. |
| `AlterColumnType<T>(..., options)` / Async | Modeldeki yeni tipe geçirir; açýk izin gerekir. |

## 75. `IMigrationRunner`

| Metot | Amaç |
|---|---|
| `RegisterScript(definition)` | Inline/önceden hazýrlanmýþ migration kaydeder. |
| `RegisterScriptFile(...)` | SQL dosyasýný kaydeder. |
| `RegisterEmbeddedScripts(...)` | Assembly içindeki embedded `.sql` dosyalarýný kaydeder. |
| `CompareRegisteredAsync()` | Read-only dry-run migration planlarýný üretir. |
| `EnsureInfrastructureAsync()` | Gerekli schema/history tablolarýný açýkça oluþturur. |
| `RunAsync()` | Gerekiyorsa infrastructure oluþturur, planlarý uygular ve history yazar. |

## 76. `IStoredProcedureSynchronizer`

| Metot | Amaç |
|---|---|
| `RegisterProcedure(...)` | Inline procedure definition kaydeder. |
| `RegisterProcedureFile(...)` | SQL dosyasýný kaydeder. |
| `CompareAsync(...)` | Tek procedure dry-run planý üretir. |
| `CompareRegisteredAsync()` | Kayýtlý procedure’leri karþýlaþtýrýr. |
| `ApplyAsync(plan)` | Tek planý uygular. |
| `SyncRegisteredAsync()` | Kayýtlý procedure’leri karþýlaþtýrýp uygular. |

## 77. Model Synchronizer

| Metot / Üye | Amaç |
|---|---|
| `FromAssemblies(...)` | Assembly içindeki provider uyumlu schema modellerini keþfeder. |
| `FromTypes(...)` | Verilen model tipleriyle sýnýrlý senkronizasyon yapar. |
| `AddSqlScript(...)` | Inline SQL script ekler. |
| `AddSqlScriptsFromEmbeddedResources(...)` | Embedded `.sql` scriptleri kategori sýrasýyla ekler. |
| `CompareAsync()` | Model/script senkronizasyonu için dry-run sonuç üretir. |
| `ModelSyncResult.SafeOperations` | Otomatik uygulanabilen iþlemler. |
| `ModelSyncResult.BlockedOperations` | Destructive, riskli veya unsupported iþlemler. |
| `ModelSyncResult.SkippedOperations` | Konfigürasyonla bilinçli atlanan güvenli iþlemler. |
| `ApplyAsync()` | Yalnýz blocked operation yoksa uygular. |

# Sürüm 1.1.0 sýnýrlarý

## 78. Bilinmesi gereken güncel sýnýrlar

- Model synchronizer yýkýcý/riskli farklarý sessiz uygulamaz; drop, rename, tip deðiþikliði ve nullable-to-not-null iþlemleri review-only kalýr.
- Yayýnlanmýþ `1.1.0` paketlerinde public property ignore ve column-name override attribute’leri yoktur; mevcut repository’deki `DbIgnore` ve `DbColumnName` yayýnlanmamýþ sertleþtirme çalýþmasýdýr.
- Schema-qualified table-name attribute kullanýmý identifier doðrulamasýna takýlýr.
- Ýndeks SQL’i otomatik çalýþtýrýlmaz.
- Foreign key parametreleri geliþmiþ quoting/cascade modellemesi saðlamaz.
- Table create/drop sýrasý foreign key dependency graph ile yönetilmez.
- Migration’lar tüm batch ve history ile tek atomik transaction garantisi vermez.
- Deðiþmiþ table script onarýmý yalnýz basit eksik kolon senaryosudur.
- SQLite type alter ve stored procedure desteklemez.
- PostgreSQL overloaded procedure desteklenmez.
- `DbColumnDefault` ve `DbColumnCheck` raw SQL kabul eder.

Bu sýnýrlar kütüphanenin kullanýlamaz olduðu anlamýna gelmez. Doðru kullaným alaný; açýk DDL üretimi, kontrollü schema initialization ve review edilmiþ SQL script yönetimidir.

# Sýk sorulan sorular

## 79. EF Core ile birlikte kullanabilir miyim?

Evet. ModelSync ORM olmadýðý için EF Core, Dapper veya ADO.NET ile birlikte kullanýlabilir. Ancak iki farklý migration otoritesi oluþturmamaya dikkat edin. Þema deðiþikliklerinin tek sahibi belirlenmelidir.

## 80. Yalnýz Core paketini kurmalý mýyým?

Normal kullanýcý hayýr. Provider paketi Core’u dependency olarak getirir. Yalnýz yeni provider geliþtirenler Core’u doðrudan kullanýr.

## 81. ModelSync model sýnýfýný veri entity’si olarak da kullanabilir miyim?

Teknik olarak evet; ancak sema modellerini ayri tutmak hala daha guvenlidir. ModelSync 1.1.0 DbIgnore destegi yardimci propertyleri haric tutabilir.

## 82. `ifNotExists: true` migration yerine geçer mi?

Hayýr. Yalnýz tablo yoksa create iþlemini güvenli hale getirir. Mevcut tablodaki kolon/tip/constraint farklarýný yönetmez.

## 83. Kolon ekledim, tablo otomatik güncellenir mi?

Yalnýz model property’sini eklemek database’i kendiliðinden deðiþtirmez. Þunlardan birini yapýn:

```csharp
await generator.AddColumnAsync<Model>(nameof(Model.NewProperty));
```

veya yeni, immutable SQL migration dosyasý ekleyin.

Model Synchronizer kullanýyorsanýz `CompareAsync()` + `ApplyAsync()` akýþý güvenli eksik kolonlarý otomatik ekleyebilir; riskli kolonlar yine bloklanýr.

## 84. Ýndeksler neden ayrý?

ModelSync indeks metadata’sýný tablo tanýmýndan ayrý SQL olarak üretir. Bu, indeksleri review etme ve provider’a uygun deployment adýmýnda yönetme esnekliði saðlar; ancak yürütme sorumluluðu kullanýcýdadýr.

## 85. Production’da startup sýrasýnda migration çalýþtýrmalý mýyým?

Tek instance, kontrollü küçük sistemlerde olabilir. Çok instance’lý production ortamýnda ayrý deployment job/console migration runner daha güvenlidir.

## 86. Hangi yaklaþýmý seçmeliyim?

| Ýhtiyaç | Öneri |
|---|---|
| Yeni prototipte hýzlý tablo oluþturma | Attribute generator + `ifNotExists` |
| DDL SQL’ini review edip DBA’ya verme | Yalnýz generator çýktýlarýný kullanma |
| Production sürüm deðiþiklikleri | Immutable SQL migration dosyalarý |
| Procedure source control | Stored procedure synchronizer |
| Runtime CRUD | Dapper/ADO.NET/EF Core gibi ayrý araç |

# Sonuç

ModelSync’in temel prensibi, þema deðiþikliðini görünür ve geliþtirici kontrollü tutmaktýr. Saðlýklý kullaným sýrasý þöyledir:

1. Doðru provider paketini kurun.
2. Þema modelini provider attribute’larýyla tanýmlayýn.
3. SQL’i üretin ve inceleyin.
4. Ayný generator örneðinde tabloyu oluþturun.
5. Ýndeksleri ayrýca yönetin.
6. Deðiþiklikleri açýk kolon operasyonu veya yeni migration scriptiyle yapýn.
7. Destructive iþlemleri yalnýz açýk izin, backup ve review ile çalýþtýrýn.
8. Stored procedure’lerde önce compare planý alýn.
9. Production’da migration dosyalarýný deðiþtirmeyin ve tek deployment otoritesi kullanýn.

Bu akýþla ModelSync; ORM yükü olmadan, provider’a özel DDL üretimi ve kontrollü database schema yönetimi için kullanýlabilir.

