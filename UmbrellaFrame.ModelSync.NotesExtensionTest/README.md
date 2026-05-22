# UmbrellaFrame.ModelSync.NotesExtensionTest

NUnit test project for ModelSync Notes.

## Coverage

- JSON load/save with UTF-8 text.
- Legacy Turkish mojibake repair.
- Atomic write temp-file cleanup.
- Oversized JSON rejection.
- Note add, edit, delete, and ownership rules.
- Note key migration with `MoveNotes`.
- Roslyn parser detection for model classes and properties.

## Example Test Shape

Most service tests create a temporary JSON file and a deterministic user:

```csharp
var repository = new JsonModelNotesRepository(notesFilePath);
var userProvider = new StaticNotesUserProvider(
    id: "owner@example.com",
    name: "Owner User",
    source: "test");

var service = new ModelNotesService(repository, userProvider, new FixedNotesClock(now));

var note = service.AddNote("ProductModel.Name", "Test note");
Assert.That(service.GetNotes("ProductModel.Name"), Has.Count.EqualTo(1));
```

Parser tests use source text directly:

```csharp
var contexts = CSharpModelNoteSyntaxParser.Parse(
    "public class ProductModel { public string Name { get; set; } }",
    "ProductModel.cs",
    name => name.EndsWith("Model", StringComparison.Ordinal));

Assert.That(contexts.Values.Any(context => context.DisplayName == "ProductModel.Name"));
```

## Run

```powershell
dotnet test .\UmbrellaFrame.ModelSync.NotesExtensionTest\UmbrellaFrame.ModelSync.NotesExtensionTest.csproj
```

The tests use temporary directories and do not write to a solution `.modelsync` folder.

## Note

Keep Notes tests independent from Visual Studio. VSIX/editor behavior belongs in manual VSIX verification or future integration tests, while this project should stay fast and deterministic.
