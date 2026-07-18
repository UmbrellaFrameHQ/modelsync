# 04 — Provider Kılavuzları

## MySQL / MariaDB

### Kurulum

```bash
dotnet add package UmbrellaFrame.ModelSync.MySql
```

### Generator Oluşturma

```csharp
// Temel kullanım
var generator = new MySqlTableGenerator("Server=localhost;Database=mydb;User=root;Password=pass;");

// ILogger ile (ASP.NET Core)
var generator = new MySqlTableGenerator(connectionString, logger);
```

### ENUM / SET Kolonu

```csharp
public enum UserStatus { Active, Passive, Deleted }
public enum UserRole { Admin, Editor, Viewer }

[MySqlColumnType(MySqlColumnType.ENUM, typeof(UserStatus))]
public UserStatus Status { get; set; }
// → ENUM('Active', 'Passive', 'Deleted')

[MySqlColumnType(MySqlColumnType.SET, typeof(UserRole))]
public string Roles { get; set; }
// → SET('Admin', 'Editor', 'Viewer')
```

### Connection String Örnekleri

```
# Temel
Server=localhost;Database=mydb;User=root;Password=secret;

# Port belirterek
Server=localhost;Port=3306;Database=mydb;User=root;Password=secret;

# SSL ile
Server=localhost;Database=mydb;User=root;Password=secret;SslMode=Required;
```

---

## SQL Server

### Kurulum

```bash
dotnet add package UmbrellaFrame.ModelSync.SqlServer
```

### Generator Oluşturma

```csharp
var generator = new SqlServerTableGenerator(
    "Server=localhost;Database=mydb;Integrated Security=True;TrustServerCertificate=True;"
);
```

### IF NOT EXISTS — SQL Server Farkı

SQL Server, `CREATE TABLE IF NOT EXISTS` sözdizimini desteklemez. ModelSync
`ifNotExists: true` verildiğinde eşdeğer bir `IF OBJECT_ID(...) IS NULL` bloğu üretir:

```csharp
var sql = generator.GenerateSqlServerTable<Product>(ifNotExists: true);
```

Tabloyu yeniden oluşturmak için drop işlemine yönelmeyin. Drop yalnız bilinçli veri kaybı senaryolarında ve `DestructiveOperationOptions.Allow()` ile kullanılmalıdır.

### IDENTITY (Auto Increment)

```csharp
[SqlServerColumnType(SqlServerColumnType.INT)]
[SqlServerColumnPrimaryKey(isAutoIncrement: true)]
public int Id { get; set; }
// → INT PRIMARY KEY IDENTITY(1,1)
```

### NVARCHAR(MAX) Kullanımı

```csharp
[SqlServerColumnType(SqlServerColumnType.NVARCHAR, "MAX")]
public string Content { get; set; }
// → NVARCHAR(MAX)
```

### Connection String Örnekleri

```
# Windows Authentication
Server=localhost;Database=mydb;Integrated Security=True;TrustServerCertificate=True;

# SQL Authentication
Server=localhost;Database=mydb;User Id=sa;Password=secret;TrustServerCertificate=True;

# Named Instance
Server=localhost\SQLEXPRESS;Database=mydb;Integrated Security=True;TrustServerCertificate=True;
```

---

## PostgreSQL

### Kurulum

```bash
dotnet add package UmbrellaFrame.ModelSync.PostgreSQL
```

### Generator Oluşturma

```csharp
var generator = new PostgresTableGenerator(
    "Host=localhost;Database=mydb;Username=postgres;Password=secret;"
);
```

### Auto Increment — SERIAL vs GENERATED

PostgreSQL'de auto-increment için `SERIAL` tipini kullanın:

```csharp
[PostgresColumnType(PostgresColumnType.SERIAL)]
[PostgresColumnPrimaryKey]
public int Id { get; set; }
// → SERIAL PRIMARY KEY
```

### JSONB Kolonu

```csharp
[PostgresColumnType(PostgresColumnType.JSONB)]
public string Metadata { get; set; }
// → JSONB
```

### TIMESTAMPTZ

