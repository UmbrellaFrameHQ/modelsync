# UmbrellaFrame.ModelSync.NotesExtension.Forms

WinForms UI layer for ModelSync Notes.

This project contains the notes dialog opened by the Visual Studio extension. It depends on `UmbrellaFrame.ModelSync.NotesExtension` for storage and business rules, but it does not know anything about Visual Studio editor adornments.

## UI Behavior

- Shows notes in a grid.
- Displays author, date, and description.
- Lets every user add notes.
- Allows edit/delete only for notes created by the current user.
- Keeps all labels and button text in English for a neutral Visual Studio extension experience.

## Open Form Example

The VSIX uses `NotesPopup.ShowForKey` after the editor glyph is clicked:

```csharp
using UmbrellaFrame.ModelSync.NotesExtension.Forms;
using UmbrellaFrame.ModelSync.NotesExtension.Services;

var service = new ModelNotesService(
    new JsonModelNotesRepository(notesFilePath),
    visualStudioUserProvider);

NotesPopup.ShowForKey(
    owner: null,
    notesService: service,
    noteKey: "ProductModel.Name",
    displayTitle: "ProductModel.Name");
```

The form reloads the current note list, lets the user add a new note, and refreshes the grid after add/edit/delete.

## Example User Flow

1. User clicks the history glyph next to `public string Name { get; set; }`.
2. The dialog opens with title `Notes`.
3. User writes: `External catalog limit: keep VARCHAR(255).`
4. The grid shows author, date, and description.
5. Edit/Delete buttons are enabled only if the current user owns the selected note.

## Build

```powershell
dotnet build .\UmbrellaFrame.ModelSync.NotesExtension.Forms\UmbrellaFrame.ModelSync.NotesExtension.Forms.csproj -c Release
```

This project targets `.NET Framework 4.7.2` because Visual Studio extensions run inside the Visual Studio process.

## Note

Keep business rules out of the form. Validation, ownership, storage, and note key migration should remain in `ModelNotesService`.
