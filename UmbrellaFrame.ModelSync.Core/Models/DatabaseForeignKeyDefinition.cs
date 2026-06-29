using System.Collections.Generic;
using System.Linq;

namespace UmbrellaFrame.ModelSync.Core
{
    /// <summary>
    /// Represents semantic foreign-key metadata discovered from a live database.
    /// </summary>
    public sealed class DatabaseForeignKeyDefinition
    {
        public string Name { get; set; } = string.Empty;
        public IList<string> LocalColumns { get; } = new List<string>();
        public string ReferencedSchema { get; set; } = string.Empty;
        public string ReferencedTable { get; set; } = string.Empty;
        public IList<string> ReferencedColumns { get; } = new List<string>();

        public bool Matches(IEnumerable<string> localColumns, string referencedSchema, string referencedTable, IEnumerable<string> referencedColumns)
            => LocalColumns.SequenceEqual(localColumns ?? Enumerable.Empty<string>(), System.StringComparer.OrdinalIgnoreCase)
               && string.Equals(ReferencedSchema ?? string.Empty, referencedSchema ?? string.Empty, System.StringComparison.OrdinalIgnoreCase)
               && string.Equals(ReferencedTable ?? string.Empty, referencedTable ?? string.Empty, System.StringComparison.OrdinalIgnoreCase)
               && ReferencedColumns.SequenceEqual(referencedColumns ?? Enumerable.Empty<string>(), System.StringComparer.OrdinalIgnoreCase);
    }
}
