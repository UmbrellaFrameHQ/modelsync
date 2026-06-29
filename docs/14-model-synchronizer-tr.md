# 14 - Model Synchronizer

Model synchronizer, attribute ile isaretlenmis C# sema modellerini canli veritabaniyla karsilastirir ve herhangi bir islem yapmadan once incelenebilir dry-run plani uretir.

Bu ozellik mevcut `ITableGenerator`, provider table generator, migration runner ve stored procedure synchronizer API'lerini bozmaz; onlarin uzerine eklenen ayri bir katmandir.

## Amac

- ModelSync tablo ve kolon attribute'larini okumak.
- `DbColumnName` ve `DbIgnore` attribute'lerini schema discovery sirasinda dikkate almak.
- Canli veritabani semasini introspect etmek.
- Incelenebilir diff plani uretmek.
- Yalniz update-safe/additive islemleri otomatik uygulamak.
- Destructive, riskli veya unsupported islemleri uygulamadan raporlamak.
- Model sync ve sirali SQL scriptlerini tek sonuc icinde birlestirmek.

## Provider Destegi

| Provider | Model diff | Guvenli apply | Stored procedure scriptleri | Trigger scriptleri | Seed scriptleri | CustomSql scriptleri |
|---|---:|---:|---:|---:|---:|---:|
| SQL Server / Azure SQL | Var | Var | Var | Var | Var | Var |
| MySQL / MariaDB | Var | Var | Var | Var | Var | Var |
| PostgreSQL | Var | Var | Var | Var | Var | Var |
| SQLite | Var | Var | Yok | Var | Var | Var |

SQLite stored procedure desteklemez. SQLite model synchronizer stored procedure scripti alirsa plan icinde unsupported operation uretir ve `ApplyAsync()` devam etmez.

## Guvenli Islemler

- Eksik tablo olusturma.
- Eksik nullable kolon ekleme.
- Default degeri olan eksik `NOT NULL` kolon ekleme.
- Eksik indeks olusturma.
- Provider guvenli ALTER destekliyorsa eksik default/check/unique/foreign key constraint ekleme.
- History/hash takipli sirali SQL script uygulama.

Guvenli islemler konfigürasyonla kapatilabilir. Bu durumda islem `ModelSyncResult.SkippedOperations` listesinde gorunur ve ilgisiz guvenli islemleri bloklamaz.

## Tablo Bazli Execution Policy

Yayinlanmamis 1.1.0 sertlestirme calismasi, ayni calistirmada farkli tablolar icin farkli migration sahipligi tanimlamayi ekler. Policy runtime options uzerinden verilir; model attribute'u degildir. Cunku manuel/otomatik migration karari genellikle ortama ve deployment stratejisine baglidir:

```csharp
options.DefaultTableMode = ModelSyncTableMode.ManualOnly;

options.TablePolicies
    .ForType<AuditLog>(ModelSyncTableMode.ApplySafeChanges)
    .ForType<Notification>(ModelSyncTableMode.ApplySafeChanges)
    .ForTable("legacy", "OldOrders", ModelSyncTableMode.Ignore)
    .ForSchema("audit", ModelSyncTableMode.ApplySafeChanges);
```

Policy cozum sirasi deterministiktir:

```text
CLR model type -> schema/table -> schema -> DefaultTableMode -> mevcut global davranis
```

| Mode | Davranis |
|---|---|
| `Inherit` | Mevcut global model-sync davranisini kullanir. Varsayilan degerdir ve geriye uyumlulugu korur. |
| `ManualOnly` | Tablo karsilastirilir; uretilen operasyonlar `ManualOperations` altinda raporlanir ve hicbir zaman otomatik calistirilmaz. Manuel operasyonlar ilgisiz otomatik guvenli operasyonlari bloklamaz. |
| `ApplySafeChanges` | Yalniz guvenli, provider tarafindan desteklenen ve dependency'leri hazir operasyonlar otomatik uygulanabilir. Destructive/riskli degisiklikler bloklu kalir. |
| `Ignore` | Tablo normal diff uretiminden cikarilir. Ignored tablolar baska managed tablolar icin dependency target olarak database'de var mi diye yine kontrol edilebilir. |

`ModelSyncResult` operasyonlari `AutomaticOperations`, `ManualOperations`, `SkippedOperations` ve `BlockedOperations` olarak ayirir. `ApplyAsync()` yalniz otomatik guvenli operasyonlari uygular. `ManualOnly` islemin guvenli oldugu anlamina gelmez; ModelSync SQL/reason bilgisini raporlar ama otomatik calistirmaz.

Otomatik bir tablo eksik manuel veya ignored parent tabloya bagimliysa ilgili foreign-key operasyonu acik dependency nedeni ile bloklanir. Parent database'de zaten varsa ve provider operasyonu destekliyorsa otomatik child tablo devam edebilir.

## Global Plan Fazlari

ModelSync model-diff islemlerini model iteration sirasina baglamadan deterministik global fazlarla uretir:

