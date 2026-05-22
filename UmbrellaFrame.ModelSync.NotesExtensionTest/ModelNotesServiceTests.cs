using System;
using System.IO;
using NUnit.Framework;
using UmbrellaFrame.ModelSync.NotesExtension.Services;
using UmbrellaFrame.ModelSync.NotesExtensionTest.Support;

namespace UmbrellaFrame.ModelSync.NotesExtensionTest
{
    [TestFixture]
    public class ModelNotesServiceTests
    {
        private sealed class MockModel
        {
            public int Id { get; set; }

            public string Name { get; set; } = string.Empty;
        }

        private string _notesFilePath = string.Empty;
        private FixedNotesClock _clock = null!;

        [SetUp]
        public void SetUp()
        {
            var testDirectory = Path.Combine(Path.GetTempPath(), "modelsync-notes-tests", Guid.NewGuid().ToString("N"));
            _notesFilePath = Path.Combine(testDirectory, ".modelsync", "notes.json");
            _clock = new FixedNotesClock(new DateTimeOffset(2026, 5, 20, 10, 35, 0, TimeSpan.Zero));
        }

        [Test]
        public void AddNote_ShouldPersistModelPropertyNoteToJson()
        {
            var service = CreateService("dev@company.com", "Dev User");

            var note = service.AddNote<MockModel>("Name", "Name alanı VARCHAR(255) kalmalı.");
            var notes = service.GetNotes<MockModel>("Name");
            var json = File.ReadAllText(_notesFilePath);

            Assert.That(note.Id, Does.StartWith("note_"));
            Assert.That(notes, Has.Count.EqualTo(1));
            Assert.That(notes[0].Text, Is.EqualTo("Name alanı VARCHAR(255) kalmalı."));
            Assert.That(notes[0].CreatedBy.Id, Is.EqualTo("dev@company.com"));
            Assert.That(notes[0].CreatedBy.Source, Is.EqualTo("visualstudio"));
            Assert.That(json, Does.Contain("MockModel.Name"));
        }

        [Test]
        public void UpdateNote_ShouldAllowOwnerOnly()
        {
            var ownerService = CreateService("owner@company.com", "Owner");
            var note = ownerService.AddNote<MockModel>("Name", "İlk not");

            var otherService = CreateService("other@company.com", "Other");
            Assert.Throws<UnauthorizedAccessException>(() =>
                otherService.UpdateNote<MockModel>("Name", note.Id, "Başkasının değişikliği"));

            _clock.UtcNow = new DateTimeOffset(2026, 5, 20, 11, 0, 0, TimeSpan.Zero);
            var updated = ownerService.UpdateNote<MockModel>("Name", note.Id, "Güncel not");

            Assert.That(updated.Text, Is.EqualTo("Güncel not"));
            Assert.That(updated.UpdatedAt, Is.EqualTo(_clock.UtcNow));
            Assert.That(updated.UpdatedBy!.Id, Is.EqualTo("owner@company.com"));
        }

        [Test]
        public void DeleteNote_ShouldAllowOwnerOnly()
        {
            var ownerService = CreateService("owner@company.com", "Owner");
            var note = ownerService.AddNote<MockModel>("Name", "Silinecek not");

            var otherService = CreateService("other@company.com", "Other");
            Assert.Throws<UnauthorizedAccessException>(() =>
                otherService.DeleteNote<MockModel>("Name", note.Id));

            ownerService.DeleteNote<MockModel>("Name", note.Id);

            Assert.That(ownerService.GetNotes<MockModel>("Name"), Is.Empty);
        }

        [Test]
        public void AddNote_ShouldRejectUnknownProperty()
        {
            var service = CreateService("dev@company.com", "Dev User");

            Assert.Throws<ArgumentException>(() =>
                service.AddNote<MockModel>("MissingProperty", "Bu not yazılamaz."));
        }

        [Test]
        public void RuntimeModelTypeOverloads_ShouldSupportVisualStudioEditorContext()
        {
            var service = CreateService("dev@company.com", "Dev User");

            var note = service.AddNote(typeof(MockModel), "Name", "Form üzerinden eklendi.");
            var notes = service.GetNotes(typeof(MockModel), "Name");

            Assert.That(note.Text, Is.EqualTo("Form üzerinden eklendi."));
            Assert.That(notes, Has.Count.EqualTo(1));
            Assert.That(ModelNotesService.CreatePropertyKey(typeof(MockModel), "Name"), Does.EndWith("MockModel.Name"));
        }

        [Test]
        public void AddNote_ShouldPersistTurkishCharactersAsUtf8()
        {
            var service = CreateService("kursat@company.com", "Kürşat Solmaz");

            service.AddNote<MockModel>("Name", "Test amaçlı eklendi: çğıöşü İ");
            var reloaded = CreateService("kursat@company.com", "Kürşat Solmaz")
                .GetNotes<MockModel>("Name");
            var json = File.ReadAllText(_notesFilePath, System.Text.Encoding.UTF8);

            Assert.That(reloaded[0].CreatedBy.Name, Is.EqualTo("Kürşat Solmaz"));
            Assert.That(reloaded[0].Text, Is.EqualTo("Test amaçlı eklendi: çğıöşü İ"));
            Assert.That(json, Does.Contain("Kürşat Solmaz"));
            Assert.That(json, Does.Contain("çğıöşü İ"));
        }

