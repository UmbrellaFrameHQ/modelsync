
using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.SQLite
{
    public class SQLiteColumnUniqueAttribute : DbColumnUniqueAttribute
    {
        public SQLiteColumnUniqueAttribute() : base() { }
        public override string GetSqlSnippet()
        {
            return "UNIQUE";
        }
    }
}
