
using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.PostgreSQL
{
    public class PostgresColumnPrimaryKeyAttribute : DbColumnPrimaryKeyAttribute
    {
        public new bool IsAutoIncrement { get; }

        public PostgresColumnPrimaryKeyAttribute() : base()
        {
            IsAutoIncrement = false;
        }

        public override string GetSqlSnippet()
        {
            var snippet = "PRIMARY KEY";

            return snippet;
        }
    }
}
