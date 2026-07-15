# 03 — Attribute Referansı

## Genel Kural

Her provider kendi attribute setine sahiptir. Attribute isimleri şu şablonu izler:

```
{Provider}{Amaç}Attribute
```

Örnek: `MySqlColumnTypeAttribute`, `SqlServerColumnPrimaryKeyAttribute`, `PostgresTableNameAttribute`, `OracleColumnTypeAttribute`

Bazı attribute'lar (`DbColumnDefault`, `DbColumnCheck`, `DbColumnIndex`, `DbColumnName`, `DbIgnore`) **tüm provider'larda ortaktır**
ve `UmbrellaFrame.ModelSync.Core` namespace'inden gelir.

---

## 1. Tablo Adı Attribute'u

### Kullanım

```csharp
[MySqlTableName("users")]           // MySQL
[SqlServerTableName("users")]       // SQL Server
[PostgresTableName("users")]        // PostgreSQL
[SQLiteTableName("users")]          // SQLite
[OracleTableName("USERS")]          // Oracle preview
public class User { ... }
```

### Detaylar

| Özellik | Değer |
|---|---|
| Hedef | `Class` |
| Zorunlu mu? | Evet (yoksa sınıf adı kullanılır, Analyzer uyarır) |
| Base sınıf | `DbTableNameAttribute` |
| Parametreler | `string tableName` |

> ⚠️ `MSYNC002` Analyzer kuralı: Model sınıfında column attribute var ama table name attribute yoksa derleme uyarısı verilir.

---

## 2. Kolon Tipi Attribute'u

Her veritabanı için enum tabanlı tip sistemi kullanılır.

### MySQL

```csharp
[MySqlColumnType(MySqlColumnType.INT)]
[MySqlColumnType(MySqlColumnType.VARCHAR, "255")]
[MySqlColumnType(MySqlColumnType.DECIMAL, "10,2")]
[MySqlColumnType(MySqlColumnType.ENUM, typeof(StatusEnum))]
[MySqlColumnType(MySqlColumnType.SET, typeof(RoleEnum))]
```

**Desteklenen Tipler:**

| Tip | Uzunluk Gerekir mi? | Açıklama |
|---|:---:|---|
| `TINYINT` | ❌ | 1 byte tam sayı |
| `SMALLINT` | ❌ | 2 byte tam sayı |
| `MEDIUMINT` | ❌ | 3 byte tam sayı |
| `INT` | ❌ | 4 byte tam sayı |
| `BIGINT` | ❌ | 8 byte tam sayı |
| `DECIMAL` | ✅ `"10,2"` | Ondalık sayı |
| `NUMERIC` | ✅ `"10,2"` | Ondalık sayı |
| `FLOAT` | ❌ | Kayan nokta |
| `DOUBLE` | ❌ | Çift hassasiyetli kayan nokta |
| `DATE` | ❌ | Tarih |
| `DATETIME` | ❌ | Tarih ve saat |
| `TIMESTAMP` | ❌ | Unix timestamp |
| `TIME` | ❌ | Sadece saat |
| `YEAR` | ❌ | Yıl |
| `CHAR` | ✅ `"50"` | Sabit uzunluklu metin |
| `VARCHAR` | ✅ `"255"` | Değişken uzunluklu metin (max 65535) |
| `TINYTEXT` | ⬜ İsteğe bağlı | Küçük metin |
| `TEXT` | ⬜ İsteğe bağlı | Orta metin |
| `MEDIUMTEXT` | ❌ | Büyük metin |
| `LONGTEXT` | ❌ | Çok büyük metin |
| `BINARY` | ✅ | İkili veri |
| `VARBINARY` | ✅ | Değişken ikili veri |
| `TINYBLOB` | ❌ | Küçük binary |
| `BLOB` | ❌ | Orta binary |
| `MEDIUMBLOB` | ❌ | Büyük binary |
| `LONGBLOB` | ❌ | Çok büyük binary |
| `ENUM` | ✅ `Type` | Enum değerleri |
| `SET` | ✅ `Type` | Set değerleri |
| `JSON` | ❌ | JSON belgesi |
| `GEOMETRY` | ❌ | Geometrik veri |
| `BIT` | ❌ | Bit alanı |
| `BOOLEAN` | ❌ | Boolean |

### SQL Server

