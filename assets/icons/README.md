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
| `modelsync-notes.png` | `UmbrellaFrame.ModelSync.NotesExtension.Vsix` | Visual Studio Notes extension icon |

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

## VSIX Icon Example

The Notes VSIX uses the icon in `source.extension.vsixmanifest`:

```xml
<Metadata>
  <Icon>modelsync-notes.png</Icon>
</Metadata>
```

The VSIX project also includes the file in the package:

```xml
<ItemGroup>
  <Content Include="modelsync-notes.png" IncludeInVSIX="true" />
</ItemGroup>
```

## Note

Do not replace package icons with screenshots or UI captures. NuGet icons should stay simple, square, and readable at small sizes.

The Notes icon combines a document and clock shape so it reads as "developer note history" in the Visual Studio Extensions window.
