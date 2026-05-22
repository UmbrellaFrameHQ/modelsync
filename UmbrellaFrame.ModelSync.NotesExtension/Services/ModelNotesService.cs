 using System;
using System.Collections.Generic;
using System.Linq;
using UmbrellaFrame.ModelSync.NotesExtension.Models;

namespace UmbrellaFrame.ModelSync.NotesExtension.Services
{
    public sealed class ModelNotesService
    {
        public const int MaxNoteTextLength = 10000;
        public const int MaxPropertyKeyLength = 512;

        private readonly IModelNotesRepository _repository;
        private readonly INotesUserProvider _userProvider;
        private readonly INotesClock _clock;

        public ModelNotesService(
            IModelNotesRepository repository,
            INotesUserProvider userProvider,
            INotesClock? clock = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _userProvider = userProvider ?? throw new ArgumentNullException(nameof(userProvider));
            _clock = clock ?? new SystemNotesClock();
        }

        public IReadOnlyList<ModelNoteEntry> GetNotes<TModel>(string propertyName)
        {
            return GetNotes(typeof(TModel), propertyName);
        }

        public IReadOnlyList<ModelNoteEntry> GetNotes(Type modelType, string propertyName)
        {
            return GetNotes(CreatePropertyKey(modelType, propertyName));
        }

        public IReadOnlyList<ModelNoteEntry> GetNotes(string modelName, string propertyName)
        {
            return GetNotes(CreatePropertyKey(modelName, propertyName));
        }

        public IReadOnlyList<ModelNoteEntry> GetNotes(string propertyKey)
        {
            var document = _repository.Load();
            var key = EnsurePropertyKey(propertyKey);

            return document.Notes.TryGetValue(key, out var entries)
                ? entries.OrderByDescending(note => note.CreatedAt).ToList()
                : new List<ModelNoteEntry>();
        }

        public ModelNoteEntry AddNote<TModel>(string propertyName, string text)
        {
            return AddNote(typeof(TModel), propertyName, text);
        }

        public ModelNoteEntry AddNote(Type modelType, string propertyName, string text)
        {
            return AddNote(CreatePropertyKey(modelType, propertyName), text);
        }

        public ModelNoteEntry AddNote(string modelName, string propertyName, string text)
        {
            return AddNote(CreatePropertyKey(modelName, propertyName), text);
        }

        public ModelNoteEntry AddNote(string propertyKey, string text)
        {
            EnsureText(text);

            var key = EnsurePropertyKey(propertyKey);
            return _repository.Update(document =>
            {
                if (!document.Notes.TryGetValue(key, out var entries))
                {
                    entries = new List<ModelNoteEntry>();
                    document.Notes[key] = entries;
                }

                var entry = new ModelNoteEntry
                {
                    Id = "note_" + Guid.NewGuid().ToString("N"),
                    CreatedAt = _clock.UtcNow,
                    CreatedBy = _userProvider.GetCurrentUser(),
                    Text = text.Trim()
                };

                entries.Add(entry);

                return entry;
            });
        }

        public ModelNoteEntry UpdateNote<TModel>(string propertyName, string noteId, string text)
        {
            return UpdateNote(typeof(TModel), propertyName, noteId, text);
        }

        public ModelNoteEntry UpdateNote(Type modelType, string propertyName, string noteId, string text)
        {
            return UpdateNote(CreatePropertyKey(modelType, propertyName), noteId, text);
        }

        public ModelNoteEntry UpdateNote(string modelName, string propertyName, string noteId, string text)
        {
            return UpdateNote(CreatePropertyKey(modelName, propertyName), noteId, text);
        }

        public ModelNoteEntry UpdateNote(string propertyKey, string noteId, string text)
        {
            EnsureText(text);

            return _repository.Update(document =>
            {
                var entry = FindOwnedNote(document, propertyKey, noteId);

                entry.Text = text.Trim();
                entry.UpdatedAt = _clock.UtcNow;
                entry.UpdatedBy = _userProvider.GetCurrentUser();

                return entry;
            });
        }

        public void DeleteNote<TModel>(string propertyName, string noteId)
        {
            DeleteNote(typeof(TModel), propertyName, noteId);
        }

        public void DeleteNote(Type modelType, string propertyName, string noteId)
        {
            DeleteNote(CreatePropertyKey(modelType, propertyName), noteId);
        }

        public void DeleteNote(string modelName, string propertyName, string noteId)
        {
            DeleteNote(CreatePropertyKey(modelName, propertyName), noteId);
        }

        public void DeleteNote(string propertyKey, string noteId)
        {
            var key = EnsurePropertyKey(propertyKey);
            _repository.Update<object?>(document =>
            {
                if (!document.Notes.TryGetValue(key, out var entries))
                {
                    throw new KeyNotFoundException("Note was not found.");
                }

                var entry = entries.FirstOrDefault(note => string.Equals(note.Id, noteId, StringComparison.Ordinal));
                if (entry == null)
                {
                    throw new KeyNotFoundException("Note was not found.");
                }

                EnsureOwner(entry);
                entries.Remove(entry);

                if (entries.Count == 0)
                {
                    document.Notes.Remove(key);
                }

                return null;
            });
        }

