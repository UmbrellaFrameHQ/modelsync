using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace UmbrellaFrame.ModelSync.Core
{
    /// <summary>Discovers and orders migration scripts from files and embedded resources.</summary>
    public static class MigrationScriptDiscovery
    {
        public static IReadOnlyList<MigrationScriptDefinition> FromEmbeddedResources(Assembly assembly, params string[] prefixes)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            var names = assembly.GetManifestResourceNames()
                .Where(n => n.EndsWith(".sql", StringComparison.OrdinalIgnoreCase));

            if (prefixes != null && prefixes.Length > 0)
            {
                names = names.Where(n => prefixes.Any(p => n.StartsWith(p, StringComparison.Ordinal)));
            }

            return Order(names.Select(n =>
            {
                using (var stream = assembly.GetManifestResourceStream(n))
                {
                    if (stream == null)
                        throw new InvalidOperationException($"Embedded SQL resource not found: {n}");

                    var sql = ReadSqlText(stream);
                    var fileName = ResolveEmbeddedFileName(n);
                    return MigrationScriptDefinition.Create(
                        ResolveId(fileName),
                        ResolveName(fileName),
                        ResolveCategory(n),
                        sql,
                        n);
                }
            })).ToList();
        }

        public static IReadOnlyList<MigrationScriptDefinition> Order(IEnumerable<MigrationScriptDefinition> definitions)
        {
            return definitions
                .OrderBy(d => d.Category)
                .ThenBy(d => ResolveNumericPrefix(d.Id))
                .ThenBy(d => d.Id, StringComparer.OrdinalIgnoreCase)
                .ThenBy(d => d.Source, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static MigrationScriptCategory ResolveCategory(string source)
        {
            if (ContainsSegment(source, "StoredProcedures"))
                return MigrationScriptCategory.StoredProcedures;
            if (ContainsSegment(source, "Triggers"))
                return MigrationScriptCategory.Triggers;
            if (ContainsSegment(source, "Seeds"))
                return MigrationScriptCategory.Seeds;
            if (ContainsSegment(source, "CustomSql") || ContainsSegment(source, "Custom"))
                return MigrationScriptCategory.CustomSql;
            return MigrationScriptCategory.Tables;
        }

        public static string ResolveId(string fileName)
        {
            var normalized = NormalizeFileName(fileName);
            var parts = normalized.Split(new[] { '_' }, 2, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0] : normalized;
        }

        public static string ResolveName(string fileName)
        {
            var normalized = NormalizeFileName(fileName);
            var parts = normalized.Split(new[] { '_' }, 2, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 2 ? parts[1] : normalized;
        }

        private static string ReadSqlText(Stream stream)
        {
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                var bytes = ms.ToArray();
                using (var sr = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, true))
                {
                    var text = sr.ReadToEnd();
                    if (text.IndexOf('\uFFFD') < 0)
                        return text;
                }

                try { return Encoding.GetEncoding(1254).GetString(bytes); }
                catch { return Encoding.Default.GetString(bytes); }
            }
        }

        private static string ResolveEmbeddedFileName(string resourceName)
        {
            var withoutExtension = resourceName.Substring(0, resourceName.Length - 4);
            var lastDot = withoutExtension.LastIndexOf('.');
            return lastDot >= 0 ? withoutExtension.Substring(lastDot + 1) : withoutExtension;
        }

        private static string NormalizeFileName(string fileName)
        {
            var name = Path.GetFileNameWithoutExtension(fileName ?? string.Empty);
            return string.IsNullOrWhiteSpace(name) ? "script" : name.Trim();
        }

        private static bool ContainsSegment(string source, string segment)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(segment))
                return false;

            var parts = source.Split(new[] { '/', '\\', '.' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Any(part => string.Equals(part, segment, StringComparison.OrdinalIgnoreCase));
        }

        private static int ResolveNumericPrefix(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return int.MaxValue;

            var digits = new string(id.TakeWhile(char.IsDigit).ToArray());
            return int.TryParse(digits, out var value) ? value : int.MaxValue;
        }
    }
}
