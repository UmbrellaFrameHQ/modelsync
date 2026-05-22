# 11 - Notes Extension

ModelSync Notes is an optional Visual Studio extension for developer-owned notes on model classes and model properties.

It is distributed separately from the core ModelSync packages. Projects that only need SQL generation do not install the VSIX or the Notes libraries.

## Architecture

```text
UmbrellaFrame.ModelSync.NotesExtension
  Storage models, JSON repository, note service, parser, identity abstractions

UmbrellaFrame.ModelSync.NotesExtension.Forms
  WinForms note grid and add/edit/delete dialog behavior

UmbrellaFrame.ModelSync.NotesExtension.Vsix
  Visual Studio package, editor adornment, glyph, count cache, user/path providers
```

## Runtime Files

Notes are stored under the active solution:

```text
.modelsync/notes.json
```

Settings are optional:

```text
.modelsync/notes-settings.json
```

Example settings:

```json
{
  "modelClassSuffixes": [ "Model", "Entity" ],
  "modelClassNames": [ "Product" ]
}
```

## Key Format

Notes are grouped by a stable key:

```text
ProductModel
ProductModel.Name
```

Class notes use the model name. Property notes use `ModelName.PropertyName`.

## Performance

- Editor adornments are created only for visible lines.
- Badge counts are cached by note key.
- Count refreshes are coalesced so scrolling does not repeatedly read JSON.
- The Visual Studio user identity is cached for the session.
- JSON file size is capped to protect the editor process.

## Safety

- JSON writes use a temp file and atomic replace.
- A named mutex protects concurrent writes from multiple Visual Studio instances.
- JSON deserialization disables type metadata and limits object depth.
- Note text is stored and displayed as plain text.
- Edit/delete is limited to the note owner through the service and UI.

This is not a secure audit log. A developer with filesystem access can edit `.modelsync/notes.json` manually. For strong enforcement, move notes to a backend service with authenticated users and server-side authorization.

## Build and Install

```powershell
dotnet build .\UmbrellaFrame.ModelSync.NotesExtension.Vsix\UmbrellaFrame.ModelSync.NotesExtension.Vsix.csproj -c Release
```

Install:

```text
UmbrellaFrame.ModelSync.NotesExtension.Vsix\bin\Release\net472\UmbrellaFrame.ModelSync.NotesExtension.Vsix.vsix
```

Close Visual Studio before installing or updating.

## Test

```powershell
dotnet test .\UmbrellaFrame.ModelSync.NotesExtensionTest\UmbrellaFrame.ModelSync.NotesExtensionTest.csproj
```
