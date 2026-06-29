using System;

using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.SqlServer
{
    [AttributeUsage(AttributeTargets.Property)]
    public class SqlServerColumnForeignKey : DbColumnForeignKeyAttribute
    {
        public new string ColumnName { get; }
        public new string ReferencedTable { get; }
        public new string ReferencedColumn { get; }

        public SqlServerColumnForeignKey(string columnName, string referencedTable, string referencedColumn) : base(columnName, referencedTable, referencedColumn)
        {
            ColumnName = columnName;
            ReferencedTable = referencedTable;
            ReferencedColumn = referencedColumn;
        }

        public override string GetSqlSnippet()
        {
            return $"FOREIGN KEY ({ColumnName}) REFERENCES {ReferencedTable}({ReferencedColumn})";
        }
    }
}
