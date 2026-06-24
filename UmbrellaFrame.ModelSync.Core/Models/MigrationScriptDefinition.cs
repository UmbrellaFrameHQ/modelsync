using System;
using System.IO;

namespace UmbrellaFrame.ModelSync.Core
{
    /// <summary>Represents a SQL migration script from a file, embedded resource, or inline string.</summary>
    public sealed class MigrationScriptDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public MigrationScriptCategory Category { get; set; }
        public string Sql { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;

        public static MigrationScriptDefinition Create(
            string id,
            string name,
            MigrationScriptCategory category,
            string sql,
            string source = "")
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Migration script id cannot be empty.", nameof(id));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Migration script name cannot be empty.", nameof(name));
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentException("Migration script SQL cannot be empty.", nameof(sql));

            return new MigrationScriptDefinition
            {
                Id = id.Trim(),
                Name = name.Trim(),
                Category = category,
                Sql = sql,
                Source = source ?? string.Empty
            };
        }

        public static MigrationScriptDefinition FromFile(
            string path,
            MigrationScriptCategory? category = null,
            string id = null,
            string name = null)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Migration script path cannot be empty.", nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException("Migration script file was not found.", path);

            var sql = File.ReadAllText(path);
            var fileName = Path.GetFileNameWithoutExtension(path);
            var resolvedCategory = category ?? MigrationScriptDiscovery.ResolveCategory(path);
            var resolvedId = string.IsNullOrWhiteSpace(id) ? MigrationScriptDiscovery.ResolveId(fileName) : id;
            var resolvedName = string.IsNullOrWhiteSpace(name) ? MigrationScriptDiscovery.ResolveName(fileName) : name;

            return Create(resolvedId, resolvedName, resolvedCategory, sql, path);
        }
    }
}
