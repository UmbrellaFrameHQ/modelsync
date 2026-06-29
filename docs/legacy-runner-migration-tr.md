# Legacy Runner Gecis Rehberi - Turkce

Bu rehber, gomulu SQL migration gecmisi olan bir projeyi eski history bilgisini kaybetmeden ModelSync'e tasimak icin hazirlanmistir.

Durum: tum provider legacy compatibility gate'leri gectikten sonra 1.2.0 hatti ile yayinlanmistir.

## Compatibility Profile

Mevcut runner table, stored procedure, trigger ve seed klasorlerini ayri yonetiyorsa `MigrationCompatibilityProfiles.LegacyEmbeddedSql` kullanilir.

Profil kategori davranisi:

| Kategori | Mod | Neden |
|---|---|---|
| Tables | HashTracked | Table scriptleri yalniz hash degistiginde tekrar uygulanmalidir. |
| StoredProcedures | EveryRun | Legacy sistemlerde routine scriptleri cogunlukla her run yeniden uygulanir. |
| Triggers | EveryRun | Trigger tanimi script govdesiyle surekli esitlenmelidir. |
| Seeds | RunOnce | Seed scriptleri mevcut datayi duplicate etmemelidir. |
| CustomSql | HashTracked | Custom SQL ilk eklendiginde veya degistiginde calismalidir. |

## Gecis Sirasi

1. Veritabani backup alin.
2. ModelSync'i legacy compatibility profile ile yapilandirin.
3. Once compare calistirin; database'i degistirmeden `SqlHash` upgrade/adoption ihtiyacini raporladigini dogrulayin.
4. Ilk mutation run'i kontrollu ortamda calistirin.
5. Legacy history tablolarina additive `SqlHash` kolonu eklendigini dogrulayin.
6. Seed duplicate korumasini kontrol edin: matching legacy seed satirlari adopt edilmeli, tekrar calismamalidir.
7. Ikinci run ile idempotency kontrolu yapin.
8. Stored procedure ve trigger `EveryRun` davranisini dogrulayin.
9. Eski runner'i yalniz ModelSync history ve idempotency dogrulandiktan sonra kaldirin.
10. Hosted service/startup tarafinda yalniz ModelSync yolunu birakin.
11. Eski migration core kodunu ve legacy runner registration'larini temizleyin.

## Expertis Notlari

Expertis tarzi SQL Server projelerinde mevcut `sec.SchemaMigration_Tables`, `sec.SchemaMigration_StoredProcedures`, `sec.SchemaMigration_Triggers` ve `sec.SchemaMigration_Seeds` satirlari korunmalidir. ModelSync orphan legacy satirlari silmeden `SqlHash` eklemelidir. Custom SQL scriptleri eklendiginde `sec.SchemaMigration_CustomSql` ModelSync tarafindan bootstrap edilebilir.

## Guvenlik Kurallari

Compare read-only kalir. Infrastructure creation, `SqlHash` upgrade, hash adoption, reset, lock ve script execution yalniz mutation API'leriyle yapilir.

Safe ALTER kurallari model tablolari icin degismez. Framework history tablosuna `SqlHash` eklenmesi infrastructure upgrade'dir; destructive model degisikliklerini otomatik hale getirmez.
