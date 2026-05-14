# .NET'te Guvenli ALTER TABLE Yaklasimi: Neden Destructive Islemler Explicit Olmali?

Veritabani semasi bir uygulamanin en hassas bolgelerinden biridir. Kodda yapilan bir hata rollback edilebilir; fakat yanlis bir `DROP COLUMN`, `ALTER COLUMN` veya `DROP TABLE` gercek veriyi etkileyebilir. Bu nedenle sema araclarinda guvenli varsayilanlar cok onemlidir.

ModelSync v1.0.2 ile destructive DDL islemleri icin explicit opt-in zorunlu hale getirildi. Yani kullanici veri kaybi olusturabilecek bir islemi calistirmak istiyorsa bunu kodda acikca belirtmek zorunda.

## Destructive DDL Nedir?

Sema islemleri ayni risk seviyesinde degildir.

Genellikle daha guvenli olan islemler:

- Yeni tablo olusturmak
- Yeni kolon eklemek
- Index SQL'i uretmek

Daha riskli veya destructive kabul edilen islemler:

- Tablo silmek
- Kolon silmek
- Kolon tipini degistirmek
- Tabloyu bosaltmak

Ozellikle kolon tipi degistirme islemi provider'a ve mevcut veriye gore veri kaybi veya donusum hatasi uretebilir.

## Kotu Varsayilan Nasil Gorunur?

Tehlikeli tasarim sudur:

```csharp
generator.DropColumn<Product>("LegacyCode");
```

Bu satir cok kolay yazilir, review'da gozden kacabilir ve production verisini etkileyebilir. Sema araclarinda "kolaylik" bazen fazla pahali olabilir.

## ModelSync'in Yaklasimi

ModelSync destructive operasyonlari acik izin nesnesiyle calistirir:

```csharp
using UmbrellaFrame.ModelSync.Core;

var allow = DestructiveOperationOptions.Allow();

generator.DropColumn<Product>("LegacyCode", allow);
generator.AlterColumnType<Product>("Price", allow);
generator.DropTables(allow);
```

Bu kucuk detay kodu okuyan kisiye sunu soyler:

> Burada veri kaybi ihtimali olan bir sema islemi bilincli olarak calistiriliyor.

Bu sadece teknik bir koruma degil, ayni zamanda kod review icin iyi bir sinyaldir.

## Onay Verilmezse Ne Olur?

Onay verilmeden destructive metot cagrilirsa ModelSync exception firlatir. Bu sayede eski veya dikkatsiz kullanim production'a sessizce ilerlemez.

```csharp
// Tasarim geregi exception firlatir.
generator.DropColumn<Product>("LegacyCode");
```

Dogru kullanim:

```csharp
generator.DropColumn<Product>(
    "LegacyCode",
    DestructiveOperationOptions.Allow()
);
```

## Identifier Guvenligi

Destructive onay tek basina yeterli degildir. SQL ureten bir kutuphane tablo ve kolon adlarini da ciddiye almalidir.

ModelSync su identifier desenini kabul eder:

```text
^[A-Za-z_][A-Za-z0-9_]*$
```

Bu bilincli olarak sunlari reddeder:

- Bosluk
- Nokta
- Tirnak
- Koseli parantez
- Noktali virgul
- Hyphen
- SQL parcalari

Sonra provider'a gore quote edilir:

| Provider | Ornek |
|---|---|
| MySQL | `` `products` `` |
| SQL Server | `[Products]` |
| PostgreSQL | `"products"` |
| SQLite | `"products"` |

## Neden Otomatik Sync Degil?

Canli veritabaniyla modeli karsilastirip otomatik ALTER TABLE calistirmak guclu bir ozelliktir, ama erken asamada risklidir. ModelSync'in ilk fazinda bu nedenle otomatik manipilasyon yerine gelistirici kontrollu DDL tercih edildi.

Faz 2 icin daha dogru yol sudur:

1. Model ile canli veritabani semasini karsilastir.
2. Bir plan uret.
3. Planin risk seviyesini siniflandir.
4. Dry-run SQL goster.
5. Destructive islemler icin acik onay iste.

Bu yaklasim hizdan biraz kaybettirir, ama veritabani araclari icin guven daha onemlidir.

## Sonuc

`ALTER TABLE` otomasyonu etkileyici gorunebilir, fakat yanlis varsayilanla kullaniciya pahali patlayabilir. ModelSync'in explicit destructive-operation modeli, kucuk bir API maliyetiyle daha okunabilir, daha guvenli ve daha review edilebilir sema operasyonlari saglar.

