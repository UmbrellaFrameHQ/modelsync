# 07 — Roslyn Analyzer

`UmbrellaFrame.ModelSync.Analyzers` paketi, model sınıflarındaki yapısal hataları **derleme zamanında** tespit eder.
Hatalı attribute kullanımları için beklemenize gerek kalmaz; IDE hemen uyarır.

---

## Kurulum

```bash
dotnet add package UmbrellaFrame.ModelSync.Analyzers
```

Analyzer'lar otomatik olarak aktive olur. Ek yapılandırma gerekmez.

---

## Kural Listesi

| Kural | Kontrol |
|---|---|
| `MSYNC001` | Public property'de kolon tipi eksik |
| `MSYNC002` | Kolon attribute'u olan sınıfta tablo adı eksik |
| `MSYNC003` | Tablo modelinde primary key eksik |
| `MSYNC004` | `DbIgnore` ile mapping attribute'ları birlikte kullanılmış |
| `MSYNC005` | `DbColumnName` güvenli identifier formatında değil |
| `MSYNC006` | Birden fazla default/generated-value attribute'u var |
| `MSYNC007` | Auto-increment desteklenmeyen CLR tipinde kullanılmış |
| `MSYNC008` | Aynı property'de birden fazla provider primary-key attribute'u var |

### MSYNC001 — Eksik Kolon Tipi Attribute'u

| Özellik | Değer |
|---|---|
| **ID** | `MSYNC001` |
| **Şiddet** | Warning |
| **Varsayılan** | Aktif |
| **Kategori** | ModelSync |

**Ne zaman tetiklenir?**  
`[*TableName]` attribute'u olan bir sınıfın public property'si üzerinde
herhangi bir `*ColumnTypeAttribute` yoksa tetiklenir.

**Örnek — Hatalı:**

```csharp
[MySqlTableName("users")]
public class User
{
    // ⚠️ MSYNC001: 'Id' property'si column type attribute eksik
    public int Id { get; set; }

    [MySqlColumnType(MySqlColumnType.VARCHAR, "100")]
    public string Name { get; set; }
}
```

**Düzeltme:**

```csharp
[MySqlColumnType(MySqlColumnType.INT)]
[MySqlColumnPrimaryKey(isAutoIncrement: true)]
public int Id { get; set; }
```

---

### MSYNC002 — Eksik Tablo Adı Attribute'u

| Özellik | Değer |
|---|---|
| **ID** | `MSYNC002` |
| **Şiddet** | Warning |
| **Varsayılan** | Aktif |
| **Kategori** | ModelSync |

**Ne zaman tetiklenir?**  
Bir sınıfın property'lerinde `*ColumnTypeAttribute` bulunmasına rağmen
sınıf üzerinde `*TableNameAttribute` yoksa tetiklenir.

**Örnek — Hatalı:**

```csharp
// ⚠️ MSYNC002: 'Product' sınıfında TableName attribute eksik
public class Product
{
    [MySqlColumnType(MySqlColumnType.INT)]
    public int Id { get; set; }
}
```

**Düzeltme:**

```csharp
[MySqlTableName("products")]
public class Product
{
    [MySqlColumnType(MySqlColumnType.INT)]
    public int Id { get; set; }
}
```

---

### MSYNC003 — Eksik Primary Key

| Özellik | Değer |
|---|---|
| **ID** | `MSYNC003` |
| **Şiddet** | Warning |
| **Varsayılan** | Aktif |
| **Kategori** | ModelSync |

**Ne zaman tetiklenir?**  
`[*TableName]` attribute'u olan bir sınıfın hiçbir property'sinde
`*PrimaryKey` attribute'u yoksa tetiklenir.

**Örnek — Hatalı:**

```csharp
[MySqlTableName("logs")]
public class Log
{
    // ⚠️ MSYNC003: 'Log' tablosunda Primary Key tanımlanmamış
    [MySqlColumnType(MySqlColumnType.INT)]
    public int LogId { get; set; }

    [MySqlColumnType(MySqlColumnType.TEXT)]
    public string Message { get; set; }
}
```

**Düzeltme:**

```csharp
[MySqlColumnType(MySqlColumnType.INT)]
[MySqlColumnPrimaryKey(isAutoIncrement: true)]
public int LogId { get; set; }
```

---

### Mapping Güvenliği — MSYNC004–MSYNC008

Bu kurallar çakışan mapping tanımlarını runtime'a bırakmadan build sırasında gösterir:

```csharp
// MSYNC004: Ignore edilen property database mapping taşımamalı.
[DbIgnore]
[MySqlColumnType(MySqlColumnType.INT)]
public int CalculatedValue { get; set; }

// MSYNC005: Güvenli identifier kullanın.
[DbColumnName("order-code")]
public string Code { get; set; } = string.Empty;

// MSYNC007: Auto increment integral CLR tiplerinde kullanılmalı.
[MySqlColumnPrimaryKey(isAutoIncrement: true)]
public string Id { get; set; } = string.Empty;
```

`MSYNC006` birden fazla default/generated-value tanımını, `MSYNC008` ise aynı property üzerindeki farklı provider primary-key attribute'larını bildirir.

---

## Kuralı Bastırma (Suppress)

Belirli bir uyarıyı kasıtlı olarak görmezden gelmek istiyorsanız:

```csharp
#pragma warning disable MSYNC003
[MySqlTableName("junction_table")]
public class UserProduct
{
    [MySqlColumnType(MySqlColumnType.INT)]
    public int UserId { get; set; }

    [MySqlColumnType(MySqlColumnType.INT)]
    public int ProductId { get; set; }
}
#pragma warning restore MSYNC003
```

Veya `.editorconfig` ile proje genelinde kapatabilirsiniz:

```ini
[*.cs]
dotnet_diagnostic.MSYNC003.severity = none
```

---

## IDE Desteği

| IDE | Destek |
|---|---|
| Visual Studio 2019+ | ✅ Tam destek (ampul önerileri, dalgalı çizgi) |
| Visual Studio Code + C# Dev Kit | ✅ |
| Rider | ✅ |
| `dotnet build` CLI | ✅ Warning olarak çıktılanır |

---

## .editorconfig ile Şiddet Ayarı

```ini
[*.cs]
# Uyarı yerine error yap (CI/CD'de derlemeyi kır)
dotnet_diagnostic.MSYNC001.severity = error
dotnet_diagnostic.MSYNC002.severity = error
dotnet_diagnostic.MSYNC003.severity = error
dotnet_diagnostic.MSYNC004.severity = error
dotnet_diagnostic.MSYNC005.severity = error
dotnet_diagnostic.MSYNC006.severity = error
dotnet_diagnostic.MSYNC007.severity = error
dotnet_diagnostic.MSYNC008.severity = error

# Kapat
dotnet_diagnostic.MSYNC001.severity = none
```
