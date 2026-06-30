using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.SQLite
{
    public sealed class SQLiteColumnDefaultSqlAttribute : DbColumnDefaultAttribute
    {
        public SQLiteColumnDefaultSqlAttribute(string sqlExpression)
            : base(sqlExpression)
        {
        }
    }
}
