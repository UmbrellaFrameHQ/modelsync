# SQLite In-Memory Example

SQLite `:memory:` senaryosu testler icin kullanislidir. ModelSync ile hizli tablo olusturup SQL uretimini dogrulayabilirsiniz.

## Paket

```bash
dotnet add package UmbrellaFrame.ModelSync.SQLite
```

## Program.cs

```csharp
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.SQLite;

[SQLiteTableName("logs")]
public class LogEntry
{
    [SQLiteColumnType(SQLiteColumnType.INTEGER)]
    [SQLiteColumnPrimaryKey]
    public int Id { get; set; }

    [SQLiteColumnType(SQLiteColumnType.TEXT)]
    [SQLiteColumnNotNull]
    public string Message { get; set; }

    [SQLiteColumnType(SQLiteColumnType.TEXT)]
    [DbColumnDefault("'Info'")]
    public string Level { get; set; }

    [SQLiteColumnType(SQLiteColumnType.REAL)]
    public double Timestamp { get; set; }
}

var generator = new SQLiteTableGenerator("Data Source=:memory:");

var sql = generator.GenerateSQLiteTable<LogEntry>(ifNotExists: true);
Console.WriteLine(sql);

generator.CreateTables();
```

## SQLite Kisitlari

- SQLite `ALTER COLUMN TYPE` desteklemez.
- ModelSync SQLite provider'i `AlterColumnType` icin `NotSupportedException` firlatir.
- Tip degistirme gerekiyorsa genellikle yeni tablo olustur, veriyi kopyala, eski tabloyu sil stratejisi kullanilir.

