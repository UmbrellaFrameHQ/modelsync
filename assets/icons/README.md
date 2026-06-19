# ModelSync Icons

This folder keeps source icon assets used by NuGet package metadata and repository documentation.

## Icon Map

| File | Package | Purpose |
|---|---|---|
| `modelsync-core.png` | `UmbrellaFrame.ModelSync.Core` | Main ModelSync package icon |
| `modelsync-sqlserver.png` | `UmbrellaFrame.ModelSync.SqlServer` | SQL Server provider icon |
| `modelsync-mysql.png` | `UmbrellaFrame.ModelSync.MySql` | MySQL/MariaDB provider icon |
| `modelsync-postgresql.png` | `UmbrellaFrame.ModelSync.PostgreSQL` | PostgreSQL provider icon |
| `modelsync-sqlite.png` | `UmbrellaFrame.ModelSync.SQLite` | SQLite provider icon |

## Requirements

- Format: PNG
- Recommended size: 128x128 px or larger
- Background: transparent or white
- Keep visual weight consistent between provider icons

## Example Package Reference

Provider `.csproj` files can include an icon like this:

```xml
<PropertyGroup>
  <PackageIcon>modelsync-mysql.png</PackageIcon>
</PropertyGroup>

<ItemGroup>
  <None Include="modelsync-mysql.png" Pack="true" PackagePath="" />
</ItemGroup>
```

## Note

Do not replace package icons with screenshots or UI captures. NuGet icons should stay simple, square, and readable at small sizes.
