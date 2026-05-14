# MySQL Quickstart

## Paket

```bash
dotnet add package UmbrellaFrame.ModelSync.MySql
```

## Program.cs

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

    [MySqlColumnType(MySqlColumnType.INT)]
    [DbColumnDefault("0")]
    public int Stock { get; set; }
}

var connectionString =
    "Server=localhost;Database=shopdb;User=root;Password=pass;";

var generator = new MySqlTableGenerator(connectionString);

generator.CreateDatabase();

var sql = generator.GenerateMySqlTable<Product>(ifNotExists: true);
Console.WriteLine(sql);

await generator.CreateTablesAsync();
```

## Notlar

- `GenerateMySqlTable<T>()` SQL'i uretir ve cache'e alir.
- `CreateTablesAsync()` cache'teki SQL'leri calistirir.
- `DbColumnDefault` ve `DbColumnCheck` raw SQL ifade kabul eder; bu ifadeleri uygulama disi girdilerden uretmeyin.

