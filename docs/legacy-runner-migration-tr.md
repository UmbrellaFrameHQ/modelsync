# Legacy Runner Geçiş Rehberi - Türkçe

Bu rehber, gömülü SQL migration geçmişi olan bir projeyi eski history bilgisini kaybetmeden ModelSync'e taşımak için hazırlanmıştır.

Durum: tüm provider legacy compatibility gate'leri geçtikten sonra 1.2.0 hattı ile yayımlanmıştır.

## Compatibility Profile

Mevcut runner table, stored procedure, trigger ve seed klasörlerini ayrı yönetiyorsa `MigrationCompatibilityProfiles.LegacyEmbeddedSql` kullanılır.

Profil kategori davranışı:

| Kategori | Mod | Neden |
|---|---|---|
| Tables | HashTracked | Table scriptleri yalnız hash değiştiğinde tekrar uygulanmalıdır. |
| StoredProcedures | EveryRun | Legacy sistemlerde routine scriptleri çoğunlukla her run yeniden uygulanır. |
| Triggers | EveryRun | Trigger tanımı script gövdesiyle sürekli eşitlenmelidir. |
| Seeds | RunOnce | Seed scriptleri mevcut datayı duplicate etmemelidir. |
| CustomSql | HashTracked | Custom SQL ilk eklendiğinde veya değiştiğinde çalışmalıdır. |

## Geçiş Sırası

1. Veritabanı backup alın.
2. ModelSync'i legacy compatibility profile ile yapılandırın.
3. Önce compare çalıştırın; database'i değiştirmeden `SqlHash` upgrade/adoption ihtiyacını raporladığını doğrulayın.
4. İlk mutation run'ı kontrollü ortamda çalıştırın.
5. Legacy history tablolarına additive `SqlHash` kolonu eklendiğini doğrulayın.
6. Seed duplicate korumasını kontrol edin: matching legacy seed satırları adopt edilmeli, tekrar çalışmamalıdır.
7. İkinci run ile idempotency kontrolü yapın.
8. Stored procedure ve trigger `EveryRun` davranışını doğrulayın.
9. Eski runner'ı yalnız ModelSync history ve idempotency doğrulandıktan sonra kaldırın.
10. Hosted service/startup tarafında yalnız ModelSync yolunu bırakın.
11. Eski migration core kodunu ve legacy runner registration'larını temizleyin.

## Expertis Notları

Expertis tarzı SQL Server projelerinde mevcut `sec.SchemaMigration_Tables`, `sec.SchemaMigration_StoredProcedures`, `sec.SchemaMigration_Triggers` ve `sec.SchemaMigration_Seeds` satırları korunmalıdır. ModelSync orphan legacy satırları silmeden `SqlHash` eklemelidir. Custom SQL scriptleri eklendiğinde `sec.SchemaMigration_CustomSql` ModelSync tarafından bootstrap edilebilir.

## Güvenlik Kuralları

Compare read-only kalır. Infrastructure creation, `SqlHash` upgrade, hash adoption, reset, lock ve script execution yalnız mutation API'leriyle yapılır.

Safe ALTER kuralları model tabloları için değişmez. Framework history tablosuna `SqlHash` eklenmesi infrastructure upgrade'dir; destructive model değişikliklerini otomatik hale getirmez.
