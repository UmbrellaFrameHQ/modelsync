# UmbrellaFrame.ModelSync.NotesExtension

Core library for ModelSync Notes. This package contains the storage model, JSON repository, ownership rules, note service, C# model parser, and test-friendly abstractions used by the Visual Studio extension.

It is intentionally separate from `UmbrellaFrame.ModelSync.Core`; applications that only need SQL generation do not need to install it.

## Responsibilities

- Store model and property notes in JSON.
- Group entries by stable note key, for example `ProductModel.Name`.
- Enforce the UI-level rule that only the original author can edit or delete a note.
- Cache and normalize Visual Studio user identity through an `INotesUserProvider`.
- Protect JSON writes with cross-process locking and atomic replace.
- Limit note size and JSON file size to avoid accidental editor slowdowns.
- Parse C# model classes using Roslyn instead of regex.

## Storage

The Visual Studio extension stores notes under the active solution folder:

```text
.modelsync/notes.json
```

Runtime workspace data is ignored by Git through the repository `.gitignore`.

Example JSON after adding a note to `ProductModel.Name`:

```json
{
  "schemaVersion": 1,
  "notes": {
    "ProductModel.Name": [
      {
        "id": "note_4b2e...",
        "createdAt": "2026-05-20T10:35:00+00:00",
        "createdBy": {
          "id": "kursat@example.com",
          "name": "Kursat Solmaz",
          "source": "git"
        },
        "text": "Name is limited to VARCHAR(255) because of the external catalog API.",
        "updatedAt": null,
        "updatedBy": null
      }
    ]
  }
}
```

## Service Example

```csharp
using UmbrellaFrame.ModelSync.NotesExtension.Models;
using UmbrellaFrame.ModelSync.NotesExtension.Services;

var notesFilePath = Path.Combine(solutionPath, ".modelsync", "notes.json");

var userProvider = new StaticNotesUserProvider(
    id: "kursat@example.com",
    name: "Kursat Solmaz",
    source: "sample");

var service = new ModelNotesService(
    new JsonModelNotesRepository(notesFilePath),
    userProvider);

var entry = service.AddNote(
    "ProductModel.Name",
    "Name is kept at VARCHAR(255) because the catalog API rejects longer values.");

var notes = service.GetNotes("ProductModel.Name");
```

The same service is used by the WinForms dialog, so ownership and validation rules stay in one place.

## Identity

`ModelNotesService` receives the current user from `INotesUserProvider`.

The VSIX implementation resolves identity from Visual Studio/git context and caches it for the session. The JSON file is still a local developer artifact, so ownership is a product rule in the extension, not a cryptographic security boundary.

## Security Model

ModelSync Notes is designed for developer workflow notes, not regulated audit logs.

- Local JSON can be edited by anyone with filesystem access.
- Edit/delete ownership is enforced by the service and UI.
- Note text is treated as plain text; renderers should keep it encoded if Markdown or HTML support is added later.
- JSON deserialization disables type metadata and limits maximum depth.
- Writes are atomic and guarded by a named mutex so two Visual Studio instances do not corrupt the file.

## Parser Example

```csharp
var contexts = CSharpModelNoteSyntaxParser.Parse(
    sourceText,
    fileKey: "Models/ProductModel.cs",
    isModelClass: className => className.EndsWith("Model", StringComparison.Ordinal));

var nameContext = contexts.Values.Single(context =>
    context.DisplayName == "ProductModel.Name");

Console.WriteLine(nameContext.NoteKey);
```

Example note key:

```text
file:Models/ProductModel.cs::MyApp.Models.ProductModel.Name
```

## Tests

```powershell
dotnet test .\UmbrellaFrame.ModelSync.NotesExtensionTest\UmbrellaFrame.ModelSync.NotesExtensionTest.csproj
```

## Note

This library is not a database migration system and it does not modify ModelSync SQL output. It only stores developer notes attached to model classes and properties.
