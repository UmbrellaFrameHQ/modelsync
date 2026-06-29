using System.Collections.Generic;

namespace UmbrellaFrame.ModelSync.Core
{
    public sealed class DatabaseTableDefinition
    {
        public string Schema { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public IDictionary<string, DatabaseColumnDefinition> Columns { get; }
            = new Dictionary<string, DatabaseColumnDefinition>(System.StringComparer.OrdinalIgnoreCase);
        public ISet<string> Indexes { get; }
            = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        public ISet<string> UniqueConstraints { get; }
            = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        public ISet<string> ForeignKeys { get; }
            = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        public IList<DatabaseIndexDefinition> SemanticIndexes { get; }
            = new List<DatabaseIndexDefinition>();
        public IList<DatabaseForeignKeyDefinition> SemanticForeignKeys { get; }
            = new List<DatabaseForeignKeyDefinition>();
    }
}
