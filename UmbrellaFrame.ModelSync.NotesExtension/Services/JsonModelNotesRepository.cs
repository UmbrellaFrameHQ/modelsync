using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UmbrellaFrame.ModelSync.NotesExtension.Models;

namespace UmbrellaFrame.ModelSync.NotesExtension.Services
{
    public sealed class JsonModelNotesRepository : IModelNotesRepository
    {
        private const long MaxNotesFileBytes = 20L * 1024 * 1024;
        private const int MaxJsonDepth = 64;
        private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(10);
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            MaxDepth = MaxJsonDepth,
            TypeNameHandling = TypeNameHandling.None,
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore
        };

        private readonly string _filePath;
        private static readonly ConcurrentDictionary<string, object> FileLocks =
            new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public JsonModelNotesRepository(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Notes file path cannot be empty.", nameof(filePath));
            }

            _filePath = filePath;
        }

        public ModelNotesDocument Load()
        {
            var fileLock = GetFileLock();
            lock (fileLock)
            {
                return LoadUnderProcessLock();
            }
        }

        public void Save(ModelNotesDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            using (AcquireCrossProcessLock())
            {
                SaveUnderProcessLock(document);
            }
        }

        public T Update<T>(Func<ModelNotesDocument, T> update)
        {
            if (update == null)
            {
                throw new ArgumentNullException(nameof(update));
            }

            using (AcquireCrossProcessLock())
            {
                var fileLock = GetFileLock();
                lock (fileLock)
                {
                    var document = LoadUnderProcessLock();
                    var result = update(document);
                    SaveUnderProcessLock(document);
                    return result;
                }
            }
        }

        private ModelNotesDocument LoadUnderProcessLock()
        {
            if (!File.Exists(_filePath))
            {
                return new ModelNotesDocument();
            }

            using (var stream = OpenForReadWithRetry(_filePath))
            using (var reader = new StreamReader(stream, Encoding.UTF8, true))
            {
                var json = reader.ReadToEnd();
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new ModelNotesDocument();
                }

                var document = JsonConvert.DeserializeObject<ModelNotesDocument>(json, SerializerSettings) ?? new ModelNotesDocument();
                if (document.SchemaVersion <= 0)
                {
                    document.SchemaVersion = 1;
                }

                RepairLoadedText(document);
                return document;
            }
        }

        private void SaveUnderProcessLock(ModelNotesDocument document)
        {
            document.SchemaVersion = document.SchemaVersion <= 0 ? 1 : document.SchemaVersion;

            var fileLock = GetFileLock();
            lock (fileLock)
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonConvert.SerializeObject(document, Formatting.Indented, SerializerSettings);
                EnsureJsonSize(json);
                var tempPath = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";

                try
                {
                    using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    {
                        writer.Write(json);
                    }

                    ReplaceFile(tempPath);
                }
                catch
                {
                    TryDeleteTempFile(tempPath);
                    throw;
                }
            }
        }

        private object GetFileLock()
        {
            return FileLocks.GetOrAdd(Path.GetFullPath(_filePath), _ => new object());
        }

        private void ReplaceFile(string tempPath)
        {
            if (File.Exists(_filePath))
            {
                File.Replace(tempPath, _filePath, null);
                return;
            }

            File.Move(tempPath, _filePath);
        }

        private IDisposable AcquireCrossProcessLock()
        {
            var mutex = new Mutex(false, "ModelSyncNotes_" + GetStablePathHash());
            try
            {
                try
                {
                    if (!mutex.WaitOne(LockTimeout))
                    {
                        throw new IOException("Timed out while waiting for the ModelSync notes file lock.");
                    }
                }
                catch (AbandonedMutexException)
                {
                    // Previous VS instance died while holding the lock; ownership is granted.
                }

                return new MutexHandle(mutex);
            }
            catch
            {
                mutex.Dispose();
                throw;
            }
        }

        private string GetStablePathHash()
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(_filePath).ToUpperInvariant()));
                return BitConverter.ToString(bytes).Replace("-", string.Empty);
            }
        }

        private static FileStream OpenForReadWithRetry(string path)
        {
            EnsureReadableFileSize(path);

            const int maxAttempts = 3;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                }
                catch (IOException) when (attempt < maxAttempts)
                {
                    Thread.Sleep(25);
                }
            }

            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        private static void EnsureReadableFileSize(string path)
        {
            var length = new FileInfo(path).Length;
            if (length > MaxNotesFileBytes)
            {
                throw new InvalidDataException("ModelSync notes file is too large to load safely.");
            }
        }

        private static void EnsureJsonSize(string json)
        {
            var byteCount = Encoding.UTF8.GetByteCount(json);
            if (byteCount > MaxNotesFileBytes)
            {
                throw new InvalidDataException("ModelSync notes file is too large to save safely.");
            }
        }

        private static void TryDeleteTempFile(string tempPath)
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Best-effort cleanup. The next successful save uses a unique temp file.
            }
        }

        private static void RepairLoadedText(ModelNotesDocument document)
        {
            foreach (var noteGroup in document.Notes.Values)
            {
                foreach (var note in noteGroup)
                {
                    note.Text = TurkishTextEncodingRepair.RepairMojibake(note.Text);
                    note.CreatedBy.Name = TurkishTextEncodingRepair.RepairMojibake(note.CreatedBy.Name);
                    note.CreatedBy.Id = TurkishTextEncodingRepair.RepairMojibake(note.CreatedBy.Id);

                    if (note.UpdatedBy != null)
                    {
                        note.UpdatedBy.Name = TurkishTextEncodingRepair.RepairMojibake(note.UpdatedBy.Name);
                        note.UpdatedBy.Id = TurkishTextEncodingRepair.RepairMojibake(note.UpdatedBy.Id);
                    }
                }
            }
        }

        private sealed class MutexHandle : IDisposable
        {
            private readonly Mutex _mutex;
            private bool _disposed;

            public MutexHandle(Mutex mutex)
            {
                _mutex = mutex;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _mutex.ReleaseMutex();
                _mutex.Dispose();
                _disposed = true;
            }
        }
    }
}
