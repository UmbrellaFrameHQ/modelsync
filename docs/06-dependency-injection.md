# 06 — Dependency Injection

ModelSync, `ITableGenerator` arayüzü üzerinden standart .NET Dependency Injection ile entegre olacak şekilde tasarlanmıştır.

---

## ASP.NET Core — Program.cs Entegrasyonu

```csharp
using UmbrellaFrame.ModelSync.Core.Interfaces;
using UmbrellaFrame.ModelSync.MySql;

var builder = WebApplication.CreateBuilder(args);

// MySQL generator'ı DI container'a kaydet
builder.Services.AddSingleton<ITableGenerator>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("MySQL");
    var logger = sp.GetRequiredService<ILogger<MySqlTableGenerator>>();
    return new MySqlTableGenerator(connectionString, logger);
});

var app = builder.Build();
app.Run();
```

### SQL Server ile

```csharp
using UmbrellaFrame.ModelSync.SqlServer;

builder.Services.AddSingleton<ITableGenerator>(sp =>
{
    var cs = builder.Configuration.GetConnectionString("SqlServer");
    var logger = sp.GetRequiredService<ILogger<SqlServerTableGenerator>>();
    return new SqlServerTableGenerator(cs, logger);
});
```

---

## appsettings.json

```json
{
  "ConnectionStrings": {
    "MySQL": "Server=localhost;Database=myapp;User=root;Password=secret;",
    "SqlServer": "Server=localhost;Database=myapp;Integrated Security=True;TrustServerCertificate=True;",
    "PostgreSQL": "Host=localhost;Database=myapp;Username=postgres;Password=secret;",
    "SQLite": "Data Source=myapp.db;"
  }
}
```

---

## Bir Service İçinde Kullanım

```csharp
public class DatabaseInitializer
{
    private readonly ITableGenerator _generator;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(ITableGenerator generator, ILogger<DatabaseInitializer> logger)
    {
        _generator = generator;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating schema...");

        await _generator.GenerateSqlTableAsync<User>(ifNotExists: true, cancellationToken);
        await _generator.GenerateSqlTableAsync<Product>(ifNotExists: true, cancellationToken);
        await _generator.GenerateSqlTableAsync<Order>(ifNotExists: true, cancellationToken);

        await _generator.CreateTablesAsync(cancellationToken);

        _logger.LogInformation("Schema ready.");
    }
}
```

```csharp
// Program.cs içinde servis kaydı
builder.Services.AddSingleton<DatabaseInitializer>();

// Uygulama başlarken çalıştır
var app = builder.Build();
var initializer = app.Services.GetRequiredService<DatabaseInitializer>();
await initializer.InitializeAsync();
```

---

## Hosted Service ile Başlangıçta Schema Oluşturma

```csharp
public class SchemaInitializerService : IHostedService
{
    private readonly ITableGenerator _generator;

    public SchemaInitializerService(ITableGenerator generator)
        => _generator = generator;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _generator.GenerateSqlTable<User>(ifNotExists: true);
        _generator.GenerateSqlTable<Product>(ifNotExists: true);
        await _generator.CreateTablesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

// Kayıt
builder.Services.AddHostedService<SchemaInitializerService>();
```

---

## Birden Fazla Provider Aynı Anda

```csharp
// Adlandırılmış kayıt için bir fabrika deseni kullanın
builder.Services.AddKeyedSingleton<ITableGenerator>("mysql", (sp, _) =>
    new MySqlTableGenerator(builder.Configuration.GetConnectionString("MySQL")));

builder.Services.AddKeyedSingleton<ITableGenerator>("sqlite", (sp, _) =>
    new SQLiteTableGenerator(builder.Configuration.GetConnectionString("SQLite")));

// Kullanım
public class MyService(
    [FromKeyedServices("mysql")] ITableGenerator mysqlGen,
    [FromKeyedServices("sqlite")] ITableGenerator sqliteGen) { }
```

> ⚠️ Keyed services .NET 8+ gerektirir. Daha eski hedefler için kendi fabrika sınıfınızı yazın.

---

## Test İçin Mock / Fake Kullanımı

`ITableGenerator` arayüzü sayesinde birim testlerde gerçek bir veritabanı gerekmez:

```csharp
public class FakeTableGenerator : ITableGenerator
{
    public List<string> GeneratedSqls { get; } = new();

    public string GenerateSqlTable<T>(bool ifNotExists = false) where T : class, new()
    {
        var sql = $"CREATE TABLE {typeof(T).Name};";
        GeneratedSqls.Add(sql);
        return sql;
    }

    public Task<string> GenerateSqlTableAsync<T>(bool ifNotExists = false, CancellationToken ct = default)
        where T : class, new()
        => Task.FromResult(GenerateSqlTable<T>(ifNotExists));

    public string GenerateDropTableSql<T>() where T : class, new() => string.Empty;
    public string GenerateTruncateTableSql<T>() where T : class, new() => string.Empty;
    public List<string> GenerateIndexSql<T>() where T : class, new() => new();
    public void CreateTables() { }
    public Task CreateTablesAsync(CancellationToken ct = default) => Task.CompletedTask;
    public void DropTables() { }
    public Task DropTablesAsync(CancellationToken ct = default) => Task.CompletedTask;
}
```
