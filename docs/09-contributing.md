# 09 — Katkı Rehberi

## Gereksinimler

| Araç | Minimum Sürüm |
|---|---|
| .NET SDK | 8.0 |
| Visual Studio | 2022 (17.8+) veya VS Code |
| Git | 2.x |

---

## Projeyi Klonlama

```bash
git clone https://github.com/UmbrellaFrame/ModelSync.git
cd ModelSync
dotnet restore
dotnet build
```

---

## Proje Yapısı

```
ModelSync/
├── UmbrellaFrame.ModelSync.Core/          # netstandard2.0
├── UmbrellaFrame.ModelSync.MySql/         # netstandard2.0
├── UmbrellaFrame.ModelSync.SqlServer/     # netstandard2.0
├── UmbrellaFrame.ModelSync.PostgreSQL/    # netstandard2.0
├── UmbrellaFrame.ModelSync.SQLite/        # netstandard2.0
├── UmbrellaFrame.ModelSync.Analyzers/     # netstandard2.0
├── UmbrellaFrame.ModelSync.CoreTest/      # net8.0
├── UmbrellaFrame.ModelSync.MySqlTest/     # net8.0
├── UmbrellaFrame.ModelSync.SqlServerTest/ # net8.0
├── UmbrellaFrame.ModelSync.PostgreSQLTest/# net8.0
├── UmbrellaFrame.ModelSync.SQLiteTest/    # net8.0
└── docs/
```

---

## Testleri Çalıştırma

### Unit Testleri (Canlı DB Gerektirmez)

```bash
dotnet test UmbrellaFrame.ModelSync.CoreTest
dotnet test UmbrellaFrame.ModelSync.SQLiteTest   # :memory: kullanır
```

### Integration Testleri (Canlı DB Gerektirir)

```bash
# MySQL için
docker run -d -p 3306:3306 -e MYSQL_ROOT_PASSWORD=secret -e MYSQL_DATABASE=testdb mysql:8

# SQL Server için
docker run -d -p 1433:1433 -e SA_PASSWORD=Secret!123 -e ACCEPT_EULA=Y mcr.microsoft.com/mssql/server:2022-latest

# PostgreSQL için
docker run -d -p 5432:5432 -e POSTGRES_PASSWORD=secret postgres:16

dotnet test
```

---

## Kod Standartları

### Naming

| Tür | Kural | Örnek |
|---|---|---|
| Sınıf | PascalCase | `MySqlTableGenerator` |
| Interface | I + PascalCase | `ITableGenerator` |
| Method | PascalCase | `GenerateSqlTable` |
| Property | PascalCase | `ConnectionString` |
| Field (private) | `_camelCase` | `_connectionString` |
| Sabit | UPPER_SNAKE | `MSYNC001` |
| Namespace | `UmbrellaFrame.ModelSync.{Provider}` | |

### Attribute Naming Kuralı

```
{Provider}{Amaç}Attribute

MySqlColumnTypeAttribute     ✅
MySQLColumntypeattribute     ❌
```

### Yorum ve XML Dokümantasyon

Tüm public API üyeleri XML dokümantasyon içermelidir:

```csharp
/// <summary>
/// Generates a CREATE TABLE SQL statement for the given model type.
/// </summary>
/// <typeparam name="T">Model class decorated with table/column attributes.</typeparam>
/// <param name="ifNotExists">When <c>true</c>, emits CREATE TABLE IF NOT EXISTS.</param>
/// <returns>The generated SQL string.</returns>
public string GenerateSqlTable<T>(bool ifNotExists = false) where T : class, new()
```

### `netstandard2.0` Uyumluluk Kuralları

- `await using` **yasak** → `using` + `OpenAsync` kullanın
- `IAsyncEnumerable` **yasak**
- C# 8+ nullable ref types — `#nullable enable` ile aktive edilebilir ama zorunlu değil
- `System.Text.Json` yerine `Newtonsoft.Json` tercih edilebilir (uyumluluk açısından)

---

## Yeni Özellik Ekleme

1. `feature/{kısa-açıklama}` branch oluşturun
2. Önce test yazın (TDD önerilir)
3. Implementasyonu yapın
4. `dotnet build` ile derleme hatası olmadığını kontrol edin
5. `dotnet test` ile testlerin geçtiğini doğrulayın
6. Dokümantasyonu güncelleyin (`docs/`)
7. PR açın

### PR Kontrol Listesi

- [ ] Tüm testler geçiyor
- [ ] Yeni kod için unit test var
- [ ] XML dokümantasyon eklenmiş
- [ ] `docs/` güncellenmiş
- [ ] `docs/10-changelog.md` güncellenmiş
- [ ] `netstandard2.0` uyumlu
- [ ] Yeni attribute eklendiyse Analyzer güncellenmiş mi?

---

## Yeni Provider Ekleme

1. `UmbrellaFrame.ModelSync.{Provider}` projesini oluşturun (netstandard2.0)
2. `UmbrellaFrame.ModelSync.Core`'a project reference ekleyin
3. Şu sınıfları ekleyin:
   - `{Provider}TableGenerator : SqlTableGenerator, ITableGenerator`
   - `{Provider}TableNameAttribute : DbTableNameAttribute`
   - `{Provider}ColumnTypeAttribute : DbColumnTypeAttribute`
   - `{Provider}ColumnPrimaryKeyAttribute : DbColumnPrimaryKeyAttribute`
   - `{Provider}ColumnNotNullAttribute : DbColumnNotNullAttribute`
   - `{Provider}ColumnUniqueAttribute : DbColumnUniqueAttribute`
   - `{Provider}ColumnForeignKeyAttribute : DbColumnForeignKeyAttribute`
4. Test projesi ekleyin: `UmbrellaFrame.ModelSync.{Provider}Test` (net8.0)
5. `docs/04-providers.md` güncelleyin
6. `docs/index.md` tablosuna ekleyin

---

## CI/CD

GitHub Actions yapılandırması `.github/workflows/ci.yml` altındadır.
Her PR ve `main` branch push'unda:

- `dotnet restore`
- `dotnet build --configuration Release`
- `dotnet test --filter "Category!=Integration"` çalışır

---

## Soru ve Destek

- **Bug Report:** GitHub Issues
- **Özellik İsteği:** GitHub Discussions
- **Güvenlik:** Güvenlik açıklarını Issues üzerinden değil, e-posta ile bildirin
