# ModelSync Examples

This folder contains copy-friendly examples for trying ModelSync with different database providers.

## Example Index

| Example | What It Shows |
|---|---|
| [MySQL quickstart](mysql-quickstart.md) | Generate and execute MySQL/MariaDB table DDL from a C# model |
| [SQL Server quickstart](sqlserver-quickstart.md) | Create SQL Server tables from attributes |
| [SQLite in-memory](sqlite-in-memory.md) | Use SQLite `:memory:` for tests and prototypes |
| [DestructiveOperationOptions](destructive-operation-options.md) | Explicit opt-in for risky `DROP` and `ALTER` operations |

## Minimal Flow

Every provider follows the same pattern:

```csharp
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.MySql;

[MySqlTableName("products")]
public sealed class ProductModel
{
    [MySqlColumnType(MySqlColumnType.INT)]
    [MySqlColumnPrimaryKey(isAutoIncrement: true)]
    public int Id { get; set; }

    [MySqlColumnType(MySqlColumnType.VARCHAR, "255")]
    [MySqlColumnNotNull]
    public string Name { get; set; } = string.Empty;
}

var generator = new MySqlTableGenerator(
    "Server=localhost;Database=appdb;User=root;Password=pass;");

var sql = generator.GenerateMySqlTable<ProductModel>(ifNotExists: true);
Console.WriteLine(sql);
```

Generated SQL:

```sql
CREATE TABLE IF NOT EXISTS `products` (
    `Id` INT PRIMARY KEY AUTO_INCREMENT,
    `Name` VARCHAR(255) NOT NULL
);
```

## Run Order

1. Pick the provider example that matches your database.
2. Install the matching NuGet package.
3. Copy the model and generator setup into a console app or test project.
4. First print generated SQL.
5. Only then call `CreateDatabase()` and `CreateTables()`.

## Note

ModelSync validates identifiers before quoting them. Use table, column, and index names like `products`, `ProductName`, or `idx_products_name`. Names with spaces, dots, semicolons, or hyphens are rejected intentionally.
