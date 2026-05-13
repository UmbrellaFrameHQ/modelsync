# 01 — Genel Bakış

## ModelSync Nedir?

**ModelSync**, C# model sınıflarınıza eklediğiniz attribute'lar aracılığıyla
`CREATE TABLE`, `DROP TABLE`, `TRUNCATE TABLE` ve `CREATE INDEX` SQL ifadelerini
**otomatik olarak üretip veritabanına uygulayan** bir .NET kütüphanesidir.

- ❌ Entity Framework Core gerektirmez
- ❌ Reflection tabanlı ağır ORM gerektirmez
- ✅ Sıfır ORM bağımlılığı
- ✅ Dört veritabanı sağlayıcısı
- ✅ Async/await tam desteği
- ✅ Dependency Injection uyumlu (`ITableGenerator`)
- ✅ Compile-time uyarılar (Roslyn Analyzer)

---

## Desteklenen Veritabanları

| Sağlayıcı | NuGet Paketi | Bağlantı Kütüphanesi | Quoting |
|---|---|---|---|
| MySQL / MariaDB | `UmbrellaFrame.ModelSync.MySql` | MySqlConnector | `` `column` `` |
| SQL Server | `UmbrellaFrame.ModelSync.SqlServer` | Microsoft.Data.SqlClient | `[column]` |
| PostgreSQL | `UmbrellaFrame.ModelSync.PostgreSQL` | Npgsql | `"column"` |
| SQLite | `UmbrellaFrame.ModelSync.SQLite` | Microsoft.Data.Sqlite | `"column"` |

---

## Proje Yapısı

```
ModelSync/
├── UmbrellaFrame.ModelSync.Core/          # Soyut altyapı (netstandard2.0)
│   ├── Attributes/                        # Tüm base attribute sınıfları
│   ├── Interfaces/                        # ITableGenerator
│   ├── Services/                          # SqlTableGenerator (abstract)
│   ├── Helpers/                           # DynamicPropertyManager<T>
│   ├── Models/                            # PropertyMetadata
│   └── Exceptions/                        # PropertyNotFoundException
│
├── UmbrellaFrame.ModelSync.MySql/         # MySQL provider (netstandard2.0)
├── UmbrellaFrame.ModelSync.SqlServer/     # SQL Server provider (netstandard2.0)
├── UmbrellaFrame.ModelSync.PostgreSQL/    # PostgreSQL provider (netstandard2.0)
├── UmbrellaFrame.ModelSync.SQLite/        # SQLite provider (netstandard2.0)
│
├── UmbrellaFrame.ModelSync.Analyzers/     # Roslyn Analyzer (netstandard2.0)
│
├── UmbrellaFrame.ModelSync.CoreTest/      # Core unit testleri (net8.0)
├── UmbrellaFrame.ModelSync.MySqlTest/     # MySQL unit testleri (net8.0)
├── UmbrellaFrame.ModelSync.SqlServerTest/ # SQL Server unit testleri (net8.0)
├── UmbrellaFrame.ModelSync.PostgreSQLTest/# PostgreSQL unit testleri (net8.0)
└── UmbrellaFrame.ModelSync.SQLiteTest/    # SQLite unit testleri (net8.0)
```

---

## Benzer Araçlarla Karşılaştırma

| Özellik | ModelSync | EF Core Migrations | FluentMigrator | Dapper |
|---|:---:|:---:|:---:|:---:|
| ORM bağımlılığı | ❌ Yok | ✅ EF Core | ❌ Yok | ❌ Yok |
| Attribute tabanlı | ✅ | ✅ | ❌ | ❌ |
| Schema üretimi | ✅ | ✅ | ✅ | ❌ |
| Async DDL | ✅ | ✅ | ❌ | - |
| DI uyumlu | ✅ | ✅ | ✅ | ✅ |
| Roslyn Analyzer | ✅ | ❌ | ❌ | ❌ |
| Sıfır yapılandırma | ✅ | ❌ | ❌ | ✅ |
| 4 DB sağlayıcı | ✅ | ✅ | ✅ | ✅ |

---

## Hedef Kitle

- Micro-servis geliştiricileri (EF Core olmadan şema yönetimi isteyenler)
- Hafif CLI araçları / migration scriptleri yazanlar
- Tam ORM kullanmadan type-safe DDL üretmek isteyenler
- Mevcut ADO.NET / Dapper projelerine şema yönetimi eklemek isteyenler
