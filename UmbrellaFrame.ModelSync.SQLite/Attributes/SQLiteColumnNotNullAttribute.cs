
using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.SQLite
{
    public class SQLiteColumnNotNullAttribute : DbColumnNotNullAttribute
    {
        public SQLiteColumnNotNullAttribute() : base() { }

        public override string GetSqlSnippet()
        {
            return "NOT NULL";
        }
    }
}
