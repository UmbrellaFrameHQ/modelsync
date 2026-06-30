using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.SqlServer
{
    public enum SqlServerDefaultExpression
    {
        NewId,
        NewSequentialId,
        GetDate,
        GetUtcDate,
        SysDateTime,
        SysUtcDateTime
    }

    public sealed class SqlServerColumnDefaultAttribute : DbColumnDefaultAttribute
    {
        public SqlServerColumnDefaultAttribute(string literalValue)
            : base(literalValue)
        {
        }

        public SqlServerColumnDefaultAttribute(SqlServerDefaultExpression expression)
            : base(ToSql(expression))
        {
        }

        private static string ToSql(SqlServerDefaultExpression expression)
        {
            switch (expression)
            {
                case SqlServerDefaultExpression.NewId:
                    return "NEWID()";
                case SqlServerDefaultExpression.NewSequentialId:
                    return "NEWSEQUENTIALID()";
                case SqlServerDefaultExpression.GetDate:
                    return "GETDATE()";
                case SqlServerDefaultExpression.GetUtcDate:
                    return "GETUTCDATE()";
                case SqlServerDefaultExpression.SysDateTime:
                    return "SYSDATETIME()";
                case SqlServerDefaultExpression.SysUtcDateTime:
                    return "SYSUTCDATETIME()";
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(expression), expression, null);
            }
        }
    }
}
