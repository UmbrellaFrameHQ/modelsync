
using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.PostgreSQL
{
    public class PostgresColumnNotNullAttribute : DbColumnNotNullAttribute
    {
        public PostgresColumnNotNullAttribute() : base() { }

        public override string GetSqlSnippet()
        {
            return "NOT NULL";
        }
    }
}
