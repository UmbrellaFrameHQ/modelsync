
using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.SQLite
{
    public class SQLiteColumnPrimaryKeyAttribute : DbColumnPrimaryKeyAttribute
    {
        public new bool IsAutoIncrement { get; }

        public SQLiteColumnPrimaryKeyAttribute() : base()
        {
            IsAutoIncrement = false;
        }

        public override string GetSqlSnippet()
        {
            var snippet = "PRIMARY KEY AUTOINCREMENT";

            if (IsAutoIncrement)
            {
                snippet += " ?";
            }

            return snippet;
        }
    }
}
