# 08 — Mimari

## Katman Diyagramı

```
┌─────────────────────────────────────────────────────────┐
│                  Kullanıcı Kodu / Uygulama              │
│         (ASP.NET Core, Console, Worker Service)         │
└────────────────────────┬────────────────────────────────┘
                         │ ITableGenerator
┌────────────────────────▼────────────────────────────────┐
│               Provider Generator Katmanı                │
│   MySqlTableGenerator  │  SqlServerTableGenerator       │
│   PostgresTableGenerator │  SQLiteTableGenerator        │
│   (netstandard2.0)                                      │
└────────────────────────┬────────────────────────────────┘
                         │ extends SqlTableGenerator
┌────────────────────────▼────────────────────────────────┐
│                    Core Katmanı                         │
│   SqlTableGenerator (abstract)                          │
│   ITableGenerator (interface)                           │
│   DynamicPropertyManager<T>                             │
│   Attribute sınıfları                                   │
│   (netstandard2.0)                                      │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│               Roslyn Analyzer Katmanı                   │
│   MSYNC001  MSYNC002  MSYNC003                          │
│   (netstandard2.0 — derleme zamanı)                     │
└─────────────────────────────────────────────────────────┘
```

---

## SQL Üretim Akışı

```
GenerateSqlTable<User>(ifNotExists: true)
        │
        ▼
DynamicPropertyManager<User>.LoadFromModel()
        │
        ├─ GetClassAttribute<DbTableNameAttribute>()  →  "users"
        │
        └─ GetAllPropertiesOrdered()   (MetadataToken sıralama)
                 │
                 ├─ Id     → INT PRIMARY KEY AUTO_INCREMENT
                 ├─ Email  → VARCHAR(255) NOT NULL UNIQUE DEFAULT ...
                 └─ ...
        │
        ▼
QuoteIdentifier("users")  →  "`users`"  (provider-specific)
        │
        ▼
StringBuilder ile SQL inşası
        │
        ├─ CREATE TABLE IF NOT EXISTS `users` (
        ├─     `Id` INT PRIMARY KEY AUTO_INCREMENT,
        ├─     `Email` VARCHAR(255) NOT NULL UNIQUE DEFAULT 'test@x.com',
        │      CHECK (Email LIKE '%@%'),
        └─ );
        │
        ▼
SqlCache[typeof(User)] = sql   (ConcurrentDictionary)
        │
        ▼
ILogger.LogDebug(...)
        │
        ▼
return sql
```

---

## DDL Çalıştırma Akışı

```
CreateTablesAsync(cancellationToken)
        │
        ▼
foreach (sql in SqlCache.Values)
        │
        ▼
new MySqlConnection(connectionString)
connection.OpenAsync(cancellationToken)
        │
        ▼
new MySqlCommand(sql, connection)
command.ExecuteNonQueryAsync(cancellationToken)
        │
        ▼
connection.Dispose()
```

---

## Tasarım Kararları

### 1. Per-Instance ConcurrentDictionary

Her generator örneği kendi önbelleğini tutar.
Bu, iki farklı provider generator'ının aynı static önbelleği paylaşmasını engeller
ve thread-safe çoklu eşzamanlı SQL üretimini destekler.

```
MySqlGen (instance A) → ConcurrentDictionary<Type, string>
SqlServerGen (instance B) → ConcurrentDictionary<Type, string>  (ayrı!)
```

### 2. MetadataToken ile Deterministik Sıralama

C#'ta reflection ile `GetProperties()` çağrısı, property sırasını garanti etmez.
`MetadataToken` kullanılarak kaynaktaki tanımlama sırası korunur.

```csharp
properties
    .OrderBy(p => p.MetadataToken)
    .ToList()
```

Bu sayede aynı model sınıfı her çalıştırmada aynı SQL'i üretir (snapshot testleri için kritik).

### 3. Provider-Specific Quoting

Her provider farklı identifier quoting kullanır:

| Provider | Quoting | Örnek |
|---|---|---|
| MySQL | Backtick | `` `column` `` |
| SQL Server | Köşeli parantez | `[column]` |
| PostgreSQL | Çift tırnak | `"column"` |
| SQLite | Çift tırnak | `"column"` |

`QuoteIdentifier` abstract metot, alt sınıflara bu farkı yaptırır.

### 4. netstandard2.0 Hedefi

Tüm library projeleri `netstandard2.0` hedefler.
Bu sayede:
- .NET Framework 4.6.1+ projeleri kullanabilir
- .NET Core 2.0+ projeleri kullanabilir
- .NET 5/6/7/8 projeleri kullanabilir
- Roslyn Analyzer projeleriyle uyumlu (`netstandard2.0` zorunlu)

### 5. ITableGenerator Arayüzü

Provider sınıfını doğrudan bağlamak yerine `ITableGenerator` arayüzü kullanmak:
- Test edilebilirliği artırır (mock/fake kolaylığı)
- DI container entegrasyonunu standardize eder
- Provider değiştirmeyi tek noktadan yapar

---

## Yeni Provider Ekleme Rehberi

Yeni bir veritabanı desteği eklemek istiyorsanız:

1. Yeni bir .NET Standard 2.0 projesi oluşturun: `UmbrellaFrame.ModelSync.{Provider}`
2. `UmbrellaFrame.ModelSync.Core`'a proje referansı ekleyin
3. `SqlTableGenerator`'ı miras alın, `ITableGenerator`'ı implement edin
4. `QuoteIdentifier` ve `IfNotExistsClause`'u override edin
5. ADO.NET provider ile `CreateTables`, `CreateTablesAsync`, `DropTables`, `DropTablesAsync` implement edin
6. Provider-specific attribute'ları `DbTableNameAttribute`, `DbColumnTypeAttribute` vb. inherit ederek tanımlayın
7. Unit test projesi ekleyin, mevcut test modellerini yeniden kullanın

```csharp
// Minimum iskelet
public class OracleTableGenerator : SqlTableGenerator, ITableGenerator
{
    private readonly string _connectionString;

    protected override string QuoteIdentifier(string identifier) => $"\"{identifier}\"";
    protected override string IfNotExistsClause => string.Empty; // Oracle desteklemez

    public OracleTableGenerator(string connectionString, ILogger<OracleTableGenerator> logger = null)
        : base(logger)
    {
        if (string.IsNullOrEmpty(connectionString))
            throw new ArgumentException("Connection string gerekli.", nameof(connectionString));
        _connectionString = connectionString;
    }

    public void CreateTables() { /* OracleConnection ile DDL */ }
    public Task CreateTablesAsync(CancellationToken ct = default) { /* async */ }
    public void DropTables() { /* */ }
    public Task DropTablesAsync(CancellationToken ct = default) { /* */ }
}
```