```csharp
[SqlServerColumnType(SqlServerColumnType.NVARCHAR, "255")]
[SqlServerColumnType(SqlServerColumnType.DECIMAL, "18,4")]
[SqlServerColumnType(SqlServerColumnType.UNIQUEIDENTIFIER)]
```

**Desteklenen Tipler:**

| Tip | Uzunluk | Açıklama |
|---|---|---|
| `TINYINT` | ❌ | 1 byte |
| `SMALLINT` | ❌ | 2 byte |
| `INT` | ❌ | 4 byte |
| `BIGINT` | ❌ | 8 byte |
| `DECIMAL` | ✅ `"18,4"` | Ondalık |
| `NUMERIC` | ✅ | Ondalık |
| `FLOAT` | ❌ | Kayan nokta |
| `REAL` | ❌ | Tek hassasiyetli kayan nokta |
| `MONEY` | ❌ | Para birimi |
| `SMALLMONEY` | ❌ | Küçük para birimi |
| `DATE` | ❌ | Tarih |
| `DATETIME` | ❌ | Tarih ve saat |
| `DATETIME2` | ❌ | Yüksek hassasiyetli tarih-saat |
| `DATETIMEOFFSET` | ❌ | Saat dilimli tarih-saat |
| `SMALLDATETIME` | ❌ | Küçük tarih-saat |
| `TIME` | ❌ | Saat |
| `CHAR` | ✅ `"10"` | Sabit metin |
| `VARCHAR` | ✅ `"255"` veya `"MAX"` | Değişken metin |
| `NCHAR` | ✅ | Unicode sabit metin |
| `NVARCHAR` | ✅ `"255"` veya `"MAX"` | Unicode değişken metin |
| `TEXT` | ❌ | Uzun metin (deprecated) |
| `NTEXT` | ❌ | Unicode uzun metin (deprecated) |
| `BINARY` | ✅ | İkili |
| `VARBINARY` | ✅ `"MAX"` | Değişken ikili |
| `IMAGE` | ❌ | Resim (deprecated) |
| `UNIQUEIDENTIFIER` | ❌ | GUID |
| `XML` | ❌ | XML belgesi |
| `GEOGRAPHY` | ❌ | Coğrafi veri |
| `GEOMETRY` | ❌ | Geometrik veri |
| `HIERARCHYID` | ❌ | Hiyerarşi |
| `BIT` | ❌ | Boolean |

### PostgreSQL

```csharp
[PostgresColumnType(PostgresColumnType.INTEGER)]
[PostgresColumnType(PostgresColumnType.VARCHAR, "255")]
[PostgresColumnType(PostgresColumnType.JSONB)]
[PostgresColumnType(PostgresColumnType.DOUBLE_PRECISION)]
```

**Desteklenen Tipler:**

| Tip | Uzunluk | Açıklama |
|---|---|---|
| `SMALLINT` | ❌ | 2 byte |
| `INTEGER` | ❌ | 4 byte |
| `BIGINT` | ❌ | 8 byte |
| `DECIMAL` | ✅ | Ondalık |
| `NUMERIC` | ✅ | Ondalık |
| `REAL` | ❌ | Tek hassasiyetli |
| `DOUBLE_PRECISION` | ❌ | Çift hassasiyetli |
| `SERIAL` | ❌ | Auto-increment int |
| `BIGSERIAL` | ❌ | Auto-increment bigint |
| `MONEY` | ❌ | Para |
| `DATE` | ❌ | Tarih |
| `TIME` | ❌ | Saat |
| `TIMESTAMP` | ❌ | Tarih-saat |
| `TIMESTAMPTZ` | ❌ | Saat dilimli tarih-saat |
| `INTERVAL` | ❌ | Süre |
| `CHAR` | ✅ | Sabit metin |
| `VARCHAR` | ✅ | Değişken metin |
| `TEXT` | ❌ | Sınırsız metin |
| `BYTEA` | ❌ | İkili veri |
| `BOOLEAN` | ❌ | Boolean |
| `UUID` | ❌ | UUID |
| `JSON` | ❌ | JSON |
| `JSONB` | ❌ | Binary JSON (indexed) |
| `XML` | ❌ | XML |
| `INET` | ❌ | IP adresi |
| `CIDR` | ❌ | Ağ adresi |
| `MACADDR` | ❌ | MAC adresi |
| `POINT`, `LINE`, `LSEG` vb. | ❌ | Geometrik tipler |
| `BIT` | ❌ | Bit |
| `VARBIT` | ❌ | Değişken bit |
| `HSTORE` | ❌ | Anahtar-değer |
| `ARRAY` | ❌ | Dizi |
| `RANGE` | ❌ | Aralık |

