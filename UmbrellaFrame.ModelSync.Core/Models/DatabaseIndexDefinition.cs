using System.Collections.Generic;
using System.Linq;

namespace UmbrellaFrame.ModelSync.Core
{
    /// <summary>
    /// Represents semantic index metadata discovered from a live database.
    /// </summary>
    public sealed class DatabaseIndexDefinition
    {
        public string Name { get; set; } = string.Empty;
        public bool IsUnique { get; set; }
        public IList<string> Columns { get; } = new List<string>();

        public bool Matches(bool isUnique, IEnumerable<string> columns)
            => IsUnique == isUnique && Columns.SequenceEqual(columns ?? Enumerable.Empty<string>(), System.StringComparer.OrdinalIgnoreCase);
    }
}
