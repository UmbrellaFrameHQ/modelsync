using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.SqlServer
{
    public sealed class SqlServerColumnDefaultSqlAttribute : DbColumnDefaultAttribute
    {
        public SqlServerColumnDefaultSqlAttribute(string sqlExpression)
            : base(sqlExpression)
        {
        }
    }
}
