
using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.SqlServer
{
    public class SqlServerColumnPrimaryKeyAttribute : DbColumnPrimaryKeyAttribute
    {
        public new bool IsAutoIncrement { get; }

        public SqlServerColumnPrimaryKeyAttribute(bool isAutoIncrement = false)
        {
            IsAutoIncrement = isAutoIncrement;
        }

        public override string GetSqlSnippet()
        {
            var snippet = "PRIMARY KEY";

            if (IsAutoIncrement)
            {
                snippet += " IDENTITY(1,1)";
            }

            return snippet;
        }
    }
}