### SQLite

```csharp
[SQLiteColumnType(SQLiteColumnType.INTEGER)]
[SQLiteColumnType(SQLiteColumnType.TEXT)]
[SQLiteColumnType(SQLiteColumnType.REAL)]
[SQLiteColumnType(SQLiteColumnType.BLOB)]
[SQLiteColumnType(SQLiteColumnType.NUMERIC)]
```

SQLite sadece 5 tip destekler (type affinity sistemi):

| Tip | .NET Karşılığı |
|---|---|
| `INTEGER` | `int`, `long`, `short`, `bool` |
| `REAL` | `float`, `double`, `decimal` |
| `TEXT` | `string`, `char`, `enum` |
| `BLOB` | `byte[]` |
| `NUMERIC` | Ondalık sayı |

### Oracle Preview

```csharp
[OracleColumnType(OracleColumnType.NUMBER, "10")]
[OracleColumnType(OracleColumnType.VARCHAR2, "255")]
[OracleColumnType(OracleColumnType.TIMESTAMP)]
[OracleColumnType(OracleColumnType.BLOB)]
```

Desteklenen tipler: `NUMBER`, `FLOAT`, `BINARY_FLOAT`, `BINARY_DOUBLE`, `CHAR`, `NCHAR`, `VARCHAR2`, `NVARCHAR2`, `CLOB`, `NCLOB`, `BLOB`, `DATE`, `TIMESTAMP`, `RAW`, `LONG` ve `XMLTYPE`.

Oracle paketi NuGet'te yayımlanmıştır ancak preview durumundadır. Attribute tabanlı tablo DDL ve güvenli model karşılaştırmasının desteklenen bölümünde kullanılmalıdır; migration runner özellikleri için [provider destek matrisini](provider-support-matrix.md) kontrol edin.

---

## 3. Primary Key Attribute'u

```csharp
// MySQL — AUTO_INCREMENT
[MySqlColumnPrimaryKey(isAutoIncrement: true)]

// SQL Server — IDENTITY(1,1)
[SqlServerColumnPrimaryKey(isAutoIncrement: true)]

// PostgreSQL — PRIMARY KEY (SERIAL ile birlikte kullanılır)
[PostgresColumnPrimaryKey]

// SQLite — PRIMARY KEY AUTOINCREMENT
[SQLiteColumnPrimaryKey]

// Oracle preview — GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY
[OracleColumnPrimaryKey(isIdentity: true)]
```

> ⚠️ `MSYNC003` Analyzer kuralı: TableName attribute'u olan sınıfta hiçbir property'de primary key yoksa uyarı verilir.

---

## 4. NOT NULL Attribute'u

```csharp
[MySqlColumnNotNull]
[SqlServerColumnNotNull]
[PostgresColumnNotNull]
[SQLiteColumnNotNull]
[OracleColumnNotNull]
```

Üretilen SQL: `NOT NULL`

---

## 5. UNIQUE Attribute'u

```csharp
[MySqlColumnUnique]
[SqlServerColumnUnique]
[PostgresColumnUnique]
[SQLiteColumnUnique]
[OracleColumnUnique]
```

Üretilen SQL: `UNIQUE`

---

## 6. Foreign Key Attribute'u

```csharp
// Parametre: (kendi kolon adı, referans tablo, referans kolon)
[MySqlForeignKey("UserId", "users", "Id")]
[SqlServerColumnForeignKey("UserId", "users", "Id")]
[PostgresForeignKey("UserId", "users", "Id")]
[SQLiteColumnForeignKey("UserId", "users", "Id")]
[OracleForeignKey("USER_ID", "USERS", "ID")]
```

Üretilen SQL:

```sql
FOREIGN KEY (UserId) REFERENCES users(Id)
```

---

## 7. DEFAULT Attribute'u (Tüm provider'larda ortak)

```csharp
using UmbrellaFrame.ModelSync.Core;

[DbColumnDefault("CURRENT_TIMESTAMP")]   // MySQL, PostgreSQL
[DbColumnDefault("GETDATE()")]           // SQL Server
[DbColumnDefault("0")]                   // Sayısal
[DbColumnDefault("'active'")]            // String (tek tırnak dahil)
[DbColumnDefault("true")]                // Boolean
```

