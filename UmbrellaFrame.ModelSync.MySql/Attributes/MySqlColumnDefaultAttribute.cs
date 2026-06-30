using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.MySql
{
    public enum MySqlDefaultExpression
    {
        Uuid,
        CurrentTimestamp
    }

    public sealed class MySqlColumnDefaultAttribute : DbColumnDefaultAttribute
    {
        public MySqlColumnDefaultAttribute(string literalValue)
            : base(literalValue)
        {
        }

        public MySqlColumnDefaultAttribute(MySqlDefaultExpression expression)
            : base(ToSql(expression))
        {
        }

        private static string ToSql(MySqlDefaultExpression expression)
        {
            switch (expression)
            {
                case MySqlDefaultExpression.Uuid:
                    return "UUID()";
                case MySqlDefaultExpression.CurrentTimestamp:
                    return "CURRENT_TIMESTAMP";
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(expression), expression, null);
            }
        }
    }
}
