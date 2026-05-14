# EF Core Migration Kullanmadan Basit Schema Generation Nasil Yapilir?

EF Core Migration .NET ekosisteminde guclu ve olgun bir cozumdur. Ancak her proje EF Core kullanmaz. Dapper kullanan, ham ADO.NET ile calisan veya sadece sema uretimi isteyen projelerde daha hafif bir yaklasim tercih edilebilir.

ModelSync bu ihtiyac icin attribute tabanli schema generation sunar.

## Hedef

Bu yazida amac su:

- C# modelinden tablo SQL'i uretmek
- Provider'a ozel type ve quoting kullanmak
- SQL'i ister ekrana yazmak, ister canli veritabaninda calistirmak
- Riskli operasyonlari otomatik yapmamak

## Kurulum

Sadece kullanacaginiz provider paketini yukleyin:

```bash
dotnet add package UmbrellaFrame.ModelSync.MySql
```

SQL Server icin:

```bash
dotnet add package UmbrellaFrame.ModelSync.SqlServer
```

SQLite icin:

```bash
dotnet add package UmbrellaFrame.ModelSync.SQLite
```

Analyzer isterseniz:

```bash
dotnet add package UmbrellaFrame.ModelSync.Analyzers
```

## Model Tanimlama

```csharp
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.MySql;

[MySqlTableName("users")]
public class User
{
    [MySqlColumnType(MySqlColumnType.INT)]
    [MySqlColumnPrimaryKey(isAutoIncrement: true)]
    public int Id { get; set; }

    [MySqlColumnType(MySqlColumnType.VARCHAR, "120")]
    [MySqlColumnNotNull]
    public string Email { get; set; }

    [MySqlColumnType(MySqlColumnType.DATETIME)]
    [DbColumnDefault("CURRENT_TIMESTAMP")]
    public DateTime CreatedAt { get; set; }
}
```

Bu modelde tablo adi, kolon tipleri, primary key, not-null ve default ifade kod uzerinden tanimlanir.

## SQL Uretmek

```csharp
var generator = new MySqlTableGenerator(
    "Server=localhost;Database=appdb;User=root;Password=pass;"
);

string sql = generator.GenerateMySqlTable<User>(ifNotExists: true);

Console.WriteLine(sql);
```

Bu adim veritabanina dokunmadan SQL uretir. CI icinde snapshot test almak veya migration review yapmak icin kullanislidir.

## SQL'i Calistirmak

ModelSync uretilen SQL'i cache'e alir. Sonra `CreateTables` ile calistirabilirsiniz:

```csharp
generator.GenerateMySqlTable<User>(ifNotExists: true);
await generator.CreateTablesAsync();
```

Burada onemli nokta: `CreateTables` sadece daha once generate edilmis tablolar uzerinden calisir.

## ALTER TABLE

Kolon eklemek additive bir operasyondur:

```csharp
generator.AddColumn<User>("DisplayName");
```

Kolon silmek veya tip degistirmek ise destructive kabul edilir:

```csharp
var allow = DestructiveOperationOptions.Allow();

generator.DropColumn<User>("DisplayName", allow);
generator.AlterColumnType<User>("Email", allow);
```

Bu ayrim, production semasinda kazara veri kaybi olusturmamak icin bilincli yapilmistir.

## EF Core Migration ile Fark

| Konu | EF Core Migration | ModelSync |
|---|---|---|
| ORM | Var | Yok |
| Migration history | Var | Yok |
| Attribute tabanli sema | Var | Var |
| SQL uretme | Var | Var |
| Live DB diff | Var | Faz 2 hedefi |
| Destructive explicit opt-in | Kismen | Var |
| Dapper/ADO.NET projelerine uygunluk | Dolayli | Dogrudan |

ModelSync, EF Core Migration'in tum yeteneklerini hedeflemez. Daha dar, daha hafif ve daha seffaf bir schema generation deneyimi sunar.

## Sonuc

EF Core kullanmadan da C# modelinden SQL sema uretmek mumkundur. ModelSync burada "migration framework" olmaktan once "attribute tabanli SQL schema generator" olarak konumlanir. Bu ayrim, projenin guvenli ve anlasilir kalmasi icin onemlidir.

