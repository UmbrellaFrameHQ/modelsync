using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UmbrellaFrame.ModelSync.NotesExtension.Services;

namespace UmbrellaFrame.ModelSync.NotesExtension.Vsix.Services
{
    internal static class ModelNotesCountCache
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, CacheEntry> Entries =
            new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);

        public static int GetCount(string filePath, string noteKey, Action? refreshed = null)
        {
            var normalizedPath = NormalizePath(filePath);
            lock (SyncRoot)
            {
                var entry = GetEntry(normalizedPath);
                var lastWriteUtc = GetLastWriteUtc(normalizedPath);
                if (entry.LastWriteUtc != lastWriteUtc)
                {
                    StartRefresh(normalizedPath, entry, lastWriteUtc, refreshed);
                }

                return entry.Counts.TryGetValue(noteKey, out var count) ? count : 0;
            }
        }

        public static void Invalidate(string filePath)
        {
            var normalizedPath = NormalizePath(filePath);
            lock (SyncRoot)
            {
                Entries.Remove(normalizedPath);
            }
        }

        public static void SetCount(string filePath, string noteKey, int count)
        {
            var normalizedPath = NormalizePath(filePath);
            lock (SyncRoot)
            {
                var entry = GetEntry(normalizedPath);
                if (count <= 0)
                {
                    entry.Counts.Remove(noteKey);
                }
                else
                {
                    entry.Counts[noteKey] = count;
                }

                entry.LastWriteUtc = GetLastWriteUtc(normalizedPath);
            }
        }

        private static CacheEntry GetEntry(string filePath)
        {
            if (!Entries.TryGetValue(filePath, out var entry))
            {
                entry = new CacheEntry();
                Entries[filePath] = entry;
            }

            return entry;
        }

        private static void StartRefresh(string filePath, CacheEntry entry, DateTime lastWriteUtc, Action? refreshed)
        {
            if (refreshed != null)
            {
                entry.PendingRefreshCallbacks.AddOrReplace(refreshed);
            }

            if (entry.RefreshInProgress)
            {
                return;
            }

            entry.RefreshInProgress = true;
            _ = Task.Run(() =>
            {
                Action[] callbacks;
                Dictionary<string, int> counts;
                try
                {
                    var document = new JsonModelNotesRepository(filePath).Load();
                    counts = document.Notes.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value.Count,
                        StringComparer.Ordinal);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("ModelSync Notes count cache refresh failed: " + ex);
                    counts = new Dictionary<string, int>(StringComparer.Ordinal);
                }

                lock (SyncRoot)
                {
                    var currentEntry = GetEntry(filePath);
                    currentEntry.Counts = counts;
                    currentEntry.LastWriteUtc = lastWriteUtc;
                    currentEntry.RefreshInProgress = false;
                    callbacks = currentEntry.PendingRefreshCallbacks.ToArray();
                    currentEntry.PendingRefreshCallbacks.Clear();
                }

                foreach (var callback in callbacks)
                {
                    callback();
                }
            });
        }

        private static DateTime GetLastWriteUtc(string filePath)
        {
            return File.Exists(filePath)
                ? File.GetLastWriteTimeUtc(filePath)
                : DateTime.MinValue;
        }

        private static string NormalizePath(string filePath)
        {
            return Path.GetFullPath(filePath);
        }

        private sealed class CacheEntry
        {
            public DateTime LastWriteUtc { get; set; }

            public bool RefreshInProgress { get; set; }

            public Dictionary<string, int> Counts { get; set; } =
                new Dictionary<string, int>(StringComparer.Ordinal);

            public CallbackList PendingRefreshCallbacks { get; } = new CallbackList();
        }

        private sealed class CallbackList
        {
            private readonly List<Action> _callbacks = new List<Action>();

            public void AddOrReplace(Action callback)
            {
                if (!_callbacks.Contains(callback))
                {
                    _callbacks.Add(callback);
                }
            }

            public Action[] ToArray()
            {
                return _callbacks.ToArray();
            }

            public void Clear()
            {
                _callbacks.Clear();
            }
        }
    }
}
