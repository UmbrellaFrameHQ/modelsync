# ModelSync Articles

This folder contains short article drafts that can be used for blog posts, LinkedIn posts, documentation pages, or release notes.

## Article Index

| Article | Topic |
|---|---|
| [ModelSync ile ORM kullanmadan C# class'tan SQL tablo uretmek](01-orm-kullanmadan-csharp-class-tan-sql-tablo-uretmek.md) | Core idea: generate SQL schema from plain C# models |
| [.NET'te guvenli ALTER TABLE yaklasimi](02-guvenli-alter-table-destructive-islemler.md) | Why destructive DDL needs explicit opt-in |
| [EF Core Migration kullanmadan schema generation](03-ef-core-migration-kullanmadan-schema-generation.md) | Schema generation in projects that do not use EF Core migrations |

## Suggested Publish Flow

1. Pick the article that matches the audience.
2. Add one real model from the target project.
3. Replace sample connection strings with placeholders.
4. Keep generated SQL visible; it makes the value proposition concrete.
5. Link back to the relevant provider guide in `docs/04-providers.md`.

## Example Snippet

```csharp
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
```

This snippet is useful in introductions because it shows ModelSync's positioning in one glance: plain class, explicit attributes, generated DDL.

## Note

Before publishing, scan for real credentials in connection string examples. Documentation should use local placeholders such as `User=root;Password=pass;` or environment variables.