        public NotesUser GetCurrentUser()
        {
            return _userProvider.GetCurrentUser();
        }

        public bool CanModify(ModelNoteEntry entry)
        {
            if (entry == null)
            {
                return false;
            }

            var currentUser = _userProvider.GetCurrentUser();
            return string.Equals(entry.CreatedBy.Id, currentUser.Id, StringComparison.OrdinalIgnoreCase);
        }

        public int MoveNotes(string oldPropertyKey, string newPropertyKey)
        {
            var oldKey = EnsurePropertyKey(oldPropertyKey);
            var newKey = EnsurePropertyKey(newPropertyKey);
            if (string.Equals(oldKey, newKey, StringComparison.Ordinal))
            {
                return GetNotes(newKey).Count;
            }

            var currentDocument = _repository.Load();
            if (!currentDocument.Notes.TryGetValue(oldKey, out var currentOldEntries) || currentOldEntries.Count == 0)
            {
                return currentDocument.Notes.TryGetValue(newKey, out var currentNewEntries)
                    ? currentNewEntries.Count
                    : 0;
            }

            return _repository.Update(document =>
            {
                if (!document.Notes.TryGetValue(oldKey, out var oldEntries) || oldEntries.Count == 0)
                {
                    return document.Notes.TryGetValue(newKey, out var currentEntries)
                        ? currentEntries.Count
                        : 0;
                }

                if (!document.Notes.TryGetValue(newKey, out var newEntries))
                {
                    newEntries = new List<ModelNoteEntry>();
                    document.Notes[newKey] = newEntries;
                }

                var existingIds = new HashSet<string>(newEntries.Select(note => note.Id), StringComparer.Ordinal);
                foreach (var note in oldEntries)
                {
                    if (existingIds.Add(note.Id))
                    {
                        newEntries.Add(note);
                    }
                }

                document.Notes.Remove(oldKey);
                return newEntries.Count;
            });
        }

        public static string CreatePropertyKey<TModel>(string propertyName)
        {
            return CreatePropertyKey(typeof(TModel), propertyName);
        }

        public static string CreatePropertyKey(Type modelType, string propertyName)
        {
            EnsurePropertyExists(modelType, propertyName);

            var modelName = string.IsNullOrWhiteSpace(modelType.FullName) ? modelType.Name : modelType.FullName;
            return CreatePropertyKey(modelName, propertyName);
        }

        public static string CreatePropertyKey(string modelName, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                throw new ArgumentException("Model name cannot be empty.", nameof(modelName));
            }

            if (string.IsNullOrWhiteSpace(propertyName))
            {
                throw new ArgumentException("Property name cannot be empty.", nameof(propertyName));
            }

            return modelName.Trim() + "." + propertyName.Trim();
        }

        private ModelNoteEntry FindOwnedNote(ModelNotesDocument document, string propertyKey, string noteId)
        {
            var key = EnsurePropertyKey(propertyKey);

            if (!document.Notes.TryGetValue(key, out var entries))
            {
                throw new KeyNotFoundException("Note was not found.");
            }

            var entry = entries.FirstOrDefault(note => string.Equals(note.Id, noteId, StringComparison.Ordinal));
            if (entry == null)
            {
                throw new KeyNotFoundException("Note was not found.");
            }

            EnsureOwner(entry);
            return entry;
        }

        private void EnsureOwner(ModelNoteEntry entry)
        {
            var currentUser = _userProvider.GetCurrentUser();
            if (!string.Equals(entry.CreatedBy.Id, currentUser.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Only the note owner can edit or delete this note.");
            }
        }

        private static void EnsurePropertyExists<TModel>(string propertyName)
        {
            EnsurePropertyExists(typeof(TModel), propertyName);
        }

        private static void EnsurePropertyExists(Type modelType, string propertyName)
        {
            if (modelType == null)
            {
                throw new ArgumentNullException(nameof(modelType));
            }

            if (string.IsNullOrWhiteSpace(propertyName))
            {
                throw new ArgumentException("Property name cannot be empty.", nameof(propertyName));
            }

            var property = modelType.GetProperty(propertyName);
            if (property == null)
            {
                throw new ArgumentException(
                    $"Property '{propertyName}' was not found on model '{modelType.Name}'.",
                    nameof(propertyName));
            }
        }

        private static void EnsureText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Note text cannot be empty.", nameof(text));
            }

            if (text.Trim().Length > MaxNoteTextLength)
            {
                throw new ArgumentException(
                    $"Note text cannot exceed {MaxNoteTextLength} characters.",
                    nameof(text));
            }
        }

        private static string EnsurePropertyKey(string propertyKey)
        {
            if (string.IsNullOrWhiteSpace(propertyKey))
            {
                throw new ArgumentException("Property key cannot be empty.", nameof(propertyKey));
            }

            var key = propertyKey.Trim();
            if (key.Length > MaxPropertyKeyLength)
            {
                throw new ArgumentException(
                    $"Property key cannot exceed {MaxPropertyKeyLength} characters.",
                    nameof(propertyKey));
            }

            return key;
        }
    }
}
