using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.Oracle
{
    public sealed class OracleColumnDefaultSqlAttribute : DbColumnDefaultAttribute
    {
        public OracleColumnDefaultSqlAttribute(string sqlExpression)
            : base(sqlExpression)
        {
        }
    }
}
