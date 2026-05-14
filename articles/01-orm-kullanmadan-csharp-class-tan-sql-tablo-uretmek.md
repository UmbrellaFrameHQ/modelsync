# ModelSync ile ORM Kullanmadan C# Class'tan SQL Tablo Uretmek

Bir .NET uygulamasinda veritabani semasini yonetmek icin her zaman tam bir ORM kullanmak zorunda degilsiniz. Bazen ihtiyac daha sadedir: bir C# modelini attribute'larla tanimlamak, bu modelden okunabilir SQL uretmek ve tabloyu bilincli sekilde olusturmak.

ModelSync bu aralik icin tasarlandi. Entity Framework gibi query tracking, change tracking veya migration sistemi sunmaz. Bunun yerine plain C# class'lari okur ve `CREATE TABLE`, `ALTER TABLE`, `DROP TABLE`, `TRUNCATE TABLE` ve `CREATE INDEX` gibi DDL komutlari uretir.

## Problem

Kucuk ve orta olcekli .NET projelerinde su ihtiyac sik gorulur:

- ORM kullanmadan tablo semasi uretmek
- Model ile SQL arasindaki kopyala-yapistir hatalarini azaltmak
- MySQL, SQL Server, PostgreSQL veya SQLite icin benzer model deneyimi kullanmak
- Destructive islemleri otomatik degil, bilincli calistirmak

Bu noktada ham SQL yazmak guclu ama tekrarlidir. Tam ORM kullanmak ise bazen ihtiyactan buyuk bir cozum olur.

## ModelSync Yaklasimi

ModelSync model sinifini attribute'larla okur:

```csharp
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.MySql;

[MySqlTableName("products")]
public class Product
{
    [MySqlColumnType(MySqlColumnType.INT)]
    [MySqlColumnPrimaryKey(isAutoIncrement: true)]
    public int Id { get; set; }

    [MySqlColumnType(MySqlColumnType.VARCHAR, "255")]
    [MySqlColumnNotNull]
    public string Name { get; set; }

    [MySqlColumnType(MySqlColumnType.DECIMAL, "10,2")]
    [DbColumnDefault("0.00")]
    [DbColumnCheck("Price >= 0")]
    public decimal Price { get; set; }
}
```

Sonra provider generator ile SQL uretilir:

```csharp
var generator = new MySqlTableGenerator(
    "Server=localhost;Database=shopdb;User=root;Password=pass;"
);

var sql = generator.GenerateMySqlTable<Product>(ifNotExists: true);
Console.WriteLine(sql);
```

Uretilen SQL yaklasik olarak su sekildedir:

```sql
CREATE TABLE IF NOT EXISTS `products` (
    `Id` INT PRIMARY KEY AUTO_INCREMENT,
    `Name` VARCHAR(255) NOT NULL,
    `Price` DECIMAL(10,2) DEFAULT 0.00 CHECK (Price >= 0)
);
```

## Neden ORM Degil?

ORM'ler degerlidir, ama her projede ayni problem cozulmez. EF Core veri okuma-yazma, tracking, LINQ sorgulari ve migration akisiyla daha buyuk bir yapi sunar. ModelSync ise daha dar bir alana odaklanir:

- Modelden SQL sema uretimi
- Provider bazli identifier quoting
- Compile-time analyzer uyarilari
- Acik DDL calistirma metotlari
- Destructive islemler icin explicit opt-in

Bu nedenle ModelSync, EF Core'un yerine gecmekten cok, ORM istemeyen projelerde sema uretim katmani olarak konumlanir.

## Guvenli Varsayilan

ModelSync v1 canli veritabanini kendi kendine senkronize etmez. Bu bilincli bir tasarim karari. Veritabani semasi ana merkezdir; otomatik ve sessiz bir `DROP COLUMN` kullanici icin pahali olabilir.

Bu yuzden destructive islemler icin acik onay gerekir:

```csharp
var allow = DestructiveOperationOptions.Allow();

generator.DropColumn<Product>("LegacyCode", allow);
generator.AlterColumnType<Product>("Price", allow);
generator.DropTables(allow);
```

Onay verilmeden bu metotlar exception firlatir.

## Ne Zaman Kullanilir?

ModelSync su senaryolarda iyi oturur:

- Mikro servislerde hafif sema olusturma
- Test ortaminda hizli tablo kurma
- ORM kullanmayan Dapper tabanli projeler
- Plugin veya modul sistemlerinde modelden tablo uretme
- SQL'i gormek ve kontrol etmek isteyen ekipler

Sema diff, migration history ve otomatik live database sync gerekiyorsa ModelSync v1 bunu henuz hedeflemez. Bu alan Faz 2 icin planlanir: canli veritabaniyla modeli karsilastirip guvenli ALTER TABLE plani uretmek.

## Sonuc

ModelSync'in ana fikri basit: C# modeli semanin kaynagi olabilir, ama veritabani degisiklikleri gelistiricinin kontrolunde kalmalidir. Bu denge, ozellikle ORM kullanmak istemeyen ama elle SQL tekrarindan da yorulan .NET projeleri icin pratik bir ara katman sunar.