```text
Create tables -> Add columns -> Add defaults -> Add checks -> Add unique constraints -> Add indexes -> Add foreign keys -> Apply scripts
```

Bu sayede child model parent modelden once gelse veya tablolar arasinda dairesel FK iliskisi olsa bile once eksik tablolar planlanir, foreign key islemleri tablo olusturma fazindan sonra gelir.

## Bloklanan Islemler

- `ReportUnmappedTables = true` ise modelde olmayan database tablolarini silme plani.
- Kolon silme.
- Kolon yeniden adlandirma.
- Kolon tipi degistirme.
- Daraltici veya uyumsuz tip degisiklikleri.
- `NULL` kolonunu `NOT NULL` yapmak.
- Mevcut tabloya defaultsuz `NOT NULL` kolon eklemek.
- Mevcut tabloya eksik primary-key, generated-value, unique veya foreign-key kolon eklemek.
- SQLite stored procedure gibi provider tarafindan desteklenmeyen islemler.

## SQL Server Ornegi

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

## Net Model Secimi

Hangi model siniflarinin karsilastirmaya dahil olacagini acikca belirlemek icin `FromTypes` kullanin:

```csharp
var result = await SqlServerModelSynchronizer
    .FromTypes(options, typeof(ProductSchema), typeof(CustomerSchema))
    .CompareAsync(cancellationToken);
```

`FromAssemblies` provider-aware calisir. SQL Server synchronizer yalniz SQL Server ModelSync attribute'larini, MySQL synchronizer yalniz MySQL attribute'larini okur. Iki model sinifi ayni schema/table ciftine map edilirse ModelSync duplicate operasyon uretmek yerine acik hata firlatir.

Varsayilan olarak `FromTypes` ve `FromAssemblies` yalniz verilen/kesfedilen model setini senkronize eder ve ilgisiz database tablolarini raporlamaz. Model setinin authoritative olmasini ve fazla database tablolarinin blocked `DropTable` olarak gorunmesini istiyorsaniz `ReportUnmappedTables = true` kullanin.

Provider synchronizer'lar structured value-generation bilgisini korur. SQL Server identity, MySQL auto-increment, PostgreSQL `SERIAL`/`BIGSERIAL` ve SQLite integer rowid primary key bilgisi plan olusturma sirasinda `DbValueGenerationKind` ile temsil edilir. Eski primary-key SQL snippet metadata'si yalniz geriye uyumluluk icin korunur.

Canli sema karsilastirmasi mumkun oldugunda semantic metadata kullanir. Ornegin database'de `UX_ManuallyNamed(Code)` adli unique index varsa modeldeki `Code` unique istegi karsilanmis kabul edilir; ModelSync yalniz kendi uretecegi `UQ_Table_Code` adini aramaz.

## Ordered Scripts

Embedded scriptler kategoriye gore su sirayla kesfedilir ve calistirilir:

```text
Tables -> StoredProcedures -> Triggers -> Seeds -> CustomSql
```

Desteklenen embedded resource klasorleri:

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

`HistorySchema`, SQL Server ve PostgreSQL gibi schema destekleyen provider'larda history tablolarinin nerede olusturulacagini belirler. SQL Server stored procedure scriptleri model synchronizer veya migration runner uzerinden calistirildiginda `CREATE OR ALTER PROCEDURE` formuna normalize edilir; stored procedure dosyalarinda tek procedure tutun ve `GO` separator kullanmayin.

Model diff islemleri risk siniflandirmasindan gecer. Kaydedilen SQL scriptleri guvenilir proje artifact'i kabul edilir; ModelSync script metnini destructive SQL acisindan parse etmez.

Migration runner karsilastirmasi read-only'dir. Infrastructure `CompareRegisteredAsync()` tarafindan degil, `RunAsync()` veya acik `EnsureInfrastructureAsync()` cagrisi tarafindan olusturulur.

## Provider Siniflari

| Provider | Options | Synchronizer |
|---|---|---|
| SQL Server | `SqlServerModelSyncOptions` | `SqlServerModelSynchronizer` |
| MySQL / MariaDB | `MySqlModelSyncOptions` | `MySqlModelSynchronizer` |
| PostgreSQL | `PostgresModelSyncOptions` | `PostgresModelSynchronizer` |
| SQLite | `SQLiteModelSyncOptions` | `SQLiteModelSynchronizer` |

## Production Kullanimi

Once `CompareAsync()` calistirin ve planin tamamini loglayin. `BlockedOperations` bos degilse otomatik apply yapmayin. Production ortamlarinda synchronizer'i uygulama trafigi baslamadan once tek deployment job icinde calistirmak daha guvenlidir.

ModelSync'i sessiz sema mutasyon motoru gibi kullanmayin. Bu ozellik guvenli eklemeleri kolaylastirmak ve riskli veritabani degisikliklerini gorunur hale getirmek icin tasarlanmistir.
