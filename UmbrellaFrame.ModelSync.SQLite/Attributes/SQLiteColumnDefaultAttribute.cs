using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.SQLite
{
    public enum SQLiteDefaultExpression
    {
        CurrentTimestamp,
        CurrentDate,
        CurrentTime
    }

    public sealed class SQLiteColumnDefaultAttribute : DbColumnDefaultAttribute
    {
        public SQLiteColumnDefaultAttribute(string literalValue)
            : base(literalValue)
        {
        }

        public SQLiteColumnDefaultAttribute(SQLiteDefaultExpression expression)
            : base(ToSql(expression))
        {
        }

        private static string ToSql(SQLiteDefaultExpression expression)
        {
            switch (expression)
            {
                case SQLiteDefaultExpression.CurrentTimestamp:
                    return "CURRENT_TIMESTAMP";
                case SQLiteDefaultExpression.CurrentDate:
                    return "CURRENT_DATE";
                case SQLiteDefaultExpression.CurrentTime:
                    return "CURRENT_TIME";
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(expression), expression, null);
            }
        }
    }
}
