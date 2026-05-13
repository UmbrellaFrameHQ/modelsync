
using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.MySql
{
    public class MySqlColumnPrimaryKeyAttribute : DbColumnPrimaryKeyAttribute
    {
        public new bool IsAutoIncrement { get; }

        public MySqlColumnPrimaryKeyAttribute(bool isAutoIncrement = false) : base()
        {
            IsAutoIncrement = isAutoIncrement;
        }

        public override string GetSqlSnippet()
        {
            var snippet = "PRIMARY KEY";

            if (IsAutoIncrement)
            {
                snippet += " AUTO_INCREMENT";
            }

            return snippet;
        }
    }
}
