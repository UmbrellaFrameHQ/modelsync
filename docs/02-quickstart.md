# 02 — Hızlı Başlangıç

## Kurulum

Veritabanınıza uygun paketi projenize ekleyin:

```bash
# MySQL / MariaDB
dotnet add package UmbrellaFrame.ModelSync.MySql

# SQL Server
dotnet add package UmbrellaFrame.ModelSync.SqlServer

# PostgreSQL
dotnet add package UmbrellaFrame.ModelSync.PostgreSQL

# SQLite
dotnet add package UmbrellaFrame.ModelSync.SQLite

# Roslyn Analyzer (derleme zamanı uyarıları için - isteğe bağlı)
dotnet add package UmbrellaFrame.ModelSync.Analyzers
```

> 💡 **Not:** Core paketi, provider paketlerine bağımlılık olarak otomatik kurulur.
> Ayrıca kurmanıza gerek yoktur.

---

## 5 Adımda Çalışan Örnek (MySQL)

### Adım 1 — Model Sınıfını Tanımla

```csharp
using UmbrellaFrame.ModelSync.MySql;
using UmbrellaFrame.ModelSync.Core;

[MySqlTableName("users")]
public class User
{
    [MySqlColumnType(MySqlColumnType.INT)]
    [MySqlColumnPrimaryKey(isAutoIncrement: true)]
    public int Id { get; set; }

    [MySqlColumnType(MySqlColumnType.VARCHAR, "255")]
    [MySqlColumnNotNull]
    [DbColumnIndex("idx_users_email", isUnique: true)]
    public string Email { get; set; }

    [MySqlColumnType(MySqlColumnType.VARCHAR, "100")]
    [MySqlColumnNotNull]
    public string FullName { get; set; }

    [MySqlColumnType(MySqlColumnType.DATETIME)]
    [DbColumnDefault("CURRENT_TIMESTAMP")]
    public DateTime CreatedAt { get; set; }

    [MySqlColumnType(MySqlColumnType.BOOLEAN)]
    [DbColumnDefault("1")]
    public bool IsActive { get; set; }
}
```

### Adım 2 — Generator Oluştur

```csharp
var generator = new MySqlTableGenerator(
    connectionString: "Server=localhost;Database=myapp;User=root;Password=secret;"
);
```

### Adım 3 — SQL Üret ve Önbelleğe Al

```csharp
// IF NOT EXISTS ile güvenli oluşturma
string sql = generator.GenerateMySqlTable<User>(ifNotExists: true);
Console.WriteLine(sql);
```

Üretilen SQL:

```sql
CREATE TABLE IF NOT EXISTS `users` (
    `Id` INT PRIMARY KEY AUTO_INCREMENT,
    `Email` VARCHAR(255) NOT NULL UNIQUE,
    `FullName` VARCHAR(100) NOT NULL,
    `CreatedAt` DATETIME DEFAULT CURRENT_TIMESTAMP,
    `IsActive` BOOLEAN DEFAULT 1
);
```

### Adım 4 — Veritabanına Uygula

```csharp
// Sync
generator.CreateTables();

// veya Async (önerilir)
await generator.CreateTablesAsync(cancellationToken);
```

### Adım 5 — Index'leri Uygula

```csharp
var indexStatements = generator.GenerateIndexSql<User>();
// ["CREATE UNIQUE INDEX `idx_users_email` ON `users` (`Email`);"]
```

---

## SQL Server Örneği

```csharp
using UmbrellaFrame.ModelSync.SqlServer;

[SqlServerTableName("products")]
public class Product
{
    [SqlServerColumnType(SqlServerColumnType.INT)]
    [SqlServerColumnPrimaryKey(isAutoIncrement: true)]
    public int Id { get; set; }

    [SqlServerColumnType(SqlServerColumnType.NVARCHAR, "255")]
    [SqlServerColumnNotNull]
    public string Name { get; set; }

    [SqlServerColumnType(SqlServerColumnType.DECIMAL, "18,2")]
    [DbColumnDefault("0")]
    [DbColumnCheck("Price >= 0")]
    public decimal Price { get; set; }
}

var generator = new SqlServerTableGenerator(
    "Server=localhost;Database=myapp;Integrated Security=True;TrustServerCertificate=True;"
);

generator.GenerateSqlServerTable<Product>(ifNotExists: true);
await generator.CreateTablesAsync();
```

Üretilen SQL:

```sql
CREATE TABLE [products] (
    [Id] INT PRIMARY KEY IDENTITY(1,1),
    [Name] NVARCHAR(255) NOT NULL,
    [Price] DECIMAL(18,2) DEFAULT 0 CHECK (Price >= 0)
);
```

---

## Birden Fazla Tablo

```csharp
var generator = new MySqlTableGenerator(connectionString);

// Tabloları önbelleğe al
generator.GenerateMySqlTable<User>(ifNotExists: true);
generator.GenerateMySqlTable<Product>(ifNotExists: true);
generator.GenerateMySqlTable<Order>(ifNotExists: true);

// Hepsini tek seferde oluştur
await generator.CreateTablesAsync();
```

---

## SQL'i Sadece Üret, Çalıştırma

```csharp
// Bağlantı gerektirmez — sadece SQL string döner
string createSql  = generator.GenerateSqlTable<User>(ifNotExists: true);
string dropSql    = generator.GenerateDropTableSql<User>();
string truncSql   = generator.GenerateTruncateTableSql<User>();
var    indexSqls  = generator.GenerateIndexSql<User>();
```
