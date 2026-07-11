using System;
using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.Oracle
{
    public enum OracleDefaultExpression
    {
        SysGuid,
        SysDate,
        CurrentTimestamp,
        Systimestamp
    }

    public sealed class OracleColumnDefaultAttribute : DbColumnDefaultAttribute
    {
        public OracleColumnDefaultAttribute(string literalValue)
            : base(literalValue)
        {
        }

        public OracleColumnDefaultAttribute(OracleDefaultExpression expression)
            : base(ToSql(expression))
        {
        }

        private static string ToSql(OracleDefaultExpression expression)
        {
            switch (expression)
            {
                case OracleDefaultExpression.SysGuid:
                    return "SYS_GUID()";
                case OracleDefaultExpression.SysDate:
                    return "SYSDATE";
                case OracleDefaultExpression.CurrentTimestamp:
                    return "CURRENT_TIMESTAMP";
                case OracleDefaultExpression.Systimestamp:
                    return "SYSTIMESTAMP";
                default:
                    throw new ArgumentOutOfRangeException(nameof(expression), expression, null);
            }
        }
    }
}