        [Test]
        public void RawNoteKey_ShouldSupportModelLevelNotes()
        {
            var service = CreateService("dev@company.com", "Dev User");

            service.AddNote("MockModel", "Model seviyesinde history notu.");
            var notes = service.GetNotes("MockModel");

            Assert.That(notes, Has.Count.EqualTo(1));
            Assert.That(notes[0].Text, Is.EqualTo("Model seviyesinde history notu."));
        }

        [Test]
        public void MoveNotes_ShouldMergeLegacyKeyIntoNewKey()
        {
            var service = CreateService("dev@company.com", "Dev User");

            service.AddNote("MockModel.Name", "Eski key ile yazılmış not.");
            var movedCount = service.MoveNotes("MockModel.Name", "file:Models/MockModel.cs::Tests.MockModel.Name");

            Assert.That(movedCount, Is.EqualTo(1));
            Assert.That(service.GetNotes("MockModel.Name"), Is.Empty);
            Assert.That(service.GetNotes("file:Models/MockModel.cs::Tests.MockModel.Name")[0].Text, Is.EqualTo("Eski key ile yazılmış not."));
        }

        [Test]
        public void MoveNotes_ShouldNotRewriteFile_WhenLegacyKeyDoesNotExist()
        {
            var service = CreateService("dev@company.com", "Dev User");

            service.AddNote("file:Models/MockModel.cs::Tests.MockModel.Name", "Yeni key notu.");
            var beforeWrite = File.GetLastWriteTimeUtc(_notesFilePath);
            var movedCount = service.MoveNotes("MockModel.Name", "file:Models/MockModel.cs::Tests.MockModel.Name");
            var afterWrite = File.GetLastWriteTimeUtc(_notesFilePath);

            Assert.That(movedCount, Is.EqualTo(1));
            Assert.That(afterWrite, Is.EqualTo(beforeWrite));
        }

        [Test]
        public void Load_ShouldRepairTurkishMojibakeFromOlderNotes()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_notesFilePath)!);
            File.WriteAllText(
                _notesFilePath,
                @"{
  ""notes"": {
    ""MockModel.Name"": [
      {
        ""id"": ""note_legacy"",
        ""createdAt"": ""2026-05-20T10:35:00+00:00"",
        ""createdBy"": {
          ""id"": ""kursat@company.com"",
          ""name"": ""KÃ¼rÅŸat Solmaz"",
          ""source"": ""visualstudio-git""
        },
        ""text"": ""Test amaÃ§lÄ± eklendi"",
        ""updatedAt"": null,
        ""updatedBy"": null
      }
    ]
  }
}",
                System.Text.Encoding.UTF8);

            var service = CreateService("kursat@company.com", "Kürşat Solmaz");
            var notes = service.GetNotes("MockModel.Name");

            Assert.That(notes[0].CreatedBy.Name, Is.EqualTo("Kürşat Solmaz"));
            Assert.That(notes[0].Text, Is.EqualTo("Test amaçlı eklendi"));
        }

        [Test]
        public void Save_ShouldWriteSchemaVersion()
        {
            var service = CreateService("dev@company.com", "Dev User");

            service.AddNote<MockModel>("Name", "Schema version kontrolü.");
            var json = File.ReadAllText(_notesFilePath, System.Text.Encoding.UTF8);

            Assert.That(json, Does.Contain(@"""SchemaVersion"": 1"));
        }

        [Test]
        public void Save_ShouldNotLeaveTempFilesAfterAtomicWrite()
        {
            var service = CreateService("dev@company.com", "Dev User");

            service.AddNote<MockModel>("Name", "Atomic write kontrolü.");
            var notesDirectory = Path.GetDirectoryName(_notesFilePath)!;
            var tempFiles = Directory.GetFiles(notesDirectory, "*.tmp");

            Assert.That(File.Exists(_notesFilePath), Is.True);
            Assert.That(tempFiles, Is.Empty);
        }

        [Test]
        public void AddNote_ShouldRejectVeryLargeNoteText()
        {
            var service = CreateService("dev@company.com", "Dev User");
            var largeText = new string('x', ModelNotesService.MaxNoteTextLength + 1);

            Assert.Throws<ArgumentException>(() => service.AddNote<MockModel>("Name", largeText));
        }

        [Test]
        public void Load_ShouldRejectOversizedNotesFile()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_notesFilePath)!);
            File.WriteAllText(_notesFilePath, new string('x', 20 * 1024 * 1024 + 1));

            var repository = new JsonModelNotesRepository(_notesFilePath);

            Assert.Throws<InvalidDataException>(() => repository.Load());
        }

        private ModelNotesService CreateService(string userId, string name)
        {
            return new ModelNotesService(
                new JsonModelNotesRepository(_notesFilePath),
                new StaticNotesUserProvider(userId, name),
                _clock);
        }
    }
}
