
using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.PostgreSQL
{
    public class PostgresColumnUniqueAttribute : DbColumnUniqueAttribute
    {
        public PostgresColumnUniqueAttribute() : base() { }
        public override string GetSqlSnippet()
        {
            return "UNIQUE";
        }
    }
}
