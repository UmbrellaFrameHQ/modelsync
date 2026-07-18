# 14 - Model Synchronizer

Model synchronizer, attribute ile işaretlenmiş C# şema modellerini canlı veritabanıyla karşılaştırır ve herhangi bir işlem yapmadan önce incelenebilir bir dry-run planı üretir.

Bu özellik mevcut `ITableGenerator`, provider table generator, migration runner ve stored procedure synchronizer API'lerini bozmaz; onların üzerine eklenen ayrı bir katmandır.

## Amaç

- ModelSync tablo ve kolon attribute'larını okumak.
- `DbColumnName` ve `DbIgnore` attribute'lerini schema discovery sırasında dikkate almak.
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

Güvenli işlemler konfigürasyonla kapatılabilir. Bu durumda işlem `ModelSyncResult.SkippedOperations` listesinde görünür ve ilgisiz güvenli işlemleri bloklamaz.

## Tablo Bazlı Execution Policy

ModelSync, aynı çalıştırmada farklı tablolar için farklı migration sahipliği tanımlayabilir. Policy runtime options üzerinden verilir; model attribute'u değildir. Çünkü manuel/otomatik migration kararı genellikle ortama ve deployment stratejisine bağlıdır:

Legacy runner compatibility aynı güvenlik sınırını korur: migration history tablolarına kontrollü `SqlHash` infrastructure upgrade uygulanabilmesi, model synchronizer safe ALTER kurallarını gevşetmez. Model tabloları için drop, narrowing, riskli not-null, riskli rename ve destructive constraint değişiklikleri otomatik safe apply kapsamında engellenmeye devam eder.

```csharp
options.DefaultTableMode = ModelSyncTableMode.ManualOnly;

options.TablePolicies
    .ForType<AuditLog>(ModelSyncTableMode.ApplySafeChanges)
    .ForType<Notification>(ModelSyncTableMode.ApplySafeChanges)
    .ForTable("legacy", "OldOrders", ModelSyncTableMode.Ignore)
    .ForSchema("audit", ModelSyncTableMode.ApplySafeChanges);
```

Policy çözüm sırası deterministiktir:

```text
CLR model type -> schema/table -> schema -> DefaultTableMode -> mevcut global davranış
```

| Mode | Davranış |
|---|---|
| `Inherit` | Mevcut global model-sync davranışını kullanır. Varsayılan değerdir ve geriye uyumluluğu korur. |
| `ManualOnly` | Tablo karşılaştırılır; üretilen operasyonlar `ManualOperations` altında raporlanır ve hiçbir zaman otomatik çalıştırılmaz. Manuel operasyonlar ilgisiz otomatik güvenli operasyonları bloklamaz. |
| `ApplySafeChanges` | Yalnız güvenli, provider tarafından desteklenen ve dependency'leri hazır operasyonlar otomatik uygulanabilir. Destructive/riskli değişiklikler bloklu kalır. |
| `Ignore` | Tablo normal diff üretiminden çıkarılır. Ignored tablolar başka managed tablolar için dependency target olarak database'de var mı diye yine kontrol edilebilir. |

`ModelSyncResult` operasyonları `AutomaticOperations`, `ManualOperations`, `SkippedOperations` ve `BlockedOperations` olarak ayırır. `ApplyAsync()` yalnız otomatik güvenli operasyonları uygular. `ManualOnly` işlemin güvenli olduğu anlamına gelmez; ModelSync SQL/reason bilgisini raporlar ama otomatik çalıştırmaz.

Otomatik bir tablo eksik manuel veya ignored parent tabloya bağımlıysa ilgili foreign-key operasyonu açık dependency nedeni ile bloklanır. Parent database'de zaten varsa ve provider operasyonu destekliyorsa otomatik child tablo devam edebilir.

## Global Plan Fazları

ModelSync model-diff işlemlerini model iteration sırasına bağlamadan deterministik global fazlarla üretir:

```text
Create tables -> Add columns -> Add defaults -> Add checks -> Add unique constraints -> Add indexes -> Add foreign keys -> Apply scripts
```

Bu sayede child model parent modelden önce gelse veya tablolar arasında dairesel FK ilişkisi olsa bile önce eksik tablolar planlanır, foreign key işlemleri tablo oluşturma fazından sonra gelir.

## Bloklanan İşlemler

- `ReportUnmappedTables = true` ise modelde olmayan database tablolarını silme planı.
- Kolon silme.
- Kolon yeniden adlandırma.
- Kolon tipi değiştirme.
- Daraltıcı veya uyumsuz tip değişiklikleri.
- `NULL` kolonunu `NOT NULL` yapmak.
- Mevcut tabloya defaultsuz `NOT NULL` kolon eklemek.
- Mevcut tabloya eksik primary-key, generated-value, unique veya foreign-key kolon eklemek.
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

Provider synchronizer'lar structured value-generation bilgisini korur. SQL Server identity, MySQL auto-increment, PostgreSQL `SERIAL`/`BIGSERIAL` ve SQLite integer rowid primary key bilgisi plan oluşturma sırasında `DbValueGenerationKind` ile temsil edilir. Eski primary-key SQL snippet metadata'si yalnız geriye uyumluluk için korunur.

Canlı şema karşılaştırması mümkün olduğunda semantic metadata kullanır. Örneğin database'de `UX_ManuallyNamed(Code)` adlı unique index varsa modeldeki `Code` unique isteği karşılanmış kabul edilir; ModelSync yalnız kendi üreteceği `UQ_Table_Code` adını aramaz.

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

Migration runner karşılaştırması read-only'dir. Infrastructure `CompareRegisteredAsync()` tarafından değil, `RunAsync()` veya açık `EnsureInfrastructureAsync()` çağrısı tarafından oluşturulur.

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
