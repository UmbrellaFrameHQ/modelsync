# ModelSync — NuGet Tam Kullanım Kılavuzu

Kurulum, model tanımlama, SQL üretimi, DDL yürütme, migration, stored procedure, canlı model senkronizasyonu ve production kullanımı.

**Sürüm kapsamı:** 1.0.8  
**Hazırlayan:** UmbrellaFrame / ModelSync

# İçindekiler

1. [Belge hakkında](#belge-hakkında)
2. [ModelSync nedir?](#1-modelsync-nedir)
3. [Kurulum](#kurulum)
4. [Temel çalışma modeli](#temel-çalışma-modeli)
5. [İlk tam örnek — MySQL/MariaDB](#ilk-tam-örnek--mysqlmariadb)
6. [Provider bazında hızlı başlangıç](#provider-bazında-hızlı-başlangıç)
7. [Attribute sistemi](#attribute-sistemi)
8. [Provider kolon tipleri](#provider-kolon-tipleri)
9. [SQL üretme API’si](#sql-üretme-apisi)
10. [Tablo ve kolon operasyonları](#tablo-ve-kolon-operasyonları)
11. [Dependency Injection ve uygulama başlangıcı](#dependency-injection-ve-uygulama-başlangıcı)
12. [Logging](#logging)
13. [Migration Runner](#migration-runner)
14. [Stored Procedure senkronizasyonu](#stored-procedure-senkronizasyonu)
15. [Canlı model senkronizasyonu](#canlı-model-senkronizasyonu)
16. [Analyzer](#analyzer)
17. [Hata yönetimi ve troubleshooting](#hata-yönetimi-ve-troubleshooting)
18. [Test yaklaşımı](#test-yaklaşımı)
19. [Production kullanım rehberi](#production-kullanım-rehberi)
20. [Tam örnek proje yapısı](#tam-örnek-proje-yapısı)
21. [API hızlı referans](#api-hızlı-referans)
22. [Sürüm 1.0.8 sınırları](#sürüm-108-sınırları)
23. [Sık sorulan sorular](#sık-sorulan-sorular)
24. [Sonuç](#sonuç)

# Belge hakkında

Bu kılavuz, **ModelSync 1.0.8** paketlerini NuGet üzerinden yükleyen bir .NET geliştiricisinin projeyi kaynak koda bakmadan doğru biçimde kullanabilmesi için hazırlanmıştır. Kurulumdan başlayarak model tanımlama, SQL üretme, tablo oluşturma, indeks yürütme, kolon işlemleri, migration scriptleri, stored procedure senkronizasyonu, dependency injection, logging, analyzer, test ve production güvenliği ele alınır.

> **En önemli tanım:** ModelSync bir ORM değildir. Nesneleri satırlara kaydetmez, LINQ sorgusu üretmez, change tracking yapmaz ve CRUD repository sağlamaz. ModelSync’in işi; C# model metadata’sından DDL üretmek, DDL’i isteğe bağlı çalıştırmak ve proje tarafındaki SQL scriptlerini kontrollü biçimde yönetmektir.

## 1. ModelSync nedir?

ModelSync, düz C# sınıflarını provider’a özel attribute’larla işaretleyerek SQL şema ifadeleri üretmenizi sağlayan, ORM bağımlılığı olmayan bir .NET kütüphanesidir.

Başlıca kullanım alanları:

- C# modelinden `CREATE TABLE` SQL’i üretmek.
- Üretilen tablo SQL’lerini veritabanında çalıştırmak.
- `DROP TABLE`, `TRUNCATE TABLE` ve `CREATE INDEX` SQL’leri üretmek.
- Attribute metadata’sına göre kolon eklemek, silmek, yeniden adlandırmak veya tip değiştirmek.
- Veri kaybına yol açabilecek işlemleri açık onay olmadan engellemek.
- SQL dosyası tabanlı migration scriptlerini kategorilere göre sıralayıp uygulamak.
- SQL Server, MySQL/MariaDB ve PostgreSQL stored procedure dosyalarını canlı veritabanıyla karşılaştırmak ve senkronize etmek.
- Roslyn analyzer ile eksik ModelSync attribute’larını derleme zamanında bildirmek.

## 2. ModelSync ne yapmaz?

| Beklenti | ModelSync davranışı |
|---|---|
| `Insert`, `Update`, `Delete`, `Select` işlemleri | Sağlamaz. Dapper, ADO.NET, EF Core veya başka bir veri erişim aracı kullanılır. |
| LINQ sorgu sağlayıcısı | Sağlamaz. |
| Entity change tracking | Sağlamaz. |
| Model değişince canlı veritabanını sessiz ve yıkıcı şekilde değiştirme | Yoktur. Model synchronizer dry-run-first çalışır ve yalnız güvenli additive işlemleri otomatik uygular. |
| Uygulanmış migration’ın her türlü şema farkını güvenle düzeltmesi | Sağlamaz. Otomatik onarım yalnız basit, eksik kolon ekleme yaklaşımıdır. |
| İndeksleri `CreateTables()` ile otomatik oluşturma | Yapmaz. `GenerateIndexSql<T>()` yalnız SQL döndürür; SQL ayrıca yürütülmelidir. |
| SQLite stored procedure | SQLite stored procedure desteklemediği için sağlanmaz. |
| İlişkisel model navigasyonları | Sağlamaz. Foreign key SQL’i attribute ile açık tanımlanır. |

## 3. Paket mimarisi ve hangi paket neden vardır?

| NuGet paketi | Amaç | Doğrudan kurulmalı mı? |
|---|---|---|
| `UmbrellaFrame.ModelSync.Core` | Ortak attribute’lar, arayüzler, SQL builder altyapısı, migration/stored procedure modelleri | Provider paketi otomatik getirir. Yalnız provider geliştirecekseniz doğrudan kurun. |
| `UmbrellaFrame.ModelSync.SqlServer` | SQL Server ve Azure SQL DDL/migration/stored procedure uygulaması | SQL Server kullanıyorsanız evet. |
| `UmbrellaFrame.ModelSync.MySql` | MySQL ve MariaDB uygulaması | MySQL/MariaDB kullanıyorsanız evet. |
| `UmbrellaFrame.ModelSync.PostgreSQL` | PostgreSQL uygulaması | PostgreSQL kullanıyorsanız evet. |
| `UmbrellaFrame.ModelSync.SQLite` | SQLite uygulaması | SQLite kullanıyorsanız evet. |
| `UmbrellaFrame.ModelSync.Analyzers` | Model attribute hatalarını IDE ve build sırasında bulur | İsteğe bağlı, tavsiye edilir. |

Paketler `netstandard2.0` hedefler. Bu nedenle modern .NET uygulamalarında kullanılabilir. Bu kılavuzdaki örnekler modern SDK stili projeler ve async kullanım üzerinden verilmiştir.

# Kurulum

## 4. Yeni proje oluşturma

```bash
dotnet new console -n ModelSyncDemo
cd ModelSyncDemo
```

ASP.NET Core kullanıyorsanız:

```bash
dotnet new webapi -n ModelSyncDemo
cd ModelSyncDemo
```

## 5. Provider paketini yükleme

Yalnız kullandığınız provider’ı yükleyin.

### SQL Server / Azure SQL

```bash
dotnet add package UmbrellaFrame.ModelSync.SqlServer --version 1.0.8
```

### MySQL / MariaDB

```bash
dotnet add package UmbrellaFrame.ModelSync.MySql --version 1.0.8
```

### PostgreSQL

```bash
dotnet add package UmbrellaFrame.ModelSync.PostgreSQL --version 1.0.8
```

### SQLite

```bash
dotnet add package UmbrellaFrame.ModelSync.SQLite --version 1.0.8
```

### Analyzer

```bash
dotnet add package UmbrellaFrame.ModelSync.Analyzers --version 1.0.8
```

`--version` kaldırılırsa NuGet’teki mevcut kararlı sürüm yüklenir. Bu belge 1.0.8 API’sine göre hazırlanmıştır.

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

# Temel çalışma modeli

## 7. ModelSync akışı neden iki aşamalıdır?

ModelSync tablo işlemlerini iki aşamaya ayırır:

1. `Generate...Table<T>()` modeli okur, SQL üretir ve generator örneğinin iç önbelleğine kaydeder.
2. `CreateTables()` veya `CreateTablesAsync()` önbellekteki SQL’leri veritabanında çalıştırır.

Bu ayrım şu yararları sağlar:

- SQL’i çalıştırmadan önce görebilirsiniz.
- Review, log veya test yapabilirsiniz.
- Birden fazla tabloyu kaydedip sonra toplu çalıştırabilirsiniz.
- SQL üretimi ile canlı veritabanı değişikliğini birbirinden ayırabilirsiniz.

```csharp
var generator = new MySqlTableGenerator(connectionString);

var sql = generator.GenerateMySqlTable<Product>(ifNotExists: true);
Console.WriteLine(sql);          // yalnız üretir ve cache'e alır

await generator.CreateTablesAsync(); // cache'teki SQL'i çalıştırır
```

> Yeni bir generator örneği oluşturursanız önceki örneğin cache’i taşınmaz. `CreateTablesAsync()` çağrısı aynı generator örneğinde yapılmalıdır.

# İlk tam örnek — MySQL/MariaDB

## 8. Model tanımlama

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

## 9. Generator oluşturma ve database hazırlama

```csharp
var connectionString =
    "Server=localhost;Port=3306;Database=shopdb;User ID=root;Password=secret;";

var generator = new MySqlTableGenerator(connectionString);

// Kullanıcının CREATE DATABASE yetkisi varsa çalıştırın.
await generator.CreateDatabaseAsync();
```

`CreateDatabaseAsync()` connection string içindeki `Database` değerini alır, database’siz bağlantı açar ve `CREATE DATABASE IF NOT EXISTS` çalıştırır. Veritabanı başka bir süreç tarafından oluşturuluyorsa bu adımı atlayabilirsiniz.

## 10. SQL üretme, inceleme ve tablo oluşturma

```csharp
var createSql = generator.GenerateMySqlTable<Product>(ifNotExists: true);
Console.WriteLine(createSql);

await generator.CreateTablesAsync();
```

Beklenen yapıya benzer SQL:

```sql
CREATE TABLE IF NOT EXISTS `products` (
    `Id` INT PRIMARY KEY AUTO_INCREMENT,
    `Name` VARCHAR(200) NOT NULL,
    `Price` DECIMAL(18,2) DEFAULT 0.00 CHECK (Price >= 0),
    `Stock` INT DEFAULT 0,
    `CreatedAt` DATETIME DEFAULT CURRENT_TIMESTAMP
);
```

## 11. İndeks SQL’lerini üretme ve çalıştırma

`DbColumnIndex` tablo SQL’inin içine eklenmez. İndeksler ayrı SQL listesi olarak üretilir:

```csharp
var indexSqlList = generator.GenerateIndexSql<Product>();

foreach (var indexSql in indexSqlList)
{
    Console.WriteLine(indexSql);
}
```

MySQL’de çalıştırma örneği:

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

> Aynı indeks ikinci kez oluşturulursa provider hata verebilir. İndeks yürütmesini migration scriptine almak veya veritabanı kataloğundan varlık kontrolü yapmak production için daha güvenlidir.

# Provider bazında hızlı başlangıç

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

// SQL Server provider CreateTablesAsync içinde CreateDatabaseAsync de çağırır.
var sql = generator.GenerateSqlServerTable<Product>(ifNotExists: true);
Console.WriteLine(sql);
await generator.CreateTablesAsync();
```

SQL Server inline `CREATE TABLE IF NOT EXISTS` desteklemediği için provider `OBJECT_ID` guard bloğu üretir.

### SQL Server kullanım notu

`ifNotExists: true` kullanırken doğrudan provider metodunu tercih edin:

```csharp
generator.GenerateSqlServerTable<Product>(ifNotExists: true);
```

Generic ve async üretim çağrıları da SQL Server provider override davranışını kullanır; yine de okunabilirlik için provider-specific metodu tercih edebilirsiniz:

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

// PostgreSQL provider CreateTablesAsync database oluşturmayı otomatik çağırmaz.
await generator.CreateDatabaseAsync();
generator.GeneratePostgresTable<Product>(ifNotExists: true);
await generator.CreateTablesAsync();
```

PostgreSQL kimlik/otomatik sayı için `SERIAL` veya `BIGSERIAL` kolon tipi kullanılır. `PostgresColumnPrimaryKey` ayrıca `PRIMARY KEY` üretir.

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

SQLite database dosyasını ilk bağlantıda oluşturur. `CreateDatabase()` ve `CreateDatabaseAsync()` no-op’tur.

### SQLite bellek içi test

`Data Source=:memory:` database’i bağlantı kapanınca yok olur. ModelSync her yürütmede kendi bağlantısını açıp kapattığı için daha sonra aynı veritabanına erişmeniz gerekiyorsa named shared memory ve açık tutulan bir keeper connection kullanın:

```csharp
using Microsoft.Data.Sqlite;
using UmbrellaFrame.ModelSync.SQLite;

var cs = "Data Source=ModelSyncTests;Mode=Memory;Cache=Shared";

await using var keeper = new SqliteConnection(cs);
await keeper.OpenAsync();

var generator = new SQLiteTableGenerator(cs);
generator.GenerateSQLiteTable<Product>(ifNotExists: true);
await generator.CreateTablesAsync();

// keeper açık kaldığı sürece başka bağlantılar aynı in-memory database'i görür.
```

### SQLite sınırlamaları

- Stored procedure desteklenmez.
- `ALTER COLUMN TYPE` doğrudan desteklenmez; provider `NotSupportedException` fırlatır.
- Tip değişikliği için create-copy-drop/rename stratejisi gerekir.
- `GenerateTruncateTableSql<T>()` SQLite provider’da `DELETE FROM "Table";` üretir; çünkü SQLite `TRUNCATE TABLE` komutunu desteklemez.

# Attribute sistemi

## 15. Tablo adı attribute’ları

| Provider | Kullanım |
|---|---|
| SQL Server | `[SqlServerTableName("Products")]` |
| MySQL/MariaDB | `[MySqlTableName("products")]` |
| PostgreSQL | `[PostgresTableName("products")]` |
| SQLite | `[SQLiteTableName("products")]` |

Tablo adı verilmezse class adı kullanılır. Buna rağmen açık tablo adı kullanmak tavsiye edilir; refactor sırasında database adı istemeden değişmez.

## 16. Kolon tipi attribute’ları

Her public property’nin provider’a uygun kolon tipi attribute’ü olmalıdır.

```csharp
[MySqlColumnType(MySqlColumnType.VARCHAR, "255")]
public string Email { get; set; } = string.Empty;
```

Kolon adı property adıdır. 1.0.8’de ayrı bir column-name override attribute’ü yoktur.

## 17. Primary key

```csharp
[MySqlColumnType(MySqlColumnType.INT)]
[MySqlColumnPrimaryKey(isAutoIncrement: true)]
public int Id { get; set; }
```

Provider karşılıkları:

| Provider | Attribute | Auto increment yaklaşımı |
|---|---|---|
| SQL Server | `SqlServerColumnPrimaryKey(isAutoIncrement: true)` | `IDENTITY(1,1)` |
| MySQL | `MySqlColumnPrimaryKey(isAutoIncrement: true)` | `AUTO_INCREMENT` |
| PostgreSQL | `PostgresColumnPrimaryKey` | Otomatik artış için kolon tipi `SERIAL`/`BIGSERIAL` seçilir. |
| SQLite | `SQLiteColumnPrimaryKey` | 1.0.8 `PRIMARY KEY AUTOINCREMENT` üretir; yalnız `INTEGER` kolonla kullanın. |

## 18. Composite primary key

Birden fazla property primary key attribute’ü taşıyorsa generator table-level composite key üretir:

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

Üretilen yapı:

```sql
PRIMARY KEY (`UserId`, `RoleId`)
```

Composite key property’lerinde auto increment kullanmayın. Table-level composite key üretiminde property-level auto increment snippet’i kullanılmaz.

## 19. NOT NULL

```csharp
[MySqlColumnNotNull]
public string Name { get; set; } = string.Empty;
```

Provider attribute’ları:

- `SqlServerColumnNotNull`
- `MySqlColumnNotNull`
- `PostgresColumnNotNull`
- `SQLiteColumnNotNull`

C# nullable/non-nullable durumu otomatik SQL’e çevrilmez. SQL nullability yalnız attribute ile belirlenir.

## 20. UNIQUE

```csharp
[MySqlColumnUnique]
public string Sku { get; set; } = string.Empty;
```

Provider attribute’ları:

- `SqlServerColumnUnique`
- `MySqlColumnUnique`
- `PostgresColumnUnique`
- `SQLiteColumnUnique`

Bu attribute column-level `UNIQUE` constraint üretir. Ayrı isimli bir unique indeks istiyorsanız `DbColumnIndex(..., isUnique: true)` kullanın.

## 21. DEFAULT

`DbColumnDefault` Core paketindedir ve tüm provider’larda kullanılır:

```csharp
[DbColumnDefault("0")]
public int Stock { get; set; }

[DbColumnDefault("CURRENT_TIMESTAMP")]
public DateTime CreatedAt { get; set; }

[DbColumnDefault("'Active'")]
public string Status { get; set; } = string.Empty;
```

`DbColumnDefault` değeri **raw SQL**’dir. String default için SQL quote’larını sizin vermeniz gerekir.

> Kullanıcı girdisini, HTTP parametresini veya dış kaynaktan gelen metni `DbColumnDefault` içine yerleştirmeyin.

## 22. CHECK

```csharp
[DbColumnCheck("Price >= 0")]
public decimal Price { get; set; }
```

Üretilen bölüm:

```sql
CHECK (Price >= 0)
```

İfade raw SQL’dir. Provider’ın desteklediği SQL sözdizimini kullanın ve dış girdiden üretmeyin.

## 23. İndeks

```csharp
[DbColumnIndex]
public string Name { get; set; } = string.Empty;

[DbColumnIndex("idx_users_email", isUnique: true)]
public string Email { get; set; } = string.Empty;
```

İsim verilmezse:

```text
idx_{table}_{property}
```

formatı kullanılır.

`DbColumnIndex` yalnız `GenerateIndexSql<T>()` çıktısına etki eder. `CreateTables()` indeksleri yürütmez.

## 24. Foreign key

Provider’ların foreign key attribute adları:

| Provider | Attribute |
|---|---|
| SQL Server | `SqlServerColumnForeignKey` |
| MySQL | `MySqlForeignKey` |
| PostgreSQL | `PostgresForeignKey` |
| SQLite | `SQLiteColumnForeignKey` |

MySQL örneği:

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

1. Yerel kolon adı.
2. Referans tablo adı.
3. Referans kolon adı.

### Foreign key kullanım kuralları

- Parametre adlarını property ve gerçek database adlarıyla birebir eşleştirin.
- Boşluk, tire, nokta veya schema-qualified ad kullanmayın; 1.0.8 foreign key snippet’i bu adları ayrıca quote etmez.
- Parent tabloyu child tablodan önce oluşturun.
- Aynı generator cache’indeki tablo yürütme sırası bağımlılık sırasını garanti etmez. İlişkili tablolar için ayrı kontrollü aşamalar veya migration scriptleri tercih edin.
- Cascade seçenekleri için 1.0.8’de hazır attribute parametresi yoktur; migration scripti kullanın.

# Provider kolon tipleri

## 25. SQL Server kolon tipleri

`SqlServerColumnType` enum değerleri:

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

`MySqlColumnType` enum değerleri:

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

Enum değerlerinden MySQL `ENUM` üretme:

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

Üretim enum isimlerini SQL string değerleri olarak kullanır. Enum adı değişikliklerini migration olarak yönetin.

## 27. PostgreSQL kolon tipleri

`PostgresColumnType` enum değerleri:

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

`ARRAY`, `RANGE` ve bazı extension tabanlı tiplerde üretilecek SQL’i mutlaka kontrol edin; 1.0.8 yalnız enum adını/uzunluğu birleştirir ve gelişmiş type declaration modellemesi yapmaz.

## 28. SQLite kolon tipleri

`SQLiteColumnType` enum değerleri:

```text
INTEGER, REAL, TEXT, BLOB, NUMERIC
```

Önerilen eşlemeler:

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

Provider alias metotları:

```csharp
GenerateSqlServerTable<T>()
GenerateMySqlTable<T>()
GeneratePostgresTable<T>()
GenerateSQLiteTable<T>()
```

## 30. SQL üretip hiç çalıştırmama

ModelSync, yalnız SQL generator olarak da kullanılabilir:

```csharp
var generator = new PostgresTableGenerator(connectionString);

var create = generator.GeneratePostgresTable<Customer>(true);
var drop = generator.GenerateDropTableSql<Customer>();
var truncate = generator.GenerateTruncateTableSql<Customer>();
var indexes = generator.GenerateIndexSql<Customer>();

File.WriteAllText("customer-create.sql", create);
```

Bu kullanım CI’da DDL snapshot testleri veya manuel DBA review süreci için uygundur.

## 31. Identifier güvenliği

Tablo, kolon ve indeks adları şu desene uymalıdır:

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

Schema-qualified tablo adları doğrudan table-name attribute’ünde kullanılamaz. Schema ihtiyacı olan gelişmiş yapılar için migration scripti tercih edin.

# Tablo ve kolon operasyonları

## 32. Kolon ekleme

Önce yeni property’yi modelde attribute’larıyla tanımlayın:

```csharp
[MySqlColumnType(MySqlColumnType.INT)]
[DbColumnDefault("0")]
public int Stock { get; set; }
```

Sonra property adını vererek ekleyin:

```csharp
await generator.AddColumnAsync<Product>(
    nameof(Product.Stock),
    cancellationToken);
```

Kolon tanımı model attribute’larından okunur. `nameof` kullanmak refactor güvenliği sağlar.

## 33. Kolon yeniden adlandırma

```csharp
await generator.RenameColumnAsync<Product>(
    oldColumnName: "OldName",
    newColumnName: "Name",
    cancellationToken);
```

Provider sözdizimleri farklıdır:

- SQL Server `sp_rename` kullanır.
- Modern MySQL, PostgreSQL ve SQLite standarda yakın `RENAME COLUMN` kullanır.

Database sürümünüzün bu komutu desteklediğini doğrulayın.

## 34. Kolon silme

Kolon silmek veri kaybıdır ve açık izin ister:

```csharp
var destructive = DestructiveOperationOptions.Allow();

await generator.DropColumnAsync<Product>(
    nameof(Product.LegacyCode),
    destructive,
    cancellationToken);
```

Aşağıdaki çağrı tasarım gereği exception fırlatır:

```csharp
await generator.DropColumnAsync<Product>(nameof(Product.LegacyCode));
```

## 35. Kolon tipi değiştirme

Modelde property’nin type attribute’ünü yeni SQL tipiyle güncelledikten sonra:

```csharp
var destructive = DestructiveOperationOptions.Allow();

await generator.AlterColumnTypeAsync<Product>(
    nameof(Product.Price),
    destructive,
    cancellationToken);
```

Dikkat edilmesi gerekenler:

- Tip dönüşümü mevcut verilerle uyumsuzsa provider hata verir.
- ModelSync otomatik veri dönüştürme veya `USING` ifadesi oluşturmaz.
- PostgreSQL karmaşık dönüşümlerde manuel SQL gerekebilir.
- SQLite bunu desteklemez ve `NotSupportedException` fırlatır.

## 36. Tabloları silme

Yalnız generator cache’ine daha önce alınmış model tabloları hedeflenir:

```csharp
generator.GenerateMySqlTable<User>();
generator.GenerateMySqlTable<Product>();

await generator.DropTablesAsync(
    DestructiveOperationOptions.Allow(),
    cancellationToken);
```

Tablolar arası foreign key varsa drop sırası hata üretebilir. Production’da bağımlılık sıralı migration scriptleri kullanın.

## 37. Truncate SQL’i

```csharp
var sql = generator.GenerateTruncateTableSql<Product>();
```

Bu metot yalnız SQL döndürür; yürütme metodu yoktur. `TRUNCATE` veri kaybına yol açar ve Core API bunu ayrıca guard etmez. Çalıştırmadan önce kendi güvenlik politikanızı uygulayın.

# Dependency Injection ve uygulama başlangıcı

## 38. Tavsiye edilen servis ömrü

Generator örneği mutable SQL cache taşır.

- Yalnız startup schema initialization için kullanılan tek bir servis: singleton kullanılabilir.
- Farklı operasyonların cache paylaşmasını istemiyorsanız: scoped veya transient tercih edin.
- Request başına tablo üretmek genellikle doğru değildir; schema değişikliklerini kontrollü startup/deployment adımında çalıştırın.

## 39. ASP.NET Core kaydı — SQL Server

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

Connection string’i kaynak koda gömmeyin. Production’da environment variable, secret manager veya platform secret store kullanın.

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

SQL Server 1.0.8’de provider-specific `ifNotExists` guard’ı için initializer’a doğrudan `SqlServerTableGenerator` enjekte edip `GenerateSqlServerTable<T>(true)` kullanın.

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

Kayıt:

```csharp
builder.Services.AddSingleton<SchemaInitializer>();
builder.Services.AddHostedService<SchemaInitializerHostedService>();
```

> Birden fazla uygulama instance’ı aynı anda başlıyorsa schema işlemlerinin eş zamanlı çalışması risklidir. Production migration’ını deployment job olarak tek instance üzerinden çalıştırmak daha güvenlidir.

# Logging

## 43. Logger kullanımı

Provider constructor’ları opsiyonel `ILogger<T>` kabul eder:

```csharp
var generator = new MySqlTableGenerator(connectionString, logger);
```

SQL üretimi debug seviyesinde, bazı migration işlemleri information seviyesinde loglanır. Connection string ve şifreleri loglamayın.

Console app örneği:

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

## 44. Ne zaman migration runner kullanılmalı?

Attribute tabanlı generator basit ve açık model DDL’i için uygundur. Aşağıdaki ihtiyaçlarda SQL migration dosyaları daha doğru olur:

- Schema, extension, view veya gelişmiş constraint oluşturma.
- Veri dönüşümü.
- Seed data.
- Trigger.
- Provider’a özgü karmaşık SQL.
- İndeks varlık kontrolleri.
- Uygulama sürümleri arasında açık, sıralı database değişiklikleri.

## 45. Önerilen klasör yapısı

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

Kategori sırası:

```text
Tables -> StoredProcedures -> Triggers -> Seeds
```

Kategori içinde dosya adının `_` öncesindeki numeric ID’si sıralamada kullanılır.

```text
001_CreateProducts.sql
```

şöyle çözülür:

```text
Id   = 001
Name = CreateProducts
```

## 46. SQL Server migration runner örneği

```csharp
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.SqlServer;

var options = new MigrationRunnerOptions
{
    EnsureHistoryTables = true,

    // Production için uygulanmış migration dosyalarını değiştirmeyin.
    // Otomatik eksik kolon onarımını kapatmak daha güvenli bir varsayımdır.
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

## 47. Diğer migration runner sınıfları

```csharp
var mysqlRunner = new MySqlMigrationRunner(connectionString, options);
var postgresRunner = new PostgresMigrationRunner(connectionString, options);
var sqliteRunner = new SQLiteMigrationRunner(connectionString, options);
```

## 48. Inline migration tanımı

```csharp
var definition = MigrationScriptDefinition.Create(
    id: "001",
    name: "CreateProducts",
    category: MigrationScriptCategory.Tables,
    sql: "CREATE TABLE ...;",
    source: "inline");

runner.RegisterScript(definition);
```

## 49. Kategori veya ID’yi açık verme

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

Kayıt:

```csharp
using System.Reflection;

runner.RegisterEmbeddedScripts(
    Assembly.GetExecutingAssembly(),
    "MyApplication.Database.Scripts.");

await runner.RunAsync(cancellationToken);
```

Yalnız `.sql` ile biten resource’lar alınır.

## 51. Migration planı

`MigrationSyncPlan` temel alanları:

| Alan | Anlamı |
|---|---|
| `Definition` | Script ID, ad, kategori, SQL ve kaynak bilgisi |
| `ChangeType` | `None`, `Apply`, `Reapply` |
| `CurrentHash` | History tablosundaki mevcut hash |
| `TargetHash` | Proje SQL’inin hesaplanan hash’i |
| `SqlToApply` | Uygulanacak SQL |
| `Reason` | Planın neden bu durumda olduğu |
| `HasChanges` | `ChangeType != None` |

## 52. History tabloları

Kategori başına bir tablo kullanılır:

```text
SchemaMigration_Tables
SchemaMigration_StoredProcedures
SchemaMigration_Triggers
SchemaMigration_Seeds
```

Temel olarak şu bilgiler saklanır:

- `Id`
- `Name`
- `SqlHash`
- `AppliedAt`
- `UpdateAt`

History tablosu migration’ın daha önce uygulanıp uygulanmadığını ve SQL hash’inin değişip değişmediğini takip eder.

## 53. Database reset

Reset tüm database’i etkileyebilecek yıkıcı bir işlemdir:

```csharp
var options = new MigrationRunnerOptions
{
    ResetDatabase = true,
    DestructiveOptions = DestructiveOperationOptions.Allow()
};

var runner = new SqlServerMigrationRunner(connectionString, options);
await runner.RunAsync(cancellationToken);
```

Açık destructive izin verilmezse işlem başlamadan exception oluşur. SQLite runner reset desteklemez.

## 54. SQL Server `GO`

SQL Server migration runner, tek satırdaki `GO` batch separator’larını ayırır:

```sql
CREATE TABLE dbo.Products (...);
GO
CREATE INDEX IX_Products_Name ON dbo.Products(Name);
GO
```

Stored procedure synchronizer dosyalarında `GO` kullanmayın. Migration runner ile stored procedure scripti çalıştırıyorsanız batch yapısını dikkatle test edin.

## 55. Migration production güvenlik kuralları

ModelSync 1.0.8 kullanırken aşağıdaki kuralları zorunlu süreç kabul edin:

1. **Uygulanmış migration dosyasını değiştirmeyin.** Yeni değişiklik için yeni ID’li dosya ekleyin.
2. Production’da `AutoAddMissingColumnsFromTableScripts = false` önerilir.
3. `CompareRegisteredAsync()` çıktısını loglayın veya onaylayın.
4. Scriptlerin idempotent olmasını sağlayın veya yalnız bir kez çalışacağını garanti edin.
5. Database yedeği alın.
6. Aynı migration runner’ı eş zamanlı birden fazla uygulama instance’ında çalıştırmayın.
7. Başarısızlık sonrası database’i kontrol etmeden tekrar çalıştırmayın.
8. 1.0.8’de batch/script/history işlemleri tüm provider’larda tek atomik transaction olarak garanti edilmez.
9. Otomatik eksik kolon onarımı kolon tipi, constraint, rename veya drop farkını çözmez.
10. Duplicate migration ID kullanmayın; ID’leri repository seviyesinde unique tutun.

# Stored Procedure senkronizasyonu

## 56. Ne için kullanılır?

Stored procedure SQL dosyanız proje tarafında source of truth olur. Synchronizer:

- Procedure yoksa `Create` planı üretir.
- Procedure varsa ve SQL farklıysa `Alter` planı üretir.
- Aynıysa `None` üretir.
- Planı uyguladığınızda provider’a uygun create/replace stratejisini çalıştırır.

Destek:

| Provider | Destek | Uygulama stratejisi |
|---|---|---|
| SQL Server / Azure SQL | Var | `CREATE OR ALTER PROCEDURE` |
| MySQL / MariaDB | Var | `DROP PROCEDURE IF EXISTS` + `CREATE PROCEDURE` |
| PostgreSQL | Var | `CREATE OR REPLACE PROCEDURE` |
| SQLite | Yok | `NotSupportedException` |

## 57. Önerilen dosya yapısı

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

Dosya adı `schema.procedure.sql` biçimindeyse schema ve procedure adı otomatik çözülür.

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

MySQL procedure değişikliğinde mevcut procedure drop edilir ve yeniden oluşturulur. Create başarısız olursa procedure geçici olarak bulunmayabilir; production review ve bakım penceresi uygulayın.

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

1.0.8 PostgreSQL overloaded procedure signature’larını desteklemez. Aynı schema ve adla farklı parametre listesine sahip procedure’ler kullanıyorsanız manuel migration yönetin.

## 61. Inline stored procedure tanımı

```csharp
var definition = StoredProcedureDefinition.Create(
    name: "usp_GetProducts",
    sql: sqlText,
    schema: "dbo");

synchronizer.RegisterProcedure(definition);
```

## 62. Tek procedure karşılaştırma ve uygulama

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

## 63. SQL dosyası kuralları

- Her dosya tek procedure tanımı içermelidir.
- SQL içindeki procedure adı kayıt edilen adla eşleşmelidir.
- SQL Server dosyasında `CREATE PROCEDURE`, `CREATE PROC`, `ALTER PROCEDURE` veya `CREATE OR ALTER PROCEDURE` kullanılabilir.
- MySQL dosyasında `CREATE PROCEDURE` kullanılmalıdır.
- PostgreSQL dosyasında `CREATE PROCEDURE`, `ALTER PROCEDURE` veya `CREATE OR REPLACE PROCEDURE` kullanılabilir.
- Stored procedure synchronizer dosyasında SQL Server `GO` kullanmayın.
- Dry-run için önce `Compare...` çağrısı yapın.

# Canlı model senkronizasyonu

Model synchronizer sınıfları, 1.0.8 ile gelen dry-run-first canlı veritabanı karşılaştırma katmanıdır.

Bu katmanı şu sorular için kullanın:

- Hangi tablolar eksik?
- Hangi kolonlar eksik?
- Hangi indeks veya desteklenen constraint eksik?
- Hangi farklar riskli/yıkıcı ve manuel review gerektiriyor?
- Hangi proje SQL scriptleri çalışmalı?

## Provider API'leri

| Provider | Options | Synchronizer |
|---|---|---|
| SQL Server / Azure SQL | `SqlServerModelSyncOptions` | `SqlServerModelSynchronizer` |
| MySQL / MariaDB | `MySqlModelSyncOptions` | `MySqlModelSynchronizer` |
| PostgreSQL | `PostgresModelSyncOptions` | `PostgresModelSynchronizer` |
| SQLite | `SQLiteModelSyncOptions` | `SQLiteModelSynchronizer` |

## SQL Server örneği

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

Assembly içinde test modeli, eski şema modeli veya DTO varsa `FromTypes` kullanın:

```csharp
var result = await SqlServerModelSynchronizer
    .FromTypes(options, typeof(ProductSchema), typeof(CustomerSchema))
    .CompareAsync(cancellationToken);
```

## Otomatik uygulanabilen güvenli işlemler

- Eksik tablo oluşturma.
- Eksik nullable kolon ekleme.
- Default değeri olan eksik `NOT NULL` kolon ekleme.
- Eksik indeks ekleme.
- Provider güvenli ALTER sözdizimi destekliyorsa eksik default/check/unique/foreign key constraint ekleme.
- History/hash takipli sıralı SQL scriptleri.

## Bloklanan işlemler

- Model setinde bulunmayan canlı database tabloları `DropTable` olarak raporlanır ve bloklanır.
- Modelde bulunmayan canlı database kolonları `DropColumn` olarak raporlanır ve bloklanır.
- Rename, tip değişikliği ve nullable-to-not-null değişiklikleri bloklanır.
- Mevcut tabloya defaultsuz `NOT NULL` kolon eklemek bloklanır.
- SQLite stored procedure scriptleri desteklenmez.

`AllowDestructiveChanges`, model diff içindeki drop/rename/type-change işlemlerini otomatik yapmaz. Bu seçenek migration runner reset gibi yıkıcı runner işlemlerine aktarılır. Model diff tarafındaki yıkıcı işlemler review-only kalır.

## Script seçenekleri

`ApplyStoredProceduresOnEveryRun` ve `ApplyTriggersOnEveryRun`, idempotent scriptleri her çalıştırmada doğrudan uygular.

`ApplySeedsWithHashTracking` ve `ApplyCustomSqlWithHashTracking` varsayılan olarak `true` değerindedir. True iken seed ve custom SQL scriptleri migration history/hash ile uygulanır. False yapılırsa her çalıştırmada doğrudan uygulanırlar.

Odaklı referans için [14 - Model Synchronizer](14-model-synchronizer.md) belgesine bakın.

# Analyzer

## 64. Neden kullanılmalı?

Runtime’da SQL üretirken karşılaşacağınız bazı model hatalarını daha kod yazarken gösterir.

Kurulum:

```bash
dotnet add package UmbrellaFrame.ModelSync.Analyzers --version 1.0.8
```

## 65. Analyzer kuralları

| Kural | Şiddet | Anlamı |
|---|---|---|
| `MSYNC001` | Warning | Public property’de provider column type attribute’ü eksik |
| `MSYNC002` | Warning | Column type kullanılan class’ta table-name attribute’ü eksik |
| `MSYNC003` | Warning | Modelde primary key attribute’ü eksik |

CI’da error yapmak için `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.MSYNC001.severity = error
dotnet_diagnostic.MSYNC002.severity = error
dotnet_diagnostic.MSYNC003.severity = error
```

Kuralı lokal bastırma:

```csharp
#pragma warning disable MSYNC003
// kasıtlı primary-key'siz model
#pragma warning restore MSYNC003
```

> Analyzer bir yardımcı kontroldür; generated SQL review ve integration test yerine geçmez. Özellikle provider’a özgü edge case’leri test edin.

# Hata yönetimi ve troubleshooting

## 66. Sık görülen hatalar

### “Column has no type attribute”

Neden: Public property üzerinde provider `ColumnType` attribute’ü yok.

Çözüm:

```csharp
[MySqlColumnType(MySqlColumnType.INT)]
public int Count { get; set; }
```

ModelSync 1.0.8 tüm public property’leri kolon kabul eder. Ignore/NotMapped attribute’ü yoktur. Database kolonu olmayacak hesaplanmış public property’yi ayrı DTO’ya taşıyın veya public model yapısını ayırın.

### “Invalid SQL identifier”

Neden: Tablo, kolon, indeks veya database adı güvenli identifier desenine uymuyor.

Çözüm: Yalnız harf/underscore ile başlayan, devamında harf/rakam/underscore içeren ad kullanın.

### “... is destructive and may cause data loss”

Neden: `DropTables`, `DropColumn` veya `AlterColumnType` açık izin olmadan çağrıldı.

Çözüm:

```csharp
var allow = DestructiveOperationOptions.Allow();
```

İşlemi gerçekten istediğinizi doğruladıktan sonra ilgili overload’a verin.

### `CreateTablesAsync()` hiçbir şey yapmıyor

Neden: Generator cache’i boş.

Çözüm: Aynı generator örneğinde önce `Generate...Table<T>()` çağırın.

### Foreign key oluştururken referenced table bulunamadı

Neden: Parent tablo henüz oluşturulmadı veya cache yürütme sırası bağımlılığı karşılamadı.

Çözüm: Parent tabloyu ayrı aşamada önce oluşturun veya migration scripti kullanın.

### İndeks oluşmadı

Neden: `GenerateIndexSql<T>()` yalnız SQL üretir.

Çözüm: SQL’i ADO.NET ile ayrıca yürütün veya migration scriptine taşıyın.

### SQLite truncate davranışı

Neden: SQLite `TRUNCATE TABLE` desteklemez. SQLite provider bu nedenle `DELETE FROM` SQL’i üretir.

Üretilen örnek:

```sql
DELETE FROM "products";
```

### SQLite “ALTER COLUMN” hatası

Neden: SQLite doğrudan type alter desteklemez.

Çözüm: Yeni tablo oluştur, veriyi dönüştürerek kopyala, eski tabloyu sil, yeni tabloyu yeniden adlandır.

### Database oluşturma yetki hatası

Neden: Connection kullanıcısında `CREATE DATABASE` yetkisi yok.

Çözüm: Database’i DBA/deployment ile önceden oluşturun ve `CreateDatabase()` çağrısını kaldırın.

## 67. CancellationToken kullanımı

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

await generator.CreateTablesAsync(cts.Token);
```

Migration ve stored procedure metotları da cancellation token kabul eder. Cancellation, database’in o ana kadar yaptığı değişiklikleri otomatik geri alacağı anlamına gelmez.

# Test yaklaşımı

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

SQL üretimi bağlantı açmadan yapılır; connection string constructor doğrulamasını geçecek biçimde dolu olmalıdır.

## 69. Integration test

Gerçek provider üzerinde şu akışı test edin:

1. Test database/container oluştur.
2. Model SQL’ini üret.
3. Tabloyu oluştur.
4. Provider kataloğundan tablo/kolon/constraint kontrolü yap.
5. Test verisi ekle.
6. Add/Rename/Alter/Drop senaryolarını ayrı database’te dene.
7. Test database’ini temizle.

SQLite shared-memory küçük testler için uygundur; SQL Server/MySQL/PostgreSQL davranışının birebir yerine geçmez.

# Production kullanım rehberi

## 70. ModelSync’i production’da hangi biçimde kullanmalıyım?

Önerilen ayrım:

### Basit uygulama/prototip

- Attribute model.
- Generated SQL review.
- `ifNotExists: true`.
- Startup initializer.

### Kurumsal/production uygulama

- Attribute generator’ı DDL üretimi ve test için kullanın.
- Gerçek sürüm değişikliklerini immutable migration scriptleriyle yönetin.
- Migration’ı uygulama request trafiği başlamadan, tek deployment job’da çalıştırın.
- Dry-run planı loglayın ve onaylayın.
- Database yedeği ve rollback scripti hazırlayın.
- Stored procedure değişikliklerini compare + review sonrasında uygulayın.

## 71. Production checklist

- [ ] Doğru provider paketi kuruldu.
- [ ] Connection string secret store’dan geliyor.
- [ ] Tüm public model property’lerinde doğru column type attribute’ü var.
- [ ] Tablo ve identifier adları güvenli desene uyuyor.
- [ ] Generated SQL code review’den geçti.
- [ ] İndekslerin ayrıca yürütüldüğü doğrulandı.
- [ ] Foreign key parent/child sırası kontrol edildi.
- [ ] Raw default/check ifadelerinde dış girdi yok.
- [ ] Destructive operasyonlar ayrı maintenance adımında.
- [ ] Production migration dosyaları immutable.
- [ ] `AutoAddMissingColumnsFromTableScripts` production’da bilinçli ayarlandı.
- [ ] Database backup/restore prosedürü test edildi.
- [ ] Migration tek instance tarafından çalıştırılıyor.
- [ ] Integration test gerçek provider sürümünde geçti.
- [ ] Stored procedure planları uygulanmadan önce incelendi.
- [ ] SQLite sınırlamaları dikkate alındı.

# Tam örnek proje yapısı

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

Şema modellerini domain entity veya API DTO’larından ayırmak, 1.0.8’de tüm public property’lerin kolon kabul edilmesi nedeniyle faydalıdır.

## 73. Uçtan uca SQL Server startup örneği

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

        // İndeksleri production'da tercihen migration scriptiyle yönetin.
    }
}
```

# API hızlı referans

## 74. `ITableGenerator`

| Metot | Amaç |
|---|---|
| `GenerateSqlTable<T>()` | CREATE TABLE SQL üretir ve cache’e alır. |
| `GenerateSqlTableAsync<T>()` | Aynı üretimin Task tabanlı biçimi. |
| `GenerateDropTableSql<T>()` | DROP TABLE SQL döndürür. |
| `GenerateTruncateTableSql<T>()` | Provider'a özel truncate/delete SQL'i döndürür. |
| `GenerateIndexSql<T>()` | Index SQL listesi döndürür. |
| `CreateDatabase()` / Async | Provider’a göre database oluşturur; SQLite no-op. |
| `CreateTables()` / Async | Cache’teki CREATE TABLE SQL’lerini çalıştırır. |
| `DropTables(options)` / Async | Cache’teki tabloları açık destructive izinle siler. |
| `AddColumn<T>()` / Async | Property metadata’sından kolon ekler. |
| `DropColumn<T>(..., options)` / Async | Kolon siler; açık destructive izin gerekir. |
| `RenameColumn<T>()` / Async | Kolon adını değiştirir. |
| `AlterColumnType<T>(..., options)` / Async | Modeldeki yeni tipe geçirir; açık izin gerekir. |

## 75. `IMigrationRunner`

| Metot | Amaç |
|---|---|
| `RegisterScript(definition)` | Inline/önceden hazırlanmış migration kaydeder. |
| `RegisterScriptFile(...)` | SQL dosyasını kaydeder. |
| `RegisterEmbeddedScripts(...)` | Assembly içindeki embedded `.sql` dosyalarını kaydeder. |
| `CompareRegisteredAsync()` | Dry-run migration planlarını üretir. |
| `RunAsync()` | Değişiklik planlarını uygular ve history yazar. |

## 76. `IStoredProcedureSynchronizer`

| Metot | Amaç |
|---|---|
| `RegisterProcedure(...)` | Inline procedure definition kaydeder. |
| `RegisterProcedureFile(...)` | SQL dosyasını kaydeder. |
| `CompareAsync(...)` | Tek procedure dry-run planı üretir. |
| `CompareRegisteredAsync()` | Kayıtlı procedure’leri karşılaştırır. |
| `ApplyAsync(plan)` | Tek planı uygular. |
| `SyncRegisteredAsync()` | Kayıtlı procedure’leri karşılaştırıp uygular. |

# Sürüm 1.0.8 sınırları

## 77. Bilinmesi gereken güncel sınırlar

- Model synchronizer yıkıcı/riskli farkları sessiz uygulamaz; drop, rename, tip değişikliği ve nullable-to-not-null işlemleri review-only kalır.
- Public property ignore attribute’ü yoktur.
- Column adı override attribute’ü yoktur.
- Schema-qualified table-name attribute kullanımı identifier doğrulamasına takılır.
- İndeks SQL’i otomatik çalıştırılmaz.
- Foreign key parametreleri gelişmiş quoting/cascade modellemesi sağlamaz.
- Table create/drop sırası foreign key dependency graph ile yönetilmez.
- Migration’lar tüm batch ve history ile tek atomik transaction garantisi vermez.
- Değişmiş table script onarımı yalnız basit eksik kolon senaryosudur.
- SQLite type alter ve stored procedure desteklemez.
- PostgreSQL overloaded procedure desteklenmez.
- `DbColumnDefault` ve `DbColumnCheck` raw SQL kabul eder.

Bu sınırlar kütüphanenin kullanılamaz olduğu anlamına gelmez. Doğru kullanım alanı; açık DDL üretimi, kontrollü schema initialization ve review edilmiş SQL script yönetimidir.

# Sık sorulan sorular

## 78. EF Core ile birlikte kullanabilir miyim?

Evet. ModelSync ORM olmadığı için EF Core, Dapper veya ADO.NET ile birlikte kullanılabilir. Ancak iki farklı migration otoritesi oluşturmamaya dikkat edin. Şema değişikliklerinin tek sahibi belirlenmelidir.

## 79. Yalnız Core paketini kurmalı mıyım?

Normal kullanıcı hayır. Provider paketi Core’u dependency olarak getirir. Yalnız yeni provider geliştirenler Core’u doğrudan kullanır.

## 80. ModelSync model sınıfını veri entity’si olarak da kullanabilir miyim?

Teknik olarak evet; ancak 1.0.8 tüm public property’leri kolon kabul ettiği için şema modellerini ayrı tutmak daha güvenlidir.

## 81. `ifNotExists: true` migration yerine geçer mi?

Hayır. Yalnız tablo yoksa create işlemini güvenli hale getirir. Mevcut tablodaki kolon/tip/constraint farklarını yönetmez.

## 82. Kolon ekledim, tablo otomatik güncellenir mi?

Hayır. Şunlardan birini yapın:

```csharp
await generator.AddColumnAsync<Model>(nameof(Model.NewProperty));
```

veya yeni, immutable SQL migration dosyası ekleyin.

## 83. İndeksler neden ayrı?

ModelSync indeks metadata’sını tablo tanımından ayrı SQL olarak üretir. Bu, indeksleri review etme ve provider’a uygun deployment adımında yönetme esnekliği sağlar; ancak yürütme sorumluluğu kullanıcıdadır.

## 84. Production’da startup sırasında migration çalıştırmalı mıyım?

Tek instance, kontrollü küçük sistemlerde olabilir. Çok instance’lı production ortamında ayrı deployment job/console migration runner daha güvenlidir.

## 85. Hangi yaklaşımı seçmeliyim?

| İhtiyaç | Öneri |
|---|---|
| Yeni prototipte hızlı tablo oluşturma | Attribute generator + `ifNotExists` |
| DDL SQL’ini review edip DBA’ya verme | Yalnız generator çıktılarını kullanma |
| Production sürüm değişiklikleri | Immutable SQL migration dosyaları |
| Procedure source control | Stored procedure synchronizer |
| Runtime CRUD | Dapper/ADO.NET/EF Core gibi ayrı araç |

# Sonuç

ModelSync’in temel prensibi, şema değişikliğini görünür ve geliştirici kontrollü tutmaktır. Sağlıklı kullanım sırası şöyledir:

1. Doğru provider paketini kurun.
2. Şema modelini provider attribute’larıyla tanımlayın.
3. SQL’i üretin ve inceleyin.
4. Aynı generator örneğinde tabloyu oluşturun.
5. İndeksleri ayrıca yönetin.
6. Değişiklikleri açık kolon operasyonu veya yeni migration scriptiyle yapın.
7. Destructive işlemleri yalnız açık izin, backup ve review ile çalıştırın.
8. Stored procedure’lerde önce compare planı alın.
9. Production’da migration dosyalarını değiştirmeyin ve tek deployment otoritesi kullanın.

Bu akışla ModelSync; ORM yükü olmadan, provider’a özel DDL üretimi ve kontrollü database schema yönetimi için kullanılabilir.

