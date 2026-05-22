using System;
using System.IO;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

namespace UmbrellaFrame.ModelSync.NotesExtension.Vsix.Services
{
    internal static class VisualStudioNotesPaths
    {
        public static string GetNotesFilePath()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return Path.Combine(GetStorageRootDirectory(), ".modelsync", "notes.json");
        }

        public static string GetStorageRootDirectory()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return GetSolutionDirectory()
                ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        public static string? GetSolutionDirectory()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dte = Package.GetGlobalService(typeof(EnvDTE.DTE)) as DTE2;
            var solutionPath = dte?.Solution?.FullName;
            return string.IsNullOrWhiteSpace(solutionPath)
                ? null
                : Path.GetDirectoryName(solutionPath);
        }
    }
}
