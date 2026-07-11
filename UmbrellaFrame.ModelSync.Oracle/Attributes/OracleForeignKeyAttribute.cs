using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.Oracle
{
    public class OracleForeignKeyAttribute : DbColumnForeignKeyAttribute
    {
        public OracleForeignKeyAttribute(string columnName, string referenceTable, string referenceColumn)
            : base(columnName, referenceTable, referenceColumn)
        {
        }

        public override string GetSqlSnippet()
            => $"FOREIGN KEY ({ColumnName}) REFERENCES {ReferencedTable}({ReferencedColumn})";
    }
}