Üretilen SQL: `DEFAULT CURRENT_TIMESTAMP`

| Parametre | Tip | Zorunlu |
|---|---|:---:|
| `defaultValue` | `string` | ✅ |

> ⚠️ `defaultValue` boş veya null olamaz — `ArgumentException` fırlatır.
> `defaultValue` raw SQL ifadesi olarak üretilir. Kullanıcı girdisinden veya dinamik metinden oluşturmayın; yalnızca incelenmiş, sabit şema ifadeleri kullanın.

---

## 8. CHECK Constraint Attribute'u (Tüm provider'larda ortak)

```csharp
using UmbrellaFrame.ModelSync.Core;

[DbColumnCheck("Price > 0")]
[DbColumnCheck("Age >= 18 AND Age <= 120")]
[DbColumnCheck("Status IN ('active', 'passive', 'deleted')")]
```

Üretilen SQL: `CHECK (Price > 0)`

| Parametre | Tip | Zorunlu |
|---|---|:---:|
| `expression` | `string` | ✅ |

> ⚠️ `expression` raw SQL ifadesi olarak üretilir. Kullanıcı girdisinden veya dinamik metinden oluşturmayın; kolon adları ve sabit değerler geliştirici tarafından bilinçli yazılmalıdır.

---

## 9. INDEX Attribute'u (Tüm provider'larda ortak)

```csharp
using UmbrellaFrame.ModelSync.Core;

[DbColumnIndex]                                    // Otomatik isim: idx_{tablo}_{kolon}
[DbColumnIndex("idx_users_email")]                 // Özel isim
[DbColumnIndex("idx_users_email", isUnique: true)] // Unique index
```

> Index SQL'i **`GenerateIndexSql<T>()`** metodu ile ayrıca üretilir.
> `CREATE TABLE` içine dahil edilmez.

Üretilen SQL:

```sql
CREATE INDEX `idx_users_email` ON `users` (`Email`);
CREATE UNIQUE INDEX `idx_users_email` ON `users` (`Email`);
```

---

## 10. Kolon Adı Eşleme Attribute'u (Tüm provider'larda ortak)

```csharp
using UmbrellaFrame.ModelSync.Core;

[DbColumnName("product_code")]
[MySqlColumnType(MySqlColumnType.VARCHAR, "64")]
public string Code { get; set; }
```

`DbColumnName` property adından farklı bir database kolon adı kullanmak için tasarlanmıştır. Verilen ad ModelSync identifier kurallarından geçmelidir; boşluk, nokta, tire, tırnak, noktalı virgül ve benzeri şüpheli karakterler reddedilir.

---

## 11. Ignore Attribute'u (Tüm provider'larda ortak)

```csharp
using UmbrellaFrame.ModelSync.Core;

[DbIgnore]
public string DisplayName => $"{FirstName} {LastName}";
```

`DbIgnore`, public olan ama database kolonu olmaması gereken hesaplanmış veya yardımcı property'leri schema discovery dışına çıkarır.

---

## Attribute Kombinasyon Örneği

```csharp
[MySqlTableName("products")]
public class Product
{
    [MySqlColumnType(MySqlColumnType.INT)]
    [MySqlColumnPrimaryKey(isAutoIncrement: true)]
    public int Id { get; set; }

    [MySqlColumnType(MySqlColumnType.VARCHAR, "255")]
    [DbColumnName("sku")]
    [MySqlColumnNotNull]
    [MySqlColumnUnique]
    [DbColumnIndex("idx_products_sku")]
    public string Sku { get; set; }

    [MySqlColumnType(MySqlColumnType.DECIMAL, "10,2")]
    [MySqlColumnNotNull]
    [DbColumnDefault("0.00")]
    [DbColumnCheck("Price >= 0")]
    public decimal Price { get; set; }

    [MySqlColumnType(MySqlColumnType.INT)]
    [MySqlForeignKey("CategoryId", "categories", "Id")]
    public int CategoryId { get; set; }

    [MySqlColumnType(MySqlColumnType.DATETIME)]
    [DbColumnDefault("CURRENT_TIMESTAMP")]
    public DateTime CreatedAt { get; set; }

    [DbIgnore]
    public string DisplayText => Sku;
}
```
