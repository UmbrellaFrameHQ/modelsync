using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.PostgreSQL
{
    public sealed class PostgresColumnDefaultSqlAttribute : DbColumnDefaultAttribute
    {
        public PostgresColumnDefaultSqlAttribute(string sqlExpression)
            : base(sqlExpression)
        {
        }
    }
}
