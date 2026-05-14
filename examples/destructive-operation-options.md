# DestructiveOperationOptions Example

ModelSync, veri kaybi olusturabilecek DDL islemlerini bilincli onay olmadan calistirmaz.

## Riskli Islemler

Asagidaki islemler explicit opt-in ister:

- `DropTables`
- `DropColumn`
- `AlterColumnType`

## Ornek

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

    [MySqlColumnType(MySqlColumnType.VARCHAR, "100")]
    public string LegacyCode { get; set; }
}

var generator = new MySqlTableGenerator(
    "Server=localhost;Database=shopdb;User=root;Password=pass;"
);

generator.GenerateMySqlTable<Product>(ifNotExists: true);

// Bu cagri tasarim geregi exception firlatir.
// generator.DropColumn<Product>("LegacyCode");

var allow = DestructiveOperationOptions.Allow();

generator.DropColumn<Product>("LegacyCode", allow);
generator.DropTables(allow);
```

## Neden Bu Tasarim?

Kodda `DestructiveOperationOptions.Allow()` gorundugunde review yapan kisi riskli bir sema operasyonu oldugunu hemen anlar. Bu, ozellikle production veritabaniyla calisan kutuphaneler icin onemli bir guvenlik sinyalidir.

