using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json;

namespace UmbrellaFrame.ModelSync.NotesExtension.Vsix.Services
{
    internal static class ModelNotesClassNameRules
    {
        private static readonly object SyncRoot = new object();
        private static readonly string[] DefaultSuffixes = { "Model" };
        private static DateTime CachedLastWriteUtc;
        private static string? CachedOptionsPath;
        private static Options CachedOptions = new Options { ModelClassSuffixes = DefaultSuffixes };

        public static bool IsModelClass(string className)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (string.IsNullOrWhiteSpace(className))
            {
                return false;
            }

            var options = GetOptions();
            return options.ModelClassNames.Any(name => string.Equals(name, className, StringComparison.Ordinal)) ||
                   options.ModelClassSuffixes.Any(suffix => className.EndsWith(suffix, StringComparison.Ordinal));
        }

        private static Options GetOptions()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var optionsPath = Path.Combine(VisualStudioNotesPaths.GetStorageRootDirectory(), ".modelsync", "notes-settings.json");
            var lastWriteUtc = File.Exists(optionsPath)
                ? File.GetLastWriteTimeUtc(optionsPath)
                : DateTime.MinValue;

            lock (SyncRoot)
            {
                if (string.Equals(CachedOptionsPath, optionsPath, StringComparison.OrdinalIgnoreCase) &&
                    CachedLastWriteUtc == lastWriteUtc)
                {
                    return CachedOptions;
                }

                CachedOptions = LoadOptions(optionsPath);
                CachedOptionsPath = optionsPath;
                CachedLastWriteUtc = lastWriteUtc;
                return CachedOptions;
            }
        }

        private static Options LoadOptions(string optionsPath)
        {
            if (!File.Exists(optionsPath))
            {
                return new Options { ModelClassSuffixes = DefaultSuffixes };
            }

            try
            {
                var json = File.ReadAllText(optionsPath);
                var options = JsonConvert.DeserializeObject<Options>(json) ?? new Options();
                options.ModelClassNames = Normalize(options.ModelClassNames);
                options.ModelClassSuffixes = Normalize(options.ModelClassSuffixes);

                if (options.ModelClassNames.Length == 0 && options.ModelClassSuffixes.Length == 0)
                {
                    options.ModelClassSuffixes = DefaultSuffixes;
                }

                return options;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ModelSync Notes options load failed: " + ex);
                return new Options { ModelClassSuffixes = DefaultSuffixes };
            }
        }

        private static string[] Normalize(IEnumerable<string>? values)
        {
            return values?
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<string>();
        }

        private sealed class Options
        {
            public string[] ModelClassSuffixes { get; set; } = Array.Empty<string>();

            public string[] ModelClassNames { get; set; } = Array.Empty<string>();
        }
    }
}
