# 14 - Model Synchronizer

Model synchronizer, attribute ile işaretlenmiş C# şema modellerini canlı veritabanıyla karşılaştırır ve herhangi bir işlem yapmadan önce incelenebilir dry-run planı üretir.

Bu özellik mevcut `ITableGenerator`, provider table generator, migration runner ve stored procedure synchronizer API'lerini bozmaz; onların üzerine eklenen ayrı bir katmandır.

## Amaç

- ModelSync tablo ve kolon attribute'larını okumak.
- Canlı veritabanı şemasını introspect etmek.
- İncelenebilir diff planı üretmek.
- Yalnız update-safe/additive işlemleri otomatik uygulamak.
- Destructive, riskli veya unsupported işlemleri uygulamadan raporlamak.
- Model sync ve sıralı SQL scriptlerini tek sonuç içinde birleştirmek.

## Provider Desteği

| Provider | Model diff | Güvenli apply | Stored procedure scriptleri | Trigger scriptleri | Seed scriptleri | CustomSql scriptleri |
|---|---:|---:|---:|---:|---:|---:|
| SQL Server / Azure SQL | Var | Var | Var | Var | Var | Var |
| MySQL / MariaDB | Var | Var | Var | Var | Var | Var |
| PostgreSQL | Var | Var | Var | Var | Var | Var |
| SQLite | Var | Var | Yok | Var | Var | Var |

SQLite stored procedure desteklemez. SQLite model synchronizer stored procedure scripti alırsa plan içinde unsupported operation üretir ve `ApplyAsync()` devam etmez.

## Güvenli İşlemler

- Eksik tablo oluşturma.
- Eksik nullable kolon ekleme.
- Default değeri olan eksik `NOT NULL` kolon ekleme.
- Eksik indeks oluşturma.
- Provider güvenli ALTER destekliyorsa eksik default/check/unique/foreign key constraint ekleme.
- History/hash takipli sıralı SQL script uygulama.

## Bloklanan İşlemler

- Tablo silme.
- Kolon silme.
- Kolon yeniden adlandırma.
- Kolon tipi değiştirme.
- Daraltıcı veya uyumsuz tip değişiklikleri.
- `NULL` kolonunu `NOT NULL` yapmak.
- Mevcut tabloya defaultsuz `NOT NULL` kolon eklemek.
- SQLite stored procedure gibi provider tarafından desteklenmeyen işlemler.

## SQL Server Örneği

```csharp
using UmbrellaFrame.ModelSync.SqlServer;

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
    .FromAssemblies(options, typeof(SomeEntity).Assembly)
    .AddSqlScriptsFromEmbeddedResources(
        typeof(SomeEntity).Assembly,
        rootNamespace: "Infrastructure.ER.Database.Providers.SqlServer.Migration.Scripts")
    .CompareAsync(cancellationToken);

foreach (var operation in result.Operations)
{
    Console.WriteLine($"{operation.ChangeType}: {operation.Schema}.{operation.Table}.{operation.Column} - {operation.Reason}");
    if (!string.IsNullOrWhiteSpace(operation.Sql))
        Console.WriteLine(operation.Sql);
}

await result.ThrowIfUnsupportedOrDestructiveAsync();
await result.ApplyAsync(cancellationToken);
```

## Net Model Seçimi

Hangi model sınıflarının karşılaştırmaya dahil olacağını açıkça belirlemek için `FromTypes` kullanın:

```csharp
var result = await SqlServerModelSynchronizer
    .FromTypes(options, typeof(ProductSchema), typeof(CustomerSchema))
    .CompareAsync(cancellationToken);
```

`FromAssemblies` provider-aware çalışır. SQL Server synchronizer yalnız SQL Server ModelSync attribute'larını, MySQL synchronizer yalnız MySQL attribute'larını okur. İki model sınıfı aynı schema/table çiftine map edilirse ModelSync duplicate operasyon üretmek yerine açık hata fırlatır.

Varsayılan olarak `FromTypes` ve `FromAssemblies` yalnız verilen/keşfedilen model setini senkronize eder ve ilgisiz database tablolarını raporlamaz. Model setinin authoritative olmasını ve fazla database tablolarının blocked `DropTable` olarak görünmesini istiyorsanız `ReportUnmappedTables = true` kullanın.

## Ordered Scripts

Embedded scriptler kategoriye göre şu sırayla keşfedilir ve çalıştırılır:

```text
Tables -> StoredProcedures -> Triggers -> Seeds -> CustomSql
```

Desteklenen embedded resource klasörleri:

```text
Scripts/Tables
Scripts/StoredProcedures
Scripts/Triggers
Scripts/Seeds
Scripts/CustomSql
```

`CustomSql` kendi history tablosuna sahiptir:

```text
SchemaMigration_CustomSql
```

`HistorySchema`, SQL Server ve PostgreSQL gibi schema destekleyen provider'larda history tablolarının nerede oluşturulacağını belirler. SQL Server stored procedure scriptleri model synchronizer veya migration runner üzerinden çalıştırıldığında `CREATE OR ALTER PROCEDURE` formuna normalize edilir; stored procedure dosyalarında tek procedure tutun ve `GO` separator kullanmayın.

Model diff işlemleri risk sınıflandırmasından geçer. Kaydedilen SQL scriptleri güvenilir proje artifact'i kabul edilir; ModelSync script metnini destructive SQL açısından parse etmez.

## Provider Sınıfları

| Provider | Options | Synchronizer |
|---|---|---|
| SQL Server | `SqlServerModelSyncOptions` | `SqlServerModelSynchronizer` |
| MySQL / MariaDB | `MySqlModelSyncOptions` | `MySqlModelSynchronizer` |
| PostgreSQL | `PostgresModelSyncOptions` | `PostgresModelSynchronizer` |
| SQLite | `SQLiteModelSyncOptions` | `SQLiteModelSynchronizer` |

## Production Kullanımı

Önce `CompareAsync()` çalıştırın ve planın tamamını loglayın. `BlockedOperations` boş değilse otomatik apply yapmayın. Production ortamlarında synchronizer'ı uygulama trafiği başlamadan önce tek deployment job içinde çalıştırmak daha güvenlidir.

ModelSync'i sessiz şema mutasyon motoru gibi kullanmayın. Bu özellik güvenli eklemeleri kolaylaştırmak ve riskli veritabanı değişikliklerini görünür hale getirmek için tasarlanmıştır.
