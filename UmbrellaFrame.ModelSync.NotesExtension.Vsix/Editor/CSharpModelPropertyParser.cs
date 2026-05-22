using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using UmbrellaFrame.ModelSync.NotesExtension.Models;
using UmbrellaFrame.ModelSync.NotesExtension.Services;
using UmbrellaFrame.ModelSync.NotesExtension.Vsix.Services;

namespace UmbrellaFrame.ModelSync.NotesExtension.Vsix.Editor
{
    internal static class CSharpModelPropertyParser
    {
        private const string CacheKey = "ModelSync.Notes.SyntaxContextCache";

        public static ModelPropertyContext? TryGetContext(ITextSnapshotLine line)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (string.IsNullOrWhiteSpace(line.GetText()))
            {
                return null;
            }

            var cache = GetOrCreateCache(line.Snapshot);
            return cache.TryGetValue(line.LineNumber, out var context) ? context : null;
        }

        private static Dictionary<int, ModelPropertyContext> GetOrCreateCache(ITextSnapshot snapshot)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (snapshot.TextBuffer.Properties.TryGetProperty(CacheKey, out SnapshotContextCache cached) &&
                cached.Version == snapshot.Version.VersionNumber)
            {
                return cached.ContextsByLine;
            }

            var contexts = BuildContexts(snapshot);
            snapshot.TextBuffer.Properties[CacheKey] = new SnapshotContextCache(snapshot.Version.VersionNumber, contexts);
            return contexts;
        }

        private static Dictionary<int, ModelPropertyContext> BuildContexts(ITextSnapshot snapshot)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var fileKey = GetFileKey(snapshot);
            var parsedContexts = CSharpModelNoteSyntaxParser.Parse(
                snapshot.GetText(),
                fileKey,
                ModelNotesClassNameRules.IsModelClass);

            var contexts = new Dictionary<int, ModelPropertyContext>();
            foreach (var parsedContext in parsedContexts)
            {
                contexts[parsedContext.Key] = ToEditorContext(parsedContext.Value);
            }

            return contexts;
        }

        private static string GetFileKey(ITextSnapshot snapshot)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!snapshot.TextBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument textDocument) ||
                string.IsNullOrWhiteSpace(textDocument.FilePath))
            {
                return string.Empty;
            }

            var filePath = Path.GetFullPath(textDocument.FilePath);
            var solutionDirectory = VisualStudioNotesPaths.GetSolutionDirectory();
            if (!string.IsNullOrWhiteSpace(solutionDirectory))
            {
                var solutionPath = Path.GetFullPath(solutionDirectory);
                if (IsSameOrChildPath(solutionPath, filePath))
                {
                    return filePath
                        .Substring(solutionPath.Length)
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .Replace(Path.DirectorySeparatorChar, '/')
                        .Replace(Path.AltDirectorySeparatorChar, '/');
                }
            }

            return filePath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        }

        private static ModelPropertyContext ToEditorContext(ParsedModelNoteContext parsedContext)
        {
            return new ModelPropertyContext(
                parsedContext.ModelName,
                parsedContext.PropertyName,
                parsedContext.NoteKey,
                parsedContext.DisplayName,
                parsedContext.LegacyNoteKey);
        }

        private static bool IsSameOrChildPath(string parentPath, string childPath)
        {
            var normalizedParent = EnsureTrailingSeparator(Path.GetFullPath(parentPath));
            var normalizedChild = Path.GetFullPath(childPath);
            return normalizedChild.Equals(
                       normalizedParent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                       StringComparison.OrdinalIgnoreCase) ||
                   normalizedChild.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
        }

        private static string EnsureTrailingSeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                   path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        private sealed class SnapshotContextCache
        {
            public SnapshotContextCache(int version, Dictionary<int, ModelPropertyContext> contextsByLine)
            {
                Version = version;
                ContextsByLine = contextsByLine;
            }

            public int Version { get; }

            public Dictionary<int, ModelPropertyContext> ContextsByLine { get; }
        }
    }
}
