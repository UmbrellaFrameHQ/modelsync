# 10 — Changelog

Tüm önemli değişiklikler bu dosyada belgelenmiştir.
Format: [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)
Sürümleme: [Semantic Versioning](https://semver.org/)

---

## [Yayınlanmamış]

### Eklendi
- Kapsamlı `docs/` dizini (10 bölümlük dokümantasyon seti)

---

## [1.0.3] — NuGet README Uyumluluğu

### Değiştirildi
- Paket README içeriğindeki HTML başlık kaldırıldı.
- Logo yolu NuGet üzerinde çalışacak mutlak GitHub raw URL ile güncellendi.
- Paket sürümleri `1.0.3` olarak güncellendi.

---

## [1.0.2] — Güvenlik Sertleştirmesi

### Eklendi
- `DestructiveOperationOptions` eklendi.
- `DropTables`, `DropColumn` ve `AlterColumnType` için açık destructive-operation onayı eklendi.
- Tablo, kolon, index ve veritabanı adları için sıkı identifier doğrulaması eklendi.
- Attribute tablo adıyla cached drop SQL üretimini doğrulayan testler eklendi.
- Şüpheli identifier değerlerinin reddedildiğini doğrulayan testler eklendi.

### Değiştirildi
- Provider quote implementasyonları `QuoteValidatedIdentifier` üzerine taşındı; doğrulama base class içinde merkezi hale getirildi.
- README tekrarlı içerikten arındırıldı ve v1.0.2 güvenlik davranışını anlatacak şekilde güncellendi.
- Paket sürümleri `1.0.2` olarak güncellendi.

### Düzeltildi
- `DropTables()` akışının attribute ile verilen tablo adı yerine model class adını kullanabilmesi düzeltildi.
- MySQL, PostgreSQL, SQL Server ve SQLite provider'larında drop işlemleri cached table-name metadata ile hizalandı.

---

## [1.0.0] — Yakında

### Eklendi
- `ITableGenerator` arayüzü (DI desteği)
- `SqlTableGenerator` soyut temel sınıfı
- Provider-specific identifier quoting (backtick / köşeli parantez / çift tırnak)
- `IF NOT EXISTS` desteği (MySQL, PostgreSQL, SQLite; SQL Server hariç)
- Async API: `GenerateSqlTableAsync`, `CreateTablesAsync`, `DropTablesAsync`
- `GenerateDropTableSql<T>()` — DROP TABLE ifadesi üretimi
- `GenerateTruncateTableSql<T>()` — TRUNCATE TABLE ifadesi üretimi
- `GenerateIndexSql<T>()` — CREATE [UNIQUE] INDEX ifadeleri üretimi
- `DbColumnDefaultAttribute` — DEFAULT constraint desteği
- `DbColumnCheckAttribute` — CHECK constraint desteği
- `DbColumnIndexAttribute` — Index tanımı desteği
- `ConcurrentDictionary` tabanlı per-instance önbellek (thread-safe)
- `MetadataToken` ile deterministik property sıralaması
- `ILogger` entegrasyonu (`NullLogger` fallback ile)
- Roslyn Analyzer: `MSYNC001` — Eksik kolon tipi attribute'u
- Roslyn Analyzer: `MSYNC002` — Eksik tablo adı attribute'u
- Roslyn Analyzer: `MSYNC003` — Eksik primary key
- GitHub Actions CI/CD pipeline
- README.md, LICENSE (MIT), .gitignore
- Tüm library'ler için NuGet metadata

### Değiştirildi
- Hedef framework `netcoreapp3.1` → `netstandard2.0` (daha geniş uyumluluk)
- Generator sınıfları yeniden isimlendirildi (`GenerateMySqlTable`, `GenerateSqlServerTable`, vb.)
- DDL çalıştırma `ExecuteReader`/`ExecuteScalar` → `ExecuteNonQuery`/`ExecuteNonQueryAsync`

### Düzeltildi
- Paylaşılan static önbellek nedeniyle provider'lar arası SQL kirlenmesi giderildi
- Property sıralama garanti altına alındı (`MetadataToken` ile)

---

## [0.1.0] — İlk Sürüm (Internal)

### Eklendi
- Temel MySQL attribute tabanlı CREATE TABLE üretimi
- `MySqlTableGenerator` sınıfı
- `MySqlColumnTypeAttribute`, `MySqlTableNameAttribute`
- `DynamicPropertyManager<T>` yansıma yardımcısı
