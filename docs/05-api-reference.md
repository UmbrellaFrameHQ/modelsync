# 05 — API Referansı

## ITableGenerator

`UmbrellaFrame.ModelSync.Core.Interfaces` namespace'i altında tanımlanmıştır.
Tüm provider generator sınıfları bu arayüzü implement eder.

```csharp
public interface ITableGenerator
{
    // SQL Üretimi
    string GenerateSqlTable<T>(bool ifNotExists = false) where T : class, new();
    Task<string> GenerateSqlTableAsync<T>(bool ifNotExists = false, CancellationToken cancellationToken = default)
        where T : class, new();

    string GenerateDropTableSql<T>() where T : class, new();
    string GenerateTruncateTableSql<T>() where T : class, new();
    List<string> GenerateIndexSql<T>() where T : class, new();

    // DDL Çalıştırma
    void CreateTables();
    Task CreateTablesAsync(CancellationToken cancellationToken = default);

    void DropTables();
    Task DropTablesAsync(CancellationToken cancellationToken = default);
}
```

---

## SqlTableGenerator (Soyut Sınıf)

`UmbrellaFrame.ModelSync.Core.Services` namespace'i altında tanımlanmıştır.
Tüm provider generator sınıfları bu sınıftan türer.

### Korumalı Soyut Üyeler

```csharp
// Alt sınıf tarafından zorunlu implement edilmesi gerekir
protected abstract string QuoteIdentifier(string identifier);

// Override için opsiyonel (varsayılan: "IF NOT EXISTS")
// SQL Server: string.Empty dönmeli
protected virtual string IfNotExistsClause { get; }
```

### Önbellek (Cache)

Her generator örneği kendi `ConcurrentDictionary<Type, string>` önbelleğini tutar.
Birden fazla `GenerateSqlTable<T>()` çağrısı aynı SQL'i yeniden oluşturmaz.
İki farklı provider örneğinin önbellekleri birbirini etkilemez.

---

## Metot Referansı

### GenerateSqlTable\<T\>

```csharp
public string GenerateSqlTable<T>(bool ifNotExists = false) where T : class, new()
```

| Parametre | Açıklama | Varsayılan |
|---|---|---|
| `ifNotExists` | `CREATE TABLE IF NOT EXISTS` emits | `false` |

**Döner:** `string` — üretilen SQL

**Davranış:**
- `T` sınıfının tüm public property'leri sırasıyla taranır.
- `[DbTableNameAttribute]` yoksa sınıf adı tablo adı olarak kullanılır.
- Üretilen SQL önbelleğe alınır (`SqlCache[typeof(T)] = sql`).
- `ILogger.LogDebug` ile üretilen SQL loglanır.

**Fırlatabilir:**
- `InvalidOperationException` — Column type attribute eksikse

---

### GenerateSqlTableAsync\<T\>

```csharp
public Task<string> GenerateSqlTableAsync<T>(
    bool ifNotExists = false,
    CancellationToken cancellationToken = default)
    where T : class, new()
```

`GenerateSqlTable<T>` ile aynı çıktıyı döner; `Task.FromResult` üzerinden sarılır.
`cancellationToken` işlenmeden önce kontrol edilir.

---

### GenerateDropTableSql\<T\>

```csharp
public string GenerateDropTableSql<T>() where T : class, new()
```

Üretilen SQL:

```sql
-- MySQL / PostgreSQL / SQLite
DROP TABLE IF EXISTS `users`;

-- SQL Server
DROP TABLE IF EXISTS [users];
```

---

### GenerateTruncateTableSql\<T\>

```csharp
public string GenerateTruncateTableSql<T>() where T : class, new()
```

Üretilen SQL:

```sql
TRUNCATE TABLE `users`;
```

> ⚠️ SQLite `TRUNCATE TABLE` desteklemez. SQLite'da `DELETE FROM` kullanılır.

---

### GenerateIndexSql\<T\>

```csharp
public List<string> GenerateIndexSql<T>() where T : class, new()
```

`[DbColumnIndex]` attribute'u olan tüm property'ler için `CREATE [UNIQUE] INDEX` ifadelerini döner.

Dönüş:

```csharp
List<string>
{
    "CREATE INDEX `idx_users_email` ON `users` (`Email`);",
    "CREATE UNIQUE INDEX `idx_users_sku` ON `users` (`Sku`);"
}
```

Index ismi belirtilmezse otomatik üretilir: `idx_{tablo}_{kolon}`

---

### CreateTables

```csharp
public void CreateTables()
public Task CreateTablesAsync(CancellationToken cancellationToken = default)
```

`SqlCache` içindeki tüm SQL ifadelerini veritabanına uygulatır.
Her SQL için ayrı bir bağlantı açılır ve kapatılır.

**Önemli:** `GenerateSqlTable<T>()` çağrılmadan çalıştırılırsa önbellek boştur ve hiçbir şey yapmaz.

---

### DropTables

```csharp
public void DropTables()
public Task DropTablesAsync(CancellationToken cancellationToken = default)
```

Her önbellekteki tip için `GenerateDropTableSql<T>()` çalıştırır.

---

## Provider-Specific Generator Sınıfları

| Provider | Sınıf | Ek Metot |
|---|---|---|
| MySQL | `MySqlTableGenerator` | `GenerateMySqlTable<T>()` |
| SQL Server | `SqlServerTableGenerator` | `GenerateSqlServerTable<T>()` |
| PostgreSQL | `PostgresTableGenerator` | `GeneratePostgresTable<T>()` |
| SQLite | `SQLiteTableGenerator` | `GenerateSQLiteTable<T>()` |

Bu ek metotlar `GenerateSqlTable<T>()` için alias niteliğindedir; yani aynı sonucu dönerler.

---

## DynamicPropertyManager\<T\>

`UmbrellaFrame.ModelSync.Core.Helpers` namespace'i altındadır.

Generator tarafından dahili olarak kullanılır. Modelin property metadata'sını okur.

### Önemli Metotlar

```csharp
// Modeli tarar ve property metadata'sını yükler
static DynamicPropertyManager<T> LoadFromModel();

// Sıralı (declaration order) property listesi döner
List<(string Name, PropertyInfo Info)> GetAllPropertiesOrdered();

// Belirli attribute'u döner
TAttribute GetAttribute<TAttribute>(string propertyName)
    where TAttribute : Attribute;

// Sınıf düzeyinde attribute döner
TAttribute GetClassAttribute<TAttribute>()
    where TAttribute : Attribute;
```

> 💡 `GetAllPropertiesOrdered()`, `MetadataToken` kullanarak property sırasını kaynak koddaki tanımlama sırasına göre garanti eder.
> Bu sayede üretilen SQL her çalıştırmada deterministik kalır.

---

## İstisna Tipleri

| İstisna | Ne Zaman Fırlatılır |
|---|---|
| `PropertyNotFoundException` | `GetAttribute()` ile var olmayan property adı verildiğinde |
| `ArgumentException` | `connectionString` boş ya da null olduğunda; `DbColumnDefault`/`DbColumnCheck` constructor'ına boş değer geçildiğinde |
| `InvalidOperationException` | `GenerateSqlTable<T>()` çağrısında column type attribute eksik olduğunda |
