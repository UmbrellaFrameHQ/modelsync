using System;

namespace UmbrellaFrame.ModelSync.Core
{
    public abstract class DbColumnForeignKeyAttribute : Attribute
    {
        public string ColumnName { get; }
        public string ReferencedTable { get; }
        public string ReferencedColumn { get; }

        public DbColumnForeignKeyAttribute(string columnName, string referencedTable, string referencedColumn)
        {
            ColumnName = columnName;
            ReferencedTable = referencedTable;
            ReferencedColumn = referencedColumn;
        }

        public abstract string GetSqlSnippet();
    }
}
