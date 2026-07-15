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
    void DropTables(DestructiveOperationOptions options);
    Task DropTablesAsync(CancellationToken cancellationToken = default);
    Task DropTablesAsync(DestructiveOperationOptions options, CancellationToken cancellationToken = default);

    void DropColumn<T>(string columnName) where T : class, new();
    void DropColumn<T>(string columnName, DestructiveOperationOptions options) where T : class, new();

    void AlterColumnType<T>(string columnName) where T : class, new();
    void AlterColumnType<T>(string columnName, DestructiveOperationOptions options) where T : class, new();
}
```

---

## SqlTableGenerator (Soyut Sınıf)

`UmbrellaFrame.ModelSync.Core.Services` namespace'i altında tanımlanmıştır.
Tüm provider generator sınıfları bu sınıftan türer.

### Korumalı Soyut Üyeler

```csharp
// Alt sınıf tarafından zorunlu implement edilir.
// Identifier base class tarafından doğrulandıktan sonra çağrılır.
protected abstract string QuoteValidatedIdentifier(string identifier);

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
-- MySQL
DROP TABLE IF EXISTS `users`;

-- SQL Server
DROP TABLE IF EXISTS [users];

-- PostgreSQL / SQLite
DROP TABLE IF EXISTS "users";
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
public void DropTables(DestructiveOperationOptions options)
public Task DropTablesAsync(CancellationToken cancellationToken = default)
public Task DropTablesAsync(
    DestructiveOperationOptions options,
    CancellationToken cancellationToken = default)
```

Önbellekteki tabloları siler. `DropTables()` ve `DropTablesAsync(CancellationToken)`
artık güvenlik nedeniyle exception fırlatır. Gerçek silme için açık onay gerekir:

```csharp
generator.DropTables(DestructiveOperationOptions.Allow());
await generator.DropTablesAsync(DestructiveOperationOptions.Allow(), cancellationToken);
```

Bu işlem attribute ile verilen tablo adını kullanır; class adı ile tablo adı farklıysa
`[{Db}TableName("...")]` değeri esas alınır.

---

### Destructive ALTER Operations

```csharp
public void DropColumn<T>(string columnName)
public void DropColumn<T>(string columnName, DestructiveOperationOptions options)
public void AlterColumnType<T>(string columnName)
public void AlterColumnType<T>(string columnName, DestructiveOperationOptions options)
```

`DropColumn` ve `AlterColumnType` veri kaybına yol açabileceği için açık onay ister:

```csharp
var allow = DestructiveOperationOptions.Allow();

generator.DropColumn<Product>("LegacyCode", allow);
generator.AlterColumnType<Product>("Price", allow);
```

Onaysız çağrılar exception fırlatır.

---

### Identifier Safety

Tablo, kolon, index ve veritabanı adları provider quote işleminden önce doğrulanır.
İzin verilen desen:

```text
^[A-Za-z_][A-Za-z0-9_]*$
```

Boşluk, tire, nokta, quote, bracket, noktalı virgül gibi şüpheli karakterler reddedilir.

---

## Provider-Specific Generator Sınıfları

| Provider | Sınıf | Ek Metot |
|---|---|---|
| MySQL | `MySqlTableGenerator` | `GenerateMySqlTable<T>()` |
| SQL Server | `SqlServerTableGenerator` | `GenerateSqlServerTable<T>()` |
| PostgreSQL | `PostgresTableGenerator` | `GeneratePostgresTable<T>()` |
| SQLite | `SQLiteTableGenerator` | `GenerateSQLiteTable<T>()` |
| Oracle preview | `OracleTableGenerator` | `GenerateOracleTable<T>()` |

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

---

## IMigrationRunner

Migration runner, registered SQL dosyalarını karşılaştırır ve kategori sırasına göre çalıştırır:

```csharp
Task<IReadOnlyList<MigrationSyncPlan>> CompareRegisteredAsync(
    CancellationToken cancellationToken = default);

Task RunAsync(CancellationToken cancellationToken = default);

Task<MigrationExecutionResult> RunWithResultAsync(
    CancellationToken cancellationToken = default);
```

`CompareRegisteredAsync()` read-only çalışır. History ve infrastructure nesneleri `RunAsync()`, `RunWithResultAsync()` veya açık `EnsureInfrastructureAsync()` çağrısında oluşturulur.

`MigrationExecutionResult`, genel başarı durumu, atomicity, lock/transaction/history bilgileri ve her script için batch, hash ve güvenli hata ayrıntıları taşır.

## Model Synchronizer

Provider synchronizer'ları aynı temel akışı sunar:

```csharp
var result = await SqlServerModelSynchronizer
    .FromAssemblies(options, typeof(Product).Assembly)
    .CompareAsync(cancellationToken);

result.AutomaticOperations;
result.ManualOperations;
result.SkippedOperations;
result.BlockedOperations;

await result.ApplyAsync(cancellationToken);
```

`ApplyAsync()` yalnız automatic-safe kapsamı uygular. `ManualOnly` işlemleri raporlanır, `Ignore` tablolar normal diff dışında kalır ve destructive/risky işlemler otomatik çalıştırılmaz.

## Reset, Lock ve Transaction Seçenekleri

`MigrationRunnerOptions` içindeki operasyonel ayarlar:

| Ayar | Amaç |
|---|---|
| `ResetOptions` | Onay, beklenen database, ortam ve readiness doğrulaması |
| `LockOptions` | Provider-native migration lock adı ve timeout |
| `TransactionPolicy` | `Auto`, `Required` veya `Forbidden` |
| `CategoryPolicies` | Script kategorisine göre `RunOnce`, `HashTracked`, `EveryRun` |

SQL Server `DatabaseResetOptions` ayrıca reset öncesi backup yolu tanımlayabilir.

## Migration Raporları

```csharp
var result = await runner.RunWithResultAsync(cancellationToken);
var markdown = MigrationExecutionMarkdownReport.Create(result);
var json = MigrationExecutionJsonReport.Create(result);
```

Raporlar connection string içermez. Failed batch preview 1024 karakterle sınırlandırılır.

## CLI

Supported migration provider values are `sqlserver`, `mysql`, `mariadb`, `postgresql`, and `sqlite`. The published Oracle preview package is not a CLI provider because its migration runner is not production-ready.

```bash
modelsync validate --scripts ./Database/Scripts
modelsync run --provider sqlite --connection-env MODELSYNC_CONNECTION_STRING --scripts ./Database/Scripts --dry-run
modelsync run --provider sqlite --connection-env MODELSYNC_CONNECTION_STRING --scripts ./Database/Scripts --apply
```

`--apply` mutation için zorunludur. `--connection-env` secret değerinin process argümanında görünmesini engeller.
