using System;
using System.IO;

namespace UmbrellaFrame.ModelSync.Core
{
    /// <summary>
    /// Represents a stored procedure definition tracked by the project.
    /// </summary>
    public sealed class StoredProcedureDefinition
    {
        /// <summary>Procedure schema. Defaults to <c>dbo</c> for SQL Server.</summary>
        public string Schema { get; set; } = "dbo";

        /// <summary>Procedure name without schema.</summary>
        public string Name { get; set; }

        /// <summary>Project-side SQL definition.</summary>
        public string Sql { get; set; }

        /// <summary>Optional source file path for diagnostics, tooling, and editor integrations.</summary>
        public string SourcePath { get; set; }

        /// <summary>Creates a stored procedure definition from explicit SQL text.</summary>
        public static StoredProcedureDefinition Create(string name, string sql, string schema = "dbo")
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Procedure name cannot be empty.", nameof(name));
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentException("Procedure SQL cannot be empty.", nameof(sql));

            return new StoredProcedureDefinition
            {
                Schema = string.IsNullOrWhiteSpace(schema) ? "dbo" : schema,
                Name = name,
                Sql = sql
            };
        }

        /// <summary>Creates a stored procedure definition from a SQL file.</summary>
        public static StoredProcedureDefinition FromFile(string path, string name = null, string schema = "dbo")
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Procedure file path cannot be empty.", nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException("Stored procedure SQL file was not found.", path);

            var fileName = Path.GetFileNameWithoutExtension(path);
            var resolvedSchema = schema;
            var resolvedName = name;

            if (string.IsNullOrWhiteSpace(resolvedName))
            {
                var parts = fileName.Split('.');
                if (parts.Length == 2)
                {
                    resolvedSchema = parts[0];
                    resolvedName = parts[1];
                }
                else
                {
                    resolvedName = fileName;
                }
            }

            return new StoredProcedureDefinition
            {
                Schema = string.IsNullOrWhiteSpace(resolvedSchema) ? "dbo" : resolvedSchema,
                Name = resolvedName,
                Sql = File.ReadAllText(path),
                SourcePath = path
            };
        }
    }
}
