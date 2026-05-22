# ModelSync Notes VSIX

Visual Studio editor integration for ModelSync Notes.

The VSIX adds a small clickable history/notes glyph beside supported model classes and properties. Clicking the glyph opens the notes form for that exact model or property key.

![ModelSync Notes icon](modelsync-notes.png)

## Icon Usage

`modelsync-notes.png` is the VSIX package icon. Visual Studio shows it in the Extensions window and installer UI.

The editor glyph is drawn by the extension at runtime. It uses a smaller history button with an optional badge count, so it is not the same asset as the VSIX package icon.

## What It Shows

- Class-level notes for model classes such as `ProductModel`.
- Property-level notes for model properties such as `ProductModel.Name`.
- A badge count beside the glyph when notes already exist.
- A tooltip with the target key.

The extension does not show glyphs for every C# class. By default, model classes are detected by suffix, for example `ProductModel`. Additional names or suffixes can be configured in `.modelsync/notes-settings.json`.

```json
{
  "modelClassSuffixes": [ "Model", "Entity" ],
  "modelClassNames": [ "Product" ]
}
```

## Model Example

Open a C# file like this in Visual Studio:

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
    [DbColumnIndex("idx_products_name")]
    public string Name { get; set; } = string.Empty;
}
```

Expected editor behavior:

- One glyph beside `ProductModel`.
- One glyph beside `Id`.
- One glyph beside `Name`.
- Badge count appears after notes are saved.

Example note for `ProductModel.Name`:

```text
Author: Kursat Solmaz
Date: 2026-05-20
Text: Keep VARCHAR(255); external catalog rejects longer names.
```

## Local Install

Build the VSIX:

```powershell
dotnet build .\UmbrellaFrame.ModelSync.NotesExtension.Vsix\UmbrellaFrame.ModelSync.NotesExtension.Vsix.csproj -c Release
```

The build includes:

```text
modelsync-notes.png
```

Install the generated file:

```text
UmbrellaFrame.ModelSync.NotesExtension.Vsix\bin\Release\net472\UmbrellaFrame.ModelSync.NotesExtension.Vsix.vsix
```

Close Visual Studio before installing or updating the VSIX.

## Experimental Instance

Open the solution in Visual Studio, set `UmbrellaFrame.ModelSync.NotesExtension.Vsix` as the startup project, then press `F5`. Visual Studio opens an Experimental Instance using `/rootsuffix Exp`.

## Runtime Files

Notes:

```text
<solution>\.modelsync\notes.json
```

Settings:

```text
<solution>\.modelsync\notes-settings.json
```

These files are intentionally solution-local. They are ignored by Git in this repository.

Example `notes.json`:

```json
{
  "schemaVersion": 1,
  "notes": {
    "file:Models/ProductModel.cs::MyApp.Models.ProductModel.Name": [
      {
        "id": "note_9d4c...",
        "createdAt": "2026-05-20T10:35:00+00:00",
        "createdBy": {
          "id": "kursat@example.com",
          "name": "Kursat Solmaz",
          "source": "visualstudio-git"
        },
        "text": "Keep VARCHAR(255); external catalog rejects longer names."
      }
    ]
  }
}
```

## Packaging

Use the repository script to create NuGet packages and the VSIX artifact:

```powershell
.\scripts\pack.ps1
```

Artifacts are written to:

```text
artifacts\
```

## Note

Close all Visual Studio instances before installing an updated VSIX. The installer waits for `devenv.exe` and related Visual Studio processes to exit.
