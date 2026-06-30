using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.MySql
{
    public sealed class MySqlColumnDefaultSqlAttribute : DbColumnDefaultAttribute
    {
        public MySqlColumnDefaultSqlAttribute(string sqlExpression)
            : base(sqlExpression)
        {
        }
    }
}