```csharp
[PostgresColumnType(PostgresColumnType.TIMESTAMPTZ)]
[DbColumnDefault("NOW()")]
public DateTime CreatedAt { get; set; }
// → TIMESTAMPTZ DEFAULT NOW()
```

### Connection String Örnekleri

```
# Temel
Host=localhost;Database=mydb;Username=postgres;Password=secret;

# Port belirterek
Host=localhost;Port=5432;Database=mydb;Username=postgres;Password=secret;

# SSL ile
Host=localhost;Database=mydb;Username=postgres;Password=secret;SSL Mode=Require;
```

---

## SQLite

### Kurulum

```bash
dotnet add package UmbrellaFrame.ModelSync.SQLite
```

### Generator Oluşturma

```csharp
// Dosya tabanlı
var generator = new SQLiteTableGenerator("Data Source=myapp.db;");

// Bellek içi (test için)
var generator = new SQLiteTableGenerator("Data Source=:memory:");
```

### SQLite Tip Sistemi (Type Affinity)

SQLite yalnızca 5 temel tip destekler.
.NET tiplerinin SQLite karşılıkları:

| .NET Tipi | SQLite Tipi |
|---|---|
| `int`, `long`, `short`, `bool` | `INTEGER` |
| `float`, `double` | `REAL` |
| `decimal` | `NUMERIC` |
| `string`, `char`, `Guid` | `TEXT` |
| `byte[]` | `BLOB` |

### AUTOINCREMENT

```csharp
[SQLiteColumnType(SQLiteColumnType.INTEGER)]
[SQLiteColumnPrimaryKey]
public int Id { get; set; }
// → INTEGER PRIMARY KEY AUTOINCREMENT
```

### Bellek İçi Veritabanı (Entegrasyon Testleri İçin)

```csharp
var generator = new SQLiteTableGenerator("Data Source=:memory:");
generator.GenerateSQLiteTable<User>(ifNotExists: true);
generator.CreateTables();
// Artık bellek içi SQLite'de 'users' tablosu mevcut
```

---

## Oracle Preview

Oracle provider `netstandard2.1` hedefler, NuGet'te yayımlanmıştır ve 1.4.0-rc.1 sürümünde tablo DDL ile güvenli model karşılaştırmasının bir bölümünü destekler. Paketin yayımlanmış olması preview durumunu değiştirmez.

```bash
dotnet add package UmbrellaFrame.ModelSync.Oracle --version 1.4.0-rc.1
```

```csharp
using UmbrellaFrame.ModelSync.Oracle;

[OracleTableName("PRODUCTS")]
public sealed class OracleProduct
{
    [OracleColumnType(OracleColumnType.NUMBER, "10")]
    [OracleColumnPrimaryKey(isIdentity: true)]
    public int Id { get; set; }

    [OracleColumnType(OracleColumnType.VARCHAR2, "200")]
    [OracleColumnNotNull]
    public string Name { get; set; } = string.Empty;
}

var generator = new OracleTableGenerator(connectionString);
var sql = generator.GenerateOracleTable<OracleProduct>(ifNotExists: true);
```

Oracle `IF NOT EXISTS` sözdizimini desteklemediği için generator doğrudan bu ifadeyi üretmez. Oracle migration runner, stored procedure synchronization, reset ve native lock desteği henüz production seviyesinde değildir. Güncel durum için [provider destek matrisini](provider-support-matrix.md) kullanın.

---

## Tüm Provider'larda Ortak Metotlar

```csharp
// SQL üretimi (bağlantı gerektirmez)
string createSql = generator.GenerateSqlTable<T>(ifNotExists: true);
string dropSql   = generator.GenerateDropTableSql<T>();
string truncSql  = generator.GenerateTruncateTableSql<T>();
var    indexes   = generator.GenerateIndexSql<T>();

// Sync DDL
generator.CreateTables();
generator.DropTables(DestructiveOperationOptions.Allow());

// Async DDL (önerilir)
await generator.CreateTablesAsync(cancellationToken);
await generator.DropTablesAsync(DestructiveOperationOptions.Allow(), cancellationToken);
```

`DropTables`, `DropColumn` ve `AlterColumnType` yıkıcı/riskli işlemler olduğu için
`DestructiveOperationOptions.Allow()` olmadan exception fırlatır.
